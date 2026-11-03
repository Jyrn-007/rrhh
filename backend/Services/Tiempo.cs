using System.Globalization;
using KitsuneHrm.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace KitsuneHrm.Api.Services;

/// <summary>Fechas en hora de Guatemala (UTC-6, sin horario de verano).</summary>
public static class Tiempo
{
    public static DateTime AhoraGt => DateTime.UtcNow.AddHours(-6);
    public static DateOnly HoyGt => DateOnly.FromDateTime(AhoraGt);

    /// <summary>1 = lunes ... 7 = domingo.</summary>
    public static int DiaSemana(DateOnly d) => d.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)d.DayOfWeek;

    /// <summary>Días hábiles (lunes a viernes) entre dos fechas, ambas incluidas. No considera feriados.</summary>
    public static int DiasHabiles(DateOnly ini, DateOnly fin)
    {
        var n = 0;
        for (var d = ini; d <= fin; d = d.AddDays(1))
            if (DiaSemana(d) <= 5) n++;
        return n;
    }

    public static decimal R(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);

    public static string Hhmm(TimeOnly t) => t.ToString("HH:mm", CultureInfo.InvariantCulture);
}

public static class ConfigExtensions
{
    public static async Task<decimal> ConfigDecimalAsync(this AppDbContext db, string clave, decimal porDefecto)
    {
        var v = await db.Configuraciones.AsNoTracking().Where(c => c.Clave == clave).Select(c => c.Valor).FirstOrDefaultAsync();
        return decimal.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : porDefecto;
    }

    public static async Task<string?> ConfigTextoAsync(this AppDbContext db, string clave) =>
        await db.Configuraciones.AsNoTracking().Where(c => c.Clave == clave).Select(c => c.Valor).FirstOrDefaultAsync();
}
