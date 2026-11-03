using System.Security.Claims;
using KitsuneHrm.Api.Data;
using KitsuneHrm.Api.Models;

namespace KitsuneHrm.Api.Middleware;

/// <summary>Captura excepciones no controladas, las guarda en logs_sistema y responde un error genérico.</summary>
public class ExceptionLoggingMiddleware(RequestDelegate next, ILogger<ExceptionLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        try
        {
            await next(ctx);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error no controlado en {Method} {Path}", ctx.Request.Method, ctx.Request.Path);
            try
            {
                var db = ctx.RequestServices.GetRequiredService<AppDbContext>();
                db.ChangeTracker.Clear();
                var uid = int.TryParse(ctx.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : (int?)null;
                var msg = ex.Message.Length > 480 ? ex.Message[..480] : ex.Message;
                db.LogsSistema.Add(new LogSistema
                {
                    Nivel = "ERROR",
                    Origen = ex.GetType().Name,
                    Mensaje = msg,
                    Excepcion = ex.ToString(),
                    UsuarioId = uid,
                    Ruta = ctx.Request.Path.Value is { Length: > 250 } p ? p[..250] : ctx.Request.Path.Value,
                    Metodo = ctx.Request.Method,
                    Ip = ctx.Connection.RemoteIpAddress?.ToString(),
                });
                await db.SaveChangesAsync();
            }
            catch (Exception logEx)
            {
                logger.LogError(logEx, "No se pudo guardar el log en la base de datos");
            }

            if (!ctx.Response.HasStarted)
            {
                ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await ctx.Response.WriteAsJsonAsync(new { error = "Ocurrió un error interno. Fue registrado." });
            }
        }
    }
}

/// <summary>Si el usuario debe cambiar su contraseña, solo se permite /api/auth/*.</summary>
public class MustChangePasswordMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        if (ctx.User.Identity?.IsAuthenticated == true
            && ctx.User.FindFirstValue("must_change") == "true"
            && !ctx.Request.Path.StartsWithSegments("/api/auth"))
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            await ctx.Response.WriteAsJsonAsync(new { error = "Debe cambiar su contraseña.", codigo = "PASSWORD_CHANGE_REQUIRED" });
            return;
        }
        await next(ctx);
    }
}
