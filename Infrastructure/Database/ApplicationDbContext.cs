using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using tmr_backend.Infrastructure.Database.Entities;
using tmr_backend.Features.Usuarios.Domain;
using tmr_backend.Features.TimeReport.Domain;
using tmr_backend.Features.Reportes.Domain;
using tmr_backend.Features.Proyectos.Domain;
using tmr_backend.Features.Lideres.Domain;
using tmr_backend.Features.Clientes.Domain;
using tmr_backend.Features.CargaActividades.Domain;
using tmr_backend.Features.Colaboradores.Domain;
using tmr_backend.Features.Configuracion.Domain;
using tmr_backend.Features.Dashboard.Domain;

namespace tmr_backend.Infrastructure.Database;

public partial class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    // ─────────────────────────────────────────────────────────────────────
    // DbSets - Entidades en memoria (Features domain models)
    // ─────────────────────────────────────────────────────────────────────
    // ── DbSets propios del módulo ──────────────────────────
    public DbSet<Cliente> Clientes { get; set; } = null!;
    public DbSet<Usuario> Usuarios { get; set; } = null!;
    public DbSet<RegistroTiempo> RegistrosTiempo { get; set; } = null!;
    public DbSet<Reporte> Reportes { get; set; } = null!;
    public DbSet<Proyecto> Proyectos { get; set; } = null!;
    public DbSet<Lider> Lideres { get; set; } = null!;
    public DbSet<Colaborador> Colaboradores { get; set; } = null!;
    public DbSet<ConfiguracionSistema> ConfiguracionesSistema { get; set; } = null!;
    public DbSet<Actividad> Actividades { get; set; } = null!;
    public DbSet<DashboardItem> DashboardItems { get; set; } = null!;

    // ─────────────────────────────────────────────────────────────────────
    // DbSets - Entidades scaffoldeadas de la base de datos real (Inv_tmr_db)
    // ─────────────────────────────────────────────────────────────────────

    // ── DbSets generados (compañeros) ─────────────────────
    public virtual DbSet<TblAdministracionApariencium> TblAdministracionApariencia { get; set; } = null!;
    public virtual DbSet<TblAdministracionCargo> TblAdministracionCargos { get; set; } = null!;
    public virtual DbSet<TblAdministracionCatalogo> TblAdministracionCatalogos { get; set; } = null!;
    public virtual DbSet<TblAdministracionCatalogoDetalle> TblAdministracionCatalogoDetalles { get; set; } = null!;
    public virtual DbSet<TblAdministracionCliente> TblAdministracionClientes { get; set; } = null!;
    public virtual DbSet<TblAdministracionClienteUsuario> TblAdministracionClienteUsuarios { get; set; } = null!;
    public virtual DbSet<TblAdministracionEmpleado> TblAdministracionEmpleados { get; set; } = null!;
    public virtual DbSet<TblAdministracionLider> TblAdministracionLiders { get; set; } = null!;
    public virtual DbSet<TblAdministracionPersona> TblAdministracionPersonas { get; set; } = null!;
    public virtual DbSet<TblAdministracionRegistroAsignacion> TblAdministracionRegistroAsignacions { get; set; } = null!;

    // Auditoría
    public virtual DbSet<TblAuditoriaAsignacionEquipo> TblAuditoriaAsignacionEquipos { get; set; } = null!;
    public virtual DbSet<TblAuditoriaEmpleado> TblAuditoriaEmpleados { get; set; } = null!;
    public virtual DbSet<TblAuditoriaEquipo> TblAuditoriaEquipos { get; set; } = null!;
    public virtual DbSet<TblAuditoriaHistoricoGeneral> TblAuditoriaHistoricoGenerals { get; set; } = null!;
    public virtual DbSet<TblAuditoriaSesionUsuario> TblAuditoriaSesionUsuarios { get; set; } = null!;

    // Autenticación
    public virtual DbSet<TblAutenticacionAplicacion> TblAutenticacionAplicacions { get; set; } = null!;
    public virtual DbSet<TblAutenticacionInventarioToken> TblAutenticacionInventarioTokens { get; set; } = null!;
    public virtual DbSet<TblAutenticacionMenu> TblAutenticacionMenus { get; set; } = null!;
    public virtual DbSet<TblAutenticacionMenuRol> TblAutenticacionMenuRols { get; set; } = null!;
    public virtual DbSet<TblAutenticacionMenuUsuario> TblAutenticacionMenuUsuarios { get; set; } = null!;
    public virtual DbSet<TblAutenticacionModulo> TblAutenticacionModulos { get; set; } = null!;
    public virtual DbSet<TblAutenticacionPasswordHistorial> TblAutenticacionPasswordHistorials { get; set; } = null!;
    public virtual DbSet<TblAutenticacionPreguntaUsuario> TblAutenticacionPreguntaUsuarios { get; set; } = null!;
    public virtual DbSet<TblAutenticacionPrivilegioRol> TblAutenticacionPrivilegioRols { get; set; } = null!;
    public virtual DbSet<TblAutenticacionPrivilegioUsuario> TblAutenticacionPrivilegioUsuarios { get; set; } = null!;
    public virtual DbSet<TblAutenticacionRolModulo> TblAutenticacionRolModulos { get; set; } = null!;
    public virtual DbSet<TblAutenticacionSesion> TblAutenticacionSesions { get; set; } = null!;
    public virtual DbSet<TblAutenticacionSesionApp> TblAutenticacionSesionApps { get; set; } = null!;
    public virtual DbSet<TblAutenticacionTokenBlacklist> TblAutenticacionTokenBlacklists { get; set; } = null!;
    public virtual DbSet<TblAutenticacionUsuario> TblAutenticacionUsuarios { get; set; } = null!;
    public virtual DbSet<TblAutenticacionUsuarioAplicacion> TblAutenticacionUsuarioAplicacions { get; set; } = null!;
    public virtual DbSet<TblAutenticacionUsuarioModulo> TblAutenticacionUsuarioModulos { get; set; } = null!;
    public virtual DbSet<TblAutenticacionUsuarioRol> TblAutenticacionUsuarioRols { get; set; } = null!;

    // Inventario
    public virtual DbSet<TblInventarioAsignacionEquipo> TblInventarioAsignacionEquipos { get; set; } = null!;
    public virtual DbSet<TblInventarioBajaEquipo> TblInventarioBajaEquipos { get; set; } = null!;
    public virtual DbSet<TblInventarioCaracteristicaEquipo> TblInventarioCaracteristicaEquipos { get; set; } = null!;
    public virtual DbSet<TblInventarioDetalleFactura> TblInventarioDetalleFacturas { get; set; } = null!;
    public virtual DbSet<TblInventarioEmpresa> TblInventarioEmpresas { get; set; } = null!;
    public virtual DbSet<TblInventarioEquipo> TblInventarioEquipos { get; set; } = null!;
    public virtual DbSet<TblInventarioFactura> TblInventarioFacturas { get; set; } = null!;
    public virtual DbSet<TblInventarioProveedor> TblInventarioProveedors { get; set; } = null!;
    public virtual DbSet<TblInventarioReparacionEquipo> TblInventarioReparacionEquipos { get; set; } = null!;
    public virtual DbSet<TblInventarioStockCategorium> TblInventarioStockCategoria { get; set; } = null!;

    // Time Report
    public virtual DbSet<TblTimeReportActividadDiarium> TblTimeReportActividadDiaria { get; set; } = null!;
    public virtual DbSet<TblTimeReportEmpleadoProyecto> TblTimeReportEmpleadoProyectos { get; set; } = null!;
    public virtual DbSet<TblTimeReportFeriado> TblTimeReportFeriados { get; set; } = null!;
    public virtual DbSet<TblTimeReportHomologacionBanco> TblTimeReportHomologacionBancos { get; set; } = null!;
    public virtual DbSet<TblTimeReportOutboxCargo> TblTimeReportOutboxCargos { get; set; } = null!;
    public virtual DbSet<TblTimeReportPermiso> TblTimeReportPermisos { get; set; } = null!;
    public virtual DbSet<TblTimeReportProyeccionHora> TblTimeReportProyeccionHoras { get; set; } = null!;
    public virtual DbSet<TblTimeReportProyeccionHorasProyecto> TblTimeReportProyeccionHorasProyectos { get; set; } = null!;
    public virtual DbSet<TblTimeReportProyecto> TblTimeReportProyectos { get; set; } = null!;
    public virtual DbSet<TblTimeReportTipoActividad> TblTimeReportTipoActividads { get; set; } = null!;
    public virtual DbSet<TblTimeReportTipoProyecto> TblTimeReportTipoProyectos { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasPostgresExtension("pgcrypto")
            .HasPostgresExtension("uuid-ossp");

        // ── MAPEO COMPAÑEROS ──────────────────────────────
        modelBuilder.Entity<TblAdministracionApariencium>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_administracion_apariencia");
            entity.ToTable("tbl_administracion_apariencia", "administracion");
            entity.Property(e => e.Id).UseIdentityAlwaysColumn().HasColumnName("id");
            entity.Property(e => e.Activo).HasDefaultValue(true).HasColumnName("activo");
            entity.Property(e => e.Bordercaja).HasColumnName("bordercaja");
            entity.Property(e => e.Colorfondo).HasMaxLength(25).HasColumnName("colorfondo");
            entity.Property(e => e.Encabezadofijo).HasColumnName("encabezadofijo");
            entity.Property(e => e.Fechacreacion).HasDefaultValueSql("now()").HasColumnName("fechacreacion");
            entity.Property(e => e.Fechamodificacion).HasColumnName("fechamodificacion");
            entity.Property(e => e.Fondocaja).HasMaxLength(25).HasColumnName("fondocaja");
            entity.Property(e => e.Fondologin).HasMaxLength(25).HasColumnName("fondologin");
            entity.Property(e => e.Ipcreacion).HasMaxLength(45).HasColumnName("ipcreacion");
            entity.Property(e => e.Ipmodificacion).HasMaxLength(45).HasColumnName("ipmodificacion");
            entity.Property(e => e.Menucolapsado).HasColumnName("menucolapsado");
            entity.Property(e => e.Posicionmenu).HasMaxLength(25).HasColumnName("posicionmenu");
            entity.Property(e => e.Tipografia).HasMaxLength(25).HasColumnName("tipografia");
            entity.Property(e => e.Usuariocreacion).HasMaxLength(50).HasColumnName("usuariocreacion");
            entity.Property(e => e.Usuariomodificacion).HasMaxLength(50).HasColumnName("usuariomodificacion");
        });

        modelBuilder.Entity<TblAdministracionCargo>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_administracion_cargo");
            entity.ToTable("tbl_administracion_cargo", "administracion");
            entity.Property(e => e.Id).UseIdentityAlwaysColumn().HasColumnName("id");
            entity.Property(e => e.Activo).HasDefaultValue(true).HasColumnName("activo");
            entity.Property(e => e.Descripcion).HasColumnName("descripcion");
            entity.Property(e => e.Fechacreacion).HasDefaultValueSql("now()").HasColumnName("fechacreacion");
            entity.Property(e => e.Fechamodificacion).HasColumnName("fechamodificacion");
            entity.Property(e => e.Iddepartamento).HasColumnName("iddepartamento");
            entity.Property(e => e.Ipcreacion).HasMaxLength(45).HasColumnName("ipcreacion");
            entity.Property(e => e.Ipmodificacion).HasMaxLength(45).HasColumnName("ipmodificacion");
            entity.Property(e => e.Nombrecargo).HasMaxLength(100).HasColumnName("nombrecargo");
            entity.Property(e => e.Usuariocreacion).HasMaxLength(50).HasColumnName("usuariocreacion");
            entity.Property(e => e.Usuariomodificacion).HasMaxLength(50).HasColumnName("usuariomodificacion");
            entity.HasOne(d => d.IddepartamentoNavigation).WithMany(p => p.TblAdministracionCargos)
                .HasForeignKey(d => d.Iddepartamento)
                .HasConstraintName("fk_administracion_cargo_departamento");
        });

        modelBuilder.Entity<TblAdministracionCatalogo>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_administracion_catalogo");
            entity.ToTable("tbl_administracion_catalogo", "administracion");
            entity.HasIndex(e => new { e.Tipocatalogo, e.Codigo }, "uq_administracion_catalogo_tipo_codigo").IsUnique();
            entity.Property(e => e.Id).UseIdentityAlwaysColumn().HasColumnName("id");
            entity.Property(e => e.Activo).HasDefaultValue(true).HasColumnName("activo");
            entity.Property(e => e.Codigo).HasMaxLength(3).HasColumnName("codigo");
            entity.Property(e => e.Descripcion).HasMaxLength(255).HasColumnName("descripcion");
            entity.Property(e => e.Fechacreacion).HasDefaultValueSql("now()").HasColumnName("fechacreacion");
            entity.Property(e => e.Fechamodificacion).HasColumnName("fechamodificacion");
            entity.Property(e => e.Ipcreacion).HasMaxLength(45).HasColumnName("ipcreacion");
            entity.Property(e => e.Ipmodificacion).HasMaxLength(45).HasColumnName("ipmodificacion");
            entity.Property(e => e.Tipocatalogo).HasMaxLength(3).HasColumnName("tipocatalogo");
            entity.Property(e => e.Usuariocreacion).HasMaxLength(50).HasColumnName("usuariocreacion");
            entity.Property(e => e.Usuariomodificacion).HasMaxLength(50).HasColumnName("usuariomodificacion");
        });

        modelBuilder.Entity<TblAdministracionCatalogoDetalle>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_administracion_catalogo_detalle");
            entity.ToTable("tbl_administracion_catalogo_detalle", "administracion");
            entity.HasIndex(e => new { e.Idcatalogo, e.Codigovalor }, "uq_administracion_catalogo_detalle_id_codigo").IsUnique();
            entity.Ignore(e => e.TblAdministracionClientes);
            entity.Ignore(e => e.TblAdministracionLiders);
            entity.Property(e => e.Id).UseIdentityAlwaysColumn().HasColumnName("id");
            entity.Property(e => e.Activo).HasDefaultValue(true).HasColumnName("activo");
            entity.Property(e => e.Codigovalor).HasMaxLength(5).HasColumnName("codigovalor");
            entity.Property(e => e.Descripcion).HasMaxLength(255).HasColumnName("descripcion");
            entity.Property(e => e.Fechacreacion).HasDefaultValueSql("now()").HasColumnName("fechacreacion");
            entity.Property(e => e.Fechamodificacion).HasColumnName("fechamodificacion");
            entity.Property(e => e.Idcatalogo).HasColumnName("idcatalogo");
            entity.Property(e => e.Ipcreacion).HasMaxLength(45).HasColumnName("ipcreacion");
            entity.Property(e => e.Ipmodificacion).HasMaxLength(45).HasColumnName("ipmodificacion");
            entity.Property(e => e.Orden).HasColumnName("orden");
            entity.Property(e => e.Usuariocreacion).HasMaxLength(50).HasColumnName("usuariocreacion");
            entity.Property(e => e.Usuariomodificacion).HasMaxLength(50).HasColumnName("usuariomodificacion");
            entity.Property(e => e.Valor).HasMaxLength(100).HasColumnName("valor");
            entity.Property(e => e.Valorextra).HasMaxLength(100).HasColumnName("valorextra");
            entity.HasOne(d => d.IdcatalogoNavigation).WithMany(p => p.TblAdministracionCatalogoDetalles)
                .HasForeignKey(d => d.Idcatalogo)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_administracion_catalogo_detalle_catalogo");
        });

        modelBuilder.Entity<TblAdministracionCliente>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_administracion_cliente");
            entity.ToTable("tbl_administracion_cliente", "administracion");
            entity.Property(e => e.Id).UseIdentityAlwaysColumn().HasColumnName("id");
            entity.Property(e => e.Activo).HasDefaultValue(true).HasColumnName("activo");
            entity.Property(e => e.Direccion).HasMaxLength(255).HasColumnName("direccion");
            entity.Property(e => e.Email).HasMaxLength(100).HasColumnName("email");
            entity.Property(e => e.Fechacreacion).HasDefaultValueSql("now()").HasColumnName("fechacreacion");
            entity.Property(e => e.Fechamodificacion).HasColumnName("fechamodificacion");
            entity.Ignore(e => e.Idtipoidentificacion);
            entity.Ignore(e => e.IdtipoidentificacionNavigation);
            entity.Property(e => e.Ipcreacion).HasMaxLength(45).HasColumnName("ipcreacion");
            entity.Property(e => e.Ipmodificacion).HasMaxLength(45).HasColumnName("ipmodificacion");
            entity.Property(e => e.Nombrecomercial).HasMaxLength(100).HasColumnName("nombrecomercial");
            entity.Property(e => e.Numeroidentificacion).HasMaxLength(20).HasColumnName("ruc");
            entity.Property(e => e.Razonsocial).HasMaxLength(150).HasColumnName("razonsocial");
            entity.Property(e => e.Telefono).HasMaxLength(20).HasColumnName("telefono");
            entity.Property(e => e.Usuariocreacion).HasMaxLength(50).HasColumnName("usuariocreacion");
            entity.Property(e => e.Usuariomodificacion).HasMaxLength(50).HasColumnName("usuariomodificacion");
        });

        modelBuilder.Entity<TblAdministracionClienteUsuario>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_administracion_cliente_usuario");
            entity.ToTable("tbl_administracion_cliente_usuario", "administracion");
            entity.Property(e => e.Id).UseIdentityAlwaysColumn().HasColumnName("id");
            entity.Property(e => e.Activo).HasDefaultValue(true).HasColumnName("activo");
            entity.Property(e => e.Fechacreacion).HasDefaultValueSql("now()").HasColumnName("fechacreacion");
            entity.Property(e => e.Fechamodificacion).HasColumnName("fechamodificacion");
            entity.Property(e => e.Idcliente).HasColumnName("idcliente");
            entity.Property(e => e.Idusuario).HasColumnName("idusuario");
            entity.Property(e => e.Ipcreacion).HasMaxLength(45).HasColumnName("ipcreacion");
            entity.Property(e => e.Ipmodificacion).HasMaxLength(45).HasColumnName("ipmodificacion");
            entity.Property(e => e.Usuariocreacion).HasMaxLength(50).HasColumnName("usuariocreacion");
            entity.Property(e => e.Usuariomodificacion).HasMaxLength(50).HasColumnName("usuariomodificacion");
            entity.HasOne(d => d.IdclienteNavigation).WithMany(p => p.TblAdministracionClienteUsuarios)
                .HasForeignKey(d => d.Idcliente).OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_administracion_cliente_usuario_cliente");
            entity.HasOne(d => d.IdusuarioNavigation).WithMany(p => p.TblAdministracionClienteUsuarios)
                .HasForeignKey(d => d.Idusuario).OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_administracion_cliente_usuario_usuario");
        });

        modelBuilder.Entity<TblAdministracionEmpleado>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_administracion_empleado");
            entity.ToTable("tbl_administracion_empleado", "administracion");
            entity.HasIndex(e => e.Idpersona, "idx_adm_empleado_persona");
            entity.HasIndex(e => e.Codigoempleado, "uq_administracion_empleado_codigo").IsUnique();
            entity.Property(e => e.Id).UseIdentityAlwaysColumn().HasColumnName("id");
            entity.Property(e => e.Activo).HasDefaultValue(true).HasColumnName("activo");
            entity.Property(e => e.Codigoempleado).HasMaxLength(20).HasColumnName("codigoempleado");
            entity.Property(e => e.Emailcorporativo).HasMaxLength(100).HasColumnName("emailcorporativo");
            entity.Property(e => e.Fechacreacion).HasDefaultValueSql("now()").HasColumnName("fechacreacion");
            entity.Property(e => e.Fechaingreso).HasColumnName("fechaingreso");
            entity.Property(e => e.Fechamodificacion).HasColumnName("fechamodificacion");
            entity.Property(e => e.Fechaterminacion).HasColumnName("fechaterminacion");
            entity.Property(e => e.Idcargo).HasColumnName("idcargo");
            entity.Property(e => e.Idcategoriaempleado).HasColumnName("idcategoriaempleado");
            entity.Property(e => e.Idempresacatalogo).HasColumnName("idempresacatalogo");
            entity.Property(e => e.Idmodotrabajo).HasColumnName("idmodotrabajo");
            entity.Property(e => e.Idpersona).HasColumnName("idpersona");
            entity.Property(e => e.Idtipocontrato).HasColumnName("idtipocontrato");
            entity.Property(e => e.Ipcreacion).HasMaxLength(45).HasColumnName("ipcreacion");
            entity.Property(e => e.Ipmodificacion).HasMaxLength(45).HasColumnName("ipmodificacion");
            entity.Property(e => e.Salario).HasPrecision(12, 2).HasColumnName("salario");
            entity.Property(e => e.Usuariocreacion).HasMaxLength(50).HasColumnName("usuariocreacion");
            entity.Property(e => e.Usuariomodificacion).HasMaxLength(50).HasColumnName("usuariomodificacion");
            entity.HasOne(d => d.IdcargoNavigation).WithMany(p => p.TblAdministracionEmpleados)
                .HasForeignKey(d => d.Idcargo).HasConstraintName("fk_administracion_empleado_cargo");
            entity.HasOne(d => d.IdcategoriaempleadoNavigation).WithMany(p => p.TblAdministracionEmpleadoIdcategoriaempleadoNavigations)
                .HasForeignKey(d => d.Idcategoriaempleado).HasConstraintName("fk_administracion_empleado_categoria");
            entity.HasOne(d => d.IdempresacatalogoNavigation).WithMany(p => p.TblAdministracionEmpleadoIdempresacatalogoNavigations)
                .HasForeignKey(d => d.Idempresacatalogo).HasConstraintName("fk_administracion_empleado_empresa");
            entity.HasOne(d => d.IdmodotrabajoNavigation).WithMany(p => p.TblAdministracionEmpleadoIdmodotrabajoNavigations)
                .HasForeignKey(d => d.Idmodotrabajo).HasConstraintName("fk_administracion_empleado_modo_trabajo");
            entity.HasOne(d => d.IdpersonaNavigation).WithMany(p => p.TblAdministracionEmpleados)
                .HasForeignKey(d => d.Idpersona).OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_administracion_empleado_persona");
            entity.HasOne(d => d.IdtipocontratoNavigation).WithMany(p => p.TblAdministracionEmpleadoIdtipocontratoNavigations)
                .HasForeignKey(d => d.Idtipocontrato).HasConstraintName("fk_administracion_empleado_contrato");
        });

        modelBuilder.Entity<TblAdministracionLider>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_administracion_lider");
            entity.ToTable("tbl_administracion_lider", "administracion");
            entity.Property(e => e.Id).UseIdentityAlwaysColumn().HasColumnName("id");
            entity.Property(e => e.Activo).HasDefaultValue(true).HasColumnName("activo");
            entity.Property(e => e.Fechacreacion).HasDefaultValueSql("now()").HasColumnName("fechacreacion");
            entity.Property(e => e.Fechamodificacion).HasColumnName("fechamodificacion");
            entity.Property(e => e.Idpersona).HasColumnName("idpersona");
            entity.Ignore(e => e.Idtipo);
            entity.Ignore(e => e.IdtipoNavigation);
            entity.Property(e => e.Ipcreacion).HasMaxLength(45).HasColumnName("ipcreacion");
            entity.Property(e => e.Ipmodificacion).HasMaxLength(45).HasColumnName("ipmodificacion");
            entity.Property(e => e.Usuariocreacion).HasMaxLength(50).HasColumnName("usuariocreacion");
            entity.Property(e => e.Usuariomodificacion).HasMaxLength(50).HasColumnName("usuariomodificacion");
            entity.HasOne(d => d.IdpersonaNavigation).WithMany(p => p.TblAdministracionLiders)
                .HasForeignKey(d => d.Idpersona).OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_administracion_lider_persona");
        });

        modelBuilder.Entity<TblAdministracionPersona>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_administracion_persona");
            entity.ToTable("tbl_administracion_persona", "administracion");
            entity.HasIndex(e => e.Numeroidentificacion, "idx_adm_persona_identificacion");

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity.Property(e => e.Activo)
                .HasDefaultValue(true)
                .HasColumnName("activo");
            entity.Property(e => e.Apellidos)
                .HasMaxLength(100)
                .HasColumnName("apellidopaterno");
            entity.Property(e => e.Direccion)
                .HasMaxLength(255)
                .HasColumnName("direccion");
            entity.Property(e => e.Email)
                .HasMaxLength(100)
                .HasColumnName("email");
            entity.Property(e => e.Fechacreacion)
                .HasDefaultValueSql("now()")
                .HasColumnName("fechacreacion");
            entity.Property(e => e.Id).UseIdentityAlwaysColumn().HasColumnName("id");
            entity.Property(e => e.Activo).HasDefaultValue(true).HasColumnName("activo");
            entity.Property(e => e.Apellidos).HasMaxLength(100).HasColumnName("apellidopaterno");
            entity.Property(e => e.Direccion).HasMaxLength(255).HasColumnName("direccion");
            entity.Property(e => e.Email).HasMaxLength(100).HasColumnName("email");
            entity.Property(e => e.Fechacreacion).HasDefaultValueSql("now()").HasColumnName("fechacreacion");
            entity.Property(e => e.Fechamodificacion).HasColumnName("fechamodificacion");
            entity.Property(e => e.Fechanacimiento).HasColumnName("fechanacimiento");
            entity.Property(e => e.Idgenero).HasColumnName("idgenero");
            entity.Property(e => e.Idnacionalidad).HasColumnName("idnacionalidad");
            entity.Property(e => e.Idtipoidentificacion).HasColumnName("idtipoidentificacion");
            entity.Property(e => e.Ipcreacion)
                .HasMaxLength(45)
                .HasColumnName("ipcreacion");
            entity.Property(e => e.Ipmodificacion)
                .HasMaxLength(45)
                .HasColumnName("ipmodificacion");
            entity.Property(e => e.Nombres)
                .HasMaxLength(100)
                .HasColumnName("primernombre");
            entity.Property(e => e.Numeroidentificacion)
                .HasMaxLength(20)
                .HasColumnName("numeroidentificacion");
            entity.Property(e => e.Telefono)
                .HasMaxLength(20)
                .HasColumnName("telefono");
            entity.Property(e => e.Tipopersona)
                .HasMaxLength(10)
                .HasColumnName("tipopersona");
            entity.Property(e => e.Usuariocreacion)
                .HasMaxLength(50)
                .HasColumnName("usuariocreacion");
            entity.Property(e => e.Usuariomodificacion)
                .HasMaxLength(50)
                .HasColumnName("usuariomodificacion");

            entity.Property(e => e.Ipcreacion).HasMaxLength(45).HasColumnName("ipcreacion");
            entity.Property(e => e.Ipmodificacion).HasMaxLength(45).HasColumnName("ipmodificacion");
            entity.Property(e => e.Nombres).HasMaxLength(100).HasColumnName("primernombre");
            entity.Property(e => e.Numeroidentificacion).HasMaxLength(20).HasColumnName("numeroidentificacion");
            entity.Property(e => e.Telefono).HasMaxLength(20).HasColumnName("telefono");
            entity.Property(e => e.Tipopersona).HasMaxLength(10).HasColumnName("tipopersona");
            entity.Property(e => e.Usuariocreacion).HasMaxLength(50).HasColumnName("usuariocreacion");
            entity.Property(e => e.Usuariomodificacion).HasMaxLength(50).HasColumnName("usuariomodificacion");
            entity.HasOne(d => d.IdgeneroNavigation).WithMany(p => p.TblAdministracionPersonaIdgeneroNavigations)
                .HasForeignKey(d => d.Idgenero).HasConstraintName("fk_administracion_persona_genero");
            entity.HasOne(d => d.IdnacionalidadNavigation).WithMany(p => p.TblAdministracionPersonaIdnacionalidadNavigations)
                .HasForeignKey(d => d.Idnacionalidad).HasConstraintName("fk_administracion_persona_nacionalidad");
            entity.HasOne(d => d.IdtipoidentificacionNavigation).WithMany(p => p.TblAdministracionPersonaIdtipoidentificacionNavigations)
                .HasForeignKey(d => d.Idtipoidentificacion).HasConstraintName("fk_administracion_persona_tipo_identificacion");
        });

        modelBuilder.Entity<TblAdministracionRegistroAsignacion>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_administracion_registro_asignacion");
            entity.ToTable("tbl_administracion_registro_asignacion", "administracion");
            entity.Property(e => e.Id).UseIdentityAlwaysColumn().HasColumnName("id");
            entity.Property(e => e.Activo).HasDefaultValue(true).HasColumnName("activo");
            entity.Property(e => e.Descripcion).HasColumnName("descripcion");
            entity.Property(e => e.Fechacreacion).HasDefaultValueSql("now()").HasColumnName("fechacreacion");
            entity.Property(e => e.Fechamodificacion).HasColumnName("fechamodificacion");
            entity.Property(e => e.Fecharegistro).HasColumnName("fecharegistro");
            entity.Property(e => e.Idempleado).HasColumnName("idempleado");
            entity.Property(e => e.Ipcreacion).HasMaxLength(45).HasColumnName("ipcreacion");
            entity.Property(e => e.Ipmodificacion).HasMaxLength(45).HasColumnName("ipmodificacion");
            entity.Property(e => e.Usuariocreacion).HasMaxLength(50).HasColumnName("usuariocreacion");
            entity.Property(e => e.Usuariomodificacion).HasMaxLength(50).HasColumnName("usuariomodificacion");
            entity.HasOne(d => d.IdempleadoNavigation).WithMany(p => p.TblAdministracionRegistroAsignacions)
                .HasForeignKey(d => d.Idempleado).HasConstraintName("fk_administracion_registro_asignacion_empleado");
        });

        modelBuilder.Entity<TblAuditoriaAsignacionEquipo>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_auditoria_asignacion_equipo");
            entity.ToTable("tbl_auditoria_asignacion_equipo", "auditoria");
            entity.Property(e => e.Id).UseIdentityAlwaysColumn().HasColumnName("id");
            entity.Property(e => e.Agenteusuario).HasMaxLength(255).HasColumnName("agenteusuario");
            entity.Property(e => e.Cambiadopor).HasMaxLength(50).HasColumnName("cambiadopor");
            entity.Property(e => e.Camposmodificados).HasColumnType("jsonb").HasColumnName("camposmodificados");
            entity.Property(e => e.Direccionip).HasMaxLength(45).HasColumnName("direccionip");
            entity.Property(e => e.Fechacreacion).HasDefaultValueSql("now()").HasColumnName("fechacreacion");
            entity.Property(e => e.Idasignacion).HasColumnName("idasignacion");
            entity.Property(e => e.Ipcreacion).HasMaxLength(45).HasColumnName("ipcreacion");
            entity.Property(e => e.Tipocambio).HasMaxLength(10).HasColumnName("tipocambio");
            entity.Property(e => e.Usuariocreacion).HasMaxLength(50).HasColumnName("usuariocreacion");
            entity.Property(e => e.Valoresanteriores).HasColumnType("jsonb").HasColumnName("valoresanteriores");
            entity.Property(e => e.Valoresnuevos).HasColumnType("jsonb").HasColumnName("valoresnuevos");
        });

        modelBuilder.Entity<TblAuditoriaEmpleado>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_auditoria_empleado");
            entity.ToTable("tbl_auditoria_empleado", "auditoria");
            entity.HasIndex(e => e.Fechacreacion, "idx_aud_empleado_fecha");
            entity.HasIndex(e => e.Idempleado, "idx_aud_empleado_id");
            entity.Property(e => e.Id).UseIdentityAlwaysColumn().HasColumnName("id");
            entity.Property(e => e.Agenteusuario).HasMaxLength(255).HasColumnName("agenteusuario");
            entity.Property(e => e.Cambiadopor).HasMaxLength(50).HasColumnName("cambiadopor");
            entity.Property(e => e.Camposmodificados).HasColumnType("jsonb").HasColumnName("camposmodificados");
            entity.Property(e => e.Direccionip).HasMaxLength(45).HasColumnName("direccionip");
            entity.Property(e => e.Fechacreacion).HasDefaultValueSql("now()").HasColumnName("fechacreacion");
            entity.Property(e => e.Idempleado).HasColumnName("idempleado");
            entity.Property(e => e.Ipcreacion).HasMaxLength(45).HasColumnName("ipcreacion");
            entity.Property(e => e.Tipocambio).HasMaxLength(10).HasColumnName("tipocambio");
            entity.Property(e => e.Usuariocreacion).HasMaxLength(50).HasColumnName("usuariocreacion");
            entity.Property(e => e.Valoresanteriores).HasColumnType("jsonb").HasColumnName("valoresanteriores");
            entity.Property(e => e.Valoresnuevos).HasColumnType("jsonb").HasColumnName("valoresnuevos");
        });

        modelBuilder.Entity<TblAuditoriaEquipo>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_auditoria_equipo");
            entity.ToTable("tbl_auditoria_equipo", "auditoria");
            entity.Property(e => e.Id).UseIdentityAlwaysColumn().HasColumnName("id");
            entity.Property(e => e.Agenteusuario).HasMaxLength(255).HasColumnName("agenteusuario");
            entity.Property(e => e.Cambiadopor).HasMaxLength(50).HasColumnName("cambiadopor");
            entity.Property(e => e.Camposmodificados).HasColumnType("jsonb").HasColumnName("camposmodificados");
            entity.Property(e => e.Direccionip).HasMaxLength(45).HasColumnName("direccionip");
            entity.Property(e => e.Fechacreacion).HasDefaultValueSql("now()").HasColumnName("fechacreacion");
            entity.Property(e => e.Idequipo).HasColumnName("idequipo");
            entity.Property(e => e.Ipcreacion).HasMaxLength(45).HasColumnName("ipcreacion");
            entity.Property(e => e.Tipocambio).HasMaxLength(10).HasColumnName("tipocambio");
            entity.Property(e => e.Usuariocreacion).HasMaxLength(50).HasColumnName("usuariocreacion");
            entity.Property(e => e.Valoresanteriores).HasColumnType("jsonb").HasColumnName("valoresanteriores");
            entity.Property(e => e.Valoresnuevos).HasColumnType("jsonb").HasColumnName("valoresnuevos");
        });

        modelBuilder.Entity<TblAuditoriaHistoricoGeneral>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_auditoria_historico_general");
            entity.ToTable("tbl_auditoria_historico_general", "auditoria");
            entity.HasIndex(e => e.Fechacambio, "idx_aud_historico_fecha");
            entity.HasIndex(e => e.Nombretabla, "idx_aud_historico_tabla");
            entity.Property(e => e.Id).UseIdentityAlwaysColumn().HasColumnName("id");
            entity.Property(e => e.Cambiadopor).HasMaxLength(50).HasColumnName("cambiadopor");
            entity.Property(e => e.Datosanteriores).HasColumnType("jsonb").HasColumnName("datosanteriores");
            entity.Property(e => e.Datosnuevos).HasColumnType("jsonb").HasColumnName("datosnuevos");
            entity.Property(e => e.Direccionip).HasMaxLength(45).HasColumnName("direccionip");
            entity.Property(e => e.Fechacambio).HasDefaultValueSql("now()").HasColumnName("fechacambio");
            entity.Property(e => e.Idregistro).HasMaxLength(50).HasColumnName("idregistro");
            entity.Property(e => e.Nombretabla).HasMaxLength(100).HasColumnName("nombretabla");
            entity.Property(e => e.Tipooperacion).HasMaxLength(10).HasColumnName("tipooperacion");
        });

        modelBuilder.Entity<TblAuditoriaSesionUsuario>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_auditoria_sesion_usuario");
            entity.ToTable("tbl_auditoria_sesion_usuario", "auditoria");
            entity.HasIndex(e => e.Idusuario, "idx_aud_sesion_usuario");
            entity.Property(e => e.Id).UseIdentityAlwaysColumn().HasColumnName("id");
            entity.Property(e => e.Agenteusuario).HasMaxLength(255).HasColumnName("agenteusuario");
            entity.Property(e => e.Direccionip).HasMaxLength(45).HasColumnName("direccionip");
            entity.Property(e => e.Fechacreacion).HasDefaultValueSql("now()").HasColumnName("fechacreacion");
            entity.Property(e => e.Idusuario).HasColumnName("idusuario");
            entity.Property(e => e.Tipocambio).HasMaxLength(10).HasColumnName("tipocambio");
        });

        modelBuilder.Entity<TblAutenticacionAplicacion>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_autenticacion_aplicacion");
            entity.ToTable("tbl_autenticacion_aplicacion", "autenticacion");
            entity.Property(e => e.Id).UseIdentityAlwaysColumn().HasColumnName("id");
            entity.Property(e => e.Activo).HasDefaultValue(true).HasColumnName("activo");
            entity.Property(e => e.Descripcion).HasMaxLength(255).HasColumnName("descripcion");
            entity.Property(e => e.Fechacreacion).HasDefaultValueSql("now()").HasColumnName("fechacreacion");
            entity.Property(e => e.Fechamodificacion).HasColumnName("fechamodificacion");
            entity.Property(e => e.Ipcreacion).HasMaxLength(45).HasColumnName("ipcreacion");
            entity.Property(e => e.Ipmodificacion).HasMaxLength(45).HasColumnName("ipmodificacion");
            entity.Property(e => e.Nombreaplicacion).HasMaxLength(100).HasColumnName("nombreaplicacion");
            entity.Property(e => e.Urlbase).HasMaxLength(255).HasColumnName("urlbase");
            entity.Property(e => e.Usuariocreacion).HasMaxLength(50).HasColumnName("usuariocreacion");
            entity.Property(e => e.Usuariomodificacion).HasMaxLength(50).HasColumnName("usuariomodificacion");
        });

        modelBuilder.Entity<TblAutenticacionInventarioToken>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_autenticacion_inventario_token");
            entity.ToTable("tbl_autenticacion_inventario_token", "autenticacion");
            entity.Property(e => e.Id).UseIdentityAlwaysColumn().HasColumnName("id");
            entity.Property(e => e.Activo).HasDefaultValue(true).HasColumnName("activo");
            entity.Property(e => e.Fechacreacion).HasDefaultValueSql("now()").HasColumnName("fechacreacion");
            entity.Property(e => e.Fechamodificacion).HasColumnName("fechamodificacion");
            entity.Property(e => e.Ipcreacion).HasMaxLength(45).HasColumnName("ipcreacion");
            entity.Property(e => e.Ipmodificacion).HasMaxLength(45).HasColumnName("ipmodificacion");
            entity.Property(e => e.Token).HasMaxLength(1500).HasColumnName("token");
            entity.Property(e => e.Usuariocreacion).HasMaxLength(50).HasColumnName("usuariocreacion");
            entity.Property(e => e.Usuariomodificacion).HasMaxLength(50).HasColumnName("usuariomodificacion");
        });

        modelBuilder.Entity<TblAutenticacionMenu>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_autenticacion_menu");
            entity.ToTable("tbl_autenticacion_menu", "autenticacion");
            entity.Property(e => e.Id).UseIdentityAlwaysColumn().HasColumnName("id");
            entity.Property(e => e.Activo).HasDefaultValue(true).HasColumnName("activo");
            entity.Property(e => e.Fechacreacion).HasDefaultValueSql("now()").HasColumnName("fechacreacion");
            entity.Property(e => e.Fechamodificacion).HasColumnName("fechamodificacion");
            entity.Property(e => e.Icono).HasMaxLength(50).HasColumnName("icono");
            entity.Property(e => e.Idaplicacion).HasColumnName("idaplicacion");
            entity.Property(e => e.Idmenupadre).HasColumnName("idmenupadre");
            entity.Property(e => e.Ipcreacion).HasMaxLength(45).HasColumnName("ipcreacion");
            entity.Property(e => e.Ipmodificacion).HasMaxLength(45).HasColumnName("ipmodificacion");
            entity.Property(e => e.Nombremenu).HasMaxLength(100).HasColumnName("nombremenu");
            entity.Property(e => e.Ordenvisualizacion).HasColumnName("ordenvisualizacion");
            entity.Property(e => e.Rutamenu).HasMaxLength(255).HasColumnName("rutamenu");
            entity.Property(e => e.Usuariocreacion).HasMaxLength(50).HasColumnName("usuariocreacion");
            entity.Property(e => e.Usuariomodificacion).HasMaxLength(50).HasColumnName("usuariomodificacion");
            entity.HasOne(d => d.IdaplicacionNavigation).WithMany(p => p.TblAutenticacionMenus)
                .HasForeignKey(d => d.Idaplicacion).HasConstraintName("fk_autenticacion_menu_aplicacion");
            entity.HasOne(d => d.IdmenupadreNavigation).WithMany(p => p.InverseIdmenupadreNavigation)
                .HasForeignKey(d => d.Idmenupadre).OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_autenticacion_menu_padre");
        });

        modelBuilder.Entity<TblAutenticacionMenuRol>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_autenticacion_menu_rol");
            entity.ToTable("tbl_autenticacion_menu_rol", "autenticacion");
            entity.Property(e => e.Id).UseIdentityAlwaysColumn().HasColumnName("id");
            entity.Property(e => e.Activo).HasDefaultValue(true).HasColumnName("activo");
            entity.Property(e => e.Fechacreacion).HasDefaultValueSql("now()").HasColumnName("fechacreacion");
            entity.Property(e => e.Idmenu).HasColumnName("idmenu");
            entity.Property(e => e.Idrol).HasColumnName("idrol");
            entity.Property(e => e.Ipcreacion).HasMaxLength(45).HasColumnName("ipcreacion");
            entity.Property(e => e.Usuariocreacion).HasMaxLength(50).HasColumnName("usuariocreacion");
            entity.HasOne(d => d.IdmenuNavigation).WithMany(p => p.TblAutenticacionMenuRols)
                .HasForeignKey(d => d.Idmenu).OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_autenticacion_menu_rol_menu");
            entity.HasOne(d => d.IdrolNavigation).WithMany(p => p.TblAutenticacionMenuRols)
                .HasForeignKey(d => d.Idrol).OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_autenticacion_menu_rol_rol");
        });

        modelBuilder.Entity<TblAutenticacionMenuUsuario>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_autenticacion_menu_usuario");
            entity.ToTable("tbl_autenticacion_menu_usuario", "autenticacion");
            entity.Property(e => e.Id).UseIdentityAlwaysColumn().HasColumnName("id");
            entity.Property(e => e.Activo).HasDefaultValue(true).HasColumnName("activo");
            entity.Property(e => e.Fechacreacion).HasDefaultValueSql("now()").HasColumnName("fechacreacion");
            entity.Property(e => e.Idmenu).HasColumnName("idmenu");
            entity.Property(e => e.Idusuario).HasColumnName("idusuario");
            entity.Property(e => e.Ipcreacion).HasMaxLength(45).HasColumnName("ipcreacion");
            entity.Property(e => e.Usuariocreacion).HasMaxLength(50).HasColumnName("usuariocreacion");
            entity.HasOne(d => d.IdmenuNavigation).WithMany(p => p.TblAutenticacionMenuUsuarios)
                .HasForeignKey(d => d.Idmenu).OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_autenticacion_menu_usuario_menu");
            entity.HasOne(d => d.IdusuarioNavigation).WithMany(p => p.TblAutenticacionMenuUsuarios)
                .HasForeignKey(d => d.Idusuario).OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_autenticacion_menu_usuario_usuario");
        });

        modelBuilder.Entity<TblAutenticacionModulo>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_autenticacion_modulo");
            entity.ToTable("tbl_autenticacion_modulo", "autenticacion");
            entity.Property(e => e.Id).UseIdentityAlwaysColumn().HasColumnName("id");
            entity.Property(e => e.Activo).HasDefaultValue(true).HasColumnName("activo");
            entity.Property(e => e.Fechacreacion).HasDefaultValueSql("now()").HasColumnName("fechacreacion");
            entity.Property(e => e.Fechamodificacion).HasColumnName("fechamodificacion");
            entity.Property(e => e.Icono).HasMaxLength(50).HasColumnName("icono");
            entity.Property(e => e.Idsubmodulo).HasColumnName("idsubmodulo");
            entity.Property(e => e.Ipcreacion).HasMaxLength(45).HasColumnName("ipcreacion");
            entity.Property(e => e.Ipmodificacion).HasMaxLength(45).HasColumnName("ipmodificacion");
            entity.Property(e => e.Nombremodulo).HasMaxLength(100).HasColumnName("nombremodulo");
            entity.Property(e => e.Ordenvisualizacion).HasColumnName("ordenvisualizacion");
            entity.Property(e => e.Rutamodulo).HasMaxLength(255).HasColumnName("rutamodulo");
            entity.Property(e => e.Usuariocreacion).HasMaxLength(50).HasColumnName("usuariocreacion");
            entity.Property(e => e.Usuariomodificacion).HasMaxLength(50).HasColumnName("usuariomodificacion");
        });

        modelBuilder.Entity<TblAutenticacionPasswordHistorial>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_autenticacion_password_historial");
            entity.ToTable("tbl_autenticacion_password_historial", "autenticacion");
            entity.Property(e => e.Id).UseIdentityAlwaysColumn().HasColumnName("id");
            entity.Property(e => e.Activo).HasDefaultValue(true).HasColumnName("activo");
            entity.Property(e => e.Fechacambio).HasDefaultValueSql("now()").HasColumnName("fechacambio");
            entity.Property(e => e.Fechacreacion).HasDefaultValueSql("now()").HasColumnName("fechacreacion");
            entity.Property(e => e.Hashpassword).HasMaxLength(255).HasColumnName("hashpassword");
            entity.Property(e => e.Idusuario).HasColumnName("idusuario");
            entity.Property(e => e.Ipcreacion).HasMaxLength(45).HasColumnName("ipcreacion");
            entity.Property(e => e.Usuariocreacion).HasMaxLength(50).HasColumnName("usuariocreacion");
            entity.HasOne(d => d.IdusuarioNavigation).WithMany(p => p.TblAutenticacionPasswordHistorials)
                .HasForeignKey(d => d.Idusuario).OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_autenticacion_password_historial_usuario");
        });

        modelBuilder.Entity<TblAutenticacionPreguntaUsuario>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_autenticacion_pregunta_usuario");
            entity.ToTable("tbl_autenticacion_pregunta_usuario", "autenticacion");
            entity.Property(e => e.Id).UseIdentityAlwaysColumn().HasColumnName("id");
            entity.Property(e => e.Activo).HasDefaultValue(true).HasColumnName("activo");
            entity.Property(e => e.Fechacreacion).HasDefaultValueSql("now()").HasColumnName("fechacreacion");
            entity.Property(e => e.Fechamodificacion).HasColumnName("fechamodificacion");
            entity.Property(e => e.Idusuario).HasColumnName("idusuario");
            entity.Property(e => e.Ipcreacion).HasMaxLength(45).HasColumnName("ipcreacion");
            entity.Property(e => e.Ipmodificacion).HasMaxLength(45).HasColumnName("ipmodificacion");
            entity.Property(e => e.Pregunta).HasMaxLength(255).HasColumnName("pregunta");
            entity.Property(e => e.Respuesta).HasMaxLength(255).HasColumnName("respuesta");
            entity.Property(e => e.Usuariocreacion).HasMaxLength(50).HasColumnName("usuariocreacion");
            entity.Property(e => e.Usuariomodificacion).HasMaxLength(50).HasColumnName("usuariomodificacion");
            entity.HasOne(d => d.IdusuarioNavigation).WithMany(p => p.TblAutenticacionPreguntaUsuarios)
                .HasForeignKey(d => d.Idusuario).OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_autenticacion_pregunta_usuario_usuario");
        });

        modelBuilder.Entity<TblAutenticacionPrivilegioRol>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_autenticacion_privilegio_rol");
            entity.ToTable("tbl_autenticacion_privilegio_rol", "autenticacion");
            entity.Property(e => e.Id).UseIdentityAlwaysColumn().HasColumnName("id");
            entity.Property(e => e.Activo).HasDefaultValue(true).HasColumnName("activo");
            entity.Property(e => e.Fechacreacion).HasDefaultValueSql("now()").HasColumnName("fechacreacion");
            entity.Property(e => e.Idprivilegio).HasColumnName("idprivilegio");
            entity.Property(e => e.Idrol).HasColumnName("idrol");
            entity.Property(e => e.Ipcreacion).HasMaxLength(45).HasColumnName("ipcreacion");
            entity.Property(e => e.Usuariocreacion).HasMaxLength(50).HasColumnName("usuariocreacion");
            entity.HasOne(d => d.IdprivilegioNavigation).WithMany(p => p.TblAutenticacionPrivilegioRolIdprivilegioNavigations)
                .HasForeignKey(d => d.Idprivilegio).OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_autenticacion_privilegio_rol_privilegio");
            entity.HasOne(d => d.IdrolNavigation).WithMany(p => p.TblAutenticacionPrivilegioRolIdrolNavigations)
                .HasForeignKey(d => d.Idrol).OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_autenticacion_privilegio_rol_rol");
        });

        modelBuilder.Entity<TblAutenticacionPrivilegioUsuario>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_autenticacion_privilegio_usuario");
            entity.ToTable("tbl_autenticacion_privilegio_usuario", "autenticacion");
            entity.Property(e => e.Id).UseIdentityAlwaysColumn().HasColumnName("id");
            entity.Property(e => e.Activo).HasDefaultValue(true).HasColumnName("activo");
            entity.Property(e => e.Fechacreacion).HasDefaultValueSql("now()").HasColumnName("fechacreacion");
            entity.Property(e => e.Idprivilegio).HasColumnName("idprivilegio");
            entity.Property(e => e.Idusuario).HasColumnName("idusuario");
            entity.Property(e => e.Ipcreacion).HasMaxLength(45).HasColumnName("ipcreacion");
            entity.Property(e => e.Usuariocreacion).HasMaxLength(50).HasColumnName("usuariocreacion");
            entity.HasOne(d => d.IdprivilegioNavigation).WithMany(p => p.TblAutenticacionPrivilegioUsuarios)
                .HasForeignKey(d => d.Idprivilegio).OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_autenticacion_privilegio_usuario_privilegio");
            entity.HasOne(d => d.IdusuarioNavigation).WithMany(p => p.TblAutenticacionPrivilegioUsuarios)
                .HasForeignKey(d => d.Idusuario).OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_autenticacion_privilegio_usuario_usuario");
        });

        modelBuilder.Entity<TblAutenticacionRolModulo>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_autenticacion_rol_modulo");
            entity.ToTable("tbl_autenticacion_rol_modulo", "autenticacion");
            entity.Property(e => e.Id).UseIdentityAlwaysColumn().HasColumnName("id");
            entity.Property(e => e.Activo).HasDefaultValue(true).HasColumnName("activo");
            entity.Property(e => e.Fechacreacion).HasDefaultValueSql("now()").HasColumnName("fechacreacion");
            entity.Property(e => e.Fechamodificacion).HasColumnName("fechamodificacion");
            entity.Property(e => e.Idmodulo).HasColumnName("idmodulo");
            entity.Property(e => e.Idrol).HasColumnName("idrol");
            entity.Property(e => e.Ipcreacion).HasMaxLength(45).HasColumnName("ipcreacion");
            entity.Property(e => e.Ipmodificacion).HasMaxLength(45).HasColumnName("ipmodificacion");
            entity.Property(e => e.Puedecrear).HasColumnName("puedecrear");
            entity.Property(e => e.Puedeeditar).HasColumnName("puedeeditar");
            entity.Property(e => e.Puedeeliminar).HasColumnName("puedeeliminar");
            entity.Property(e => e.Puedever).HasColumnName("puedever");
            entity.Property(e => e.Usuariocreacion).HasMaxLength(50).HasColumnName("usuariocreacion");
            entity.Property(e => e.Usuariomodificacion).HasMaxLength(50).HasColumnName("usuariomodificacion");
            entity.HasOne(d => d.IdmoduloNavigation).WithMany(p => p.TblAutenticacionRolModulos)
                .HasForeignKey(d => d.Idmodulo).OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_autenticacion_rol_modulo_modulo");
            entity.HasOne(d => d.IdrolNavigation).WithMany(p => p.TblAutenticacionRolModulos)
                .HasForeignKey(d => d.Idrol).OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_autenticacion_rol_modulo_rol");
        });

        modelBuilder.Entity<TblAutenticacionSesion>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_autenticacion_sesion");
            entity.ToTable("tbl_autenticacion_sesion", "autenticacion");
            entity.HasIndex(e => e.Estaactiva, "idx_aut_sesion_activa");
            entity.HasIndex(e => e.Idusuario, "idx_aut_sesion_usuario");
            entity.Property(e => e.Id).UseIdentityAlwaysColumn().HasColumnName("id");
            entity.Property(e => e.Activo).HasDefaultValue(true).HasColumnName("activo");
            entity.Property(e => e.Agenteusuario).HasMaxLength(512).HasColumnName("agenteusuario");
            entity.Property(e => e.Direccionip).HasMaxLength(45).HasColumnName("direccionip");
            entity.Property(e => e.Dispositivoinfo).HasMaxLength(255).HasColumnName("dispositivoinfo");
            entity.Property(e => e.Estaactiva).HasColumnName("estaactiva");
            entity.Property(e => e.Fechacreacion).HasDefaultValueSql("now()").HasColumnName("fechacreacion");
            entity.Property(e => e.Fechamodificacion).HasColumnName("fechamodificacion");
            entity.Property(e => e.Horaingreso).HasDefaultValueSql("now()").HasColumnName("horaingreso");
            entity.Property(e => e.Horasalida).HasColumnName("horasalida");
            entity.Property(e => e.Idusuario).HasColumnName("idusuario");
            entity.Property(e => e.Ipcreacion).HasMaxLength(45).HasColumnName("ipcreacion");
            entity.Property(e => e.Ipmodificacion).HasMaxLength(45).HasColumnName("ipmodificacion");
            entity.Property(e => e.Tokensesion).HasMaxLength(1000).HasColumnName("tokensesion");
            entity.Property(e => e.Ubicacioninfo).HasMaxLength(255).HasColumnName("ubicacioninfo");
            entity.Property(e => e.Usuariocreacion).HasMaxLength(50).HasColumnName("usuariocreacion");
            entity.Property(e => e.Usuariomodificacion).HasMaxLength(50).HasColumnName("usuariomodificacion");
            entity.HasOne(d => d.IdusuarioNavigation).WithMany(p => p.TblAutenticacionSesions)
                .HasForeignKey(d => d.Idusuario).OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_autenticacion_sesion_usuario");
        });

        modelBuilder.Entity<TblAutenticacionSesionApp>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_autenticacion_sesion_app");
            entity.ToTable("tbl_autenticacion_sesion_app", "autenticacion");
            entity.Property(e => e.Id).UseIdentityAlwaysColumn().HasColumnName("id");
            entity.Property(e => e.Activo).HasDefaultValue(true).HasColumnName("activo");
            entity.Property(e => e.Fechacreacion).HasDefaultValueSql("now()").HasColumnName("fechacreacion");
            entity.Property(e => e.Fechaexpiracion).HasColumnName("fechaexpiracion");
            entity.Property(e => e.Idaplicacion).HasColumnName("idaplicacion");
            entity.Property(e => e.Idusuario).HasColumnName("idusuario");
            entity.Property(e => e.Ipcreacion).HasMaxLength(45).HasColumnName("ipcreacion");
            entity.Property(e => e.Tokenapp).HasColumnName("tokenapp");
            entity.Property(e => e.Usuariocreacion).HasMaxLength(50).HasColumnName("usuariocreacion");
            entity.HasOne(d => d.IdaplicacionNavigation).WithMany(p => p.TblAutenticacionSesionApps)
                .HasForeignKey(d => d.Idaplicacion).OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_autenticacion_sesion_app_aplicacion");
            entity.HasOne(d => d.IdusuarioNavigation).WithMany(p => p.TblAutenticacionSesionApps)
                .HasForeignKey(d => d.Idusuario).OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_autenticacion_sesion_app_usuario");
        });

        modelBuilder.Entity<TblAutenticacionTokenBlacklist>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_autenticacion_token_blacklist");
            entity.ToTable("tbl_autenticacion_token_blacklist", "autenticacion");
            entity.HasIndex(e => e.Token, "idx_aut_token_blacklist_token");
            entity.Property(e => e.Id).UseIdentityAlwaysColumn().HasColumnName("id");
            entity.Property(e => e.Activo).HasDefaultValue(true).HasColumnName("activo");
            entity.Property(e => e.Fechacreacion).HasDefaultValueSql("now()").HasColumnName("fechacreacion");
            entity.Property(e => e.Fechaexpiracion).HasColumnName("fechaexpiracion");
            entity.Property(e => e.Token).HasColumnName("token");
            entity.Property(e => e.Usuariocreacion).HasMaxLength(50).HasColumnName("usuariocreacion");
        });

        modelBuilder.Entity<TblAutenticacionUsuario>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_autenticacion_usuario");
            entity.ToTable("tbl_autenticacion_usuario", "autenticacion");
            entity.HasIndex(e => e.Email, "uq_autenticacion_usuario_nombre").IsUnique();
            entity.Property(e => e.Id).UseIdentityAlwaysColumn().HasColumnName("id");
            entity.Property(e => e.Activo).HasDefaultValue(true).HasColumnName("activo");
            entity.Property(e => e.Debecambiarpassword).HasColumnName("debecambiarpassword");
            entity.Property(e => e.Email).HasMaxLength(100).HasColumnName("email");
            entity.Property(e => e.Estaactivo).HasColumnName("estaactivo");
            entity.Property(e => e.Fechacreacion).HasDefaultValueSql("now()").HasColumnName("fechacreacion");
            entity.Property(e => e.Fechamodificacion).HasColumnName("fechamodificacion");
            entity.Property(e => e.Hashpassword).HasMaxLength(255).HasColumnName("hashpassword");
            entity.Property(e => e.Idpersona).HasColumnName("idpersona");
            entity.Property(e => e.Ipcreacion).HasMaxLength(45).HasColumnName("ipcreacion");
            entity.Property(e => e.Ipmodificacion).HasMaxLength(45).HasColumnName("ipmodificacion");
            entity.Property(e => e.Ultimologin).HasColumnName("ultimologin");
            entity.Property(e => e.Usuariocreacion).HasMaxLength(50).HasColumnName("usuariocreacion");
            entity.Property(e => e.Usuariomodificacion).HasMaxLength(50).HasColumnName("usuariomodificacion");
            entity.HasOne(d => d.IdpersonaNavigation).WithMany(p => p.TblAutenticacionUsuarios)
                .HasForeignKey(d => d.Idpersona).HasConstraintName("fk_autenticacion_usuario_persona");
        });

        modelBuilder.Entity<TblAutenticacionUsuarioAplicacion>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_autenticacion_usuario_aplicacion");
            entity.ToTable("tbl_autenticacion_usuario_aplicacion", "autenticacion");
            entity.Property(e => e.Id).UseIdentityAlwaysColumn().HasColumnName("id");
            entity.Property(e => e.Activo).HasDefaultValue(true).HasColumnName("activo");
            entity.Property(e => e.Fechacreacion).HasDefaultValueSql("now()").HasColumnName("fechacreacion");
            entity.Property(e => e.Idaplicacion).HasColumnName("idaplicacion");
            entity.Property(e => e.Idusuario).HasColumnName("idusuario");
            entity.Property(e => e.Ipcreacion).HasMaxLength(45).HasColumnName("ipcreacion");
            entity.Property(e => e.Usuariocreacion).HasMaxLength(50).HasColumnName("usuariocreacion");
            entity.HasOne(d => d.IdaplicacionNavigation).WithMany(p => p.TblAutenticacionUsuarioAplicacions)
                .HasForeignKey(d => d.Idaplicacion).OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_autenticacion_usuario_aplicacion_aplicacion");
            entity.HasOne(d => d.IdusuarioNavigation).WithMany(p => p.TblAutenticacionUsuarioAplicacions)
                .HasForeignKey(d => d.Idusuario).OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_autenticacion_usuario_aplicacion_usuario");
        });

        modelBuilder.Entity<TblAutenticacionUsuarioModulo>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_autenticacion_usuario_modulo");
            entity.ToTable("tbl_autenticacion_usuario_modulo", "autenticacion");
            entity.Property(e => e.Id).UseIdentityAlwaysColumn().HasColumnName("id");
            entity.Property(e => e.Activo).HasDefaultValue(true).HasColumnName("activo");
            entity.Property(e => e.Fechacreacion).HasDefaultValueSql("now()").HasColumnName("fechacreacion");
            entity.Property(e => e.Fechamodificacion).HasColumnName("fechamodificacion");
            entity.Property(e => e.Idmodulo).HasColumnName("idmodulo");
            entity.Property(e => e.Idusuario).HasColumnName("idusuario");
            entity.Property(e => e.Ipcreacion).HasMaxLength(45).HasColumnName("ipcreacion");
            entity.Property(e => e.Ipmodificacion).HasMaxLength(45).HasColumnName("ipmodificacion");
            entity.Property(e => e.Puedecrear).HasColumnName("puedecrear");
            entity.Property(e => e.Puedeeditar).HasColumnName("puedeeditar");
            entity.Property(e => e.Puedeeliminar).HasColumnName("puedeeliminar");
            entity.Property(e => e.Puedever).HasColumnName("puedever");
            entity.Property(e => e.Usuariocreacion).HasMaxLength(50).HasColumnName("usuariocreacion");
            entity.Property(e => e.Usuariomodificacion).HasMaxLength(50).HasColumnName("usuariomodificacion");
            entity.HasOne(d => d.IdmoduloNavigation).WithMany(p => p.TblAutenticacionUsuarioModulos)
                .HasForeignKey(d => d.Idmodulo).OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_autenticacion_usuario_modulo_modulo");
            entity.HasOne(d => d.IdusuarioNavigation).WithMany(p => p.TblAutenticacionUsuarioModulos)
                .HasForeignKey(d => d.Idusuario).OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_autenticacion_usuario_modulo_usuario");
        });

        modelBuilder.Entity<TblAutenticacionUsuarioRol>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_autenticacion_usuario_rol");
            entity.ToTable("tbl_autenticacion_usuario_rol", "autenticacion");
            entity.Property(e => e.Id).UseIdentityAlwaysColumn().HasColumnName("id");
            entity.Property(e => e.Activo).HasDefaultValue(true).HasColumnName("activo");
            entity.Property(e => e.Fechacreacion).HasDefaultValueSql("now()").HasColumnName("fechacreacion");
            entity.Property(e => e.Idrol).HasColumnName("idrol");
            entity.Property(e => e.Idusuario).HasColumnName("idusuario");
            entity.Property(e => e.Ipcreacion).HasMaxLength(45).HasColumnName("ipcreacion");
            entity.Property(e => e.Usuariocreacion).HasMaxLength(50).HasColumnName("usuariocreacion");
            entity.HasOne(d => d.IdrolNavigation).WithMany(p => p.TblAutenticacionUsuarioRols)
                .HasForeignKey(d => d.Idrol).OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_autenticacion_usuario_rol_rol");
            entity.HasOne(d => d.IdusuarioNavigation).WithMany(p => p.TblAutenticacionUsuarioRols)
                .HasForeignKey(d => d.Idusuario).OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_autenticacion_usuario_rol_usuario");
        });

        modelBuilder.Entity<TblInventarioAsignacionEquipo>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_inventario_asignacion_equipo");
            entity.ToTable("tbl_inventario_asignacion_equipo", "inventario");
            entity.HasIndex(e => e.Fechadevolucion, "idx_inv_asignacion_devolucion");
            entity.HasIndex(e => e.Idempleado, "idx_inv_asignacion_empleado");
            entity.HasIndex(e => e.Idequipo, "idx_inv_asignacion_equipo");
            entity.Property(e => e.Id).UseIdentityAlwaysColumn().HasColumnName("id");
            entity.Property(e => e.Activo).HasDefaultValue(true).HasColumnName("activo");
            entity.Property(e => e.Fechaasignacion).HasDefaultValueSql("now()").HasColumnName("fechaasignacion");
            entity.Property(e => e.Fechacreacion).HasDefaultValueSql("now()").HasColumnName("fechacreacion");
            entity.Property(e => e.Fechadevolucion).HasColumnName("fechadevolucion");
            entity.Property(e => e.Fechamodificacion).HasColumnName("fechamodificacion");
            entity.Property(e => e.Idempleado).HasColumnName("idempleado");
            entity.Property(e => e.Idequipo).HasColumnName("idequipo");
            entity.Property(e => e.Ipcreacion).HasMaxLength(45).HasColumnName("ipcreacion");
            entity.Property(e => e.Ipmodificacion).HasMaxLength(45).HasColumnName("ipmodificacion");
            entity.Property(e => e.Observacion).HasMaxLength(500).HasColumnName("observacion");
            entity.Property(e => e.Usuariocreacion).HasMaxLength(50).HasColumnName("usuariocreacion");
            entity.Property(e => e.Usuariomodificacion).HasMaxLength(50).HasColumnName("usuariomodificacion");
            entity.HasOne(d => d.IdempleadoNavigation).WithMany(p => p.TblInventarioAsignacionEquipos)
                .HasForeignKey(d => d.Idempleado).OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_inventario_asignacion_equipo_empleado");
            entity.HasOne(d => d.IdequipoNavigation).WithMany(p => p.TblInventarioAsignacionEquipos)
                .HasForeignKey(d => d.Idequipo).OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_inventario_asignacion_equipo_equipo");
        });

        modelBuilder.Entity<TblInventarioBajaEquipo>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_inventario_baja_equipo");
            entity.ToTable("tbl_inventario_baja_equipo", "inventario");
            entity.HasIndex(e => e.Idequipo, "idx_inv_baja_equipo");
            entity.HasIndex(e => e.Idtipobaja, "idx_inv_baja_tipo");
            entity.Property(e => e.Id).UseIdentityAlwaysColumn().HasColumnName("id");
            entity.Property(e => e.Activo).HasDefaultValue(true).HasColumnName("activo");
            entity.Property(e => e.Fechabaja).HasColumnName("fechabaja");
            entity.Property(e => e.Fechacreacion).HasDefaultValueSql("now()").HasColumnName("fechacreacion");
            entity.Property(e => e.Fechamodificacion).HasColumnName("fechamodificacion");
            entity.Property(e => e.Idequipo).HasColumnName("idequipo");
            entity.Property(e => e.Idtipobaja).HasColumnName("idtipobaja");
            entity.Property(e => e.Ipcreacion).HasMaxLength(45).HasColumnName("ipcreacion");
            entity.Property(e => e.Ipmodificacion).HasMaxLength(45).HasColumnName("ipmodificacion");
            entity.Property(e => e.Motivobaja).HasMaxLength(500).HasColumnName("motivobaja");
            entity.Property(e => e.Usuariocreacion).HasMaxLength(50).HasColumnName("usuariocreacion");
            entity.Property(e => e.Usuariomodificacion).HasMaxLength(50).HasColumnName("usuariomodificacion");
            entity.HasOne(d => d.IdequipoNavigation).WithMany(p => p.TblInventarioBajaEquipos)
                .HasForeignKey(d => d.Idequipo).OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_inventario_baja_equipo_equipo");
            entity.HasOne(d => d.IdtipobajaNavigation).WithMany(p => p.TblInventarioBajaEquipos)
                .HasForeignKey(d => d.Idtipobaja).HasConstraintName("fk_inventario_baja_equipo_tipo_baja");
        });

        modelBuilder.Entity<TblInventarioCaracteristicaEquipo>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_inventario_caracteristica_equipo");
            entity.ToTable("tbl_inventario_caracteristica_equipo", "inventario");
            entity.Property(e => e.Id).UseIdentityAlwaysColumn().HasColumnName("id");
            entity.Property(e => e.Activo).HasDefaultValue(true).HasColumnName("activo");
            entity.Property(e => e.Fechacreacion).HasDefaultValueSql("now()").HasColumnName("fechacreacion");
            entity.Property(e => e.Fechamodificacion).HasColumnName("fechamodificacion");
            entity.Property(e => e.Idequipo).HasColumnName("idequipo");
            entity.Property(e => e.Idtipocomponente).HasColumnName("idtipocomponente");
            entity.Property(e => e.Ipcreacion).HasMaxLength(45).HasColumnName("ipcreacion");
            entity.Property(e => e.Ipmodificacion).HasMaxLength(45).HasColumnName("ipmodificacion");
            entity.Property(e => e.Nombrecaracteristica).HasMaxLength(100).HasColumnName("nombrecaracteristica");
            entity.Property(e => e.Usuariocreacion).HasMaxLength(50).HasColumnName("usuariocreacion");
            entity.Property(e => e.Usuariomodificacion).HasMaxLength(50).HasColumnName("usuariomodificacion");
            entity.Property(e => e.Valorcaracteristica).HasMaxLength(255).HasColumnName("valorcaracteristica");
            entity.HasOne(d => d.IdequipoNavigation).WithMany(p => p.TblInventarioCaracteristicaEquipos)
                .HasForeignKey(d => d.Idequipo).OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_inventario_caracteristica_equipo_equipo");
            entity.HasOne(d => d.IdtipocomponenteNavigation).WithMany(p => p.TblInventarioCaracteristicaEquipos)
                .HasForeignKey(d => d.Idtipocomponente).HasConstraintName("fk_inventario_caracteristica_equipo_tipo_componente");
        });

        modelBuilder.Entity<TblInventarioDetalleFactura>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_inventario_detalle_factura");
            entity.ToTable("tbl_inventario_detalle_factura", "inventario");
            entity.HasIndex(e => e.Idfactura, "idx_inv_detalle_factura");
            entity.Property(e => e.Id).UseIdentityAlwaysColumn().HasColumnName("id");
            entity.Property(e => e.Activo).HasDefaultValue(true).HasColumnName("activo");
            entity.Property(e => e.Cantidad).HasDefaultValue(1).HasColumnName("cantidad");
            entity.Property(e => e.Descripcion).HasMaxLength(255).HasColumnName("descripcion");
            entity.Property(e => e.Fechacreacion).HasDefaultValueSql("now()").HasColumnName("fechacreacion");
            entity.Property(e => e.Fechamodificacion).HasColumnName("fechamodificacion");
            entity.Property(e => e.Idfactura).HasColumnName("idfactura");
            entity.Property(e => e.Ipcreacion).HasMaxLength(45).HasColumnName("ipcreacion");
            entity.Property(e => e.Ipmodificacion).HasMaxLength(45).HasColumnName("ipmodificacion");
            entity.Property(e => e.Iva).HasPrecision(18, 2).HasColumnName("iva");
            entity.Property(e => e.Observacion).HasMaxLength(500).HasColumnName("observacion");
            entity.Property(e => e.Preciounitario).HasPrecision(18, 2).HasColumnName("preciounitario");
            entity.Property(e => e.Subtotal).HasPrecision(18, 2).HasColumnName("subtotal");
            entity.Property(e => e.Total).HasPrecision(18, 2).HasColumnName("total");
            entity.Property(e => e.Usuariocreacion).HasMaxLength(50).HasColumnName("usuariocreacion");
            entity.Property(e => e.Usuariomodificacion).HasMaxLength(50).HasColumnName("usuariomodificacion");
            entity.HasOne(d => d.IdfacturaNavigation).WithMany(p => p.TblInventarioDetalleFacturas)
                .HasForeignKey(d => d.Idfactura).OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_inventario_detalle_factura_factura");
        });

        modelBuilder.Entity<TblInventarioEmpresa>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_inventario_empresa");
            entity.ToTable("tbl_inventario_empresa", "inventario");
            entity.Property(e => e.Id).UseIdentityAlwaysColumn().HasColumnName("id");
            entity.Property(e => e.Activo).HasDefaultValue(true).HasColumnName("activo");
            entity.Property(e => e.Direccion).HasMaxLength(255).HasColumnName("direccion");
            entity.Property(e => e.Email).HasMaxLength(100).HasColumnName("email");
            entity.Property(e => e.Fechacreacion).HasDefaultValueSql("now()").HasColumnName("fechacreacion");
            entity.Property(e => e.Fechamodificacion).HasColumnName("fechamodificacion");
            entity.Property(e => e.Ipcreacion).HasMaxLength(45).HasColumnName("ipcreacion");
            entity.Property(e => e.Ipmodificacion).HasMaxLength(45).HasColumnName("ipmodificacion");
            entity.Property(e => e.Nombreempresa).HasMaxLength(100).HasColumnName("nombreempresa");
            entity.Property(e => e.Ruc).HasMaxLength(13).HasColumnName("ruc");
            entity.Property(e => e.Telefono).HasMaxLength(20).HasColumnName("telefono");
            entity.Property(e => e.Usuariocreacion).HasMaxLength(50).HasColumnName("usuariocreacion");
            entity.Property(e => e.Usuariomodificacion).HasMaxLength(50).HasColumnName("usuariomodificacion");
        });

        modelBuilder.Entity<TblInventarioEquipo>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_inventario_equipo");
            entity.ToTable("tbl_inventario_equipo", "inventario");
            entity.HasIndex(e => e.Activo, "idx_inv_equipo_activo");
            entity.HasIndex(e => e.Idcategoria, "idx_inv_equipo_categoria");
            entity.HasIndex(e => e.Idestado, "idx_inv_equipo_estado");
            entity.HasIndex(e => e.Numeroserie, "uq_inventario_equipo_serie").IsUnique();
            entity.Property(e => e.Id).UseIdentityAlwaysColumn().HasColumnName("id");
            entity.Property(e => e.Activo).HasDefaultValue(true).HasColumnName("activo");
            entity.Property(e => e.Datosadicionales).HasColumnType("jsonb").HasColumnName("datosadicionales");
            entity.Property(e => e.Descripcion).HasMaxLength(255).HasColumnName("descripcion");
            entity.Property(e => e.Fechaadquisicion).HasColumnName("fechaadquisicion");
            entity.Property(e => e.Fechacreacion).HasDefaultValueSql("now()").HasColumnName("fechacreacion");
            entity.Property(e => e.Fechamodificacion).HasColumnName("fechamodificacion");
            entity.Property(e => e.Fechavencimientogarantia).HasColumnName("fechavencimientogarantia");
            entity.Property(e => e.Idcategoria).HasColumnName("idcategoria");
            entity.Property(e => e.Idcondicion).HasColumnName("idcondicion");
            entity.Property(e => e.Idestado).HasColumnName("idestado");
            entity.Property(e => e.Idfactura).HasColumnName("idfactura");
            entity.Property(e => e.Idtipogarantia).HasColumnName("idtipogarantia");
            entity.Property(e => e.Ipcreacion).HasMaxLength(45).HasColumnName("ipcreacion");
            entity.Property(e => e.Ipmodificacion).HasMaxLength(45).HasColumnName("ipmodificacion");
            entity.Property(e => e.Marca).HasMaxLength(100).HasColumnName("marca");
            entity.Property(e => e.Modelo).HasMaxLength(100).HasColumnName("modelo");
            entity.Property(e => e.Numeroserie).HasMaxLength(100).HasColumnName("numeroserie");
            entity.Property(e => e.Usuariocreacion).HasMaxLength(50).HasColumnName("usuariocreacion");
            entity.Property(e => e.Usuariomodificacion).HasMaxLength(50).HasColumnName("usuariomodificacion");
            entity.Property(e => e.Valoradquisicion).HasPrecision(18, 2).HasColumnName("valoradquisicion");
            entity.HasOne(d => d.IdcategoriaNavigation).WithMany(p => p.TblInventarioEquipoIdcategoriaNavigations)
                .HasForeignKey(d => d.Idcategoria).OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_inventario_equipo_categoria");
            entity.HasOne(d => d.IdcondicionNavigation).WithMany(p => p.TblInventarioEquipoIdcondicionNavigations)
                .HasForeignKey(d => d.Idcondicion).HasConstraintName("fk_inventario_equipo_condicion");
            entity.HasOne(d => d.IdestadoNavigation).WithMany(p => p.TblInventarioEquipoIdestadoNavigations)
                .HasForeignKey(d => d.Idestado).OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_inventario_equipo_estado");
            entity.HasOne(d => d.IdfacturaNavigation).WithMany(p => p.TblInventarioEquipos)
                .HasForeignKey(d => d.Idfactura).HasConstraintName("fk_inventario_equipo_factura");
            entity.HasOne(d => d.IdtipogarantiaNavigation).WithMany(p => p.TblInventarioEquipoIdtipogarantiaNavigations)
                .HasForeignKey(d => d.Idtipogarantia).HasConstraintName("fk_inventario_equipo_tipo_garantia");
        });

        modelBuilder.Entity<TblInventarioFactura>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_inventario_factura");
            entity.ToTable("tbl_inventario_factura", "inventario");
            entity.HasIndex(e => e.Idproveedor, "idx_inv_factura_proveedor");
            entity.HasIndex(e => e.Numerofactura, "uq_inventario_factura_numero").IsUnique();
            entity.Property(e => e.Id).UseIdentityAlwaysColumn().HasColumnName("id");
            entity.Property(e => e.Activo).HasDefaultValue(true).HasColumnName("activo");
            entity.Property(e => e.Fechacreacion).HasDefaultValueSql("now()").HasColumnName("fechacreacion");
            entity.Property(e => e.Fechafactura).HasColumnName("fechafactura");
            entity.Property(e => e.Fechamodificacion).HasColumnName("fechamodificacion");
            entity.Property(e => e.Idproveedor).HasColumnName("idproveedor");
            entity.Property(e => e.Ipcreacion).HasMaxLength(45).HasColumnName("ipcreacion");
            entity.Property(e => e.Ipmodificacion).HasMaxLength(45).HasColumnName("ipmodificacion");
            entity.Property(e => e.Numerofactura).HasMaxLength(50).HasColumnName("numerofactura");
            entity.Property(e => e.Usuariocreacion).HasMaxLength(50).HasColumnName("usuariocreacion");
            entity.Property(e => e.Usuariomodificacion).HasMaxLength(50).HasColumnName("usuariomodificacion");
            entity.HasOne(d => d.IdproveedorNavigation).WithMany(p => p.TblInventarioFacturas)
                .HasForeignKey(d => d.Idproveedor).HasConstraintName("fk_inventario_factura_proveedor");
        });

        modelBuilder.Entity<TblInventarioProveedor>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_inventario_proveedor");
            entity.ToTable("tbl_inventario_proveedor", "inventario");
            entity.Property(e => e.Id).UseIdentityAlwaysColumn().HasColumnName("id");
            entity.Property(e => e.Activo).HasDefaultValue(true).HasColumnName("activo");
            entity.Property(e => e.Direccion).HasMaxLength(255).HasColumnName("direccion");
            entity.Property(e => e.Email).HasMaxLength(100).HasColumnName("email");
            entity.Property(e => e.Fechacreacion).HasDefaultValueSql("now()").HasColumnName("fechacreacion");
            entity.Property(e => e.Fechamodificacion).HasColumnName("fechamodificacion");
            entity.Property(e => e.Idtipoproveedor).HasColumnName("idtipoproveedor");
            entity.Property(e => e.Ipcreacion).HasMaxLength(45).HasColumnName("ipcreacion");
            entity.Property(e => e.Ipmodificacion).HasMaxLength(45).HasColumnName("ipmodificacion");
            entity.Property(e => e.Nombreproveedor).HasMaxLength(150).HasColumnName("nombreproveedor");
            entity.Property(e => e.Ruc).HasMaxLength(13).HasColumnName("ruc");
            entity.Property(e => e.Telefono).HasMaxLength(20).HasColumnName("telefono");
            entity.Property(e => e.Usuariocreacion).HasMaxLength(50).HasColumnName("usuariocreacion");
            entity.Property(e => e.Usuariomodificacion).HasMaxLength(50).HasColumnName("usuariomodificacion");
            entity.HasOne(d => d.IdtipoproveedorNavigation).WithMany(p => p.TblInventarioProveedors)
                .HasForeignKey(d => d.Idtipoproveedor).HasConstraintName("fk_inventario_proveedor_tipo_proveedor");
        });

        modelBuilder.Entity<TblInventarioReparacionEquipo>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_inventario_reparacion_equipo");
            entity.ToTable("tbl_inventario_reparacion_equipo", "inventario");
            entity.HasIndex(e => e.Idequipo, "idx_inv_reparacion_equipo");
            entity.Property(e => e.Id).UseIdentityAlwaysColumn().HasColumnName("id");
            entity.Property(e => e.Activo).HasDefaultValue(true).HasColumnName("activo");
            entity.Property(e => e.Costoreparacion).HasPrecision(18, 2).HasColumnName("costoreparacion");
            entity.Property(e => e.Descripcion).HasMaxLength(255).HasColumnName("descripcion");
            entity.Property(e => e.Fechacreacion).HasDefaultValueSql("now()").HasColumnName("fechacreacion");
            entity.Property(e => e.Fechafin).HasColumnName("fechafin");
            entity.Property(e => e.Fechainicio).HasColumnName("fechainicio");
            entity.Property(e => e.Fechamodificacion).HasColumnName("fechamodificacion");
            entity.Property(e => e.Idequipo).HasColumnName("idequipo");
            entity.Property(e => e.Ipcreacion).HasMaxLength(45).HasColumnName("ipcreacion");
            entity.Property(e => e.Ipmodificacion).HasMaxLength(45).HasColumnName("ipmodificacion");
            entity.Property(e => e.Usuariocreacion).HasMaxLength(50).HasColumnName("usuariocreacion");
            entity.Property(e => e.Usuariomodificacion).HasMaxLength(50).HasColumnName("usuariomodificacion");
            entity.HasOne(d => d.IdequipoNavigation).WithMany(p => p.TblInventarioReparacionEquipos)
                .HasForeignKey(d => d.Idequipo).OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_inventario_reparacion_equipo_equipo");
        });

        modelBuilder.Entity<TblInventarioStockCategorium>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_inventario_stock_categoria");
            entity.ToTable("tbl_inventario_stock_categoria", "inventario");
            entity.Property(e => e.Id).UseIdentityAlwaysColumn().HasColumnName("id");
            entity.Property(e => e.Activo).HasDefaultValue(true).HasColumnName("activo");
            entity.Property(e => e.Fechacreacion).HasDefaultValueSql("now()").HasColumnName("fechacreacion");
            entity.Property(e => e.Fechamodificacion).HasColumnName("fechamodificacion");
            entity.Property(e => e.Idcategoria).HasColumnName("idcategoria");
            entity.Property(e => e.Ipcreacion).HasMaxLength(45).HasColumnName("ipcreacion");
            entity.Property(e => e.Ipmodificacion).HasMaxLength(45).HasColumnName("ipmodificacion");
            entity.Property(e => e.Stockmaximo).HasColumnName("stockmaximo");
            entity.Property(e => e.Stockminimo).HasColumnName("stockminimo");
            entity.Property(e => e.Usuariocreacion).HasMaxLength(50).HasColumnName("usuariocreacion");
            entity.Property(e => e.Usuariomodificacion).HasMaxLength(50).HasColumnName("usuariomodificacion");
            entity.HasOne(d => d.IdcategoriaNavigation).WithMany(p => p.TblInventarioStockCategoria)
                .HasForeignKey(d => d.Idcategoria).OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_inventario_stock_categoria_categoria");
        });

        modelBuilder.Entity<TblTimeReportActividadDiarium>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_time_report_actividad_diaria");
            entity.ToTable("tbl_time_report_actividad_diaria", "time_report");
            entity.HasIndex(e => e.Idempleado, "idx_tr_actividad_empleado");
            entity.HasIndex(e => e.Fechaactividad, "idx_tr_actividad_fecha");
            entity.HasIndex(e => e.Idproyecto, "idx_tr_actividad_proyecto");
            entity.Property(e => e.Id).UseIdentityAlwaysColumn().HasColumnName("id");
            entity.Property(e => e.Activo).HasDefaultValue(true).HasColumnName("activo");
            entity.Property(e => e.Aprobadopor).HasColumnName("aprobadopor");
            entity.Property(e => e.Cantidadhoras).HasPrecision(5, 2).HasColumnName("cantidadhoras");
            entity.Property(e => e.Codigorequerimiento).HasMaxLength(50).HasColumnName("codigorequerimiento");
            entity.Property(e => e.Descripcionactividad).HasMaxLength(255).HasColumnName("descripcionactividad");
            entity.Property(e => e.Esbillable).HasColumnName("esbillable");
            entity.Property(e => e.Fechaactividad).HasColumnName("fechaactividad");
            entity.Property(e => e.Fechaaprobacion).HasColumnName("fechaaprobacion");
            entity.Property(e => e.Fechacreacion).HasDefaultValueSql("now()").HasColumnName("fechacreacion");
            entity.Property(e => e.Fechamodificacion).HasColumnName("fechamodificacion");
            entity.Property(e => e.Idempleado).HasColumnName("idempleado");
            entity.Property(e => e.Idproyecto).HasColumnName("idproyecto");
            entity.Property(e => e.Idtipoactividad).HasColumnName("idtipoactividad");
            entity.Property(e => e.Ipcreacion).HasMaxLength(45).HasColumnName("ipcreacion");
            entity.Property(e => e.Ipmodificacion).HasMaxLength(45).HasColumnName("ipmodificacion");
            entity.Property(e => e.Notas).HasColumnName("notas");
            entity.Property(e => e.Usuariocreacion).HasMaxLength(50).HasColumnName("usuariocreacion");
            entity.Property(e => e.Usuariomodificacion).HasMaxLength(50).HasColumnName("usuariomodificacion");
            entity.HasOne(d => d.AprobadoporNavigation).WithMany(p => p.TblTimeReportActividadDiariumAprobadoporNavigations)
                .HasForeignKey(d => d.Aprobadopor).HasConstraintName("fk_time_report_actividad_diaria_aprobador");
            entity.HasOne(d => d.IdempleadoNavigation).WithMany(p => p.TblTimeReportActividadDiariumIdempleadoNavigations)
                .HasForeignKey(d => d.Idempleado).OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_time_report_actividad_diaria_empleado");
            entity.HasOne(d => d.IdproyectoNavigation).WithMany(p => p.TblTimeReportActividadDiaria)
                .HasForeignKey(d => d.Idproyecto).HasConstraintName("fk_time_report_actividad_diaria_proyecto");
            entity.HasOne(d => d.IdtipoactividadNavigation).WithMany(p => p.TblTimeReportActividadDiaria)
                .HasForeignKey(d => d.Idtipoactividad).OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_time_report_actividad_diaria_tipo");
        });

        modelBuilder.Entity<TblTimeReportEmpleadoProyecto>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_time_report_empleado_proyecto");
            entity.ToTable("tbl_time_report_empleado_proyecto", "time_report");
            entity.HasIndex(e => e.Idempleado, "idx_tr_empleado_proyecto_emp");
            entity.Property(e => e.Id).UseIdentityAlwaysColumn().HasColumnName("id");
            entity.Property(e => e.Activo).HasDefaultValue(true).HasColumnName("activo");
            entity.Property(e => e.Costoporhora).HasPrecision(10, 2).HasColumnName("costoporhora");
            entity.Property(e => e.Fechaasignacion).HasColumnName("fechaasignacion");
            entity.Property(e => e.Fechacreacion).HasDefaultValueSql("now()").HasColumnName("fechacreacion");
            entity.Property(e => e.Fechafinasignacion).HasColumnName("fechafinasignacion");
            entity.Property(e => e.Fechamodificacion).HasColumnName("fechamodificacion");
            entity.Property(e => e.Horasasignadas).HasPrecision(10, 2).HasColumnName("horasasignadas");
            entity.Property(e => e.Idempleado).HasColumnName("idempleado");
            entity.Property(e => e.Idproveedor).HasColumnName("idproveedor");
            entity.Property(e => e.Idproyecto).HasColumnName("idproyecto");
            entity.Property(e => e.Ipcreacion).HasMaxLength(45).HasColumnName("ipcreacion");
            entity.Property(e => e.Ipmodificacion).HasMaxLength(45).HasColumnName("ipmodificacion");
            entity.Property(e => e.Rolasignado).HasMaxLength(100).HasColumnName("rolasignado");
            entity.Property(e => e.Usuariocreacion).HasMaxLength(50).HasColumnName("usuariocreacion");
            entity.Property(e => e.Usuariomodificacion).HasMaxLength(50).HasColumnName("usuariomodificacion");
            entity.HasOne(d => d.IdempleadoNavigation).WithMany(p => p.TblTimeReportEmpleadoProyectos)
                .HasForeignKey(d => d.Idempleado).HasConstraintName("fk_time_report_empleado_proyecto_empleado");
            entity.HasOne(d => d.IdproyectoNavigation).WithMany(p => p.TblTimeReportEmpleadoProyectos)
                .HasForeignKey(d => d.Idproyecto).OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_time_report_empleado_proyecto_proyecto");
        });

        modelBuilder.Entity<TblTimeReportFeriado>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_time_report_feriado");
            entity.ToTable("tbl_time_report_feriado", "time_report");
            entity.Property(e => e.Id).UseIdentityAlwaysColumn().HasColumnName("id");
            entity.Property(e => e.Activo).HasDefaultValue(true).HasColumnName("activo");
            entity.Property(e => e.Descripcion).HasMaxLength(255).HasColumnName("descripcion");
            entity.Property(e => e.Esrecurrente).HasColumnName("esrecurrente");
            entity.Property(e => e.Fechacreacion).HasDefaultValueSql("now()").HasColumnName("fechacreacion");
            entity.Property(e => e.Fechaferiado).HasColumnName("fechaferiado");
            entity.Property(e => e.Fechamodificacion).HasColumnName("fechamodificacion");
            entity.Property(e => e.Ipcreacion).HasMaxLength(45).HasColumnName("ipcreacion");
            entity.Property(e => e.Ipmodificacion).HasMaxLength(45).HasColumnName("ipmodificacion");
            entity.Property(e => e.Nombreferiado).HasMaxLength(150).HasColumnName("nombreferiado");
            entity.Property(e => e.Tipoferiado).HasMaxLength(50).HasColumnName("tipoferiado");
            entity.Property(e => e.Usuariocreacion).HasMaxLength(50).HasColumnName("usuariocreacion");
            entity.Property(e => e.Usuariomodificacion).HasMaxLength(50).HasColumnName("usuariomodificacion");
        });

        modelBuilder.Entity<TblTimeReportHomologacionBanco>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_time_report_homologacion_banco");
            entity.ToTable("tbl_time_report_homologacion_banco", "time_report");
            entity.HasIndex(e => new { e.Idempleado, e.Proyectotr }, "uq_time_report_homologacion_banco").IsUnique();
            entity.Property(e => e.Id).UseIdentityAlwaysColumn().HasColumnName("id");
            entity.Property(e => e.Cedulatr).HasMaxLength(20).HasColumnName("cedulatr");
            entity.Property(e => e.Clientetr).HasMaxLength(200).HasColumnName("clientetr");
            entity.Property(e => e.Fecharegistro).HasDefaultValueSql("now()").HasColumnName("fecharegistro");
            entity.Property(e => e.Idempleado).HasColumnName("idempleado");
            entity.Property(e => e.Nombrecompletobanco).HasMaxLength(200).HasColumnName("nombrecompletobanco");
            entity.Property(e => e.Nombrecompletotr).HasMaxLength(200).HasColumnName("nombrecompletotr");
            entity.Property(e => e.Observacion).HasMaxLength(100).HasColumnName("observacion");
            entity.Property(e => e.Proyectotr).HasMaxLength(200).HasColumnName("proyectotr");
            entity.HasOne(d => d.IdempleadoNavigation).WithMany(p => p.TblTimeReportHomologacionBancos)
                .HasForeignKey(d => d.Idempleado).OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_time_report_homologacion_banco_empleado");
        });

        modelBuilder.Entity<TblTimeReportOutboxCargo>(entity =>
        {
            entity.HasKey(e => e.Idoutbox).HasName("pk_time_report_outbox_cargo");
            entity.ToTable("tbl_time_report_outbox_cargo", "time_report");
            entity.HasIndex(e => new { e.Proximointento, e.Procesadoen }, "idx_tr_outbox_procesamiento")
                .HasFilter("(procesadoen IS NULL)");
            entity.Property(e => e.Idoutbox).HasDefaultValueSql("gen_random_uuid()").HasColumnName("idoutbox");
            entity.Property(e => e.Creadoen).HasDefaultValueSql("now()").HasColumnName("creadoen");
            entity.Property(e => e.Idagregado).HasColumnName("idagregado");
            entity.Property(e => e.Intentos).HasColumnName("intentos");
            entity.Property(e => e.Mensajeerror).HasMaxLength(2000).HasColumnName("mensajeerror");
            entity.Property(e => e.Operacion).HasMaxLength(1).HasColumnName("operacion");
            entity.Property(e => e.Payloadjson).HasColumnType("jsonb").HasColumnName("payloadjson");
            entity.Property(e => e.Procesadoen).HasColumnName("procesadoen");
            entity.Property(e => e.Proximointento).HasDefaultValueSql("now()").HasColumnName("proximointento");
        });

        modelBuilder.Entity<TblTimeReportPermiso>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_time_report_permiso");
            entity.ToTable("tbl_time_report_permiso", "time_report");
            entity.HasIndex(e => e.Idempleado, "idx_tr_permiso_empleado");
            entity.HasIndex(e => e.Idestadoaprobacion, "idx_tr_permiso_estado");
            entity.Property(e => e.Id).UseIdentityAlwaysColumn().HasColumnName("id");
            entity.Property(e => e.Activo).HasDefaultValue(true).HasColumnName("activo");
            entity.Property(e => e.Aprobadopor).HasColumnName("aprobadopor");
            entity.Property(e => e.Descripcion).HasColumnName("descripcion");
            entity.Property(e => e.Espagado).HasColumnName("espagado");
            entity.Property(e => e.Fechaaprobacion).HasColumnName("fechaaprobacion");
            entity.Property(e => e.Fechacreacion).HasDefaultValueSql("now()").HasColumnName("fechacreacion");
            entity.Property(e => e.Fechafin).HasColumnName("fechafin");
            entity.Property(e => e.Fechainicio).HasColumnName("fechainicio");
            entity.Property(e => e.Fechamodificacion).HasColumnName("fechamodificacion");
            entity.Property(e => e.Idempleado).HasColumnName("idempleado");
            entity.Property(e => e.Idestadoaprobacion).HasColumnName("idestadoaprobacion");
            entity.Property(e => e.Idtipopermiso).HasColumnName("idtipopermiso");
            entity.Property(e => e.Ipcreacion).HasMaxLength(45).HasColumnName("ipcreacion");
            entity.Property(e => e.Ipmodificacion).HasMaxLength(45).HasColumnName("ipmodificacion");
            entity.Property(e => e.Observacion).HasColumnName("observacion");
            entity.Property(e => e.Totaldias).HasPrecision(6, 2).HasColumnName("totaldias");
            entity.Property(e => e.Totalhoras).HasPrecision(6, 2).HasColumnName("totalhoras");
            entity.Property(e => e.Usuariocreacion).HasMaxLength(50).HasColumnName("usuariocreacion");
            entity.Property(e => e.Usuariomodificacion).HasMaxLength(50).HasColumnName("usuariomodificacion");
            entity.HasOne(d => d.AprobadoporNavigation).WithMany(p => p.TblTimeReportPermisoAprobadoporNavigations)
                .HasForeignKey(d => d.Aprobadopor).HasConstraintName("fk_time_report_permiso_aprobador");
            entity.HasOne(d => d.IdempleadoNavigation).WithMany(p => p.TblTimeReportPermisoIdempleadoNavigations)
                .HasForeignKey(d => d.Idempleado).OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_time_report_permiso_empleado");
            entity.HasOne(d => d.IdestadoaprobacionNavigation).WithMany(p => p.TblTimeReportPermisoIdestadoaprobacionNavigations)
                .HasForeignKey(d => d.Idestadoaprobacion).OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_time_report_permiso_estado_aprobacion");
            entity.HasOne(d => d.IdtipopermisoNavigation).WithMany(p => p.TblTimeReportPermisoIdtipopermisoNavigations)
                .HasForeignKey(d => d.Idtipopermiso).OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_time_report_permiso_tipo");
        });

        modelBuilder.Entity<TblTimeReportProyeccionHora>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_time_report_proyeccion_horas");
            entity.ToTable("tbl_time_report_proyeccion_horas", "time_report");
            entity.HasIndex(e => e.Grupoproyeccion, "idx_tr_proyeccion_grupo");
            entity.Property(e => e.Id).UseIdentityAlwaysColumn().HasColumnName("id");
            entity.Property(e => e.Activo).HasDefaultValue(true).HasColumnName("activo");
            entity.Property(e => e.Cantidadperiodo).HasColumnName("cantidadperiodo");
            entity.Property(e => e.Cantidadrecurso).HasColumnName("cantidadrecurso");
            entity.Property(e => e.Costoporhora).HasPrecision(18, 2).HasColumnName("costoporhora");
            entity.Property(e => e.Costorecurso).HasPrecision(18, 2).HasColumnName("costorecurso");
            entity.Property(e => e.Distribuciontiempo).HasColumnName("distribuciontiempo");
            entity.Property(e => e.Fechacreacion).HasDefaultValueSql("now()").HasColumnName("fechacreacion");
            entity.Property(e => e.Fechamodificacion).HasColumnName("fechamodificacion");
            entity.Property(e => e.Grupoproyeccion).HasColumnName("grupoproyeccion");
            entity.Property(e => e.Idtiporecurso).HasColumnName("idtiporecurso");
            entity.Property(e => e.Ipcreacion).HasMaxLength(45).HasColumnName("ipcreacion");
            entity.Property(e => e.Ipmodificacion).HasMaxLength(45).HasColumnName("ipmodificacion");
            entity.Property(e => e.Nombreproyeccion).HasMaxLength(255).HasColumnName("nombreproyeccion");
            entity.Property(e => e.Nombrerecurso).HasMaxLength(255).HasColumnName("nombrerecurso");
            entity.Property(e => e.Porcentajeparticipacion).HasPrecision(5, 2).HasColumnName("porcentajeparticipacion");
            entity.Property(e => e.Tiempototal).HasPrecision(18, 2).HasColumnName("tiempototal");
            entity.Property(e => e.Tipoperiodo).HasColumnName("tipoperiodo");
            entity.Property(e => e.Usuariocreacion).HasMaxLength(50).HasColumnName("usuariocreacion");
            entity.Property(e => e.Usuariomodificacion).HasMaxLength(50).HasColumnName("usuariomodificacion");
            entity.HasOne(d => d.IdtiporecursoNavigation).WithMany(p => p.TblTimeReportProyeccionHoras)
                .HasForeignKey(d => d.Idtiporecurso).OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_time_report_proyeccion_horas_tipo_recurso");
        });

        modelBuilder.Entity<TblTimeReportProyeccionHorasProyecto>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_time_report_proyeccion_horas_proyecto");
            entity.ToTable("tbl_time_report_proyeccion_horas_proyecto", "time_report");
            entity.Property(e => e.Id).UseIdentityAlwaysColumn().HasColumnName("id");
            entity.Property(e => e.Activo).HasDefaultValue(true).HasColumnName("activo");
            entity.Property(e => e.Cantidadperiodo).HasColumnName("cantidadperiodo");
            entity.Property(e => e.Cantidadrecurso).HasColumnName("cantidadrecurso");
            entity.Property(e => e.Costoporhora).HasPrecision(18, 2).HasColumnName("costoporhora");
            entity.Property(e => e.Costorecurso).HasPrecision(18, 2).HasColumnName("costorecurso");
            entity.Property(e => e.Distribuciontiempo).HasColumnName("distribuciontiempo");
            entity.Property(e => e.Fechacreacion).HasDefaultValueSql("now()").HasColumnName("fechacreacion");
            entity.Property(e => e.Fechamodificacion).HasColumnName("fechamodificacion");
            entity.Property(e => e.Idproyecto).HasColumnName("idproyecto");
            entity.Property(e => e.Idtiporecurso).HasColumnName("idtiporecurso");
            entity.Property(e => e.Ipcreacion).HasMaxLength(45).HasColumnName("ipcreacion");
            entity.Property(e => e.Ipmodificacion).HasMaxLength(45).HasColumnName("ipmodificacion");
            entity.Property(e => e.Nombrerecurso).HasMaxLength(255).HasColumnName("nombrerecurso");
            entity.Property(e => e.Porcentajeparticipacion).HasPrecision(5, 2).HasColumnName("porcentajeparticipacion");
            entity.Property(e => e.Tiempototal).HasPrecision(18, 2).HasColumnName("tiempototal");
            entity.Property(e => e.Tipoperiodo).HasColumnName("tipoperiodo");
            entity.Property(e => e.Usuariocreacion).HasMaxLength(50).HasColumnName("usuariocreacion");
            entity.Property(e => e.Usuariomodificacion).HasMaxLength(50).HasColumnName("usuariomodificacion");
            entity.HasOne(d => d.IdproyectoNavigation).WithMany(p => p.TblTimeReportProyeccionHorasProyectos)
                .HasForeignKey(d => d.Idproyecto).OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_time_report_proyeccion_horas_proyecto_proyecto");
            entity.HasOne(d => d.IdtiporecursoNavigation).WithMany(p => p.TblTimeReportProyeccionHorasProyectos)
                .HasForeignKey(d => d.Idtiporecurso).OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_time_report_proyeccion_horas_proyecto_tipo_recurso");
        });

        modelBuilder.Entity<TblTimeReportProyecto>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_time_report_proyecto");
            entity.ToTable("tbl_time_report_proyecto", "time_report");
            entity.HasIndex(e => e.Idcliente, "idx_tr_proyecto_cliente");
            entity.HasIndex(e => e.Idlider, "idx_tr_proyecto_lider");
            entity.HasIndex(e => e.Idtipoproyecto, "idx_tr_proyecto_tipo");
            entity.Property(e => e.Id).UseIdentityAlwaysColumn().HasColumnName("id");
            entity.Property(e => e.Activo).HasDefaultValue(true).HasColumnName("activo");
            entity.Property(e => e.Codigo).HasMaxLength(50).HasColumnName("codigo");
            entity.Property(e => e.Descripcion).HasMaxLength(255).HasColumnName("descripcion");
            entity.Property(e => e.Fechacreacion).HasDefaultValueSql("now()").HasColumnName("fechacreacion");
            entity.Property(e => e.Fechafinespera).HasColumnName("fechafinespera");
            entity.Property(e => e.Fechafinplaneada).HasColumnName("fechafinplaneada");
            entity.Property(e => e.Fechafinreal).HasColumnName("fechafinreal");
            entity.Property(e => e.Fechainicioespera).HasColumnName("fechainicioespera");
            entity.Property(e => e.Fechainicioplaneada).HasColumnName("fechainicioplaneada");
            entity.Property(e => e.Fechainicioreal).HasColumnName("fechainicioreal");
            entity.Property(e => e.Fechamodificacion).HasColumnName("fechamodificacion");
            entity.Property(e => e.Horasasignadas).HasPrecision(10, 2).HasColumnName("horasasignadas");
            entity.Property(e => e.Idcliente).HasColumnName("idcliente");
            entity.Property(e => e.Idestadoproyecto).HasColumnName("idestadoproyecto");
            entity.Property(e => e.Idlider).HasColumnName("idlider");
            entity.Property(e => e.Idtipoproyecto).HasColumnName("idtipoproyecto");
            entity.Property(e => e.Ipcreacion).HasMaxLength(45).HasColumnName("ipcreacion");
            entity.Property(e => e.Ipmodificacion).HasMaxLength(45).HasColumnName("ipmodificacion");
            entity.Property(e => e.Nombre).HasMaxLength(150).HasColumnName("nombre");
            entity.Property(e => e.Observacion).HasMaxLength(255).HasColumnName("observacion");
            entity.Property(e => e.Presupuesto).HasPrecision(15, 2).HasColumnName("presupuesto");
            entity.Property(e => e.Usuariocreacion).HasMaxLength(50).HasColumnName("usuariocreacion");
            entity.Property(e => e.Usuariomodificacion).HasMaxLength(50).HasColumnName("usuariomodificacion");
            entity.HasOne(d => d.IdclienteNavigation).WithMany(p => p.TblTimeReportProyectos)
                .HasForeignKey(d => d.Idcliente).HasConstraintName("fk_time_report_proyecto_cliente");
            entity.HasOne(d => d.IdestadoproyectoNavigation).WithMany(p => p.TblTimeReportProyectos)
                .HasForeignKey(d => d.Idestadoproyecto).OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_time_report_proyecto_estado");
            entity.HasOne(d => d.IdliderNavigation).WithMany(p => p.TblTimeReportProyectos)
                .HasForeignKey(d => d.Idlider).HasConstraintName("fk_time_report_proyecto_lider");
            entity.HasOne(d => d.IdtipoproyectoNavigation).WithMany(p => p.TblTimeReportProyectos)
                .HasForeignKey(d => d.Idtipoproyecto).HasConstraintName("fk_time_report_proyecto_tipo");
        });

        modelBuilder.Entity<TblTimeReportTipoActividad>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_time_report_tipo_actividad");
            entity.ToTable("tbl_time_report_tipo_actividad", "time_report");
            entity.Property(e => e.Id).UseIdentityAlwaysColumn().HasColumnName("id");
            entity.Property(e => e.Activo).HasDefaultValue(true).HasColumnName("activo");
            entity.Property(e => e.Codigocolor).HasMaxLength(7).HasColumnName("codigocolor");
            entity.Property(e => e.Descripcion).HasMaxLength(255).HasColumnName("descripcion");
            entity.Property(e => e.Fechacreacion).HasDefaultValueSql("now()").HasColumnName("fechacreacion");
            entity.Property(e => e.Fechamodificacion).HasColumnName("fechamodificacion");
            entity.Property(e => e.Ipcreacion).HasMaxLength(45).HasColumnName("ipcreacion");
            entity.Property(e => e.Ipmodificacion).HasMaxLength(45).HasColumnName("ipmodificacion");
            entity.Property(e => e.Nombretipo).HasMaxLength(100).HasColumnName("nombretipo");
            entity.Property(e => e.Usuariocreacion).HasMaxLength(50).HasColumnName("usuariocreacion");
            entity.Property(e => e.Usuariomodificacion).HasMaxLength(50).HasColumnName("usuariomodificacion");
        });

        modelBuilder.Entity<TblTimeReportTipoProyecto>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_time_report_tipo_proyecto");
            entity.ToTable("tbl_time_report_tipo_proyecto", "time_report");
            entity.Property(e => e.Id).UseIdentityAlwaysColumn().HasColumnName("id");
            entity.Property(e => e.Activo).HasDefaultValue(true).HasColumnName("activo");
            entity.Property(e => e.Essubtipo).HasColumnName("essubtipo");
            entity.Property(e => e.Fechacreacion).HasDefaultValueSql("now()").HasColumnName("fechacreacion");
            entity.Property(e => e.Fechamodificacion).HasColumnName("fechamodificacion");
            entity.Property(e => e.Ipcreacion).HasMaxLength(45).HasColumnName("ipcreacion");
            entity.Property(e => e.Ipmodificacion).HasMaxLength(45).HasColumnName("ipmodificacion");
            entity.Property(e => e.Nombretipo).HasMaxLength(50).HasColumnName("nombretipo");
            entity.Property(e => e.Usuariocreacion).HasMaxLength(50).HasColumnName("usuariocreacion");
            entity.Property(e => e.Usuariomodificacion).HasMaxLength(50).HasColumnName("usuariomodificacion");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
