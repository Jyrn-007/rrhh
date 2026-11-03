using KitsuneHrm.Api.Data;
using KitsuneHrm.Api.Models;
using KitsuneHrm.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KitsuneHrm.Api.Controllers;

[ApiController, Authorize]
[Route("api/organizacion")]
public class OrganizacionController(AppDbContext db, AuditService audit) : ControllerBase
{
    // ------------------------------------------------------------ Departamentos
    [HttpGet("departamentos"), Authorize(Policy = Permisos.OrganizacionVer)]
    public async Task<IActionResult> Departamentos()
    {
        var deps = await db.Departamentos.AsNoTracking().OrderBy(d => d.Nombre).ToListAsync();
        var conteo = await db.Empleados.AsNoTracking().Where(e => e.Activo && e.DepartamentoId != null)
            .GroupBy(e => e.DepartamentoId!.Value).Select(g => new { Id = g.Key, N = g.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.N);
        var jefeIds = deps.Where(d => d.JefeEmpleadoId != null).Select(d => d.JefeEmpleadoId!.Value).ToList();
        var jefes = await db.Empleados.AsNoTracking().Where(e => jefeIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, e => e.Nombres + " " + e.Apellidos);

        return Ok(deps.Select(d => new
        {
            d.Id, d.Nombre, d.Descripcion, d.Activo, d.DepartamentoPadreId, d.JefeEmpleadoId,
            Jefe = jefes.GetValueOrDefault(d.JefeEmpleadoId ?? 0),
            Empleados = conteo.GetValueOrDefault(d.Id),
        }));
    }

    [HttpPost("departamentos"), Authorize(Policy = Permisos.OrganizacionGestionar)]
    public async Task<IActionResult> CrearDepartamento(DepartamentoRequest r)
    {
        var error = await ValidarDepartamento(r, null);
        if (error is not null) return BadRequest(new { error });
        var d = new Departamento { Nombre = r.Nombre.Trim(), Descripcion = r.Descripcion, JefeEmpleadoId = r.JefeEmpleadoId, DepartamentoPadreId = r.DepartamentoPadreId };
        db.Departamentos.Add(d);
        await db.SaveChangesAsync();
        audit.Registrar("CREAR", "Departamento", d.Id, null, new { d.Nombre, d.JefeEmpleadoId, d.DepartamentoPadreId });
        await db.SaveChangesAsync();
        return Created($"api/organizacion/departamentos/{d.Id}", new { d.Id });
    }

    [HttpPut("departamentos/{id:int}"), Authorize(Policy = Permisos.OrganizacionGestionar)]
    public async Task<IActionResult> EditarDepartamento(int id, DepartamentoRequest r)
    {
        var d = await db.Departamentos.FirstOrDefaultAsync(x => x.Id == id);
        if (d is null) return NotFound(new { error = "Departamento no encontrado." });
        var error = await ValidarDepartamento(r, id);
        if (error is not null) return BadRequest(new { error });
        var antes = new { d.Nombre, d.Descripcion, d.JefeEmpleadoId, d.DepartamentoPadreId };
        d.Nombre = r.Nombre.Trim();
        d.Descripcion = r.Descripcion;
        d.JefeEmpleadoId = r.JefeEmpleadoId;
        d.DepartamentoPadreId = r.DepartamentoPadreId;
        audit.Registrar("ACTUALIZAR", "Departamento", id, antes, new { d.Nombre, d.Descripcion, d.JefeEmpleadoId, d.DepartamentoPadreId });
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("departamentos/{id:int}/estado"), Authorize(Policy = Permisos.OrganizacionGestionar)]
    public async Task<IActionResult> EstadoDepartamento(int id, ActivoRequest r)
    {
        var d = await db.Departamentos.FirstOrDefaultAsync(x => x.Id == id);
        if (d is null) return NotFound(new { error = "Departamento no encontrado." });
        if (!r.Activo && await db.Empleados.AnyAsync(e => e.DepartamentoId == id && e.Activo))
            return BadRequest(new { error = "No se puede desactivar: tiene empleados activos asignados." });
        audit.Registrar(r.Activo ? "ACTIVAR" : "DESACTIVAR", "Departamento", id, new { d.Activo }, new { r.Activo });
        d.Activo = r.Activo;
        await db.SaveChangesAsync();
        return NoContent();
    }

    async Task<string?> ValidarDepartamento(DepartamentoRequest r, int? id)
    {
        var nombre = r.Nombre.Trim();
        if (await db.Departamentos.AnyAsync(d => d.Nombre == nombre && d.Id != id)) return "Ya existe un departamento con ese nombre.";
        if (r.DepartamentoPadreId is int p)
        {
            if (p == id) return "Un departamento no puede ser su propio padre.";
            if (!await db.Departamentos.AnyAsync(d => d.Id == p)) return "Departamento padre inválido.";
        }
        if (r.JefeEmpleadoId is int j && !await db.Empleados.AnyAsync(e => e.Id == j && e.Activo)) return "El jefe indicado no existe o está inactivo.";
        return null;
    }

    // ------------------------------------------------------------ Puestos
    [HttpGet("puestos"), Authorize(Policy = Permisos.OrganizacionVer)]
    public async Task<IActionResult> Puestos()
    {
        var conteo = await db.Empleados.AsNoTracking().Where(e => e.Activo && e.PuestoId != null)
            .GroupBy(e => e.PuestoId!.Value).Select(g => new { Id = g.Key, N = g.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.N);
        var puestos = await db.Puestos.AsNoTracking().OrderBy(p => p.Nombre)
            .Select(p => new { p.Id, p.Nombre, p.DepartamentoId, Departamento = p.Departamento.Nombre, p.SalarioMinimo, p.SalarioMaximo, p.Activo })
            .ToListAsync();
        return Ok(puestos.Select(p => new
        {
            p.Id, p.Nombre, p.DepartamentoId, p.Departamento, p.SalarioMinimo, p.SalarioMaximo, p.Activo,
            Empleados = conteo.GetValueOrDefault(p.Id),
        }));
    }

    [HttpPost("puestos"), Authorize(Policy = Permisos.OrganizacionGestionar)]
    public async Task<IActionResult> CrearPuesto(PuestoRequest r)
    {
        var error = await ValidarPuesto(r, null);
        if (error is not null) return BadRequest(new { error });
        var p = new Puesto { Nombre = r.Nombre.Trim(), DepartamentoId = r.DepartamentoId, SalarioMinimo = r.SalarioMinimo, SalarioMaximo = r.SalarioMaximo };
        db.Puestos.Add(p);
        await db.SaveChangesAsync();
        audit.Registrar("CREAR", "Puesto", p.Id, null, new { p.Nombre, p.DepartamentoId });
        await db.SaveChangesAsync();
        return Created($"api/organizacion/puestos/{p.Id}", new { p.Id });
    }

    [HttpPut("puestos/{id:int}"), Authorize(Policy = Permisos.OrganizacionGestionar)]
    public async Task<IActionResult> EditarPuesto(int id, PuestoRequest r)
    {
        var p = await db.Puestos.FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return NotFound(new { error = "Puesto no encontrado." });
        var error = await ValidarPuesto(r, id);
        if (error is not null) return BadRequest(new { error });
        var antes = new { p.Nombre, p.DepartamentoId, p.SalarioMinimo, p.SalarioMaximo };
        p.Nombre = r.Nombre.Trim();
        p.DepartamentoId = r.DepartamentoId;
        p.SalarioMinimo = r.SalarioMinimo;
        p.SalarioMaximo = r.SalarioMaximo;
        audit.Registrar("ACTUALIZAR", "Puesto", id, antes, new { p.Nombre, p.DepartamentoId, p.SalarioMinimo, p.SalarioMaximo });
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("puestos/{id:int}/estado"), Authorize(Policy = Permisos.OrganizacionGestionar)]
    public async Task<IActionResult> EstadoPuesto(int id, ActivoRequest r)
    {
        var p = await db.Puestos.FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return NotFound(new { error = "Puesto no encontrado." });
        if (!r.Activo && await db.Empleados.AnyAsync(e => e.PuestoId == id && e.Activo))
            return BadRequest(new { error = "No se puede desactivar: tiene empleados activos asignados." });
        audit.Registrar(r.Activo ? "ACTIVAR" : "DESACTIVAR", "Puesto", id, new { p.Activo }, new { r.Activo });
        p.Activo = r.Activo;
        await db.SaveChangesAsync();
        return NoContent();
    }

    async Task<string?> ValidarPuesto(PuestoRequest r, int? id)
    {
        var nombre = r.Nombre.Trim();
        if (!await db.Departamentos.AnyAsync(d => d.Id == r.DepartamentoId && d.Activo)) return "Departamento inválido.";
        if (await db.Puestos.AnyAsync(p => p.Nombre == nombre && p.DepartamentoId == r.DepartamentoId && p.Id != id)) return "Ya existe ese puesto en el departamento.";
        if (r.SalarioMinimo is decimal min && r.SalarioMaximo is decimal max && min > max) return "El salario mínimo no puede superar al máximo.";
        return null;
    }

    // ------------------------------------------------------------ Sucursales
    [HttpGet("sucursales"), Authorize(Policy = Permisos.OrganizacionVer)]
    public async Task<IActionResult> Sucursales()
    {
        var conteo = await db.Empleados.AsNoTracking().Where(e => e.Activo && e.SucursalId != null)
            .GroupBy(e => e.SucursalId!.Value).Select(g => new { Id = g.Key, N = g.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.N);
        var sucs = await db.Sucursales.AsNoTracking().OrderBy(s => s.Nombre).ToListAsync();
        return Ok(sucs.Select(s => new { s.Id, s.Nombre, s.Direccion, s.Telefono, s.Activo, Empleados = conteo.GetValueOrDefault(s.Id) }));
    }

    [HttpPost("sucursales"), Authorize(Policy = Permisos.OrganizacionGestionar)]
    public async Task<IActionResult> CrearSucursal(SucursalRequest r)
    {
        var nombre = r.Nombre.Trim();
        if (await db.Sucursales.AnyAsync(s => s.Nombre == nombre)) return BadRequest(new { error = "Ya existe una sucursal con ese nombre." });
        var s = new Sucursal { Nombre = nombre, Direccion = r.Direccion, Telefono = r.Telefono };
        db.Sucursales.Add(s);
        await db.SaveChangesAsync();
        audit.Registrar("CREAR", "Sucursal", s.Id, null, new { s.Nombre, s.Direccion });
        await db.SaveChangesAsync();
        return Created($"api/organizacion/sucursales/{s.Id}", new { s.Id });
    }

    [HttpPut("sucursales/{id:int}"), Authorize(Policy = Permisos.OrganizacionGestionar)]
    public async Task<IActionResult> EditarSucursal(int id, SucursalRequest r)
    {
        var s = await db.Sucursales.FirstOrDefaultAsync(x => x.Id == id);
        if (s is null) return NotFound(new { error = "Sucursal no encontrada." });
        var nombre = r.Nombre.Trim();
        if (await db.Sucursales.AnyAsync(x => x.Nombre == nombre && x.Id != id)) return BadRequest(new { error = "Ya existe una sucursal con ese nombre." });
        var antes = new { s.Nombre, s.Direccion, s.Telefono };
        s.Nombre = nombre;
        s.Direccion = r.Direccion;
        s.Telefono = r.Telefono;
        audit.Registrar("ACTUALIZAR", "Sucursal", id, antes, new { s.Nombre, s.Direccion, s.Telefono });
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("sucursales/{id:int}/estado"), Authorize(Policy = Permisos.OrganizacionGestionar)]
    public async Task<IActionResult> EstadoSucursal(int id, ActivoRequest r)
    {
        var s = await db.Sucursales.FirstOrDefaultAsync(x => x.Id == id);
        if (s is null) return NotFound(new { error = "Sucursal no encontrada." });
        if (!r.Activo && await db.Empleados.AnyAsync(e => e.SucursalId == id && e.Activo))
            return BadRequest(new { error = "No se puede desactivar: tiene empleados activos asignados." });
        audit.Registrar(r.Activo ? "ACTIVAR" : "DESACTIVAR", "Sucursal", id, new { s.Activo }, new { r.Activo });
        s.Activo = r.Activo;
        await db.SaveChangesAsync();
        return NoContent();
    }
}
