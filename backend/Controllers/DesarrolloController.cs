using KitsuneHrm.Api.Data;
using KitsuneHrm.Api.Models;
using KitsuneHrm.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KitsuneHrm.Api.Controllers;

/// <summary>Evaluaciones de desempeño y objetivos. RRHH/Admin ven y gestionan todo; el supervisor, su equipo; el empleado, lo propio.</summary>
[ApiController, Authorize]
[Route("api/desarrollo")]
public class DesarrolloController(AppDbContext db, AuditService audit, ContextoUsuario ctx) : ControllerBase
{
    static readonly string[] EstadosObjetivo = { "PENDIENTE", "EN_PROGRESO", "CUMPLIDO", "NO_CUMPLIDO" };

    /// <summary>Ok=false: sin acceso. Ids=null: todos los empleados; lista: solo esos.</summary>
    async Task<(bool Ok, List<int>? Ids)> AlcanceAsync()
    {
        if (ctx.Tiene(Permisos.CapacitacionVer)) return (true, null);
        if (ctx.Tiene(Permisos.EvaluacionesGestionar) && ctx.Tiene(Permisos.EquipoVer)) return (true, await ctx.EquipoIdsAsync());
        return (false, null);
    }

    async Task<bool> PuedeGestionarAsync(int empleadoId)
    {
        if (!ctx.Tiene(Permisos.EvaluacionesGestionar) && !ctx.Tiene(Permisos.CapacitacionGestionar)) return false;
        if (ctx.Tiene(Permisos.CapacitacionVer)) return true;
        return ctx.Tiene(Permisos.EquipoVer) && (await ctx.EquipoIdsAsync()).Contains(empleadoId);
    }

    // ------------------------------------------------------------ evaluaciones
    [HttpGet("evaluaciones")]
    public async Task<IActionResult> Evaluaciones([FromQuery] int? empleadoId)
    {
        var (ok, ids) = await AlcanceAsync();
        if (!ok) return Forbid();
        var q = db.Evaluaciones.AsNoTracking().AsQueryable();
        if (ids is not null) q = q.Where(e => ids.Contains(e.EmpleadoId));
        if (empleadoId is not null) q = q.Where(e => e.EmpleadoId == empleadoId);

        var items = await q.OrderByDescending(e => e.Fecha).ThenByDescending(e => e.Id).Take(500).Select(e => new
        {
            e.Id, e.EmpleadoId, Codigo = e.Empleado.Codigo, Empleado = e.Empleado.Apellidos + ", " + e.Empleado.Nombres,
            e.Periodo, e.Fecha, e.Productividad, e.Calidad, e.TrabajoEquipo, e.Puntualidad, e.Total, e.Comentarios, e.EvaluadorEmpleadoId,
        }).ToListAsync();

        var evIds = items.Where(i => i.EvaluadorEmpleadoId != null).Select(i => i.EvaluadorEmpleadoId!.Value).Distinct().ToList();
        var nombres = await db.Empleados.AsNoTracking().Where(e => evIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e.Nombres + " " + e.Apellidos);
        return Ok(items.Select(i => new
        {
            i.Id, i.EmpleadoId, i.Codigo, i.Empleado, i.Periodo, i.Fecha, i.Productividad, i.Calidad, i.TrabajoEquipo, i.Puntualidad,
            i.Total, i.Comentarios, Evaluador = nombres.GetValueOrDefault(i.EvaluadorEmpleadoId ?? 0),
        }));
    }

    [HttpGet("evaluaciones/mias"), Authorize(Policy = Permisos.DesarrolloPropio)]
    public async Task<IActionResult> MisEvaluaciones()
    {
        var empId = await ctx.EmpleadoIdAsync();
        if (empId is null) return BadRequest(new { error = "Su usuario no está vinculado a un empleado.", codigo = "SIN_EMPLEADO" });
        var items = await db.Evaluaciones.AsNoTracking().Where(e => e.EmpleadoId == empId)
            .OrderByDescending(e => e.Fecha).ThenByDescending(e => e.Id)
            .Select(e => new { e.Id, e.Periodo, e.Fecha, e.Productividad, e.Calidad, e.TrabajoEquipo, e.Puntualidad, e.Total, e.Comentarios }).ToListAsync();
        return Ok(items);
    }

    [HttpPost("evaluaciones")]
    public async Task<IActionResult> CrearEvaluacion(EvaluacionRequest r)
    {
        if (!await PuedeGestionarAsync(r.EmpleadoId)) return Forbid();
        var yo = await ctx.EmpleadoIdAsync();
        if (yo == r.EmpleadoId) return BadRequest(new { error = "No puede evaluarse a sí mismo." });
        if (!await db.Empleados.AnyAsync(e => e.Id == r.EmpleadoId && e.Activo)) return BadRequest(new { error = "Empleado inválido o inactivo." });

        var total = Math.Round((r.Productividad + r.Calidad + r.TrabajoEquipo + r.Puntualidad) / 4m, 2);
        var e = new Evaluacion
        {
            EmpleadoId = r.EmpleadoId, EvaluadorEmpleadoId = yo, EvaluadorUsuarioId = ctx.UsuarioId, Periodo = r.Periodo.Trim(),
            Fecha = Tiempo.HoyGt, Productividad = r.Productividad, Calidad = r.Calidad, TrabajoEquipo = r.TrabajoEquipo,
            Puntualidad = r.Puntualidad, Total = total, Comentarios = r.Comentarios,
        };
        db.Evaluaciones.Add(e);
        await db.SaveChangesAsync();
        audit.Registrar("CREAR", "Evaluacion", e.Id, null, new { e.EmpleadoId, e.Periodo, e.Total });
        await db.SaveChangesAsync();
        return Created($"api/desarrollo/evaluaciones/{e.Id}", new { e.Id, e.Total });
    }

    // ------------------------------------------------------------ objetivos
    [HttpGet("objetivos")]
    public async Task<IActionResult> Objetivos([FromQuery] int? empleadoId)
    {
        var (ok, ids) = await AlcanceAsync();
        if (!ok) return Forbid();
        var q = db.Objetivos.AsNoTracking().AsQueryable();
        if (ids is not null) q = q.Where(o => ids.Contains(o.EmpleadoId));
        if (empleadoId is not null) q = q.Where(o => o.EmpleadoId == empleadoId);
        var items = await q.OrderBy(o => o.Estado == "CUMPLIDO" || o.Estado == "NO_CUMPLIDO").ThenBy(o => o.FechaLimite).Take(500).Select(o => new
        {
            o.Id, o.EmpleadoId, Codigo = o.Empleado.Codigo, Empleado = o.Empleado.Apellidos + ", " + o.Empleado.Nombres,
            o.Titulo, o.Descripcion, o.FechaLimite, o.Progreso, o.Estado,
        }).ToListAsync();
        return Ok(items);
    }

    [HttpGet("objetivos/mios"), Authorize(Policy = Permisos.DesarrolloPropio)]
    public async Task<IActionResult> MisObjetivos()
    {
        var empId = await ctx.EmpleadoIdAsync();
        if (empId is null) return BadRequest(new { error = "Su usuario no está vinculado a un empleado.", codigo = "SIN_EMPLEADO" });
        var items = await db.Objetivos.AsNoTracking().Where(o => o.EmpleadoId == empId)
            .OrderBy(o => o.Estado == "CUMPLIDO" || o.Estado == "NO_CUMPLIDO").ThenBy(o => o.FechaLimite)
            .Select(o => new { o.Id, o.Titulo, o.Descripcion, o.FechaLimite, o.Progreso, o.Estado }).ToListAsync();
        return Ok(items);
    }

    [HttpPost("objetivos")]
    public async Task<IActionResult> CrearObjetivo(ObjetivoRequest r)
    {
        if (!await PuedeGestionarAsync(r.EmpleadoId)) return Forbid();
        if (!await db.Empleados.AnyAsync(e => e.Id == r.EmpleadoId && e.Activo)) return BadRequest(new { error = "Empleado inválido o inactivo." });
        var o = new Objetivo
        {
            EmpleadoId = r.EmpleadoId, Titulo = r.Titulo.Trim(), Descripcion = r.Descripcion, FechaLimite = r.FechaLimite, CreadoPor = ctx.UsuarioId,
        };
        db.Objetivos.Add(o);
        await db.SaveChangesAsync();
        audit.Registrar("CREAR", "Objetivo", o.Id, null, new { o.EmpleadoId, o.Titulo, o.FechaLimite });
        await db.SaveChangesAsync();
        return Created($"api/desarrollo/objetivos/{o.Id}", new { o.Id });
    }

    /// <summary>El supervisor/RRHH edita todo; el propio empleado solo reporta progreso y estado (no puede marcar NO_CUMPLIDO).</summary>
    [HttpPut("objetivos/{id:int}")]
    public async Task<IActionResult> ActualizarObjetivo(int id, ObjetivoUpdateRequest r)
    {
        if (!EstadosObjetivo.Contains(r.Estado)) return BadRequest(new { error = "Estado inválido." });
        var o = await db.Objetivos.FirstOrDefaultAsync(x => x.Id == id);
        if (o is null) return NotFound(new { error = "Objetivo no encontrado." });

        var gestor = await PuedeGestionarAsync(o.EmpleadoId);
        var propio = ctx.Tiene(Permisos.DesarrolloPropio) && await ctx.EmpleadoIdAsync() == o.EmpleadoId;
        if (!gestor && !propio) return Forbid();
        if (!gestor && r.Estado == "NO_CUMPLIDO") return BadRequest(new { error = "Solo su supervisor o RRHH pueden marcar un objetivo como no cumplido." });

        var antes = new { o.Titulo, o.Progreso, o.Estado, o.FechaLimite };
        if (gestor)
        {
            if (!string.IsNullOrWhiteSpace(r.Titulo)) o.Titulo = r.Titulo.Trim();
            o.Descripcion = r.Descripcion;
            o.FechaLimite = r.FechaLimite;
        }
        o.Progreso = r.Estado == "CUMPLIDO" ? 100 : r.Progreso;
        o.Estado = r.Estado;
        audit.Registrar("ACTUALIZAR", "Objetivo", id, antes, new { o.Titulo, o.Progreso, o.Estado, o.FechaLimite });
        await db.SaveChangesAsync();
        return NoContent();
    }
}
