using System.Security.Claims;
using System.Text.Json;
using KitsuneHrm.Api.Data;
using KitsuneHrm.Api.Models;

namespace KitsuneHrm.Api.Services;

/// <summary>
/// Agrega el registro de auditoría al contexto SIN guardar: el controlador llama a SaveChangesAsync una sola vez,
/// de modo que el cambio y su auditoría se confirman en la misma transacción.
/// </summary>
public class AuditService(AppDbContext db, IHttpContextAccessor http)
{
    static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    public int? UsuarioActualId =>
        int.TryParse(http.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public void Registrar(string accion, string entidad, object? entidadId, object? antes = null, object? despues = null)
    {
        var ctx = http.HttpContext;
        db.Auditoria.Add(new AuditoriaLog
        {
            UsuarioId = UsuarioActualId,
            Username = ctx?.User.FindFirstValue(ClaimTypes.Name),
            Accion = accion,
            Entidad = entidad,
            EntidadId = entidadId?.ToString(),
            ValoresAntes = antes is null ? null : JsonSerializer.Serialize(antes, Json),
            ValoresDespues = despues is null ? null : JsonSerializer.Serialize(despues, Json),
            Ip = ctx?.Connection.RemoteIpAddress?.ToString(),
            Fecha = DateTime.UtcNow,
        });
    }
}
