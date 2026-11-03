using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace KitsuneHrm.Api.Models;

public static class Permisos
{
    public const string DashboardVer = "dashboard.ver";
    public const string EmpleadosVer = "empleados.ver";
    public const string EmpleadosCrear = "empleados.crear";
    public const string EmpleadosEditar = "empleados.editar";
    public const string EmpleadosEstado = "empleados.estado";
    public const string UsuariosVer = "usuarios.ver";
    public const string UsuariosGestionar = "usuarios.gestionar";
    public const string AuditoriaVer = "auditoria.ver";
    public const string ReportesVer = "reportes.ver";

    public const string PerfilVer = "perfil.ver";
    public const string EquipoVer = "equipo.ver";
    public const string OrganizacionVer = "organizacion.ver";
    public const string OrganizacionGestionar = "organizacion.gestionar";
    public const string ContratosVer = "contratos.ver";
    public const string ContratosGestionar = "contratos.gestionar";
    public const string AsistenciaMarcar = "asistencia.marcar";
    public const string AsistenciaVer = "asistencia.ver";
    public const string AsistenciaGestionar = "asistencia.gestionar";
    public const string AusenciasSolicitar = "ausencias.solicitar";
    public const string AusenciasVer = "ausencias.ver";
    public const string AusenciasAprobar = "ausencias.aprobar";
    public const string AusenciasGestionar = "ausencias.gestionar";
    public const string NominaPropia = "nomina.propia";
    public const string NominaVer = "nomina.ver";
    public const string NominaGestionar = "nomina.gestionar";
    public const string DesarrolloPropio = "desarrollo.propio";
    public const string CapacitacionVer = "capacitacion.ver";
    public const string CapacitacionGestionar = "capacitacion.gestionar";
    public const string EvaluacionesGestionar = "evaluaciones.gestionar";
    public const string BajasVer = "bajas.ver";
    public const string BajasGestionar = "bajas.gestionar";

    /// <summary>Todas las constantes de permiso (se registran como políticas de autorización).</summary>
    public static readonly string[] Todos = typeof(Permisos)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => f.IsLiteral && !f.IsInitOnly)
        .Select(f => (string)f.GetRawConstantValue()!)
        .ToArray();
}

public record LoginRequest([Required, StringLength(150)] string Usuario, [Required, StringLength(200)] string Password);
public record RefreshRequest([Required] string RefreshToken);
public record LogoutRequest(string? RefreshToken);
public record CambiarPasswordRequest([Required] string PasswordActual, [Required] string PasswordNueva);

public record SesionUsuarioDto(int Id, string Username, string Email, string Rol, string[] Permisos, bool DebeCambiarPassword, int? EmpleadoId);
public record LoginResponse(string AccessToken, DateTime ExpiraEn, string RefreshToken, SesionUsuarioDto Usuario);
public record AuthResult(bool Ok, int Status, string? Error, LoginResponse? Data, string? Codigo = null);

public class EmpleadoRequest
{
    [Required, StringLength(100)] public string Nombres { get; set; } = "";
    [Required, StringLength(100)] public string Apellidos { get; set; } = "";
    [Required, RegularExpression(@"^\d{13}$", ErrorMessage = "El DPI debe tener 13 dígitos")] public string Dpi { get; set; } = "";
    [StringLength(15)] public string? Nit { get; set; }
    [Required] public DateOnly FechaNacimiento { get; set; }
    [StringLength(1)] public string? Genero { get; set; }
    [StringLength(20)] public string? EstadoCivil { get; set; }
    [EmailAddress, StringLength(150)] public string? Email { get; set; }
    [StringLength(30)] public string? Telefono { get; set; }
    [StringLength(250)] public string? Direccion { get; set; }
    [StringLength(300)] public string? FotografiaUrl { get; set; }
    [StringLength(120)] public string? ContactoEmergenciaNombre { get; set; }
    [StringLength(30)] public string? ContactoEmergenciaTelefono { get; set; }
    [StringLength(50)] public string? ContactoEmergenciaParentesco { get; set; }
    [Required] public DateOnly FechaIngreso { get; set; }
    public int? DepartamentoId { get; set; }
    public int? PuestoId { get; set; }
    public int? SucursalId { get; set; }
    public int? JefeId { get; set; }
    public int? HorarioId { get; set; }
}

public record EstadoRequest(bool Activo, [StringLength(200)] string? Motivo);

public record UsuarioCrearRequest(
    [Required, StringLength(50, MinimumLength = 3), RegularExpression(@"^[a-zA-Z0-9._-]+$")] string Username,
    [Required, EmailAddress, StringLength(150)] string Email,
    [Required] int RolId,
    int? EmpleadoId,
    [Required] string PasswordTemporal);

public record UsuarioEditarRequest([Required, EmailAddress, StringLength(150)] string Email, [Required] int RolId, int? EmpleadoId);
