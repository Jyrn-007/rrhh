using KitsuneHrm.Api.Data;
using KitsuneHrm.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace KitsuneHrm.Api.Services;

public class AuthService(AppDbContext db, TokenService tokens, IConfiguration cfg)
{
    // Hash ficticio para igualar tiempos de respuesta cuando el usuario no existe
    static readonly string DummyHash = BCrypt.Net.BCrypt.HashPassword("kitsune-dummy-password", 12);

    int MaxIntentos => cfg.GetValue("Security:MaxFailedAttempts", 5);
    int MinutosBloqueo => cfg.GetValue("Security:LockoutMinutes", 15);
    int DiasRefresh => cfg.GetValue("Jwt:RefreshTokenDays", 7);

    IQueryable<Usuario> UsuariosConPermisos() =>
        db.Usuarios.Include(u => u.Rol).ThenInclude(r => r.Permisos).ThenInclude(rp => rp.Permiso);

    static AuthResult Fail(int status, string msg, string? codigo = null) => new(false, status, msg, null, codigo);

    public async Task<AuthResult> LoginAsync(LoginRequest req, string? ip, string? ua)
    {
        var ident = req.Usuario.Trim().ToLower();
        var u = await UsuariosConPermisos().FirstOrDefaultAsync(x => x.Username.ToLower() == ident || x.Email.ToLower() == ident);

        if (u is null)
        {
            BCrypt.Net.BCrypt.Verify(req.Password, DummyHash);
            await Acceso(null, req.Usuario.Trim(), "LOGIN_FALLIDO", false, "Usuario inexistente", ip, ua);
            return Fail(401, "Credenciales inválidas.");
        }

        if (!u.Activo)
        {
            await Acceso(u.Id, u.Username, "LOGIN_FALLIDO", false, "Cuenta inactiva", ip, ua);
            return Fail(403, "Cuenta inactiva. Contacte al administrador.");
        }

        if (u.Bloqueado)
        {
            if (u.BloqueadoHasta is { } hasta && hasta <= DateTime.UtcNow)
            {
                u.Bloqueado = false;
                u.BloqueadoHasta = null;
                u.IntentosFallidos = 0;
                await Acceso(u.Id, u.Username, "DESBLOQUEO", true, "Desbloqueo automático por vencimiento", ip, ua);
            }
            else
            {
                await Acceso(u.Id, u.Username, "LOGIN_FALLIDO", false, "Cuenta bloqueada", ip, ua);
                var msg = u.BloqueadoHasta is { } h
                    ? $"Cuenta bloqueada. Intente de nuevo en {Math.Max(1, (int)Math.Ceiling((h - DateTime.UtcNow).TotalMinutes))} minuto(s)."
                    : "Cuenta bloqueada. Contacte al administrador.";
                return Fail(423, msg);
            }
        }

        if (!BCrypt.Net.BCrypt.Verify(req.Password, u.PasswordHash))
        {
            u.IntentosFallidos++;
            if (u.IntentosFallidos >= MaxIntentos)
            {
                u.Bloqueado = true;
                u.BloqueadoHasta = DateTime.UtcNow.AddMinutes(MinutosBloqueo);
                await Acceso(u.Id, u.Username, "BLOQUEO", false, $"Bloqueada tras {u.IntentosFallidos} intentos fallidos", ip, ua);
                return Fail(423, $"Cuenta bloqueada por {MinutosBloqueo} minutos tras demasiados intentos fallidos.");
            }
            await Acceso(u.Id, u.Username, "LOGIN_FALLIDO", false, $"Contraseña incorrecta (intento {u.IntentosFallidos})", ip, ua);
            return Fail(401, "Credenciales inválidas.");
        }

        u.IntentosFallidos = 0;
        u.Bloqueado = false;
        u.BloqueadoHasta = null;
        u.UltimoLogin = DateTime.UtcNow;
        u.UltimoLoginIp = ip;

        var resp = await EmitirSesion(u, ip);
        await Acceso(u.Id, u.Username, "LOGIN_OK", true, null, ip, ua);
        return new AuthResult(true, 200, null, resp);
    }

    public async Task<AuthResult> RefreshAsync(string refreshToken, string? ip, string? ua)
    {
        var hash = TokenService.Hash(refreshToken);
        var rt = await db.RefreshTokens.FirstOrDefaultAsync(x => x.TokenHash == hash);
        if (rt is null) return Fail(401, "Sesión inválida.");

        if (rt.RevocadoEn is not null)
        {
            // Reutilización de un token ya rotado: posible robo -> se revocan todas las sesiones del usuario
            var activos = await db.RefreshTokens.Where(x => x.UsuarioId == rt.UsuarioId && x.RevocadoEn == null).ToListAsync();
            foreach (var t in activos) t.RevocadoEn = DateTime.UtcNow;
            await Acceso(rt.UsuarioId, "(token)", "TOKEN_REUTILIZADO", false, "Refresh token reutilizado; sesiones revocadas", ip, ua);
            return Fail(401, "Sesión inválida.");
        }
        if (rt.ExpiraEn <= DateTime.UtcNow) return Fail(401, "La sesión expiró.");

        var u = await UsuariosConPermisos().FirstAsync(x => x.Id == rt.UsuarioId);
        var bloqueado = u.Bloqueado && (u.BloqueadoHasta is null || u.BloqueadoHasta > DateTime.UtcNow);
        if (!u.Activo || bloqueado) return Fail(401, "Cuenta no disponible.");

        var resp = await EmitirSesion(u, ip, rt);
        await Acceso(u.Id, u.Username, "TOKEN_REFRESH", true, null, ip, ua);
        return new AuthResult(true, 200, null, resp);
    }

    public async Task LogoutAsync(string? refreshToken, int usuarioId, string username, string? ip, string? ua)
    {
        if (!string.IsNullOrEmpty(refreshToken))
        {
            var hash = TokenService.Hash(refreshToken);
            var rt = await db.RefreshTokens.FirstOrDefaultAsync(x => x.TokenHash == hash && x.UsuarioId == usuarioId);
            if (rt is { RevocadoEn: null }) rt.RevocadoEn = DateTime.UtcNow;
        }
        await Acceso(usuarioId, username, "LOGOUT", true, null, ip, ua);
    }

    public async Task<AuthResult> CambiarPasswordAsync(int usuarioId, CambiarPasswordRequest req, string? ip, string? ua)
    {
        var u = await UsuariosConPermisos().FirstAsync(x => x.Id == usuarioId);
        if (!BCrypt.Net.BCrypt.Verify(req.PasswordActual, u.PasswordHash))
        {
            await Acceso(u.Id, u.Username, "CAMBIO_PASSWORD_FALLIDO", false, "Contraseña actual incorrecta", ip, ua);
            return Fail(400, "La contraseña actual es incorrecta.");
        }
        if (!PasswordPolicy.EsValida(req.PasswordNueva, out var err)) return Fail(400, err);
        if (BCrypt.Net.BCrypt.Verify(req.PasswordNueva, u.PasswordHash)) return Fail(400, "La nueva contraseña debe ser distinta a la actual.");

        u.PasswordHash = PasswordPolicy.Hash(req.PasswordNueva);
        u.PasswordCambiadaEn = DateTime.UtcNow;
        u.DebeCambiarPassword = false;
        u.ActualizadoEn = DateTime.UtcNow;

        // Cierra todas las demás sesiones
        var activos = await db.RefreshTokens.Where(x => x.UsuarioId == u.Id && x.RevocadoEn == null).ToListAsync();
        foreach (var t in activos) t.RevocadoEn = DateTime.UtcNow;

        var resp = await EmitirSesion(u, ip);
        await Acceso(u.Id, u.Username, "CAMBIO_PASSWORD", true, null, ip, ua);
        return new AuthResult(true, 200, null, resp);
    }

    async Task<LoginResponse> EmitirSesion(Usuario u, string? ip, RefreshToken? reemplaza = null)
    {
        var permisos = u.Rol.Permisos.Select(p => p.Permiso.Codigo).Distinct().ToArray();
        var (access, expira) = tokens.CrearAccessToken(u, permisos);

        var raw = TokenService.NuevoRefreshToken();
        var hash = TokenService.Hash(raw);
        db.RefreshTokens.Add(new RefreshToken
        {
            UsuarioId = u.Id,
            TokenHash = hash,
            ExpiraEn = DateTime.UtcNow.AddDays(DiasRefresh),
            CreadoIp = ip,
        });
        if (reemplaza is not null)
        {
            reemplaza.RevocadoEn = DateTime.UtcNow;
            reemplaza.ReemplazadoPor = hash;
        }

        return new LoginResponse(access, expira, raw,
            new SesionUsuarioDto(u.Id, u.Username, u.Email, u.Rol.Nombre, permisos, u.DebeCambiarPassword, u.EmpleadoId));
    }

    /// <summary>Registra el evento y guarda TODOS los cambios pendientes en una sola transacción.</summary>
    async Task Acceso(int? usuarioId, string username, string evento, bool ok, string? detalle, string? ip, string? ua)
    {
        db.RegistroAccesos.Add(new RegistroAcceso
        {
            UsuarioId = usuarioId,
            Username = username.Length > 50 ? username[..50] : username,
            Evento = evento,
            Exitoso = ok,
            Detalle = detalle,
            Ip = ip,
            UserAgent = ua is { Length: > 300 } ? ua[..300] : ua,
            Fecha = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }
}
