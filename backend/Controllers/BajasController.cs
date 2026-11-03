using KitsuneHrm.Api.Data;
using KitsuneHrm.Api.Models;
using KitsuneHrm.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KitsuneHrm.Api.Controllers;

/// <summary>
/// Bajas y liquidaciones. El cálculo es una ESTIMACIÓN (indemnización, aguinaldo y bono 14 proporcionales,
/// vacaciones pendientes y salario del mes) que debe validarse con contabilidad / asesoría legal; se puede ajustar
/// con "otros ingresos" y "descuentos" (por ejemplo, aguinaldo o bono 14 ya pagados).
/// </summary>
[ApiController, Authorize]
[Route("api/bajas")]
public class BajasController(AppDbContext db, AuditService audit, ContextoUsuario ctx, VacacionesService vac) : ControllerBase
{
    static readonly string[] Tipos = { "RENUNCIA", "DESPIDO_JUSTIFICADO", "DESPIDO_INJUSTIFICADO", "FIN_CONTRATO", "MUTUO_ACUERDO" };

    public record LiquidacionCalculo(decimal SalarioBase, decimal AniosServicio, decimal Indemnizacion, decimal Aguinaldo,
        decimal Bono14, decimal VacacionesDias, decimal VacacionesMonto, decimal SalarioPendiente);

    static LiquidacionCalculo Calcular(DateOnly ingreso, DateOnly baja, decimal salario, string tipo, int vacacionesUsadas, int diasPorAnio)
    {
        var dias = Math.Max(0, baja.DayNumber - ingreso.DayNumber + 1);
        var anios = Math.Round(dias / 365m, 2);
        var indemnizacion = tipo == "DESPIDO_INJUSTIFICADO" ? Tiempo.R(salario * anios) : 0m;

        // Aguinaldo: ciclo 1 dic - 30 nov
        var inicioAguinaldo = new DateOnly(baja.Month == 12 ? baja.Year : baja.Year - 1, 12, 1);
        if (inicioAguinaldo < ingreso) inicioAguinaldo = ingreso;
        var diasAguinaldo = Math.Clamp(baja.DayNumber - inicioAguinaldo.DayNumber + 1, 0, 365);
        var aguinaldo = Tiempo.R(salario * diasAguinaldo / 365m);

        // Bono 14: ciclo 1 jul - 30 jun
        var inicioBono = new DateOnly(baja.Month >= 7 ? baja.Year : baja.Year - 1, 7, 1);
        if (inicioBono < ingreso) inicioBono = ingreso;
        var diasBono = Math.Clamp(baja.DayNumber - inicioBono.DayNumber + 1, 0, 365);
        var bono14 = Tiempo.R(salario * diasBono / 365m);

        var acumuladas = Math.Round(diasPorAnio * dias / 365m, 2);
        var vacacionesDias = Math.Max(0m, acumuladas - vacacionesUsadas);
        var vacacionesMonto = Tiempo.R(salario / 30m * vacacionesDias);
        var salarioPendiente = Tiempo.R(salario / 30m * baja.Day);

        return new LiquidacionCalculo(Tiempo.R(salario), anios, indemnizacion, aguinaldo, bono14, vacacionesDias, vacacionesMonto, salarioPendiente);
    }

    static decimal Total(LiquidacionCalculo c, decimal otros, decimal descuentos) =>
        Tiempo.R(c.Indemnizacion + c.Aguinaldo + c.Bono14 + c.VacacionesMonto + c.SalarioPendiente + otros - descuentos);

    async Task<(LiquidacionCalculo? Calc, string? Error, Empleado? Emp)> PrepararAsync(int empleadoId, string tipo, DateOnly fechaBaja)
    {
        if (!Tipos.Contains(tipo)) return (null, "Tipo de baja inválido.", null);
        var emp = await db.Empleados.FirstOrDefaultAsync(e => e.Id == empleadoId);
        if (emp is null) return (null, "Empleado no encontrado.", null);
        if (!emp.Activo) return (null, "El empleado ya está inactivo o dado de baja.", null);
        if (await ctx.EmpleadoIdAsync() == emp.Id) return (null, "No puede dar de baja su propio empleado.", null);
        if (fechaBaja < emp.FechaIngreso) return (null, "La fecha de baja no puede ser anterior al ingreso.", null);
        if (fechaBaja > Tiempo.HoyGt) return (null, "Registre la baja el día en que se hace efectiva (no se admiten fechas futuras).", null);

        var contrato = await db.Contratos.AsNoTracking().Where(c => c.EmpleadoId == empleadoId)
            .OrderByDescending(c => c.Estado == "VIGENTE").ThenByDescending(c => c.FechaInicio).FirstOrDefaultAsync();
        var salario = contrato?.Salario ?? 0m;
        var calc = Calcular(emp.FechaIngreso, fechaBaja, salario, tipo, await vac.DiasUsadosAsync(empleadoId), await vac.DiasPorAnioAsync());
        return (calc, null, emp);
    }

    [HttpGet, Authorize(Policy = Permisos.BajasVer)]
    public async Task<IActionResult> Listar()
    {
        var items = await db.Bajas.AsNoTracking().OrderByDescending(b => b.FechaBaja).ThenByDescending(b => b.Id).Select(b => new
        {
            b.Id, b.EmpleadoId, Codigo = b.Empleado.Codigo, Empleado = b.Empleado.Apellidos + ", " + b.Empleado.Nombres,
            b.Tipo, b.FechaBaja, b.Motivo,
            Total = db.Liquidaciones.Where(l => l.BajaId == b.Id).Select(l => l.Total).FirstOrDefault(),
        }).ToListAsync();
        return Ok(items);
    }

    [HttpGet("{id:int}"), Authorize(Policy = Permisos.BajasVer)]
    public async Task<IActionResult> Obtener(int id)
    {
        var x = await db.Bajas.AsNoTracking().Where(b => b.Id == id).Select(b => new
        {
            Baja = b,
            Codigo = b.Empleado.Codigo,
            Nombre = b.Empleado.Nombres + " " + b.Empleado.Apellidos,
            b.Empleado.Dpi,
            b.Empleado.FechaIngreso,
            Puesto = b.Empleado.Puesto != null ? b.Empleado.Puesto.Nombre : null,
            Departamento = b.Empleado.Departamento != null ? b.Empleado.Departamento.Nombre : null,
        }).FirstOrDefaultAsync();
        if (x is null) return NotFound(new { error = "Baja no encontrada." });
        var liq = await db.Liquidaciones.AsNoTracking().FirstOrDefaultAsync(l => l.BajaId == id);
        var empresa = await db.ConfigTextoAsync("empresa.nombre") ?? "KITSUNE HRM";
        return Ok(new
        {
            empresa,
            baja = new { x.Baja.Id, x.Baja.Tipo, x.Baja.FechaBaja, x.Baja.Motivo, x.Baja.Observaciones },
            empleado = new { x.Codigo, x.Nombre, x.Dpi, x.FechaIngreso, x.Puesto, x.Departamento },
            liquidacion = liq,
        });
    }

    /// <summary>Vista previa del cálculo, sin guardar nada.</summary>
    [HttpPost("calcular"), Authorize(Policy = Permisos.BajasGestionar)]
    public async Task<IActionResult> CalcularPrevio(BajaCalcularRequest r)
    {
        var (calc, error, _) = await PrepararAsync(r.EmpleadoId, r.Tipo, r.FechaBaja);
        if (error is not null || calc is null) return BadRequest(new { error });
        return Ok(new { calculo = calc, total = Total(calc, 0, 0) });
    }

    /// <summary>Registra la baja: desactiva al empleado y su usuario, rescinde su contrato y guarda la liquidación.</summary>
    [HttpPost, Authorize(Policy = Permisos.BajasGestionar)]
    public async Task<IActionResult> Registrar(BajaRequest r)
    {
        var (calc, error, emp) = await PrepararAsync(r.EmpleadoId, r.Tipo, r.FechaBaja);
        if (error is not null || calc is null || emp is null) return BadRequest(new { error });

        var baja = new Baja
        {
            EmpleadoId = emp.Id, Tipo = r.Tipo, FechaBaja = r.FechaBaja, Motivo = r.Motivo, Observaciones = r.Observaciones, RegistradoPor = ctx.UsuarioId,
        };
        var liq = new Liquidacion
        {
            Baja = baja, SalarioBase = calc.SalarioBase, AniosServicio = calc.AniosServicio, Indemnizacion = calc.Indemnizacion,
            Aguinaldo = calc.Aguinaldo, Bono14 = calc.Bono14, VacacionesDias = calc.VacacionesDias, VacacionesMonto = calc.VacacionesMonto,
            SalarioPendiente = calc.SalarioPendiente, OtrosIngresos = r.OtrosIngresos, Descuentos = r.Descuentos,
            Total = Total(calc, r.OtrosIngresos, r.Descuentos), Observaciones = r.Observaciones,
        };
        db.Liquidaciones.Add(liq);

        var antes = new { emp.Activo, emp.EstadoLaboral };
        emp.Activo = false;
        emp.EstadoLaboral = "RETIRADO";
        emp.FechaBaja = r.FechaBaja;
        emp.ActualizadoEn = DateTime.UtcNow;

        foreach (var c in await db.Contratos.Where(c => c.EmpleadoId == emp.Id && c.Estado == "VIGENTE").ToListAsync()) c.Estado = "RESCINDIDO";
        foreach (var a in await db.Ausencias.Where(a => a.EmpleadoId == emp.Id && a.Estado == "PENDIENTE").ToListAsync())
        {
            a.Estado = "CANCELADA";
            a.ComentarioResolucion = "Cancelada por baja del empleado";
            a.ResueltoEn = DateTime.UtcNow;
        }
        foreach (var u in await db.Usuarios.Where(u => u.EmpleadoId == emp.Id && u.Activo).ToListAsync())
        {
            u.Activo = false;
            u.ActualizadoEn = DateTime.UtcNow;
            foreach (var t in await db.RefreshTokens.Where(t => t.UsuarioId == u.Id && t.RevocadoEn == null).ToListAsync()) t.RevocadoEn = DateTime.UtcNow;
            audit.Registrar("DESACTIVAR", "Usuario", u.Id, new { u.Username, Activo = true }, new { u.Username, Activo = false, Motivo = "Baja del empleado" });
        }

        audit.Registrar("BAJA", "Empleado", emp.Id, antes, new { r.Tipo, r.FechaBaja, r.Motivo, liq.Total });
        await db.SaveChangesAsync();
        return Created($"api/bajas/{baja.Id}", new { baja.Id, liq.Total });
    }

    [HttpPut("{id:int}/liquidacion"), Authorize(Policy = Permisos.BajasGestionar)]
    public async Task<IActionResult> AjustarLiquidacion(int id, LiquidacionAjusteRequest r)
    {
        var l = await db.Liquidaciones.FirstOrDefaultAsync(x => x.BajaId == id);
        if (l is null) return NotFound(new { error = "Liquidación no encontrada." });
        var antes = new { l.OtrosIngresos, l.Descuentos, l.Total };
        l.OtrosIngresos = r.OtrosIngresos;
        l.Descuentos = r.Descuentos;
        l.Observaciones = r.Observaciones;
        l.Total = Tiempo.R(l.Indemnizacion + l.Aguinaldo + l.Bono14 + l.VacacionesMonto + l.SalarioPendiente + l.OtrosIngresos - l.Descuentos);
        audit.Registrar("AJUSTAR", "Liquidacion", id, antes, new { l.OtrosIngresos, l.Descuentos, l.Total });
        await db.SaveChangesAsync();
        return Ok(new { l.Total });
    }
}
