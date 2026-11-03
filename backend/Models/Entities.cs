using System.ComponentModel.DataAnnotations.Schema;

namespace KitsuneHrm.Api.Models;

[Table("roles")]
public class Rol
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public string? Descripcion { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
    public ICollection<RolPermiso> Permisos { get; set; } = new List<RolPermiso>();
}

[Table("permisos")]
public class Permiso
{
    public int Id { get; set; }
    public string Codigo { get; set; } = "";
    public string Modulo { get; set; } = "";
    public string? Descripcion { get; set; }
}

[Table("roles_permisos")]
public class RolPermiso
{
    public int RolId { get; set; }
    public int PermisoId { get; set; }
    public Rol Rol { get; set; } = null!;
    public Permiso Permiso { get; set; } = null!;
}

[Table("usuarios")]
public class Usuario
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public int RolId { get; set; }
    public int? EmpleadoId { get; set; }
    public bool Activo { get; set; } = true;
    public bool Bloqueado { get; set; }
    public int IntentosFallidos { get; set; }
    public DateTime? BloqueadoHasta { get; set; }
    public bool DebeCambiarPassword { get; set; }
    public DateTime? PasswordCambiadaEn { get; set; }
    public DateTime? UltimoLogin { get; set; }
    public string? UltimoLoginIp { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
    public DateTime ActualizadoEn { get; set; } = DateTime.UtcNow;
    public Rol Rol { get; set; } = null!;
    public Empleado? Empleado { get; set; }
}

[Table("refresh_tokens")]
public class RefreshToken
{
    public long Id { get; set; }
    public int UsuarioId { get; set; }
    public string TokenHash { get; set; } = "";
    public DateTime ExpiraEn { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
    public string? CreadoIp { get; set; }
    public DateTime? RevocadoEn { get; set; }
    public string? ReemplazadoPor { get; set; }
    public Usuario Usuario { get; set; } = null!;
}

[Table("registro_accesos")]
public class RegistroAcceso
{
    public long Id { get; set; }
    public int? UsuarioId { get; set; }
    public string Username { get; set; } = "";
    public string Evento { get; set; } = "";
    public bool Exitoso { get; set; }
    public string? Detalle { get; set; }
    public string? Ip { get; set; }
    public string? UserAgent { get; set; }
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
}

[Table("auditoria")]
public class AuditoriaLog
{
    public long Id { get; set; }
    public int? UsuarioId { get; set; }
    public string? Username { get; set; }
    public string Accion { get; set; } = "";
    public string Entidad { get; set; } = "";
    public string? EntidadId { get; set; }
    public string? ValoresAntes { get; set; }
    public string? ValoresDespues { get; set; }
    public string? Ip { get; set; }
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
}

[Table("logs_sistema")]
public class LogSistema
{
    public long Id { get; set; }
    public string Nivel { get; set; } = "INFO";
    public string? Origen { get; set; }
    public string Mensaje { get; set; } = "";
    public string? Excepcion { get; set; }
    public int? UsuarioId { get; set; }
    public string? Ruta { get; set; }
    public string? Metodo { get; set; }
    public string? Ip { get; set; }
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
}

[Table("sucursales")]
public class Sucursal
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public string? Direccion { get; set; }
    public string? Telefono { get; set; }
    public bool Activo { get; set; } = true;
}

[Table("departamentos")]
public class Departamento
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public string? Descripcion { get; set; }
    public int? DepartamentoPadreId { get; set; }
    public int? JefeEmpleadoId { get; set; }
    public bool Activo { get; set; } = true;
}

[Table("puestos")]
public class Puesto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public int DepartamentoId { get; set; }
    public decimal? SalarioMinimo { get; set; }
    public decimal? SalarioMaximo { get; set; }
    public bool Activo { get; set; } = true;
    public Departamento Departamento { get; set; } = null!;
}

[Table("empleados")]
public class Empleado
{
    public int Id { get; set; }
    public string Codigo { get; set; } = "";
    public string Nombres { get; set; } = "";
    public string Apellidos { get; set; } = "";
    public string Dpi { get; set; } = "";
    public string? Nit { get; set; }
    public DateOnly FechaNacimiento { get; set; }
    public string? Genero { get; set; }
    public string? EstadoCivil { get; set; }
    public string? Email { get; set; }
    public string? Telefono { get; set; }
    public string? Direccion { get; set; }
    public string? FotografiaUrl { get; set; }
    public string? ContactoEmergenciaNombre { get; set; }
    public string? ContactoEmergenciaTelefono { get; set; }
    public string? ContactoEmergenciaParentesco { get; set; }
    public DateOnly FechaIngreso { get; set; }
    public DateOnly? FechaBaja { get; set; }
    public string EstadoLaboral { get; set; } = "ACTIVO";
    public bool Activo { get; set; } = true;
    public int? DepartamentoId { get; set; }
    public int? PuestoId { get; set; }
    public int? SucursalId { get; set; }
    public int? JefeId { get; set; }
    public int? HorarioId { get; set; }
    public int? CreadoPor { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
    public DateTime ActualizadoEn { get; set; } = DateTime.UtcNow;
    public Departamento? Departamento { get; set; }
    public Puesto? Puesto { get; set; }
    public Sucursal? Sucursal { get; set; }
    public Empleado? Jefe { get; set; }
}

[Table("contratos")]
public class Contrato
{
    public int Id { get; set; }
    public int EmpleadoId { get; set; }
    public string Tipo { get; set; } = "";
    public DateOnly FechaInicio { get; set; }
    public DateOnly? FechaFin { get; set; }
    public decimal Salario { get; set; }
    public string Estado { get; set; } = "VIGENTE";
    public string? DocumentoUrl { get; set; }
    public string? Observaciones { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
    public Empleado Empleado { get; set; } = null!;
}

[Table("ausencias")]
public class Ausencia
{
    public int Id { get; set; }
    public int EmpleadoId { get; set; }
    public string Tipo { get; set; } = "";
    public DateOnly FechaInicio { get; set; }
    public DateOnly FechaFin { get; set; }
    public string? Motivo { get; set; }
    public string Estado { get; set; } = "PENDIENTE";
    public int? AprobadoPor { get; set; }
    public int Dias { get; set; }
    public string? ComentarioResolucion { get; set; }
    public DateTime? ResueltoEn { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
    public Empleado Empleado { get; set; } = null!;
}

[Table("configuracion")]
public class Configuracion
{
    [System.ComponentModel.DataAnnotations.Key]
    public string Clave { get; set; } = "";
    public string Valor { get; set; } = "";
    public string? Descripcion { get; set; }
}

[Table("horarios")]
public class Horario
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public TimeOnly HoraEntrada { get; set; }
    public TimeOnly HoraSalida { get; set; }
    public int ToleranciaMin { get; set; } = 10;
    public string DiasLaborales { get; set; } = "1,2,3,4,5";
    public bool Activo { get; set; } = true;
}

[Table("asistencias")]
public class Asistencia
{
    public long Id { get; set; }
    public int EmpleadoId { get; set; }
    public DateOnly Fecha { get; set; }
    public DateTime? HoraEntrada { get; set; }
    public DateTime? HoraSalida { get; set; }
    public int MinutosRetardo { get; set; }
    public decimal HorasTrabajadas { get; set; }
    public decimal HorasExtra { get; set; }
    public string Estado { get; set; } = "PRESENTE";
    public string Origen { get; set; } = "MARCA";
    public string? Observaciones { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
    public Empleado Empleado { get; set; } = null!;
}

[Table("nominas")]
public class Nomina
{
    public int Id { get; set; }
    public int Anio { get; set; }
    public int Mes { get; set; }
    public DateOnly FechaInicio { get; set; }
    public DateOnly FechaFin { get; set; }
    public string Estado { get; set; } = "BORRADOR";
    public decimal TotalDevengado { get; set; }
    public decimal TotalDescuentos { get; set; }
    public decimal TotalNeto { get; set; }
    public int? GeneradoPor { get; set; }
    public DateTime GeneradoEn { get; set; } = DateTime.UtcNow;
    public DateTime? CerradoEn { get; set; }
    public ICollection<NominaDetalle> Detalles { get; set; } = new List<NominaDetalle>();
}

[Table("nomina_detalle")]
public class NominaDetalle
{
    public int Id { get; set; }
    public int NominaId { get; set; }
    public int EmpleadoId { get; set; }
    public decimal SalarioBase { get; set; }
    public int DiasFaltados { get; set; }
    public decimal DescuentoFaltas { get; set; }
    public decimal HorasExtra { get; set; }
    public decimal MontoHorasExtra { get; set; }
    public decimal BonificacionIncentivo { get; set; }
    public decimal OtrosBonos { get; set; }
    public decimal TotalDevengado { get; set; }
    public decimal Igss { get; set; }
    public decimal Isr { get; set; }
    public decimal OtrosDescuentos { get; set; }
    public decimal TotalDescuentos { get; set; }
    public decimal Neto { get; set; }
    public string? Observaciones { get; set; }
    public Nomina Nomina { get; set; } = null!;
    public Empleado Empleado { get; set; } = null!;
}

[Table("capacitaciones")]
public class Capacitacion
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public string? Descripcion { get; set; }
    public string? Proveedor { get; set; }
    public DateOnly FechaInicio { get; set; }
    public DateOnly FechaFin { get; set; }
    public int Horas { get; set; }
    public string Estado { get; set; } = "PROGRAMADA";
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
}

[Table("capacitacion_participantes")]
public class CapacitacionParticipante
{
    public int Id { get; set; }
    public int CapacitacionId { get; set; }
    public int EmpleadoId { get; set; }
    public string Estado { get; set; } = "INSCRITO";
    public decimal? Calificacion { get; set; }
    public Capacitacion Capacitacion { get; set; } = null!;
    public Empleado Empleado { get; set; } = null!;
}

[Table("evaluaciones")]
public class Evaluacion
{
    public int Id { get; set; }
    public int EmpleadoId { get; set; }
    public int? EvaluadorEmpleadoId { get; set; }
    public int? EvaluadorUsuarioId { get; set; }
    public string Periodo { get; set; } = "";
    public DateOnly Fecha { get; set; }
    public int Productividad { get; set; }
    public int Calidad { get; set; }
    public int TrabajoEquipo { get; set; }
    public int Puntualidad { get; set; }
    public decimal Total { get; set; }
    public string? Comentarios { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
    public Empleado Empleado { get; set; } = null!;
}

[Table("objetivos")]
public class Objetivo
{
    public int Id { get; set; }
    public int EmpleadoId { get; set; }
    public string Titulo { get; set; } = "";
    public string? Descripcion { get; set; }
    public DateOnly? FechaLimite { get; set; }
    public int Progreso { get; set; }
    public string Estado { get; set; } = "PENDIENTE";
    public int? CreadoPor { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
    public Empleado Empleado { get; set; } = null!;
}

[Table("bajas")]
public class Baja
{
    public int Id { get; set; }
    public int EmpleadoId { get; set; }
    public string Tipo { get; set; } = "";
    public DateOnly FechaBaja { get; set; }
    public string? Motivo { get; set; }
    public string? Observaciones { get; set; }
    public int? RegistradoPor { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
    public Empleado Empleado { get; set; } = null!;
}

[Table("liquidaciones")]
public class Liquidacion
{
    public int Id { get; set; }
    public int BajaId { get; set; }
    public decimal SalarioBase { get; set; }
    public decimal AniosServicio { get; set; }
    public decimal Indemnizacion { get; set; }
    public decimal Aguinaldo { get; set; }
    public decimal Bono14 { get; set; }
    public decimal VacacionesDias { get; set; }
    public decimal VacacionesMonto { get; set; }
    public decimal SalarioPendiente { get; set; }
    public decimal OtrosIngresos { get; set; }
    public decimal Descuentos { get; set; }
    public decimal Total { get; set; }
    public string? Observaciones { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
    public Baja Baja { get; set; } = null!;
}
