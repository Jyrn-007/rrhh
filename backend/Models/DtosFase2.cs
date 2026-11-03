using System.ComponentModel.DataAnnotations;

namespace KitsuneHrm.Api.Models;

public class DepartamentoRequest
{
    [Required, StringLength(100)] public string Nombre { get; set; } = "";
    [StringLength(250)] public string? Descripcion { get; set; }
    public int? JefeEmpleadoId { get; set; }
    public int? DepartamentoPadreId { get; set; }
}

public class PuestoRequest
{
    [Required, StringLength(100)] public string Nombre { get; set; } = "";
    [Required] public int DepartamentoId { get; set; }
    public decimal? SalarioMinimo { get; set; }
    public decimal? SalarioMaximo { get; set; }
}

public class SucursalRequest
{
    [Required, StringLength(100)] public string Nombre { get; set; } = "";
    [StringLength(250)] public string? Direccion { get; set; }
    [StringLength(30)] public string? Telefono { get; set; }
}

public class ActivoRequest { public bool Activo { get; set; } }

public class ContratoRequest
{
    [Required] public int EmpleadoId { get; set; }
    [Required] public string Tipo { get; set; } = "";
    [Required] public DateOnly FechaInicio { get; set; }
    public DateOnly? FechaFin { get; set; }
    [Range(0.01, 10000000)] public decimal Salario { get; set; }
    [StringLength(300)] public string? DocumentoUrl { get; set; }
    [StringLength(500)] public string? Observaciones { get; set; }
}

public class HorarioRequest
{
    [Required, StringLength(80)] public string Nombre { get; set; } = "";
    [Required] public string HoraEntrada { get; set; } = "08:00";
    [Required] public string HoraSalida { get; set; } = "17:00";
    [Range(0, 120)] public int ToleranciaMin { get; set; } = 10;
    [Required, RegularExpression(@"^[1-7](,[1-7])*$", ErrorMessage = "Días laborales: números 1-7 separados por coma")] public string DiasLaborales { get; set; } = "1,2,3,4,5";
    public bool Activo { get; set; } = true;
}

public class AsistenciaManualRequest
{
    [Required] public int EmpleadoId { get; set; }
    [Required] public DateOnly Fecha { get; set; }
    public string? HoraEntrada { get; set; }
    public string? HoraSalida { get; set; }
    [StringLength(300)] public string? Observaciones { get; set; }
}

public class FechaRequest { [Required] public DateOnly Fecha { get; set; } }

public class AusenciaRequest
{
    [Required] public string Tipo { get; set; } = "";
    [Required] public DateOnly FechaInicio { get; set; }
    [Required] public DateOnly FechaFin { get; set; }
    [StringLength(300)] public string? Motivo { get; set; }
}

public class AusenciaRegistrarRequest : AusenciaRequest
{
    [Required] public int EmpleadoId { get; set; }
}

public class ResolverRequest
{
    public bool Aprobar { get; set; }
    [StringLength(300)] public string? Comentario { get; set; }
}

public class NominaGenerarRequest
{
    [Range(2020, 2100)] public int Anio { get; set; }
    [Range(1, 12)] public int Mes { get; set; }
}

public class NominaDetalleRequest
{
    [Range(0, 10000000)] public decimal OtrosBonos { get; set; }
    [Range(0, 10000000)] public decimal Isr { get; set; }
    [Range(0, 10000000)] public decimal OtrosDescuentos { get; set; }
    [StringLength(300)] public string? Observaciones { get; set; }
}

public class CapacitacionRequest
{
    [Required, StringLength(150)] public string Nombre { get; set; } = "";
    [StringLength(500)] public string? Descripcion { get; set; }
    [StringLength(120)] public string? Proveedor { get; set; }
    [Required] public DateOnly FechaInicio { get; set; }
    [Required] public DateOnly FechaFin { get; set; }
    [Range(0, 1000)] public int Horas { get; set; }
    public string Estado { get; set; } = "PROGRAMADA";
}

public class ParticipantesRequest { public List<int> EmpleadoIds { get; set; } = new(); }

public class ParticipanteUpdateRequest
{
    [Required] public string Estado { get; set; } = "INSCRITO";
    [Range(0, 100)] public decimal? Calificacion { get; set; }
}

public class EvaluacionRequest
{
    [Required] public int EmpleadoId { get; set; }
    [Required, StringLength(30)] public string Periodo { get; set; } = "";
    [Range(1, 5)] public int Productividad { get; set; }
    [Range(1, 5)] public int Calidad { get; set; }
    [Range(1, 5)] public int TrabajoEquipo { get; set; }
    [Range(1, 5)] public int Puntualidad { get; set; }
    [StringLength(1000)] public string? Comentarios { get; set; }
}

public class ObjetivoRequest
{
    [Required] public int EmpleadoId { get; set; }
    [Required, StringLength(150)] public string Titulo { get; set; } = "";
    [StringLength(500)] public string? Descripcion { get; set; }
    public DateOnly? FechaLimite { get; set; }
}

public class ObjetivoUpdateRequest
{
    [StringLength(150)] public string? Titulo { get; set; }
    [StringLength(500)] public string? Descripcion { get; set; }
    public DateOnly? FechaLimite { get; set; }
    [Range(0, 100)] public int Progreso { get; set; }
    [Required] public string Estado { get; set; } = "EN_PROGRESO";
}

public class BajaCalcularRequest
{
    [Required] public int EmpleadoId { get; set; }
    [Required] public string Tipo { get; set; } = "";
    [Required] public DateOnly FechaBaja { get; set; }
}

public class BajaRequest : BajaCalcularRequest
{
    [StringLength(300)] public string? Motivo { get; set; }
    [StringLength(500)] public string? Observaciones { get; set; }
    [Range(0, 10000000)] public decimal OtrosIngresos { get; set; }
    [Range(0, 10000000)] public decimal Descuentos { get; set; }
}

public class LiquidacionAjusteRequest
{
    [Range(0, 10000000)] public decimal OtrosIngresos { get; set; }
    [Range(0, 10000000)] public decimal Descuentos { get; set; }
    [StringLength(500)] public string? Observaciones { get; set; }
}

public class PerfilRequest
{
    [EmailAddress, StringLength(150)] public string? Email { get; set; }
    [StringLength(30)] public string? Telefono { get; set; }
    [StringLength(250)] public string? Direccion { get; set; }
    [StringLength(120)] public string? ContactoEmergenciaNombre { get; set; }
    [StringLength(30)] public string? ContactoEmergenciaTelefono { get; set; }
    [StringLength(50)] public string? ContactoEmergenciaParentesco { get; set; }
}
