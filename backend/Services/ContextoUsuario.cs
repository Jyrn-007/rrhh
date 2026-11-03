using System.Security.Claims;
using KitsuneHrm.Api.Data;
using KitsuneHrm.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace KitsuneHrm.Api.Services;

/// <summary>
/// Información del usuario autenticado y su alcance: el empleado vinculado y,
/// para supervisores, el equipo a cargo (reportes directos e indirectos por jefe_id).
/// </summary>
public class ContextoUsuario(IHttpContextAccessor http, AppDbContext db)
{
    int? _empleadoId;
    bool _empleadoCargado;
    List<int>? _equipo;

    ClaimsPrincipal User => http.HttpContext!.User;

    public int UsuarioId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    public string Username => User.Identity?.Name ?? "";
    public bool Tiene(string permiso) => User.HasClaim("permiso", permiso);

    public async Task<int?> EmpleadoIdAsync()
    {
        if (!_empleadoCargado)
        {
            var uid = UsuarioId;
            _empleadoId = await db.Usuarios.AsNoTracking().Where(u => u.Id == uid).Select(u => u.EmpleadoId).FirstOrDefaultAsync();
            _empleadoCargado = true;
        }
        return _empleadoId;
    }

    /// <summary>Empleados activos a cargo (hasta 5 niveles). No incluye al propio usuario.</summary>
    public async Task<List<int>> EquipoIdsAsync()
    {
        if (_equipo is not null) return _equipo;
        var yo = await EmpleadoIdAsync();
        var ids = new HashSet<int>();
        if (yo is not null)
        {
            var frontera = new List<int> { yo.Value };
            for (var nivel = 0; nivel < 5 && frontera.Count > 0; nivel++)
            {
                var actuales = frontera;
                var hijos = await db.Empleados.AsNoTracking()
                    .Where(e => e.Activo && e.JefeId != null && actuales.Contains(e.JefeId.Value))
                    .Select(e => e.Id).ToListAsync();
                frontera = hijos.Where(id => id != yo.Value && ids.Add(id)).ToList();
            }
        }
        _equipo = ids.ToList();
        return _equipo;
    }

    /// <summary>true si tiene el permiso "total", es el propio empleado o está en su equipo (con equipo.ver).</summary>
    public async Task<bool> EnAlcanceAsync(int empleadoId, string permisoTotal)
    {
        if (Tiene(permisoTotal)) return true;
        if (await EmpleadoIdAsync() == empleadoId) return true;
        if (!Tiene(Permisos.EquipoVer)) return false;
        return (await EquipoIdsAsync()).Contains(empleadoId);
    }
}
