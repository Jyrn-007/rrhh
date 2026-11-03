using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using KitsuneHrm.Api.Data;
using KitsuneHrm.Api.Middleware;
using KitsuneHrm.Api.Models;
using KitsuneHrm.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;

var jwtKey = cfg["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Length < 32)
    throw new InvalidOperationException("Configure Jwt__Key (mínimo 32 caracteres) como variable de entorno.");

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseMySql(cfg.GetConnectionString("Default"), new MySqlServerVersion(new Version(8, 0, 36))));

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<ContextoUsuario>();
builder.Services.AddScoped<VacacionesService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = cfg["Jwt:Issuer"],
        ValidateAudience = true,
        ValidAudience = cfg["Jwt:Audience"],
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ClockSkew = TimeSpan.FromSeconds(30),
    };
    // Si la cuenta se desactiva o bloquea, el token deja de servir de inmediato
    o.Events = new JwtBearerEvents
    {
        OnTokenValidated = async ctx =>
        {
            var db = ctx.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
            if (!int.TryParse(ctx.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
            {
                ctx.Fail("Token inválido");
                return;
            }
            var ahora = DateTime.UtcNow;
            var ok = await db.Usuarios.AsNoTracking().AnyAsync(u =>
                u.Id == id && u.Activo && !(u.Bloqueado && (u.BloqueadoHasta == null || u.BloqueadoHasta > ahora)));
            if (!ok) ctx.Fail("Cuenta no disponible");
        },
    };
});

builder.Services.AddAuthorization(o =>
{
    foreach (var p in Permisos.Todos)
        o.AddPolicy(p, pol => pol.RequireAuthenticatedUser().RequireClaim("permiso", p));
});

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(cfg.GetSection("Cors:Origins").Get<string[]>() ?? Array.Empty<string>())
    .AllowAnyHeader().AllowAnyMethod()));

// Limita fuerza bruta por IP además del bloqueo por cuenta
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("login", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    // Renovar sesión al recargar la página: más holgado que el login
    o.AddPolicy("refresh", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownNetworks.Clear();
    o.KnownProxies.Clear();
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseForwardedHeaders();
app.UseMiddleware<ExceptionLoggingMiddleware>();
if (!app.Environment.IsDevelopment()) app.UseHsts();

app.Use(async (ctx, next) =>
{
    var h = ctx.Response.Headers;
    h["X-Content-Type-Options"] = "nosniff";
    h["X-Frame-Options"] = "DENY";
    h["Referrer-Policy"] = "no-referrer";
    await next();
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<MustChangePasswordMiddleware>();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// Usuario administrador inicial (solo si no hay usuarios y hay variables Seed__*)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var user = cfg["Seed:AdminUser"];
    var email = cfg["Seed:AdminEmail"];
    var pwd = cfg["Seed:AdminPassword"];
    if (!string.IsNullOrWhiteSpace(user) && !string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(pwd)
        && !await db.Usuarios.AnyAsync())
    {
        if (!PasswordPolicy.EsValida(pwd, out var err))
            throw new InvalidOperationException("Seed__AdminPassword no cumple la política: " + err);
        var rol = await db.Roles.FirstAsync(r => r.Nombre == "Administrador");
        db.Usuarios.Add(new Usuario
        {
            Username = user.Trim(),
            Email = email.Trim(),
            PasswordHash = PasswordPolicy.Hash(pwd),
            RolId = rol.Id,
            DebeCambiarPassword = true,
        });
        await db.SaveChangesAsync();
        app.Logger.LogWarning("Usuario administrador inicial creado: {User}. Debe cambiar su contraseña al ingresar.", user);
    }
}

app.Run();
