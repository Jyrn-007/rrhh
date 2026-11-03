using KitsuneHrm.Api.Data;
using KitsuneHrm.Api.Models;
using KitsuneHrm.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KitsuneHrm.Api.Controllers;

/// <summary>Autoservicio (empleado) y vista del supervisor sobre su equipo.</summary>
[ApiController, Authorize]
[Route("api/mi")]
public class MiController(AppDbContext db, AuditService audit, ContextoUsuario ctx, VacacionesService vac) : ControllerBase
{
    IActionResult SinEmpleado() => BadRequest(new { error = "Su usuario no está vinculado a un empleado. Pida a un administrador que lo vincule.", codigo = "SIN_EMPLEADO" });

    [HttpGet("perfil"), Authorize(Policy = Permisos.PerfilVer)]
    public async Task<IActionResult> Perfil()
    {
        var empId = await ctx.EmpleadoIdAsync();
        if (empId is null) return SinEmpleado();
        var uid = ctx.UsuarioId;

        var e = await db.Empleados.AsNoTracking().Where(x => x.Id == empId).Select(x => new
        {
            x.Id, x.Codigo, x.Nombres, x.Apellidos, x.Dpi, x.Nit, x.FechaNacimiento, x.Genero, x.EstadoCivil, x.Email, x.Telefono, x.Direccion,
            x.ContactoEmergenciaNombre, x.ContactoEmergenciaTelefono, x.ContactoEmergenciaParentesco, x.FechaIngreso,
            Departamento = x.Departamento != null ? x.Departamento.Nombre : null,
            Puesto = x.Puesto != null ? x.Puesto.Nombre : null,
            Sucursal = x.Sucursal != null ? x.Sucursal.Nombre : null,
            Jefe = x.Jefe != null ? x.Jefe.Nombres + " " + x.Jefe.Apellidos : null,
        }).FirstAsync();
        var contrato = await db.Contratos.AsNoTracking().Where(c => c.EmpleadoId == empId && c.Estado == "VIGENTE")
            .OrderByDescending(c => c.FechaInicio).Select(c => new { c.Tipo, c.FechaInicio, c.FechaFin, c.Salario }).FirstOrDefaultAsync();
        var usuario = await db.Usuarios.AsNoTracking().Where(u => u.Id == uid).Select(u => new { u.Username, u.Email, Rol = u.Rol.Nombre }).FirstAsync();
        return Ok(new { empleado = e, contrato, vacaciones = await vac.SaldoAsync(empId.Value), usuario });
    }

    /// <summary>El empleado solo puede editar sus datos de contacto.</summary>
    [HttpPut("perfil"), Authorize(Policy = Permisos.PerfilVer)]
    public async Task<IActionResult> ActualizarPerfil(PerfilRequest r)
    {
        var empId = await ctx.EmpleadoIdAsync();
        if (empId is null) return SinEmpleado();
        var e = await db.Empleados.FirstAsync(x => x.Id == empId);
        var antes = new { e.Email, e.Telefono, e.Direccion, e.ContactoEmergenciaNombre, e.ContactoEmergenciaTelefono, e.ContactoEmergenciaParentesco };
        e.Email = r.Email;
        e.Telefono = r.Telefono;
        e.Direccion = r.Direccion;
        e.ContactoEmergenciaNombre = r.ContactoEmergenciaNombre;
        e.ContactoEmergenciaTelefono = r.ContactoEmergenciaTelefono;
        e.ContactoEmergenciaParentesco = r.ContactoEmergenciaParentesco;
        e.ActualizadoEn = DateTime.UtcNow;
        audit.Registrar("ACTUALIZAR_PERFIL", "Empleado", e.Id, antes,
            new { e.Email, e.Telefono, e.Direccion, e.ContactoEmergenciaNombre, e.ContactoEmergenciaTelefono, e.ContactoEmergenciaParentesco });
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("resumen"), Authorize(Policy = Permisos.DashboardVer)]
    public async Task<IActionResult> Resumen()
    {
        var empId = await ctx.EmpleadoIdAsync();
        if (empId is null) return Ok(new { vinculado = false });
        var id = empId.Value;
        var hoy = Tiempo.HoyGt;

        var emp = await db.Empleados.AsNoTracking().Where(e => e.Id == id).Select(e => new
        {
            e.Id, Nombre = e.Nombres + " " + e.Apellidos,
            Puesto = e.Puesto != null ? e.Puesto.Nombre : null,
            Departamento = e.Departamento != null ? e.Departamento.Nombre : null,
        }).FirstAsync();

        var asistencia = await db.Asistencias.AsNoTracking().Where(a => a.EmpleadoId == id && a.Fecha == hoy)
            .Select(a => new { a.HoraEntrada, a.HoraSalida, a.Estado, a.MinutosRetardo, a.HorasTrabajadas }).FirstOrDefaultAsync();
        var horario = await AsistenciaController.HorarioDeAsync(db, id);
        var saldo = ctx.Tiene(Permisos.AusenciasSolicitar) ? await vac.SaldoAsync(id) : null;
        var pendientes = await db.Ausencias.CountAsync(a => a.EmpleadoId == id && a.Estado == "PENDIENTE");
        var objetivos = await db.Objetivos.CountAsync(o => o.EmpleadoId == id && (o.Estado == "PENDIENTE" || o.Estado == "EN_PROGRESO"));
        var capacitaciones = await db.CapacitacionParticipantes.CountAsync(p => p.EmpleadoId == id && p.Estado == "INSCRITO"
            && (p.Capacitacion.Estado == "PROGRAMADA" || p.Capacitacion.Estado == "EN_CURSO"));

        object? equipo = null;
        if (ctx.Tiene(Permisos.EquipoVer))
        {
            var ids = await ctx.EquipoIdsAsync();
            var regs = await db.Asistencias.AsNoTracking().Where(a => a.Fecha == hoy && ids.Contains(a.EmpleadoId))
                .Select(a => new { a.EmpleadoId, a.Estado, a.HoraEntrada }).ToListAsync();
            var ausentes = await db.Ausencias.CountAsync(a => a.Estado == "APROBADA" && a.FechaInicio <= hoy && a.FechaFin >= hoy && ids.Contains(a.EmpleadoId));
            var porAprobar = await db.Ausencias.CountAsync(a => a.Estado == "PENDIENTE" && ids.Contains(a.EmpleadoId));
            equipo = new
            {
                miembros = ids.Count,
                presentes = regs.Count(x => x.HoraEntrada != null),
                retardos = regs.Count(x => x.Estado == "RETARDO"),
                ausentes,
                pendientesAprobacion = porAprobar,
            };
        }

        return Ok(new
        {
            vinculado = true,
            empleado = emp,
            asistenciaHoy = asistencia,
            horario = new { entrada = Tiempo.Hhmm(horario.HoraEntrada), salida = Tiempo.Hhmm(horario.HoraSalida) },
            vacaciones = saldo,
            solicitudesPendientes = pendientes,
            objetivosActivos = objetivos,
            capacitacionesProximas = capacitaciones,
            equipo,
        });
    }

    [HttpGet("equipo"), Authorize(Policy = Permisos.EquipoVer)]
    public async Task<IActionResult> Equipo()
    {
        var hoy = Tiempo.HoyGt;
        var ids = await ctx.EquipoIdsAsync();
        var miembros = await db.Empleados.AsNoTracking().Where(e => ids.Contains(e.Id)).OrderBy(e => e.Apellidos).ThenBy(e => e.Nombres)
            .Select(e => new
            {
                e.Id, e.Codigo, Nombre = e.Apellidos + ", " + e.Nombres, e.Email, e.Telefono, e.FechaIngreso,
                Puesto = e.Puesto != null ? e.Puesto.Nombre : null,
                Departamento = e.Departamento != null ? e.Departamento.Nombre : null,
            }).ToListAsync();
        var regs = await db.Asistencias.AsNoTracking().Where(a => a.Fecha == hoy && ids.Contains(a.EmpleadoId)).ToListAsync();
        var aus = await db.Ausencias.AsNoTracking()
            .Where(a => a.Estado == "APROBADA" && a.FechaInicio <= hoy && a.FechaFin >= hoy && ids.Contains(a.EmpleadoId)).ToListAsync();

        return Ok(miembros.Select(m =>
        {
            var r = regs.FirstOrDefault(x => x.EmpleadoId == m.Id);
            var a = aus.FirstOrDefault(x => x.EmpleadoId == m.Id);
            return new
            {
                m.Id, m.Codigo, m.Nombre, m.Email, m.Telefono, m.FechaIngreso, m.Puesto, m.Departamento,
                estadoHoy = a != null ? a.Tipo : r?.Estado,
                horaEntrada = r?.HoraEntrada,
                horaSalida = r?.HoraSalida,
            };
        }));
    }
}
