using System.Text.RegularExpressions;
using KitsuneHrm.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace KitsuneHrm.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Rol> Roles => Set<Rol>();
    public DbSet<Permiso> Permisos => Set<Permiso>();
    public DbSet<RolPermiso> RolesPermisos => Set<RolPermiso>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<RegistroAcceso> RegistroAccesos => Set<RegistroAcceso>();
    public DbSet<AuditoriaLog> Auditoria => Set<AuditoriaLog>();
    public DbSet<LogSistema> LogsSistema => Set<LogSistema>();
    public DbSet<Sucursal> Sucursales => Set<Sucursal>();
    public DbSet<Departamento> Departamentos => Set<Departamento>();
    public DbSet<Puesto> Puestos => Set<Puesto>();
    public DbSet<Empleado> Empleados => Set<Empleado>();
    public DbSet<Contrato> Contratos => Set<Contrato>();
    public DbSet<Ausencia> Ausencias => Set<Ausencia>();
    public DbSet<Configuracion> Configuraciones => Set<Configuracion>();
    public DbSet<Horario> Horarios => Set<Horario>();
    public DbSet<Asistencia> Asistencias => Set<Asistencia>();
    public DbSet<Nomina> Nominas => Set<Nomina>();
    public DbSet<NominaDetalle> NominaDetalles => Set<NominaDetalle>();
    public DbSet<Capacitacion> Capacitaciones => Set<Capacitacion>();
    public DbSet<CapacitacionParticipante> CapacitacionParticipantes => Set<CapacitacionParticipante>();
    public DbSet<Evaluacion> Evaluaciones => Set<Evaluacion>();
    public DbSet<Objetivo> Objetivos => Set<Objetivo>();
    public DbSet<Baja> Bajas => Set<Baja>();
    public DbSet<Liquidacion> Liquidaciones => Set<Liquidacion>();

    protected override void ConfigureConventions(ModelConfigurationBuilder cb)
    {
        cb.Properties<decimal>().HavePrecision(14, 2);
    }

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<RolPermiso>().HasKey(x => new { x.RolId, x.PermisoId });

        // PascalCase (C#) -> snake_case (MySQL)
        foreach (var entity in mb.Model.GetEntityTypes())
            foreach (var prop in entity.GetProperties())
                prop.SetColumnName(Regex.Replace(prop.Name, "(?<=[a-z0-9])([A-Z])", "_$1").ToLowerInvariant());

        mb.Entity<AuditoriaLog>().Property(x => x.ValoresAntes).HasColumnType("json");
        mb.Entity<AuditoriaLog>().Property(x => x.ValoresDespues).HasColumnType("json");

        mb.Entity<Contrato>().Property(x => x.Salario).HasPrecision(12, 2);
        mb.Entity<Puesto>().Property(x => x.SalarioMinimo).HasPrecision(12, 2);
        mb.Entity<Puesto>().Property(x => x.SalarioMaximo).HasPrecision(12, 2);

        mb.Entity<Empleado>().HasOne(e => e.Jefe).WithMany().HasForeignKey(e => e.JefeId);
    }
}
