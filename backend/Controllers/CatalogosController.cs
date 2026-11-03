using KitsuneHrm.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KitsuneHrm.Api.Controllers;

[ApiController, Authorize]
[Route("api/catalogos")]
public class CatalogosController(AppDbContext db) : ControllerBase
{
    [HttpGet("roles")]
    public async Task<IActionResult> Roles() =>
        Ok(await db.Roles.AsNoTracking().Where(r => r.Activo).Select(r => new { r.Id, r.Nombre }).ToListAsync());

    [HttpGet("departamentos")]
    public async Task<IActionResult> Departamentos() =>
        Ok(await db.Departamentos.AsNoTracking().Where(d => d.Activo).OrderBy(d => d.Nombre).Select(d => new { d.Id, d.Nombre }).ToListAsync());

    [HttpGet("puestos")]
    public async Task<IActionResult> Puestos([FromQuery] int? departamentoId) =>
        Ok(await db.Puestos.AsNoTracking().Where(p => p.Activo && (departamentoId == null || p.DepartamentoId == departamentoId))
            .OrderBy(p => p.Nombre).Select(p => new { p.Id, p.Nombre, p.DepartamentoId }).ToListAsync());

    [HttpGet("sucursales")]
    public async Task<IActionResult> Sucursales() =>
        Ok(await db.Sucursales.AsNoTracking().Where(s => s.Activo).OrderBy(s => s.Nombre).Select(s => new { s.Id, s.Nombre }).ToListAsync());
}
