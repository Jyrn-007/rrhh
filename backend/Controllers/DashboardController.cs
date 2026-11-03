using KitsuneHrm.Api.Data;
using KitsuneHrm.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KitsuneHrm.Api.Controllers;

[ApiController, Authorize(Policy = Permisos.EmpleadosVer)]
[Route("api/dashboard")]
public class DashboardController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Resumen()
    {
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-6)); // Guatemala (UTC-6, sin horario de verano)
        var limite = hoy.AddDays(30);

        var total = await db.Empleados.CountAsync();
        var activos = await db.Empleados.CountAsync(e => e.Activo);

        // Cumpleaños del mes (se filtra en memoria: solo 3 columnas de empleados activos)
        var nacimientos = await db.Empleados.AsNoTracking().Where(e => e.Activo)
            .Select(e => new { e.Id, e.Nombres, e.Apellidos, e.FechaNacimiento }).ToListAsync();
        var cumples = nacimientos.Where(e => e.FechaNacimiento.Month == hoy.Month)
            .OrderBy(e => e.FechaNacimiento.Day)
            .Select(e => new { e.Id, Nombre = $"{e.Nombres} {e.Apellidos}", Dia = e.FechaNacimiento.Day }).ToList();

        var ausentesHoy = await db.Ausencias.CountAsync(a => a.Estado == "APROBADA" && a.FechaInicio <= hoy && a.FechaFin >= hoy);
        var solicitudesPendientes = await db.Ausencias.CountAsync(a => a.Estado == "PENDIENTE");

        var contratos = await db.Contratos.AsNoTracking()
            .Where(c => c.Estado == "VIGENTE" && c.FechaFin != null && c.FechaFin >= hoy && c.FechaFin <= limite)
            .OrderBy(c => c.FechaFin)
            .Select(c => new { c.Id, Empleado = c.Empleado.Nombres + " " + c.Empleado.Apellidos, c.Tipo, c.FechaFin })
            .Take(20).ToListAsync();

        var porDepartamento = await db.Empleados.AsNoTracking().Where(e => e.Activo)
            .GroupBy(e => e.Departamento == null ? "Sin departamento" : e.Departamento.Nombre)
            .Select(g => new { Departamento = g.Key, Total = g.Count() })
            .OrderByDescending(x => x.Total).ToListAsync();

        return Ok(new
        {
            totalEmpleados = total,
            empleadosActivos = activos,
            empleadosInactivos = total - activos,
            ausentesHoy,
            solicitudesPendientes,
            cumpleanosDelMes = cumples,
            contratosPorVencer = contratos,
            empleadosPorDepartamento = porDepartamento,
        });
    }
}
