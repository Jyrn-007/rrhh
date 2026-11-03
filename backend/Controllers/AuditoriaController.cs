using KitsuneHrm.Api.Data;
using KitsuneHrm.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KitsuneHrm.Api.Controllers;

[ApiController, Authorize(Policy = Permisos.AuditoriaVer)]
[Route("api/auditoria")]
public class AuditoriaController(AppDbContext db) : ControllerBase
{
    static (int page, int size) Pag(int page, int size) => (Math.Max(1, page), Math.Clamp(size, 1, 200));

    [HttpGet]
    public async Task<IActionResult> Operaciones([FromQuery] string? usuario, [FromQuery] string? accion, [FromQuery] string? entidad,
        [FromQuery] DateTime? desde, [FromQuery] DateTime? hasta, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var (p, s) = Pag(page, pageSize);
        var q = db.Auditoria.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(usuario)) q = q.Where(a => a.Username != null && a.Username.Contains(usuario));
        if (!string.IsNullOrWhiteSpace(accion)) q = q.Where(a => a.Accion == accion);
        if (!string.IsNullOrWhiteSpace(entidad)) q = q.Where(a => a.Entidad == entidad);
        if (desde is not null) q = q.Where(a => a.Fecha >= desde);
        if (hasta is not null) q = q.Where(a => a.Fecha < hasta.Value.AddDays(1));
        var total = await q.CountAsync();
        var items = await q.OrderByDescending(a => a.Id).Skip((p - 1) * s).Take(s).ToListAsync();
        return Ok(new { total, page = p, pageSize = s, items });
    }

    [HttpGet("accesos")]
    public async Task<IActionResult> Accesos([FromQuery] string? usuario, [FromQuery] string? evento, [FromQuery] bool? exitoso,
        [FromQuery] DateTime? desde, [FromQuery] DateTime? hasta, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var (p, s) = Pag(page, pageSize);
        var q = db.RegistroAccesos.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(usuario)) q = q.Where(a => a.Username.Contains(usuario));
        if (!string.IsNullOrWhiteSpace(evento)) q = q.Where(a => a.Evento == evento);
        if (exitoso is not null) q = q.Where(a => a.Exitoso == exitoso);
        if (desde is not null) q = q.Where(a => a.Fecha >= desde);
        if (hasta is not null) q = q.Where(a => a.Fecha < hasta.Value.AddDays(1));
        var total = await q.CountAsync();
        var items = await q.OrderByDescending(a => a.Id).Skip((p - 1) * s).Take(s).ToListAsync();
        return Ok(new { total, page = p, pageSize = s, items });
    }

    [HttpGet("logs")]
    public async Task<IActionResult> Logs([FromQuery] string? nivel, [FromQuery] DateTime? desde, [FromQuery] DateTime? hasta,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var (p, s) = Pag(page, pageSize);
        var q = db.LogsSistema.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(nivel)) q = q.Where(l => l.Nivel == nivel);
        if (desde is not null) q = q.Where(l => l.Fecha >= desde);
        if (hasta is not null) q = q.Where(l => l.Fecha < hasta.Value.AddDays(1));
        var total = await q.CountAsync();
        var items = await q.OrderByDescending(l => l.Id).Skip((p - 1) * s).Take(s)
            .Select(l => new { l.Id, l.Nivel, l.Origen, l.Mensaje, l.Ruta, l.Metodo, l.Ip, l.UsuarioId, l.Fecha }).ToListAsync();
        return Ok(new { total, page = p, pageSize = s, items });
    }
}
