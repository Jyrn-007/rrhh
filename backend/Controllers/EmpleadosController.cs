using KitsuneHrm.Api.Data;
using KitsuneHrm.Api.Models;
using KitsuneHrm.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KitsuneHrm.Api.Controllers;

[ApiController]
[Route("api/empleados")]
public class EmpleadosController(AppDbContext db, AuditService audit) : ControllerBase
{
    static object Snapshot(Empleado e) => new
    {
        e.Codigo, e.Nombres, e.Apellidos, e.Dpi, e.Nit, e.FechaNacimiento, e.Email, e.Telefono, e.Direccion,
        e.FechaIngreso, e.EstadoLaboral, e.Activo, e.DepartamentoId, e.PuestoId, e.SucursalId, e.JefeId,
    };

    [HttpGet, Authorize(Policy = Permisos.EmpleadosVer)]
    public async Task<IActionResult> Listar([FromQuery] string? buscar, [FromQuery] bool? activo, [FromQuery] string? estado,
        [FromQuery] int? departamentoId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var q = db.Empleados.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(buscar))
        {
            var b = buscar.Trim();
            q = q.Where(e => e.Nombres.Contains(b) || e.Apellidos.Contains(b) || e.Codigo.Contains(b) || e.Dpi.Contains(b));
        }
        if (activo is not null) q = q.Where(e => e.Activo == activo);
        if (!string.IsNullOrWhiteSpace(estado)) q = q.Where(e => e.EstadoLaboral == estado);
        if (departamentoId is not null) q = q.Where(e => e.DepartamentoId == departamentoId);

        var total = await q.CountAsync();
        var items = await q.OrderBy(e => e.Apellidos).ThenBy(e => e.Nombres)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(e => new
            {
                e.Id, e.Codigo, e.Nombres, e.Apellidos, e.Dpi, e.Email, e.Telefono, e.FechaIngreso,
                e.EstadoLaboral, e.Activo,
                Departamento = e.Departamento != null ? e.Departamento.Nombre : null,
                Puesto = e.Puesto != null ? e.Puesto.Nombre : null,
                Sucursal = e.Sucursal != null ? e.Sucursal.Nombre : null,
            }).ToListAsync();

        return Ok(new { total, page, pageSize, items });
    }

    /// <summary>Lista liviana para selectores (sin paginar).</summary>
    [HttpGet("lista"), Authorize(Policy = Permisos.EmpleadosVer)]
    public async Task<IActionResult> Lista([FromQuery] bool soloActivos = true) =>
        Ok(await db.Empleados.AsNoTracking().Where(e => !soloActivos || e.Activo)
            .OrderBy(e => e.Apellidos).ThenBy(e => e.Nombres).Take(3000)
            .Select(e => new { e.Id, e.Codigo, Nombre = e.Apellidos + ", " + e.Nombres, e.Activo, e.DepartamentoId }).ToListAsync());

    [HttpGet("{id:int}"), Authorize(Policy = Permisos.EmpleadosVer)]
    public async Task<IActionResult> Obtener(int id)
    {
        var e = await db.Empleados.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return e is null ? NotFound(new { error = "Empleado no encontrado." }) : Ok(e);
    }

    [HttpPost, Authorize(Policy = Permisos.EmpleadosCrear)]
    public async Task<IActionResult> Crear(EmpleadoRequest req)
    {
        var error = await Validar(req, null);
        if (error is not null) return BadRequest(new { error });

        var ultimo = await db.Empleados.OrderByDescending(e => e.Id).Select(e => e.Id).FirstOrDefaultAsync();
        var e = new Empleado { Codigo = $"EMP-{ultimo + 1:D4}", CreadoPor = audit.UsuarioActualId };
        Aplicar(e, req);
        db.Empleados.Add(e);
        await db.SaveChangesAsync();

        audit.Registrar("CREAR", "Empleado", e.Id, null, Snapshot(e));
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Obtener), new { id = e.Id }, e);
    }

    [HttpPut("{id:int}"), Authorize(Policy = Permisos.EmpleadosEditar)]
    public async Task<IActionResult> Actualizar(int id, EmpleadoRequest req)
    {
        var e = await db.Empleados.FirstOrDefaultAsync(x => x.Id == id);
        if (e is null) return NotFound(new { error = "Empleado no encontrado." });

        var error = await Validar(req, id);
        if (error is not null) return BadRequest(new { error });

        var antes = Snapshot(e);
        Aplicar(e, req);
        e.ActualizadoEn = DateTime.UtcNow;
        audit.Registrar("ACTUALIZAR", "Empleado", e.Id, antes, Snapshot(e));
        await db.SaveChangesAsync();
        return Ok(e);
    }

    /// <summary>Activa o desactiva (baja lógica). Nunca se borra físicamente un empleado.</summary>
    [HttpPost("{id:int}/estado"), Authorize(Policy = Permisos.EmpleadosEstado)]
    public async Task<IActionResult> CambiarEstado(int id, EstadoRequest req)
    {
        var e = await db.Empleados.FirstOrDefaultAsync(x => x.Id == id);
        if (e is null) return NotFound(new { error = "Empleado no encontrado." });

        var antes = Snapshot(e);
        e.Activo = req.Activo;
        e.EstadoLaboral = req.Activo ? "ACTIVO" : "INACTIVO";
        e.FechaBaja = req.Activo ? null : DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-6));
        e.ActualizadoEn = DateTime.UtcNow;

        // Si el empleado se desactiva, su usuario de sistema también pierde acceso
        if (!req.Activo)
        {
            var usuarios = await db.Usuarios.Where(u => u.EmpleadoId == id && u.Activo).ToListAsync();
            foreach (var u in usuarios)
            {
                u.Activo = false;
                u.ActualizadoEn = DateTime.UtcNow;
                var tokens = await db.RefreshTokens.Where(t => t.UsuarioId == u.Id && t.RevocadoEn == null).ToListAsync();
                foreach (var t in tokens) t.RevocadoEn = DateTime.UtcNow;
                audit.Registrar("DESACTIVAR", "Usuario", u.Id, new { u.Username, Activo = true }, new { u.Username, Activo = false, Motivo = "Empleado dado de baja" });
            }
        }

        audit.Registrar(req.Activo ? "ACTIVAR" : "DESACTIVAR", "Empleado", e.Id, antes, new { Snapshot = Snapshot(e), req.Motivo });
        await db.SaveChangesAsync();
        return Ok(e);
    }

    static void Aplicar(Empleado e, EmpleadoRequest r)
    {
        e.Nombres = r.Nombres.Trim();
        e.Apellidos = r.Apellidos.Trim();
        e.Dpi = r.Dpi;
        e.Nit = string.IsNullOrWhiteSpace(r.Nit) ? null : r.Nit.Trim();
        e.FechaNacimiento = r.FechaNacimiento;
        e.Genero = r.Genero;
        e.EstadoCivil = r.EstadoCivil;
        e.Email = r.Email;
        e.Telefono = r.Telefono;
        e.Direccion = r.Direccion;
        e.FotografiaUrl = r.FotografiaUrl;
        e.ContactoEmergenciaNombre = r.ContactoEmergenciaNombre;
        e.ContactoEmergenciaTelefono = r.ContactoEmergenciaTelefono;
        e.ContactoEmergenciaParentesco = r.ContactoEmergenciaParentesco;
        e.FechaIngreso = r.FechaIngreso;
        e.DepartamentoId = r.DepartamentoId;
        e.PuestoId = r.PuestoId;
        e.SucursalId = r.SucursalId;
        e.JefeId = r.JefeId;
        e.HorarioId = r.HorarioId;
    }

    async Task<string?> Validar(EmpleadoRequest r, int? idActual)
    {
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-6));
        if (r.FechaNacimiento.Year < 1900 || r.FechaIngreso.Year < 1900) return "Las fechas no son válidas.";
        if (r.FechaNacimiento > hoy.AddYears(-16)) return "El empleado debe tener al menos 16 años.";
        if (r.FechaIngreso > hoy.AddYears(1)) return "La fecha de ingreso no es válida.";
        if (await db.Empleados.AnyAsync(e => e.Dpi == r.Dpi && e.Id != idActual)) return "Ya existe un empleado con ese DPI.";
        if (!string.IsNullOrWhiteSpace(r.Nit) && await db.Empleados.AnyAsync(e => e.Nit == r.Nit && e.Id != idActual)) return "Ya existe un empleado con ese NIT.";
        if (r.JefeId is int j && j == idActual) return "Un empleado no puede ser su propio jefe.";
        return null;
    }
}
