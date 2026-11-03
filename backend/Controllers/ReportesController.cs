using ClosedXML.Excel;
using KitsuneHrm.Api.Data;
using KitsuneHrm.Api.Models;
using KitsuneHrm.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KitsuneHrm.Api.Controllers;

[ApiController, Authorize(Policy = Permisos.ReportesVer)]
[Route("api/reportes")]
public class ReportesController(AppDbContext db, AuditService audit, ContextoUsuario ctx) : ControllerBase
{
    const string XlsxMime = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    static void Poner(IXLCell c, object? v)
    {
        switch (v)
        {
            case null: c.Value = ""; break;
            case string s: c.Value = s; break;
            case decimal d: c.Value = (double)d; break;
            case double dd: c.Value = dd; break;
            case int i: c.Value = (double)i; break;
            case long l: c.Value = (double)l; break;
            case bool b: c.Value = b; break;
            case DateTime dt: c.Value = dt; break;
            case DateOnly dO: c.Value = dO.ToString("yyyy-MM-dd"); break;
            default: c.Value = v.ToString() ?? ""; break;
        }
    }

    async Task<IActionResult> Excel(string archivo, string hoja, string[] cabeceras, List<object?[]> filas, object? filtros = null)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet(hoja);
        for (var i = 0; i < cabeceras.Length; i++) ws.Cell(1, i + 1).Value = cabeceras[i];
        ws.Range(1, 1, 1, cabeceras.Length).Style.Font.Bold = true;
        for (var r = 0; r < filas.Count; r++)
            for (var c = 0; c < cabeceras.Length; c++)
                Poner(ws.Cell(r + 2, c + 1), filas[r][c]);
        ws.Columns().AdjustToContents();

        audit.Registrar("EXPORTAR", "Reporte", archivo, null, new { filas = filas.Count, filtros });
        await db.SaveChangesAsync();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return File(ms.ToArray(), XlsxMime, $"{archivo}-{DateTime.UtcNow:yyyyMMdd-HHmm}.xlsx");
    }

    static string Fh(DateTime? d) => d is null ? "" : d.Value.ToString("yyyy-MM-dd HH:mm");

    [HttpGet("empleados.xlsx")]
    public async Task<IActionResult> Empleados([FromQuery] bool? activo)
    {
        var q = db.Empleados.AsNoTracking().AsQueryable();
        if (activo is not null) q = q.Where(e => e.Activo == activo);
        var f = await q.OrderBy(e => e.Apellidos).ThenBy(e => e.Nombres).Select(e => new
        {
            e.Codigo, e.Nombres, e.Apellidos, e.Dpi, e.Nit, e.Email, e.Telefono, e.FechaIngreso, e.EstadoLaboral,
            Departamento = e.Departamento != null ? e.Departamento.Nombre : "",
            Puesto = e.Puesto != null ? e.Puesto.Nombre : "",
            Sucursal = e.Sucursal != null ? e.Sucursal.Nombre : "",
        }).ToListAsync();
        return await Excel("empleados", "Empleados",
            new[] { "Código", "Nombres", "Apellidos", "DPI", "NIT", "Email", "Teléfono", "Ingreso", "Estado", "Departamento", "Puesto", "Sucursal" },
            f.Select(x => new object?[] { x.Codigo, x.Nombres, x.Apellidos, x.Dpi, x.Nit, x.Email, x.Telefono, x.FechaIngreso, x.EstadoLaboral, x.Departamento, x.Puesto, x.Sucursal }).ToList(),
            new { activo });
    }

    [HttpGet("asistencia.xlsx")]
    public async Task<IActionResult> Asistencia([FromQuery] DateOnly? desde, [FromQuery] DateOnly? hasta)
    {
        var h = hasta ?? Tiempo.HoyGt;
        var d = desde ?? new DateOnly(h.Year, h.Month, 1);
        var f = await db.Asistencias.AsNoTracking().Where(a => a.Fecha >= d && a.Fecha <= h)
            .OrderBy(a => a.Fecha).ThenBy(a => a.Empleado.Apellidos)
            .Select(a => new
            {
                a.Fecha, a.Empleado.Codigo, Empleado = a.Empleado.Apellidos + ", " + a.Empleado.Nombres,
                a.HoraEntrada, a.HoraSalida, a.Estado, a.MinutosRetardo, a.HorasTrabajadas, a.HorasExtra, a.Origen,
            }).ToListAsync();
        return await Excel("asistencia", "Asistencia",
            new[] { "Fecha", "Código", "Empleado", "Entrada", "Salida", "Estado", "Retardo (min)", "Horas", "Horas extra", "Origen" },
            f.Select(x => new object?[] { x.Fecha, x.Codigo, x.Empleado, Fh(x.HoraEntrada), Fh(x.HoraSalida), x.Estado, x.MinutosRetardo, x.HorasTrabajadas, x.HorasExtra, x.Origen }).ToList(),
            new { desde = d, hasta = h });
    }

    [HttpGet("ausencias.xlsx")]
    public async Task<IActionResult> Ausencias([FromQuery] DateOnly? desde, [FromQuery] DateOnly? hasta)
    {
        var h = hasta ?? Tiempo.HoyGt.AddMonths(6);
        var d = desde ?? new DateOnly(Tiempo.HoyGt.Year, 1, 1);
        var f = await db.Ausencias.AsNoTracking().Where(a => a.FechaFin >= d && a.FechaInicio <= h)
            .OrderBy(a => a.FechaInicio).ThenBy(a => a.Empleado.Apellidos)
            .Select(a => new
            {
                a.Empleado.Codigo, Empleado = a.Empleado.Apellidos + ", " + a.Empleado.Nombres,
                a.Tipo, a.FechaInicio, a.FechaFin, a.Dias, a.Estado, a.Motivo, a.ComentarioResolucion,
            }).ToListAsync();
        return await Excel("vacaciones-permisos", "Ausencias",
            new[] { "Código", "Empleado", "Tipo", "Inicio", "Fin", "Días hábiles", "Estado", "Motivo", "Resolución" },
            f.Select(x => new object?[] { x.Codigo, x.Empleado, x.Tipo, x.FechaInicio, x.FechaFin, x.Dias, x.Estado, x.Motivo, x.ComentarioResolucion }).ToList(),
            new { desde = d, hasta = h });
    }

    [HttpGet("contratos.xlsx")]
    public async Task<IActionResult> Contratos([FromQuery] string? estado)
    {
        var q = db.Contratos.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(estado)) q = q.Where(c => c.Estado == estado);
        var f = await q.OrderBy(c => c.Empleado.Apellidos).ThenByDescending(c => c.FechaInicio)
            .Select(c => new { c.Empleado.Codigo, Empleado = c.Empleado.Apellidos + ", " + c.Empleado.Nombres, c.Tipo, c.FechaInicio, c.FechaFin, c.Salario, c.Estado }).ToListAsync();
        return await Excel("contratos", "Contratos",
            new[] { "Código", "Empleado", "Tipo", "Inicio", "Fin", "Salario", "Estado" },
            f.Select(x => new object?[] { x.Codigo, x.Empleado, x.Tipo, x.FechaInicio, x.FechaFin, x.Salario, x.Estado }).ToList(),
            new { estado });
    }

    [HttpGet("nomina/{id:int}/xlsx")]
    public async Task<IActionResult> Nomina(int id)
    {
        if (!ctx.Tiene(Permisos.NominaVer)) return Forbid();
        var n = await db.Nominas.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (n is null) return NotFound(new { error = "Nómina no encontrada." });
        var f = await db.NominaDetalles.AsNoTracking().Where(d => d.NominaId == id).OrderBy(d => d.Empleado.Apellidos)
            .Select(d => new
            {
                d.Empleado.Codigo, Empleado = d.Empleado.Apellidos + ", " + d.Empleado.Nombres, d.SalarioBase, d.DiasFaltados, d.DescuentoFaltas,
                d.HorasExtra, d.MontoHorasExtra, d.BonificacionIncentivo, d.OtrosBonos, d.TotalDevengado, d.Igss, d.Isr, d.OtrosDescuentos,
                d.TotalDescuentos, d.Neto,
            }).ToListAsync();
        return await Excel($"nomina-{n.Anio}-{n.Mes:D2}", "Nómina",
            new[] { "Código", "Empleado", "Salario base", "Faltas", "Desc. faltas", "Horas extra", "Monto HE", "Bonif. incentivo", "Otros bonos", "Devengado", "IGSS", "ISR", "Otros desc.", "Descuentos", "Neto" },
            f.Select(x => new object?[] { x.Codigo, x.Empleado, x.SalarioBase, x.DiasFaltados, x.DescuentoFaltas, x.HorasExtra, x.MontoHorasExtra, x.BonificacionIncentivo, x.OtrosBonos, x.TotalDevengado, x.Igss, x.Isr, x.OtrosDescuentos, x.TotalDescuentos, x.Neto }).ToList(),
            new { n.Anio, n.Mes, n.Estado });
    }

    [HttpGet("bajas.xlsx")]
    public async Task<IActionResult> Bajas([FromQuery] DateOnly? desde, [FromQuery] DateOnly? hasta)
    {
        var h = hasta ?? Tiempo.HoyGt;
        var d = desde ?? new DateOnly(h.Year, 1, 1);
        var f = await db.Bajas.AsNoTracking().Where(b => b.FechaBaja >= d && b.FechaBaja <= h).OrderBy(b => b.FechaBaja)
            .Select(b => new
            {
                b.Empleado.Codigo, Empleado = b.Empleado.Apellidos + ", " + b.Empleado.Nombres, b.Tipo, b.FechaBaja, b.Motivo,
                Total = db.Liquidaciones.Where(l => l.BajaId == b.Id).Select(l => l.Total).FirstOrDefault(),
            }).ToListAsync();
        return await Excel("bajas", "Bajas",
            new[] { "Código", "Empleado", "Tipo", "Fecha de baja", "Motivo", "Liquidación total" },
            f.Select(x => new object?[] { x.Codigo, x.Empleado, x.Tipo, x.FechaBaja, x.Motivo, x.Total }).ToList(),
            new { desde = d, hasta = h });
    }

    [HttpGet("capacitaciones.xlsx")]
    public async Task<IActionResult> Capacitaciones()
    {
        var f = await db.CapacitacionParticipantes.AsNoTracking().OrderBy(p => p.Capacitacion.FechaInicio).ThenBy(p => p.Empleado.Apellidos)
            .Select(p => new
            {
                Capacitacion = p.Capacitacion.Nombre, p.Capacitacion.FechaInicio, p.Capacitacion.FechaFin, p.Capacitacion.Horas,
                p.Empleado.Codigo, Empleado = p.Empleado.Apellidos + ", " + p.Empleado.Nombres, p.Estado, p.Calificacion,
            }).ToListAsync();
        return await Excel("capacitaciones", "Capacitaciones",
            new[] { "Capacitación", "Inicio", "Fin", "Horas", "Código", "Empleado", "Estado", "Calificación" },
            f.Select(x => new object?[] { x.Capacitacion, x.FechaInicio, x.FechaFin, x.Horas, x.Codigo, x.Empleado, x.Estado, x.Calificacion }).ToList());
    }
}
