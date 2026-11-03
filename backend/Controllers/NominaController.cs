using KitsuneHrm.Api.Data;
using KitsuneHrm.Api.Models;
using KitsuneHrm.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KitsuneHrm.Api.Controllers;

/// <summary>
/// Nómina mensual. Los porcentajes y montos (IGSS laboral, bonificación incentivo, factor de hora extra)
/// salen de la tabla `configuracion` y deben validarse con contabilidad. El ISR se captura manualmente por empleado.
/// </summary>
[ApiController, Authorize]
[Route("api/nomina")]
public class NominaController(AppDbContext db, AuditService audit, ContextoUsuario ctx) : ControllerBase
{
    static decimal R(decimal v) => Tiempo.R(v);

    static void Recalcular(NominaDetalle d, decimal igssTasa)
    {
        d.TotalDevengado = R(d.SalarioBase + d.MontoHorasExtra + d.BonificacionIncentivo + d.OtrosBonos);
        var baseIgss = Math.Max(0m, d.SalarioBase - d.DescuentoFaltas + d.MontoHorasExtra);
        d.Igss = R(baseIgss * igssTasa);
        d.TotalDescuentos = R(d.Igss + d.Isr + d.OtrosDescuentos + d.DescuentoFaltas);
        d.Neto = R(d.TotalDevengado - d.TotalDescuentos);
    }

    static void Totales(Nomina n, IEnumerable<NominaDetalle> detalles)
    {
        var lista = detalles.ToList();
        n.TotalDevengado = R(lista.Sum(d => d.TotalDevengado));
        n.TotalDescuentos = R(lista.Sum(d => d.TotalDescuentos));
        n.TotalNeto = R(lista.Sum(d => d.Neto));
    }

    // ------------------------------------------------------------ gestión
    [HttpGet, Authorize(Policy = Permisos.NominaVer)]
    public async Task<IActionResult> Listar()
    {
        var items = await db.Nominas.AsNoTracking().OrderByDescending(n => n.Anio).ThenByDescending(n => n.Mes)
            .Select(n => new
            {
                n.Id, n.Anio, n.Mes, n.Estado, n.TotalDevengado, n.TotalDescuentos, n.TotalNeto,
                Empleados = n.Detalles.Count, n.GeneradoEn, n.CerradoEn,
            }).ToListAsync();
        return Ok(items);
    }

    [HttpGet("{id:int}"), Authorize(Policy = Permisos.NominaVer)]
    public async Task<IActionResult> Obtener(int id)
    {
        var n = await db.Nominas.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (n is null) return NotFound(new { error = "Nómina no encontrada." });
        var detalle = await db.NominaDetalles.AsNoTracking().Where(d => d.NominaId == id)
            .OrderBy(d => d.Empleado.Apellidos).ThenBy(d => d.Empleado.Nombres)
            .Select(d => new
            {
                d.Id, d.EmpleadoId, Codigo = d.Empleado.Codigo, Empleado = d.Empleado.Apellidos + ", " + d.Empleado.Nombres,
                d.SalarioBase, d.DiasFaltados, d.DescuentoFaltas, d.HorasExtra, d.MontoHorasExtra, d.BonificacionIncentivo,
                d.OtrosBonos, d.TotalDevengado, d.Igss, d.Isr, d.OtrosDescuentos, d.TotalDescuentos, d.Neto, d.Observaciones,
            }).ToListAsync();
        return Ok(new
        {
            nomina = new { n.Id, n.Anio, n.Mes, n.FechaInicio, n.FechaFin, n.Estado, n.TotalDevengado, n.TotalDescuentos, n.TotalNeto, n.GeneradoEn, n.CerradoEn },
            detalle,
        });
    }

    [HttpPost("generar"), Authorize(Policy = Permisos.NominaGestionar)]
    public async Task<IActionResult> Generar(NominaGenerarRequest r)
    {
        var inicio = new DateOnly(r.Anio, r.Mes, 1);
        var fin = inicio.AddMonths(1).AddDays(-1);
        if (inicio > Tiempo.HoyGt) return BadRequest(new { error = "No se puede generar la nómina de un mes futuro." });
        if (await db.Nominas.AnyAsync(n => n.Anio == r.Anio && n.Mes == r.Mes))
            return BadRequest(new { error = "Ya existe una nómina para ese mes. Elimine el borrador si desea regenerarla." });

        var contratos = await db.Contratos.AsNoTracking()
            .Where(c => c.Estado == "VIGENTE" && c.FechaInicio <= fin && c.Empleado.Activo).ToListAsync();
        var vigentes = contratos.GroupBy(c => c.EmpleadoId).Select(g => g.OrderByDescending(c => c.FechaInicio).First()).ToList();
        if (vigentes.Count == 0) return BadRequest(new { error = "No hay empleados activos con contrato vigente." });

        var asistencias = await db.Asistencias.AsNoTracking().Where(a => a.Fecha >= inicio && a.Fecha <= fin)
            .Select(a => new { a.EmpleadoId, a.Estado, a.HorasExtra }).ToListAsync();
        var igss = await db.ConfigDecimalAsync("nomina.igss_laboral", 0.0483m);
        var bonificacion = await db.ConfigDecimalAsync("nomina.bonificacion_incentivo", 250m);
        var factorHe = await db.ConfigDecimalAsync("nomina.factor_hora_extra", 1.5m);

        var nomina = new Nomina { Anio = r.Anio, Mes = r.Mes, FechaInicio = inicio, FechaFin = fin, GeneradoPor = ctx.UsuarioId };
        foreach (var c in vigentes)
        {
            var mias = asistencias.Where(a => a.EmpleadoId == c.EmpleadoId).ToList();
            var faltas = Math.Min(30, mias.Count(a => a.Estado == "FALTA"));
            var horasExtra = mias.Sum(a => a.HorasExtra);
            var valorDia = c.Salario / 30m;
            var d = new NominaDetalle
            {
                EmpleadoId = c.EmpleadoId,
                SalarioBase = R(c.Salario),
                DiasFaltados = faltas,
                DescuentoFaltas = R(valorDia * faltas),
                HorasExtra = horasExtra,
                MontoHorasExtra = R(valorDia / 8m * factorHe * horasExtra),
                BonificacionIncentivo = bonificacion,
            };
            Recalcular(d, igss);
            nomina.Detalles.Add(d);
        }
        Totales(nomina, nomina.Detalles);
        db.Nominas.Add(nomina);
        audit.Registrar("GENERAR", "Nomina", $"{r.Anio}-{r.Mes:D2}", null, new { empleados = nomina.Detalles.Count, nomina.TotalNeto });
        await db.SaveChangesAsync();
        return Created($"api/nomina/{nomina.Id}", new { nomina.Id });
    }

    [HttpPut("detalle/{id:int}"), Authorize(Policy = Permisos.NominaGestionar)]
    public async Task<IActionResult> AjustarDetalle(int id, NominaDetalleRequest r)
    {
        var d = await db.NominaDetalles.Include(x => x.Nomina).FirstOrDefaultAsync(x => x.Id == id);
        if (d is null) return NotFound(new { error = "Registro no encontrado." });
        if (d.Nomina.Estado != "BORRADOR") return BadRequest(new { error = "Solo se puede ajustar una nómina en borrador." });

        var antes = new { d.OtrosBonos, d.Isr, d.OtrosDescuentos, d.Observaciones, d.Neto };
        d.OtrosBonos = R(r.OtrosBonos);
        d.Isr = R(r.Isr);
        d.OtrosDescuentos = R(r.OtrosDescuentos);
        d.Observaciones = r.Observaciones;
        Recalcular(d, await db.ConfigDecimalAsync("nomina.igss_laboral", 0.0483m));

        var todos = await db.NominaDetalles.Where(x => x.NominaId == d.NominaId).ToListAsync();
        Totales(d.Nomina, todos);
        audit.Registrar("AJUSTAR", "NominaDetalle", id, antes, new { d.OtrosBonos, d.Isr, d.OtrosDescuentos, d.Observaciones, d.Neto });
        await db.SaveChangesAsync();
        return Ok(new { d.Neto, d.TotalDevengado, d.TotalDescuentos, d.Igss });
    }

    [HttpPost("{id:int}/cerrar"), Authorize(Policy = Permisos.NominaGestionar)]
    public async Task<IActionResult> Cerrar(int id)
    {
        var n = await db.Nominas.FirstOrDefaultAsync(x => x.Id == id);
        if (n is null) return NotFound(new { error = "Nómina no encontrada." });
        if (n.Estado != "BORRADOR") return BadRequest(new { error = "La nómina ya fue cerrada." });
        n.Estado = "CERRADA";
        n.CerradoEn = DateTime.UtcNow;
        audit.Registrar("CERRAR", "Nomina", id, new { Estado = "BORRADOR" }, new { n.Estado, n.TotalNeto });
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id:int}/pagar"), Authorize(Policy = Permisos.NominaGestionar)]
    public async Task<IActionResult> Pagar(int id)
    {
        var n = await db.Nominas.FirstOrDefaultAsync(x => x.Id == id);
        if (n is null) return NotFound(new { error = "Nómina no encontrada." });
        if (n.Estado != "CERRADA") return BadRequest(new { error = "Solo una nómina cerrada puede marcarse como pagada." });
        n.Estado = "PAGADA";
        audit.Registrar("PAGAR", "Nomina", id, new { Estado = "CERRADA" }, new { n.Estado, n.TotalNeto });
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}"), Authorize(Policy = Permisos.NominaGestionar)]
    public async Task<IActionResult> Eliminar(int id)
    {
        var n = await db.Nominas.FirstOrDefaultAsync(x => x.Id == id);
        if (n is null) return NotFound(new { error = "Nómina no encontrada." });
        if (n.Estado != "BORRADOR") return BadRequest(new { error = "Solo se puede eliminar una nómina en borrador." });
        audit.Registrar("ELIMINAR", "Nomina", id, new { n.Anio, n.Mes, n.TotalNeto }, null);
        db.Nominas.Remove(n);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ------------------------------------------------------------ recibos propios
    [HttpGet("mis-recibos"), Authorize(Policy = Permisos.NominaPropia)]
    public async Task<IActionResult> MisRecibos()
    {
        var empId = await ctx.EmpleadoIdAsync();
        if (empId is null) return BadRequest(new { error = "Su usuario no está vinculado a un empleado.", codigo = "SIN_EMPLEADO" });
        var items = await db.NominaDetalles.AsNoTracking()
            .Where(d => d.EmpleadoId == empId && d.Nomina.Estado != "BORRADOR")
            .OrderByDescending(d => d.Nomina.Anio).ThenByDescending(d => d.Nomina.Mes)
            .Select(d => new { d.Id, d.Nomina.Anio, d.Nomina.Mes, d.Nomina.Estado, d.TotalDevengado, d.TotalDescuentos, d.Neto })
            .ToListAsync();
        return Ok(items);
    }

    /// <summary>Recibo de pago: el propio empleado (si la nómina está cerrada) o quien tenga nomina.ver.</summary>
    [HttpGet("recibo/{id:int}")]
    public async Task<IActionResult> Recibo(int id)
    {
        var x = await db.NominaDetalles.AsNoTracking().Where(d => d.Id == id).Select(d => new
        {
            Detalle = d,
            d.Nomina.Anio, d.Nomina.Mes, d.Nomina.FechaInicio, d.Nomina.FechaFin, d.Nomina.Estado,
            Codigo = d.Empleado.Codigo,
            Nombre = d.Empleado.Nombres + " " + d.Empleado.Apellidos,
            d.Empleado.Dpi,
            Puesto = d.Empleado.Puesto != null ? d.Empleado.Puesto.Nombre : null,
            Departamento = d.Empleado.Departamento != null ? d.Empleado.Departamento.Nombre : null,
        }).FirstOrDefaultAsync();
        if (x is null) return NotFound(new { error = "Recibo no encontrado." });

        if (!ctx.Tiene(Permisos.NominaVer))
        {
            var yo = await ctx.EmpleadoIdAsync();
            if (!ctx.Tiene(Permisos.NominaPropia) || yo != x.Detalle.EmpleadoId || x.Estado == "BORRADOR") return NotFound(new { error = "Recibo no encontrado." });
        }

        var empresa = await db.ConfigTextoAsync("empresa.nombre") ?? "KITSUNE HRM";
        var d0 = x.Detalle;
        return Ok(new
        {
            empresa,
            periodo = new { x.Anio, x.Mes, x.FechaInicio, x.FechaFin, x.Estado },
            empleado = new { x.Codigo, x.Nombre, x.Dpi, x.Puesto, x.Departamento },
            detalle = new
            {
                d0.Id, d0.SalarioBase, d0.DiasFaltados, d0.DescuentoFaltas, d0.HorasExtra, d0.MontoHorasExtra, d0.BonificacionIncentivo,
                d0.OtrosBonos, d0.TotalDevengado, d0.Igss, d0.Isr, d0.OtrosDescuentos, d0.TotalDescuentos, d0.Neto, d0.Observaciones,
            },
        });
    }
}
