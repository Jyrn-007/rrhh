using KitsuneHrm.Api.Data;
using KitsuneHrm.Api.Models;
using KitsuneHrm.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KitsuneHrm.Api.Controllers;

[ApiController, Authorize]
[Route("api/capacitaciones")]
public class CapacitacionController(AppDbContext db, AuditService audit, ContextoUsuario ctx) : ControllerBase
{
    static readonly string[] Estados = { "PROGRAMADA", "EN_CURSO", "FINALIZADA", "CANCELADA" };
    static readonly string[] EstadosParticipante = { "INSCRITO", "COMPLETADO", "NO_COMPLETADO" };

    static object Snapshot(Capacitacion c) => new { c.Nombre, c.Proveedor, c.FechaInicio, c.FechaFin, c.Horas, c.Estado };

    [HttpGet, Authorize(Policy = Permisos.CapacitacionVer)]
    public async Task<IActionResult> Listar([FromQuery] string? estado)
    {
        var q = db.Capacitaciones.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(estado)) q = q.Where(c => c.Estado == estado);
        var items = await q.OrderByDescending(c => c.FechaInicio).Select(c => new
        {
            c.Id, c.Nombre, c.Descripcion, c.Proveedor, c.FechaInicio, c.FechaFin, c.Horas, c.Estado,
            Participantes = db.CapacitacionParticipantes.Count(p => p.CapacitacionId == c.Id),
        }).ToListAsync();
        return Ok(items);
    }

    [HttpGet("{id:int}"), Authorize(Policy = Permisos.CapacitacionVer)]
    public async Task<IActionResult> Obtener(int id)
    {
        var c = await db.Capacitaciones.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return NotFound(new { error = "Capacitación no encontrada." });
        var participantes = await db.CapacitacionParticipantes.AsNoTracking().Where(p => p.CapacitacionId == id)
            .OrderBy(p => p.Empleado.Apellidos)
            .Select(p => new { p.Id, p.EmpleadoId, Codigo = p.Empleado.Codigo, Empleado = p.Empleado.Apellidos + ", " + p.Empleado.Nombres, p.Estado, p.Calificacion })
            .ToListAsync();
        return Ok(new { capacitacion = c, participantes });
    }

    [HttpPost, Authorize(Policy = Permisos.CapacitacionGestionar)]
    public async Task<IActionResult> Crear(CapacitacionRequest r)
    {
        var error = Validar(r);
        if (error is not null) return BadRequest(new { error });
        var c = new Capacitacion
        {
            Nombre = r.Nombre.Trim(), Descripcion = r.Descripcion, Proveedor = r.Proveedor,
            FechaInicio = r.FechaInicio, FechaFin = r.FechaFin, Horas = r.Horas, Estado = r.Estado,
        };
        db.Capacitaciones.Add(c);
        await db.SaveChangesAsync();
        audit.Registrar("CREAR", "Capacitacion", c.Id, null, Snapshot(c));
        await db.SaveChangesAsync();
        return Created($"api/capacitaciones/{c.Id}", new { c.Id });
    }

    [HttpPut("{id:int}"), Authorize(Policy = Permisos.CapacitacionGestionar)]
    public async Task<IActionResult> Editar(int id, CapacitacionRequest r)
    {
        var c = await db.Capacitaciones.FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return NotFound(new { error = "Capacitación no encontrada." });
        var error = Validar(r);
        if (error is not null) return BadRequest(new { error });
        var antes = Snapshot(c);
        c.Nombre = r.Nombre.Trim();
        c.Descripcion = r.Descripcion;
        c.Proveedor = r.Proveedor;
        c.FechaInicio = r.FechaInicio;
        c.FechaFin = r.FechaFin;
        c.Horas = r.Horas;
        c.Estado = r.Estado;
        audit.Registrar("ACTUALIZAR", "Capacitacion", id, antes, Snapshot(c));
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id:int}/participantes"), Authorize(Policy = Permisos.CapacitacionGestionar)]
    public async Task<IActionResult> AgregarParticipantes(int id, ParticipantesRequest r)
    {
        var c = await db.Capacitaciones.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return NotFound(new { error = "Capacitación no encontrada." });
        if (c.Estado == "CANCELADA") return BadRequest(new { error = "La capacitación está cancelada." });
        if (r.EmpleadoIds.Count == 0) return BadRequest(new { error = "Seleccione al menos un empleado." });

        var validos = await db.Empleados.AsNoTracking().Where(e => e.Activo && r.EmpleadoIds.Contains(e.Id)).Select(e => e.Id).ToListAsync();
        var existentes = (await db.CapacitacionParticipantes.AsNoTracking().Where(p => p.CapacitacionId == id).Select(p => p.EmpleadoId).ToListAsync()).ToHashSet();
        var nuevos = validos.Where(e => !existentes.Contains(e)).Distinct().ToList();
        foreach (var e in nuevos) db.CapacitacionParticipantes.Add(new CapacitacionParticipante { CapacitacionId = id, EmpleadoId = e });
        audit.Registrar("INSCRIBIR", "Capacitacion", id, null, new { agregados = nuevos.Count });
        await db.SaveChangesAsync();
        return Ok(new { agregados = nuevos.Count });
    }

    [HttpPut("participantes/{pid:int}"), Authorize(Policy = Permisos.CapacitacionGestionar)]
    public async Task<IActionResult> ActualizarParticipante(int pid, ParticipanteUpdateRequest r)
    {
        if (!EstadosParticipante.Contains(r.Estado)) return BadRequest(new { error = "Estado inválido." });
        var p = await db.CapacitacionParticipantes.FirstOrDefaultAsync(x => x.Id == pid);
        if (p is null) return NotFound(new { error = "Participante no encontrado." });
        var antes = new { p.Estado, p.Calificacion };
        p.Estado = r.Estado;
        p.Calificacion = r.Calificacion;
        audit.Registrar("ACTUALIZAR", "CapacitacionParticipante", pid, antes, new { p.Estado, p.Calificacion });
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("participantes/{pid:int}"), Authorize(Policy = Permisos.CapacitacionGestionar)]
    public async Task<IActionResult> QuitarParticipante(int pid)
    {
        var p = await db.CapacitacionParticipantes.FirstOrDefaultAsync(x => x.Id == pid);
        if (p is null) return NotFound(new { error = "Participante no encontrado." });
        audit.Registrar("QUITAR", "CapacitacionParticipante", pid, new { p.CapacitacionId, p.EmpleadoId }, null);
        db.CapacitacionParticipantes.Remove(p);
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Mi historial de formación.</summary>
    [HttpGet("mias"), Authorize(Policy = Permisos.DesarrolloPropio)]
    public async Task<IActionResult> Mias()
    {
        var empId = await ctx.EmpleadoIdAsync();
        if (empId is null) return BadRequest(new { error = "Su usuario no está vinculado a un empleado.", codigo = "SIN_EMPLEADO" });
        var items = await db.CapacitacionParticipantes.AsNoTracking().Where(p => p.EmpleadoId == empId)
            .OrderByDescending(p => p.Capacitacion.FechaInicio)
            .Select(p => new
            {
                p.Id, Capacitacion = p.Capacitacion.Nombre, p.Capacitacion.Proveedor, p.Capacitacion.FechaInicio, p.Capacitacion.FechaFin,
                p.Capacitacion.Horas, EstadoCapacitacion = p.Capacitacion.Estado, p.Estado, p.Calificacion,
            }).ToListAsync();
        return Ok(items);
    }

    static string? Validar(CapacitacionRequest r)
    {
        if (!Estados.Contains(r.Estado)) return "Estado inválido.";
        if (r.FechaFin < r.FechaInicio) return "La fecha final no puede ser anterior a la inicial.";
        return null;
    }
}
