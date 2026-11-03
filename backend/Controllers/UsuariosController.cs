using KitsuneHrm.Api.Data;
using KitsuneHrm.Api.Models;
using KitsuneHrm.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KitsuneHrm.Api.Controllers;

[ApiController]
[Route("api/usuarios")]
public class UsuariosController(AppDbContext db, AuditService audit) : ControllerBase
{
    static object Snapshot(Usuario u) => new { u.Username, u.Email, u.RolId, u.EmpleadoId, u.Activo, u.Bloqueado };

    [HttpGet, Authorize(Policy = Permisos.UsuariosVer)]
    public async Task<IActionResult> Listar([FromQuery] string? buscar, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var q = db.Usuarios.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(buscar))
        {
            var b = buscar.Trim();
            q = q.Where(u => u.Username.Contains(b) || u.Email.Contains(b));
        }
        var total = await q.CountAsync();
        var items = await q.OrderBy(u => u.Username).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(u => new
            {
                u.Id, u.Username, u.Email, Rol = u.Rol.Nombre, u.RolId, u.EmpleadoId,
                Empleado = u.Empleado != null ? u.Empleado.Nombres + " " + u.Empleado.Apellidos : null, u.Activo, u.Bloqueado,
                u.IntentosFallidos, u.BloqueadoHasta, u.UltimoLogin, u.UltimoLoginIp, u.DebeCambiarPassword,
            }).ToListAsync();
        return Ok(new { total, page, pageSize, items });
    }

    [HttpPost, Authorize(Policy = Permisos.UsuariosGestionar)]
    public async Task<IActionResult> Crear(UsuarioCrearRequest req)
    {
        if (!PasswordPolicy.EsValida(req.PasswordTemporal, out var err)) return BadRequest(new { error = err });
        if (!await db.Roles.AnyAsync(r => r.Id == req.RolId && r.Activo)) return BadRequest(new { error = "Rol inválido." });
        if (await db.Usuarios.AnyAsync(u => u.Username == req.Username)) return BadRequest(new { error = "El nombre de usuario ya existe." });
        if (await db.Usuarios.AnyAsync(u => u.Email == req.Email)) return BadRequest(new { error = "El correo ya está registrado." });
        if (req.EmpleadoId is int eid && !await db.Empleados.AnyAsync(e => e.Id == eid && e.Activo))
            return BadRequest(new { error = "Empleado inválido o inactivo." });

        var u = new Usuario
        {
            Username = req.Username.Trim(),
            Email = req.Email.Trim(),
            PasswordHash = PasswordPolicy.Hash(req.PasswordTemporal),
            RolId = req.RolId,
            EmpleadoId = req.EmpleadoId,
            DebeCambiarPassword = true,
        };
        db.Usuarios.Add(u);
        await db.SaveChangesAsync();
        audit.Registrar("CREAR", "Usuario", u.Id, null, Snapshot(u));
        await db.SaveChangesAsync();
        return Created($"api/usuarios/{u.Id}", new { u.Id, u.Username });
    }

    [HttpPut("{id:int}"), Authorize(Policy = Permisos.UsuariosGestionar)]
    public async Task<IActionResult> Editar(int id, UsuarioEditarRequest req)
    {
        var u = await db.Usuarios.FirstOrDefaultAsync(x => x.Id == id);
        if (u is null) return NotFound(new { error = "Usuario no encontrado." });
        if (!await db.Roles.AnyAsync(r => r.Id == req.RolId && r.Activo)) return BadRequest(new { error = "Rol inválido." });
        if (await db.Usuarios.AnyAsync(x => x.Email == req.Email && x.Id != id)) return BadRequest(new { error = "El correo ya está registrado." });
        if (id == audit.UsuarioActualId && req.RolId != u.RolId) return BadRequest(new { error = "No puede cambiar su propio rol." });

        var antes = Snapshot(u);
        u.Email = req.Email.Trim();
        u.RolId = req.RolId;
        u.EmpleadoId = req.EmpleadoId;
        u.ActualizadoEn = DateTime.UtcNow;
        audit.Registrar("ACTUALIZAR", "Usuario", id, antes, Snapshot(u));
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id:int}/estado"), Authorize(Policy = Permisos.UsuariosGestionar)]
    public async Task<IActionResult> Estado(int id, EstadoRequest req)
    {
        var u = await db.Usuarios.FirstOrDefaultAsync(x => x.Id == id);
        if (u is null) return NotFound(new { error = "Usuario no encontrado." });
        if (id == audit.UsuarioActualId && !req.Activo) return BadRequest(new { error = "No puede desactivar su propia cuenta." });

        var antes = Snapshot(u);
        u.Activo = req.Activo;
        u.ActualizadoEn = DateTime.UtcNow;
        if (!req.Activo) await RevocarSesiones(id);
        audit.Registrar(req.Activo ? "ACTIVAR" : "DESACTIVAR", "Usuario", id, antes, new { Snapshot = Snapshot(u), req.Motivo });
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id:int}/bloquear"), Authorize(Policy = Permisos.UsuariosGestionar)]
    public async Task<IActionResult> Bloquear(int id)
    {
        var u = await db.Usuarios.FirstOrDefaultAsync(x => x.Id == id);
        if (u is null) return NotFound(new { error = "Usuario no encontrado." });
        if (id == audit.UsuarioActualId) return BadRequest(new { error = "No puede bloquear su propia cuenta." });

        var antes = Snapshot(u);
        u.Bloqueado = true;
        u.BloqueadoHasta = null; // bloqueo manual: solo un administrador lo levanta
        u.ActualizadoEn = DateTime.UtcNow;
        await RevocarSesiones(id);
        audit.Registrar("BLOQUEAR", "Usuario", id, antes, Snapshot(u));
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id:int}/desbloquear"), Authorize(Policy = Permisos.UsuariosGestionar)]
    public async Task<IActionResult> Desbloquear(int id)
    {
        var u = await db.Usuarios.FirstOrDefaultAsync(x => x.Id == id);
        if (u is null) return NotFound(new { error = "Usuario no encontrado." });

        var antes = Snapshot(u);
        u.Bloqueado = false;
        u.BloqueadoHasta = null;
        u.IntentosFallidos = 0;
        u.ActualizadoEn = DateTime.UtcNow;
        db.RegistroAccesos.Add(new RegistroAcceso
        {
            UsuarioId = u.Id, Username = u.Username, Evento = "DESBLOQUEO", Exitoso = true,
            Detalle = "Desbloqueo manual por administrador", Ip = HttpContext.Connection.RemoteIpAddress?.ToString(),
        });
        audit.Registrar("DESBLOQUEAR", "Usuario", id, antes, Snapshot(u));
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Genera una contraseña temporal; se muestra UNA sola vez.</summary>
    [HttpPost("{id:int}/reset-password"), Authorize(Policy = Permisos.UsuariosGestionar)]
    public async Task<IActionResult> ResetPassword(int id)
    {
        var u = await db.Usuarios.FirstOrDefaultAsync(x => x.Id == id);
        if (u is null) return NotFound(new { error = "Usuario no encontrado." });

        var temporal = PasswordPolicy.Generar();
        u.PasswordHash = PasswordPolicy.Hash(temporal);
        u.DebeCambiarPassword = true;
        u.IntentosFallidos = 0;
        u.ActualizadoEn = DateTime.UtcNow;
        await RevocarSesiones(id);
        audit.Registrar("RESET_PASSWORD", "Usuario", id, null, new { u.Username });
        await db.SaveChangesAsync();
        return Ok(new { passwordTemporal = temporal });
    }

    async Task RevocarSesiones(int usuarioId)
    {
        var tokens = await db.RefreshTokens.Where(t => t.UsuarioId == usuarioId && t.RevocadoEn == null).ToListAsync();
        foreach (var t in tokens) t.RevocadoEn = DateTime.UtcNow;
    }
}
