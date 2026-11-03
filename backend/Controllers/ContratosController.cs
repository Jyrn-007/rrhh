using KitsuneHrm.Api.Data;
using KitsuneHrm.Api.Models;
using KitsuneHrm.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KitsuneHrm.Api.Controllers;

[ApiController]
[Route("api/contratos")]
public class ContratosController(AppDbContext db, AuditService audit) : ControllerBase
{
    static readonly string[] Tipos = { "INDEFINIDO", "PLAZO_FIJO", "POR_OBRA", "TEMPORAL", "PRUEBA" };

    static object Snapshot(Contrato c) => new { c.EmpleadoId, c.Tipo, c.FechaInicio, c.FechaFin, c.Salario, c.Estado, c.DocumentoUrl };

    [HttpGet, Authorize(Policy = Permisos.ContratosVer)]
    public async Task<IActionResult> Listar([FromQuery] int? empleadoId, [FromQuery] string? estado, [FromQuery] bool? porVencer,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var hoy = Tiempo.HoyGt;

        // Los contratos con fecha de fin vencida pasan a VENCIDO
        await db.Contratos.Where(c => c.Estado == "VIGENTE" && c.FechaFin != null && c.FechaFin < hoy)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Estado, "VENCIDO"));

        var q = db.Contratos.AsNoTracking().AsQueryable();
        if (empleadoId is not null) q = q.Where(c => c.EmpleadoId == empleadoId);
        if (!string.IsNullOrWhiteSpace(estado)) q = q.Where(c => c.Estado == estado);
        if (porVencer == true)
        {
            var limite = hoy.AddDays(30);
            q = q.Where(c => c.Estado == "VIGENTE" && c.FechaFin != null && c.FechaFin <= limite);
        }

        var total = await q.CountAsync();
        var items = await q.OrderByDescending(c => c.FechaInicio).ThenByDescending(c => c.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(c => new
            {
                c.Id, c.EmpleadoId, Codigo = c.Empleado.Codigo, Empleado = c.Empleado.Apellidos + ", " + c.Empleado.Nombres,
                c.Tipo, c.FechaInicio, c.FechaFin, c.Salario, c.Estado, c.DocumentoUrl, c.Observaciones,
            }).ToListAsync();
        return Ok(new { total, page, pageSize, items });
    }

    [HttpPost, Authorize(Policy = Permisos.ContratosGestionar)]
    public async Task<IActionResult> Crear(ContratoRequest r)
    {
        var error = await Validar(r);
        if (error is not null) return BadRequest(new { error });

        // Un empleado solo tiene un contrato vigente: el anterior pasa a RENOVADO
        var previos = await db.Contratos.Where(c => c.EmpleadoId == r.EmpleadoId && c.Estado == "VIGENTE").ToListAsync();
        foreach (var p in previos) p.Estado = "RENOVADO";

        var c = new Contrato
        {
            EmpleadoId = r.EmpleadoId, Tipo = r.Tipo, FechaInicio = r.FechaInicio,
            FechaFin = r.Tipo == "INDEFINIDO" ? null : r.FechaFin,
            Salario = r.Salario, DocumentoUrl = r.DocumentoUrl, Observaciones = r.Observaciones, Estado = "VIGENTE",
        };
        db.Contratos.Add(c);
        await db.SaveChangesAsync();
        audit.Registrar("CREAR", "Contrato", c.Id, null, Snapshot(c));
        await db.SaveChangesAsync();
        return Created($"api/contratos/{c.Id}", new { c.Id });
    }

    [HttpPut("{id:int}"), Authorize(Policy = Permisos.ContratosGestionar)]
    public async Task<IActionResult> Editar(int id, ContratoRequest r)
    {
        var c = await db.Contratos.FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return NotFound(new { error = "Contrato no encontrado." });
        if (c.Estado != "VIGENTE") return BadRequest(new { error = "Solo se pueden editar contratos vigentes." });
        if (r.EmpleadoId != c.EmpleadoId) return BadRequest(new { error = "No se puede cambiar el empleado de un contrato." });
        var error = await Validar(r);
        if (error is not null) return BadRequest(new { error });

        var antes = Snapshot(c);
        c.Tipo = r.Tipo;
        c.FechaInicio = r.FechaInicio;
        c.FechaFin = r.Tipo == "INDEFINIDO" ? null : r.FechaFin;
        c.Salario = r.Salario;
        c.DocumentoUrl = r.DocumentoUrl;
        c.Observaciones = r.Observaciones;
        audit.Registrar("ACTUALIZAR", "Contrato", id, antes, Snapshot(c));
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id:int}/rescindir"), Authorize(Policy = Permisos.ContratosGestionar)]
    public async Task<IActionResult> Rescindir(int id)
    {
        var c = await db.Contratos.FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return NotFound(new { error = "Contrato no encontrado." });
        if (c.Estado != "VIGENTE") return BadRequest(new { error = "Solo se pueden rescindir contratos vigentes." });
        var antes = Snapshot(c);
        c.Estado = "RESCINDIDO";
        audit.Registrar("RESCINDIR", "Contrato", id, antes, Snapshot(c));
        await db.SaveChangesAsync();
        return NoContent();
    }

    async Task<string?> Validar(ContratoRequest r)
    {
        if (!Tipos.Contains(r.Tipo)) return "Tipo de contrato inválido.";
        if (!await db.Empleados.AnyAsync(e => e.Id == r.EmpleadoId && e.Activo)) return "Empleado inválido o inactivo.";
        if (r.Tipo != "INDEFINIDO")
        {
            if (r.FechaFin is null) return "Este tipo de contrato requiere fecha de finalización.";
            if (r.FechaFin <= r.FechaInicio) return "La fecha de fin debe ser posterior a la de inicio.";
        }
        return null;
    }
}
