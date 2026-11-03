using KitsuneHrm.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace KitsuneHrm.Api.Services;

public record SaldoVacaciones(int Derecho, int Usados, int Pendientes, int Disponible, int Anios);

public class VacacionesService(AppDbContext db)
{
    public static int AniosCompletos(DateOnly ingreso, DateOnly hoy)
    {
        var a = hoy.Year - ingreso.Year;
        if (hoy < ingreso.AddYears(a)) a--;
        return Math.Max(0, a);
    }

    public async Task<int> DiasPorAnioAsync() => (int)await db.ConfigDecimalAsync("vacaciones.dias_por_anio", 15);

    public async Task<int> DiasUsadosAsync(int empleadoId)
    {
        var total = await db.Ausencias.Where(a => a.EmpleadoId == empleadoId && a.Tipo == "VACACIONES" && a.Estado == "APROBADA")
            .SumAsync(a => (int?)a.Dias);
        return total ?? 0;
    }

    /// <summary>Derecho = años completos de servicio × días por año (criterio legal: se gozan tras cada año cumplido).</summary>
    public async Task<SaldoVacaciones> SaldoAsync(int empleadoId)
    {
        var ingreso = await db.Empleados.AsNoTracking().Where(e => e.Id == empleadoId).Select(e => e.FechaIngreso).FirstAsync();
        var anios = AniosCompletos(ingreso, Tiempo.HoyGt);
        var derecho = anios * await DiasPorAnioAsync();
        var usados = await DiasUsadosAsync(empleadoId);
        var pendientes = await db.Ausencias.Where(a => a.EmpleadoId == empleadoId && a.Tipo == "VACACIONES" && a.Estado == "PENDIENTE")
            .SumAsync(a => (int?)a.Dias) ?? 0;
        return new SaldoVacaciones(derecho, usados, pendientes, derecho - usados - pendientes, anios);
    }
}
