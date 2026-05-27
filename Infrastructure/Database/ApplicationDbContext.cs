using Microsoft.EntityFrameworkCore;
using tmr_backend.Features.Clientes.Domain;
using tmr_backend.Features.Auth.Domain;
using tmr_backend.Features.CargaActividades.Domain;
using tmr_backend.Features.Colaboradores.Domain;
using tmr_backend.Features.Configuracion.Domain;
using tmr_backend.Features.Dashboard.Domain;
using tmr_backend.Features.Lideres.Domain;
using tmr_backend.Features.Proyectos.Domain;
using tmr_backend.Features.Reportes.Domain;
using tmr_backend.Features.TimeReport.Domain;

namespace tmr_backend.Infrastructure.Database;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    // Agregar aquí los DbSets para cada feature
    public DbSet<Cliente> Clientes { get; set; } = null!;
    public DbSet<Usuario> Usuarios { get; set; } = null!;
    public DbSet<Actividad> Actividades { get; set; } = null!;
    public DbSet<Colaborador> Colaboradores { get; set; } = null!;
    public DbSet<ConfiguracionSistema> ConfiguracionesSistema { get; set; } = null!;
    public DbSet<DashboardItem> DashboardItems { get; set; } = null!;
    public DbSet<Lider> Lideres { get; set; } = null!;
    public DbSet<Proyecto> Proyectos { get; set; } = null!;
    public DbSet<Reporte> Reportes { get; set; } = null!;
    public DbSet<RegistroTiempo> RegistrosTiempo { get; set; } = null!;

    // Entidades del Módulo de Reportes (DB Real PostgreSQL)
    public DbSet<AdministracionPersona> AdministracionPersonas { get; set; } = null!;
    public DbSet<AdministracionEmpleado> AdministracionEmpleados { get; set; } = null!;
    public DbSet<AdministracionCliente> AdministracionClientes { get; set; } = null!;
    public DbSet<AdministracionLider> AdministracionLideres { get; set; } = null!;
    public DbSet<AdministracionCargo> AdministracionCargos { get; set; } = null!;
    public DbSet<TimeReportProyecto> TimeReportProyectos { get; set; } = null!;
    public DbSet<TimeReportTipoActividad> TimeReportTiposActividad { get; set; } = null!;
    public DbSet<TimeReportActividadDiaria> TimeReportActividadesDiarias { get; set; } = null!;
    public DbSet<TimeReportEmpleadoProyecto> TimeReportEmpleadosProyectos { get; set; } = null!;
    public DbSet<TimeReportPermiso> TimeReportPermisos { get; set; } = null!;
    public DbSet<TimeReportFeriado> TimeReportFeriados { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configuraciones de entidades (Fluent API) para DDD
        modelBuilder.Entity<Cliente>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Nombre).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Empresa).HasMaxLength(100);
        });

        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Nombre).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Descripcion).HasMaxLength(200);
        });

        modelBuilder.Entity<Actividad>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Nombre).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Descripcion).HasMaxLength(200);
        });

        modelBuilder.Entity<Colaborador>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Nombre).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Descripcion).HasMaxLength(200);
        });

        modelBuilder.Entity<ConfiguracionSistema>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Nombre).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Descripcion).HasMaxLength(200);
        });

        modelBuilder.Entity<DashboardItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Nombre).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Descripcion).HasMaxLength(200);
        });

        modelBuilder.Entity<Lider>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Nombre).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Descripcion).HasMaxLength(200);
        });

        modelBuilder.Entity<Proyecto>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Nombre).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Descripcion).HasMaxLength(200);
        });

        modelBuilder.Entity<Reporte>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Nombre).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Descripcion).HasMaxLength(200);
        });

        modelBuilder.Entity<RegistroTiempo>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Nombre).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Descripcion).HasMaxLength(200);
        });
    }
}
