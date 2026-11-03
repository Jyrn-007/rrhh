using KitsuneHrm.Api.Data;
using KitsuneHrm.Api.Models;
using KitsuneHrm.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KitsuneHrm.Api.Controllers;

[ApiController, Authorize]
[Route("api/ausencias")]
public class AusenciasController(AppDbContext db, AuditService audit, ContextoUsuario ctx, VacacionesService vac) : ControllerBase
{
    static readonly string[] Tipos = { "VACACIONES", "PERMISO", "INCAPACIDAD" };

    static object Snapshot(Ausencia a) => new { a.EmpleadoId, a.Tipo, a.FechaInicio, a.FechaFin, a.Dias, a.Estado, a.Motivo };

    // ------------------------------------------------------------ autoservicio
    [HttpGet("saldo"), Authorize(Policy = Permisos.AusenciasSolicitar)]
    public async Task<IActionResult> Saldo()
    {
        var empId = await ctx.EmpleadoIdAsync();
        if (empId is null) return BadRequest(new { error = "Su usuario no está vinculado a un empleado.", codigo = "SIN_EMPLEADO" });
        return Ok(await vac.SaldoAsync(empId.Value));
    }

    [HttpGet("mias"), Authorize(Policy = Permisos.AusenciasSolicitar)]
    public async Task<IActionResult> Mias()
    {
        var empId = await ctx.EmpleadoIdAsync();
        if (empId is null) return BadRequest(new { error = "Su usuario no está vinculado a un empleado.", codigo = "SIN_EMPLEADO" });
        var items = await db.Ausencias.AsNoTracking().Where(a => a.EmpleadoId == empId)
            .OrderByDescending(a => a.FechaInicio).ThenByDescending(a => a.Id)
            .Select(a => new { a.Id, a.Tipo, a.FechaInicio, a.FechaFin, a.Dias, a.Motivo, a.Estado, a.ComentarioResolucion, a.ResueltoEn }).ToListAsync();
        return Ok(items);
    }

    [HttpPost, Authorize(Policy = Permisos.AusenciasSolicitar)]
    public async Task<IActionResult> Solicitar(AusenciaRequest r)
    {
        var empId = await ctx.EmpleadoIdAsync();
        if (empId is null) return BadRequest(new { error = "Su usuario no está vinculado a un empleado.", codigo = "SIN_EMPLEADO" });
        var error = await ValidarRango(empId.Value, r, esRegistroRrhh: false);
        if (error is not null) return BadRequest(new { error });

        var a = new Ausencia
        {
            EmpleadoId = empId.Value, Tipo = r.Tipo, FechaInicio = r.FechaInicio, FechaFin = r.FechaFin,
            Dias = Tiempo.DiasHabiles(r.FechaInicio, r.FechaFin), Motivo = r.Motivo, Estado = "PENDIENTE",
        };
        db.Ausencias.Add(a);
        await db.SaveChangesAsync();
        audit.Registrar("SOLICITAR", "Ausencia", a.Id, null, Snapshot(a));
        await db.SaveChangesAsync();
        return Created($"api/ausencias/{a.Id}", new { a.Id, a.Dias });
    }

    [HttpPost("{id:int}/cancelar"), Authorize(Policy = Permisos.AusenciasSolicitar)]
    public async Task<IActionResult> Cancelar(int id)
    {
        var empId = await ctx.EmpleadoIdAsync();
        var a = await db.Ausencias.FirstOrDefaultAsync(x => x.Id == id && x.EmpleadoId == empId);
        if (a is null) return NotFound(new { error = "Solicitud no encontrada." });
        var cancelable = a.Estado == "PENDIENTE" || (a.Estado == "APROBADA" && a.FechaInicio > Tiempo.HoyGt);
        if (!cancelable) return BadRequest(new { error = "Esta solicitud ya no se puede cancelar." });
        var antes = Snapshot(a);
        a.Estado = "CANCELADA";
        a.ResueltoEn = DateTime.UtcNow;
        audit.Registrar("CANCELAR", "Ausencia", id, antes, Snapshot(a));
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ------------------------------------------------------------ gestión (RRHH todos / supervisor su equipo)
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] string? estado, [FromQuery] int? empleadoId, [FromQuery] DateOnly? desde, [FromQuery] DateOnly? hasta,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (!ctx.Tiene(Permisos.AusenciasVer) && !ctx.Tiene(Permisos.AusenciasAprobar)) return Forbid();
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var q = db.Ausencias.AsNoTracking().AsQueryable();
        if (!ctx.Tiene(Permisos.AusenciasVer))
        {
            var ids = await ctx.EquipoIdsAsync();
            q = q.Where(a => ids.Contains(a.EmpleadoId));
        }
        if (!string.IsNullOrWhiteSpace(estado)) q = q.Where(a => a.Estado == estado);
        if (empleadoId is not null) q = q.Where(a => a.EmpleadoId == empleadoId);
        if (desde is not null) q = q.Where(a => a.FechaFin >= desde);
        if (hasta is not null) q = q.Where(a => a.FechaInicio <= hasta);

        var total = await q.CountAsync();
        var items = await q.OrderByDescending(a => a.Estado == "PENDIENTE").ThenByDescending(a => a.FechaInicio)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(a => new
            {
                a.Id, a.EmpleadoId, Codigo = a.Empleado.Codigo, Empleado = a.Empleado.Apellidos + ", " + a.Empleado.Nombres,
                a.Tipo, a.FechaInicio, a.FechaFin, a.Dias, a.Motivo, a.Estado, a.ComentarioResolucion, a.ResueltoEn,
            }).ToListAsync();
        return Ok(new { total, page, pageSize, items });
    }

    [HttpPost("{id:int}/resolver")]
    public async Task<IActionResult> Resolver(int id, ResolverRequest r)
    {
        var a = await db.Ausencias.FirstOrDefaultAsync(x => x.Id == id);
        if (a is null) return NotFound(new { error = "Solicitud no encontrada." });
        if (a.Estado != "PENDIENTE") return BadRequest(new { error = "La solicitud ya fue resuelta." });
        if (await ctx.EmpleadoIdAsync() == a.EmpleadoId) return BadRequest(new { error = "No puede resolver su propia solicitud." });

        var permitido = ctx.Tiene(Permisos.AusenciasGestionar)
            || (ctx.Tiene(Permisos.AusenciasAprobar) && (await ctx.EquipoIdsAsync()).Contains(a.EmpleadoId));
        if (!permitido) return Forbid();

        if (r.Aprobar && a.Tipo == "VACACIONES")
        {
            var s = await vac.SaldoAsync(a.EmpleadoId);
            if (s.Derecho - s.Usados < a.Dias) return BadRequest(new { error = "El empleado no tiene saldo de vacaciones suficiente." });
        }
        if (!r.Aprobar && string.IsNullOrWhiteSpace(r.Comentario)) return BadRequest(new { error = "Indique el motivo del rechazo." });

        var antes = Snapshot(a);
        a.Estado = r.Aprobar ? "APROBADA" : "RECHAZADA";
        a.AprobadoPor = ctx.UsuarioId;
        a.ComentarioResolucion = r.Comentario;
        a.ResueltoEn = DateTime.UtcNow;
        audit.Registrar(r.Aprobar ? "APROBAR" : "RECHAZAR", "Ausencia", id, antes, new { Snapshot = Snapshot(a), r.Comentario });
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>RRHH registra una ausencia ya aprobada (por ejemplo una incapacidad del IGSS).</summary>
    [HttpPost("registrar"), Authorize(Policy = Permisos.AusenciasGestionar)]
    public async Task<IActionResult> Registrar(AusenciaRegistrarRequest r)
    {
        if (!await db.Empleados.AnyAsync(e => e.Id == r.EmpleadoId && e.Activo)) return BadRequest(new { error = "Empleado inválido o inactivo." });
        var error = await ValidarRango(r.EmpleadoId, r, esRegistroRrhh: true);
        if (error is not null) return BadRequest(new { error });

        var a = new Ausencia
        {
            EmpleadoId = r.EmpleadoId, Tipo = r.Tipo, FechaInicio = r.FechaInicio, FechaFin = r.FechaFin,
            Dias = Tiempo.DiasHabiles(r.FechaInicio, r.FechaFin), Motivo = r.Motivo, Estado = "APROBADA",
            AprobadoPor = ctx.UsuarioId, ResueltoEn = DateTime.UtcNow, ComentarioResolucion = "Registrada por RRHH",
        };
        db.Ausencias.Add(a);
        await db.SaveChangesAsync();
        audit.Registrar("REGISTRAR", "Ausencia", a.Id, null, Snapshot(a));
        await db.SaveChangesAsync();
        return Created($"api/ausencias/{a.Id}", new { a.Id, a.Dias });
    }

    async Task<string?> ValidarRango(int empleadoId, AusenciaRequest r, bool esRegistroRrhh)
    {
        if (!Tipos.Contains(r.Tipo)) return "Tipo de solicitud inválido.";
        if (r.FechaFin < r.FechaInicio) return "La fecha final no puede ser anterior a la inicial.";
        if ((r.FechaFin.DayNumber - r.FechaInicio.DayNumber) > 90) return "El rango máximo es de 90 días.";

        var hoy = Tiempo.HoyGt;
        if (!esRegistroRrhh && r.Tipo != "INCAPACIDAD" && r.FechaInicio < hoy) return "No se pueden solicitar fechas pasadas.";
        if (!esRegistroRrhh && r.Tipo == "INCAPACIDAD" && r.FechaInicio < hoy.AddDays(-30)) return "Las incapacidades retroactivas deben registrarse con RRHH.";

        var dias = Tiempo.DiasHabiles(r.FechaInicio, r.FechaFin);
        if (dias == 0) return "El rango seleccionado no incluye días hábiles.";

        var traslape = await db.Ausencias.AnyAsync(x => x.EmpleadoId == empleadoId
            && (x.Estado == "PENDIENTE" || x.Estado == "APROBADA") && x.FechaInicio <= r.FechaFin && x.FechaFin >= r.FechaInicio);
        if (traslape) return "Ya existe una solicitud pendiente o aprobada en esas fechas.";

        if (r.Tipo == "VACACIONES")
        {
            var s = await vac.SaldoAsync(empleadoId);
            if (dias > s.Disponible) return $"Saldo insuficiente: solicita {dias} día(s) y tiene {s.Disponible} disponible(s).";
        }
        return null;
    }
}
