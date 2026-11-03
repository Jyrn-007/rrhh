using KitsuneHrm.Api.Data;
using KitsuneHrm.Api.Models;
using KitsuneHrm.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KitsuneHrm.Api.Controllers;

[ApiController, Authorize]
[Route("api/asistencia")]
public class AsistenciaController(AppDbContext db, AuditService audit, ContextoUsuario ctx) : ControllerBase
{
    // ------------------------------------------------------------ utilidades
    static object Fila(Asistencia a) => new
    {
        a.Id, a.Fecha, a.HoraEntrada, a.HoraSalida, a.Estado, a.MinutosRetardo, a.HorasTrabajadas, a.HorasExtra, a.Origen, a.Observaciones,
    };

    static object HorarioDto(Horario h) => new
    {
        h.Id, h.Nombre, HoraEntrada = Tiempo.Hhmm(h.HoraEntrada), HoraSalida = Tiempo.Hhmm(h.HoraSalida),
        h.ToleranciaMin, h.DiasLaborales, h.Activo,
    };

    public static async Task<Horario> HorarioDeAsync(AppDbContext db, int empleadoId)
    {
        var hid = await db.Empleados.AsNoTracking().Where(e => e.Id == empleadoId).Select(e => e.HorarioId).FirstOrDefaultAsync();
        Horario? h = null;
        if (hid is not null) h = await db.Horarios.AsNoTracking().FirstOrDefaultAsync(x => x.Id == hid && x.Activo);
        h ??= await db.Horarios.AsNoTracking().Where(x => x.Activo).OrderBy(x => x.Id).FirstOrDefaultAsync();
        return h ?? new Horario { Nombre = "Predeterminado", HoraEntrada = new TimeOnly(8, 0), HoraSalida = new TimeOnly(17, 0), ToleranciaMin = 10 };
    }

    /// <summary>Calcula retardo, horas trabajadas y horas extra (la jornada es la duración del horario; no descuenta almuerzo).</summary>
    public static void Calcular(Asistencia a, Horario h)
    {
        if (a.HoraEntrada is { } ent)
        {
            var programada = a.Fecha.ToDateTime(h.HoraEntrada);
            var retraso = (ent - programada).TotalMinutes;
            a.MinutosRetardo = retraso > h.ToleranciaMin ? (int)Math.Round(retraso) : 0;
            a.Estado = a.MinutosRetardo > 0 ? "RETARDO" : "PRESENTE";
            if (a.HoraSalida is { } sal && sal > ent)
            {
                var horas = (sal - ent).TotalHours;
                var jornada = (h.HoraSalida.ToTimeSpan() - h.HoraEntrada.ToTimeSpan()).TotalHours;
                a.HorasTrabajadas = Math.Round((decimal)horas, 2);
                a.HorasExtra = Math.Round((decimal)Math.Max(0, horas - jornada), 2);
            }
        }
    }

    // ------------------------------------------------------------ horarios
    [HttpGet("horarios"), Authorize(Policy = Permisos.AsistenciaVer)]
    public async Task<IActionResult> Horarios()
    {
        var hs = await db.Horarios.AsNoTracking().OrderBy(h => h.Nombre).ToListAsync();
        return Ok(hs.Select(HorarioDto));
    }

    [HttpPost("horarios"), Authorize(Policy = Permisos.AsistenciaGestionar)]
    public async Task<IActionResult> CrearHorario(HorarioRequest r)
    {
        if (!TryParseHorario(r, out var ent, out var sal, out var error)) return BadRequest(new { error });
        var nombre = r.Nombre.Trim();
        if (await db.Horarios.AnyAsync(h => h.Nombre == nombre)) return BadRequest(new { error = "Ya existe un horario con ese nombre." });
        var h = new Horario { Nombre = nombre, HoraEntrada = ent, HoraSalida = sal, ToleranciaMin = r.ToleranciaMin, DiasLaborales = r.DiasLaborales, Activo = r.Activo };
        db.Horarios.Add(h);
        await db.SaveChangesAsync();
        audit.Registrar("CREAR", "Horario", h.Id, null, HorarioDto(h));
        await db.SaveChangesAsync();
        return Created($"api/asistencia/horarios/{h.Id}", new { h.Id });
    }

    [HttpPut("horarios/{id:int}"), Authorize(Policy = Permisos.AsistenciaGestionar)]
    public async Task<IActionResult> EditarHorario(int id, HorarioRequest r)
    {
        var h = await db.Horarios.FirstOrDefaultAsync(x => x.Id == id);
        if (h is null) return NotFound(new { error = "Horario no encontrado." });
        if (!TryParseHorario(r, out var ent, out var sal, out var error)) return BadRequest(new { error });
        var nombre = r.Nombre.Trim();
        if (await db.Horarios.AnyAsync(x => x.Nombre == nombre && x.Id != id)) return BadRequest(new { error = "Ya existe un horario con ese nombre." });
        var antes = HorarioDto(h);
        h.Nombre = nombre;
        h.HoraEntrada = ent;
        h.HoraSalida = sal;
        h.ToleranciaMin = r.ToleranciaMin;
        h.DiasLaborales = r.DiasLaborales;
        h.Activo = r.Activo;
        audit.Registrar("ACTUALIZAR", "Horario", id, antes, HorarioDto(h));
        await db.SaveChangesAsync();
        return NoContent();
    }

    static bool TryParseHorario(HorarioRequest r, out TimeOnly ent, out TimeOnly sal, out string error)
    {
        error = "";
        sal = default;
        if (!TimeOnly.TryParse(r.HoraEntrada, out ent) || !TimeOnly.TryParse(r.HoraSalida, out sal)) { error = "Hora inválida (use HH:mm)."; return false; }
        if (sal <= ent) { error = "La hora de salida debe ser posterior a la de entrada."; return false; }
        return true;
    }

    // ------------------------------------------------------------ marcación propia
    [HttpPost("marcar"), Authorize(Policy = Permisos.AsistenciaMarcar)]
    public async Task<IActionResult> Marcar()
    {
        var empId = await ctx.EmpleadoIdAsync();
        if (empId is null) return BadRequest(new { error = "Su usuario no está vinculado a un empleado.", codigo = "SIN_EMPLEADO" });
        var id = empId.Value;
        if (!await db.Empleados.AnyAsync(e => e.Id == id && e.Activo)) return BadRequest(new { error = "Su empleado está inactivo." });

        var ahora = Tiempo.AhoraGt;
        var hoy = DateOnly.FromDateTime(ahora);
        var h = await HorarioDeAsync(db, id);
        var a = await db.Asistencias.FirstOrDefaultAsync(x => x.EmpleadoId == id && x.Fecha == hoy);

        string accion;
        if (a is null)
        {
            a = new Asistencia { EmpleadoId = id, Fecha = hoy, HoraEntrada = ahora, Origen = "MARCA" };
            db.Asistencias.Add(a);
            accion = "ENTRADA";
        }
        else if (a.HoraEntrada is null)
        {
            a.HoraEntrada = ahora;
            a.Origen = "MARCA";
            accion = "ENTRADA";
        }
        else if (a.HoraSalida is null)
        {
            if ((ahora - a.HoraEntrada.Value).TotalMinutes < 1) return BadRequest(new { error = "Acaba de marcar su entrada. Espere un minuto para marcar la salida." });
            a.HoraSalida = ahora;
            accion = "SALIDA";
        }
        else return BadRequest(new { error = "Ya registró su entrada y salida de hoy." });

        Calcular(a, h);
        audit.Registrar("MARCAR_" + accion, "Asistencia", $"{id}:{hoy:yyyy-MM-dd}", null, new { a.HoraEntrada, a.HoraSalida, a.Estado });
        await db.SaveChangesAsync();
        return Ok(new { accion, asistencia = Fila(a) });
    }

    [HttpGet("mias"), Authorize(Policy = Permisos.AsistenciaMarcar)]
    public async Task<IActionResult> Mias([FromQuery] DateOnly? desde, [FromQuery] DateOnly? hasta)
    {
        var empId = await ctx.EmpleadoIdAsync();
        if (empId is null) return BadRequest(new { error = "Su usuario no está vinculado a un empleado.", codigo = "SIN_EMPLEADO" });
        var id = empId.Value;
        var hoy = Tiempo.HoyGt;
        var d = desde ?? new DateOnly(hoy.Year, hoy.Month, 1);
        var h = hasta ?? hoy;

        var items = await db.Asistencias.AsNoTracking().Where(a => a.EmpleadoId == id && a.Fecha >= d && a.Fecha <= h)
            .OrderByDescending(a => a.Fecha).ToListAsync();
        var delDia = await db.Asistencias.AsNoTracking().FirstOrDefaultAsync(a => a.EmpleadoId == id && a.Fecha == hoy);
        var horario = await HorarioDeAsync(db, id);

        return Ok(new
        {
            hoy = delDia is null ? null : Fila(delDia),
            horario = new { entrada = Tiempo.Hhmm(horario.HoraEntrada), salida = Tiempo.Hhmm(horario.HoraSalida), horario.ToleranciaMin },
            resumen = new
            {
                presentes = items.Count(a => a.Estado == "PRESENTE"),
                retardos = items.Count(a => a.Estado == "RETARDO"),
                faltas = items.Count(a => a.Estado == "FALTA"),
                horasExtra = items.Sum(a => a.HorasExtra),
            },
            items = items.Select(Fila),
        });
    }

    // ------------------------------------------------------------ equipo (supervisor)
    [HttpGet("equipo"), Authorize(Policy = Permisos.EquipoVer)]
    public async Task<IActionResult> Equipo([FromQuery] DateOnly? fecha)
    {
        var dia = fecha ?? Tiempo.HoyGt;
        var ids = await ctx.EquipoIdsAsync();
        var emps = await db.Empleados.AsNoTracking().Where(e => ids.Contains(e.Id))
            .OrderBy(e => e.Apellidos).ThenBy(e => e.Nombres)
            .Select(e => new { e.Id, e.Codigo, Nombre = e.Apellidos + ", " + e.Nombres }).ToListAsync();
        var regs = await db.Asistencias.AsNoTracking().Where(a => a.Fecha == dia && ids.Contains(a.EmpleadoId)).ToListAsync();
        var aus = await db.Ausencias.AsNoTracking()
            .Where(a => a.Estado == "APROBADA" && a.FechaInicio <= dia && a.FechaFin >= dia && ids.Contains(a.EmpleadoId)).ToListAsync();

        return Ok(emps.Select(e =>
        {
            var r = regs.FirstOrDefault(x => x.EmpleadoId == e.Id);
            var au = aus.FirstOrDefault(x => x.EmpleadoId == e.Id);
            return new { e.Id, e.Codigo, e.Nombre, asistencia = r is null ? null : Fila(r), ausencia = au?.Tipo };
        }));
    }

    // ------------------------------------------------------------ vista RRHH
    [HttpGet, Authorize(Policy = Permisos.AsistenciaVer)]
    public async Task<IActionResult> Listar([FromQuery] DateOnly? desde, [FromQuery] DateOnly? hasta, [FromQuery] int? empleadoId,
        [FromQuery] string? estado, [FromQuery] int page = 1, [FromQuery] int pageSize = 30)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var hoy = Tiempo.HoyGt;
        var d = desde ?? hoy;
        var h = hasta ?? hoy;

        var q = db.Asistencias.AsNoTracking().Where(a => a.Fecha >= d && a.Fecha <= h);
        if (empleadoId is not null) q = q.Where(a => a.EmpleadoId == empleadoId);
        if (!string.IsNullOrWhiteSpace(estado)) q = q.Where(a => a.Estado == estado);

        var total = await q.CountAsync();
        var items = await q.OrderByDescending(a => a.Fecha).ThenBy(a => a.Empleado.Apellidos)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(a => new
            {
                a.Id, a.EmpleadoId, Codigo = a.Empleado.Codigo, Empleado = a.Empleado.Apellidos + ", " + a.Empleado.Nombres,
                a.Fecha, a.HoraEntrada, a.HoraSalida, a.Estado, a.MinutosRetardo, a.HorasTrabajadas, a.HorasExtra, a.Origen, a.Observaciones,
            }).ToListAsync();
        return Ok(new { total, page, pageSize, items });
    }

    /// <summary>Crea o corrige el registro de un día (corrección manual con auditoría).</summary>
    [HttpPost("manual"), Authorize(Policy = Permisos.AsistenciaGestionar)]
    public async Task<IActionResult> Manual(AsistenciaManualRequest r)
    {
        if (r.Fecha > Tiempo.HoyGt) return BadRequest(new { error = "No se puede registrar asistencia de fechas futuras." });
        if (!await db.Empleados.AnyAsync(e => e.Id == r.EmpleadoId)) return BadRequest(new { error = "Empleado inválido." });

        TimeOnly? ent = null, sal = null;
        if (!string.IsNullOrWhiteSpace(r.HoraEntrada))
        {
            if (!TimeOnly.TryParse(r.HoraEntrada, out var e)) return BadRequest(new { error = "Hora de entrada inválida." });
            ent = e;
        }
        if (!string.IsNullOrWhiteSpace(r.HoraSalida))
        {
            if (!TimeOnly.TryParse(r.HoraSalida, out var s)) return BadRequest(new { error = "Hora de salida inválida." });
            sal = s;
        }
        if (ent is null) return BadRequest(new { error = "Indique la hora de entrada." });
        if (sal is not null && sal <= ent) return BadRequest(new { error = "La salida debe ser posterior a la entrada." });

        var h = await HorarioDeAsync(db, r.EmpleadoId);
        var a = await db.Asistencias.FirstOrDefaultAsync(x => x.EmpleadoId == r.EmpleadoId && x.Fecha == r.Fecha);
        object? antes = a is null ? null : Fila(a);
        if (a is null)
        {
            a = new Asistencia { EmpleadoId = r.EmpleadoId, Fecha = r.Fecha };
            db.Asistencias.Add(a);
        }
        a.HoraEntrada = r.Fecha.ToDateTime(ent.Value);
        a.HoraSalida = sal is null ? null : r.Fecha.ToDateTime(sal.Value);
        a.HorasTrabajadas = 0;
        a.HorasExtra = 0;
        a.Origen = "MANUAL";
        a.Observaciones = r.Observaciones;
        Calcular(a, h);

        audit.Registrar(antes is null ? "CREAR" : "ACTUALIZAR", "Asistencia", $"{r.EmpleadoId}:{r.Fecha:yyyy-MM-dd}", antes, Fila(a));
        await db.SaveChangesAsync();
        return Ok(Fila(a));
    }

    /// <summary>Marca FALTA a quienes no registraron asistencia en un día laborable y no tienen ausencia aprobada.</summary>
    [HttpPost("generar-faltas"), Authorize(Policy = Permisos.AsistenciaGestionar)]
    public async Task<IActionResult> GenerarFaltas(FechaRequest r)
    {
        if (r.Fecha >= Tiempo.HoyGt) return BadRequest(new { error = "Solo se pueden generar faltas de días anteriores a hoy." });

        var empleados = await db.Empleados.AsNoTracking().Where(e => e.Activo && e.FechaIngreso <= r.Fecha)
            .Select(e => new { e.Id, e.HorarioId }).ToListAsync();
        var conRegistro = (await db.Asistencias.AsNoTracking().Where(a => a.Fecha == r.Fecha).Select(a => a.EmpleadoId).ToListAsync()).ToHashSet();
        var conAusencia = (await db.Ausencias.AsNoTracking()
            .Where(a => a.Estado == "APROBADA" && a.FechaInicio <= r.Fecha && a.FechaFin >= r.Fecha).Select(a => a.EmpleadoId).ToListAsync()).ToHashSet();
        var horarios = await db.Horarios.AsNoTracking().Where(h => h.Activo).ToListAsync();
        var porDefecto = horarios.OrderBy(h => h.Id).FirstOrDefault();
        var dia = Tiempo.DiaSemana(r.Fecha);

        var creadas = 0;
        foreach (var e in empleados)
        {
            if (conRegistro.Contains(e.Id) || conAusencia.Contains(e.Id)) continue;
            var h = horarios.FirstOrDefault(x => x.Id == e.HorarioId) ?? porDefecto;
            var laborables = h?.DiasLaborales ?? "1,2,3,4,5";
            if (!laborables.Split(',').Contains(dia.ToString())) continue;
            db.Asistencias.Add(new Asistencia { EmpleadoId = e.Id, Fecha = r.Fecha, Estado = "FALTA", Origen = "SISTEMA" });
            creadas++;
        }
        audit.Registrar("GENERAR_FALTAS", "Asistencia", r.Fecha.ToString("yyyy-MM-dd"), null, new { creadas });
        await db.SaveChangesAsync();
        return Ok(new { creadas });
    }
}
