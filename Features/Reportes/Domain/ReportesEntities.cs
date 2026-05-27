using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace tmr_backend.Features.Reportes.Domain
{
    [Table("tbl_administracion_persona", Schema = "administracion")]
    public class AdministracionPersona
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }
        [Column("idgenero")]
        public int? IdGenero { get; set; }
        [Column("idnacionalidad")]
        public int? IdNacionalidad { get; set; }
        [Column("idtipoidentificacion")]
        public int? IdTipoIdentificacion { get; set; }
        [Required]
        [Column("numeroidentificacion")]
        public string NumeroIdentificacion { get; set; } = null!;
        [Required]
        [Column("tipopersona")]
        public string TipoPersona { get; set; } = null!;
        [Required]
        [Column("primernombre")]
        public string PrimerNombre { get; set; } = null!;
        [Required]
        [Column("apellidopaterno")]
        public string ApellidoPaterno { get; set; } = null!;
        [Column("fechanacimiento")]
        public DateTime? FechaNacimiento { get; set; }
        [Column("email")]
        public string? Email { get; set; }
        [Column("telefono")]
        public string? Telefono { get; set; }
        [Column("direccion")]
        public string? Direccion { get; set; }
        [Required]
        [Column("activo")]
        public bool Activo { get; set; }
    }

    [Table("tbl_administracion_cargo", Schema = "administracion")]
    public class AdministracionCargo
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }
        [Required]
        [Column("nombrecargo")]
        public string NombreCargo { get; set; } = null!;
    }

    [Table("tbl_administracion_lider", Schema = "administracion")]
    public class AdministracionLider
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }
        [Required]
        [Column("idpersona")]
        public int IdPersona { get; set; }
        [ForeignKey("IdPersona")]
        public AdministracionPersona Persona { get; set; } = null!;
    }

    [Table("tbl_administracion_cliente", Schema = "administracion")]
    public class AdministracionCliente
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }
        [Column("nombrecomercial")]
        public string? NombreComercial { get; set; }
        [Column("razonsocial")]
        public string? RazonSocial { get; set; }
    }

    [Table("tbl_administracion_empleado", Schema = "administracion")]
    public class AdministracionEmpleado
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }
        [Required]
        [Column("idpersona")]
        public int IdPersona { get; set; }
        [ForeignKey("IdPersona")]
        public AdministracionPersona Persona { get; set; } = null!;
        [Column("idcargo")]
        public int? IdCargo { get; set; }
        [ForeignKey("IdCargo")]
        public AdministracionCargo? Cargo { get; set; }
        [Column("iddepartamento")]
        public int? IdDepartamento { get; set; }
        [Column("idmodotrabajo")]
        public int? IdModoTrabajo { get; set; }
        [Column("idcategoriaempleado")]
        public int? IdCategoriaEmpleado { get; set; }
        [Column("idempresacatalogo")]
        public int? IdEmpresaCatalogo { get; set; }
        [Required]
        [Column("codigoempleado")]
        public string CodigoEmpleado { get; set; } = null!;
        [Column("fechaingreso")]
        public DateTime? FechaIngreso { get; set; }
        [Column("fechaterminacion")]
        public DateTime? FechaTerminacion { get; set; }
        [Column("tipocontrato")]
        public bool? TipoContrato { get; set; }
        [Column("emailcorporativo")]
        public string? EmailCorporativo { get; set; }
        [Column("salario")]
        public decimal? Salario { get; set; }
        [Required]
        [Column("activo")]
        public bool Activo { get; set; }
    }

    [Table("tbl_time_report_proyecto", Schema = "time_report")]
    public class TimeReportProyecto
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }
        [Column("idcliente")]
        public int? IdCliente { get; set; }
        [ForeignKey("IdCliente")]
        public AdministracionCliente? Cliente { get; set; }
        [Required]
        [Column("idestadoproyecto")]
        public int IdEstadoProyecto { get; set; }
        [Column("idtipoproyecto")]
        public int? IdTipoProyecto { get; set; }
        [Column("idlider")]
        public int? IdLider { get; set; }
        [ForeignKey("IdLider")]
        public AdministracionLider? Lider { get; set; }
        [Column("codigo")]
        public string? Codigo { get; set; }
        [Required]
        [Column("nombre")]
        public string Nombre { get; set; } = null!;
        [Column("descripcion")]
        public string? Descripcion { get; set; }
        [Column("fechainicioplaneada")]
        public DateTime? FechaInicioPlaneada { get; set; }
        [Column("fechafinplaneada")]
        public DateTime? FechaFinPlaneada { get; set; }
        [Column("fechainicioreal")]
        public DateTime? FechaInicioReal { get; set; }
        [Column("fechafinreal")]
        public DateTime? FechaFinReal { get; set; }
        [Column("fechainicioespera")]
        public DateTime? FechaInicioEspera { get; set; }
        [Column("fechafinespera")]
        public DateTime? FechaFinEspera { get; set; }
        [Column("observacion")]
        public string? Observacion { get; set; }
        [Column("presupuesto")]
        public decimal? Presupuesto { get; set; }
        [Column("horasasignadas")]
        public decimal? HorasAsignadas { get; set; }
        [Required]
        [Column("activo")]
        public bool Activo { get; set; }
    }

    [Table("tbl_time_report_tipo_actividad", Schema = "time_report")]
    public class TimeReportTipoActividad
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }
        [Required]
        [Column("nombretipo")]
        public string NombreTipo { get; set; } = null!;
        [Column("descripcion")]
        public string? Descripcion { get; set; }
        [Column("codigocolor")]
        public string? CodigoColor { get; set; }
        [Required]
        [Column("activo")]
        public bool Activo { get; set; }
    }

    [Table("tbl_time_report_actividad_diaria", Schema = "time_report")]
    public class TimeReportActividadDiaria
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }
        [Required]
        [Column("idempleado")]
        public int IdEmpleado { get; set; }
        [ForeignKey("IdEmpleado")]
        public AdministracionEmpleado Empleado { get; set; } = null!;
        [Column("idproyecto")]
        public int? IdProyecto { get; set; }
        [ForeignKey("IdProyecto")]
        public TimeReportProyecto? Proyecto { get; set; }
        [Required]
        [Column("idtipoactividad")]
        public int IdTipoActividad { get; set; }
        [ForeignKey("IdTipoActividad")]
        public TimeReportTipoActividad TipoActividad { get; set; } = null!;
        [Column("codigorequerimiento")]
        public string? CodigoRequerimiento { get; set; }
        [Required]
        [Column("cantidadhoras")]
        public decimal CantidadHoras { get; set; }
        [Required]
        [Column("fechaactividad")]
        public DateTime FechaActividad { get; set; }
        [Required]
        [Column("descripcionactividad")]
        public string DescripcionActividad { get; set; } = null!;
        [Column("notas")]
        public string? Notas { get; set; }
        [Column("esbillable")]
        public bool? EsBillable { get; set; }
        [Column("aprobadopor")]
        public int? AprobadoPor { get; set; }
        [Column("fechaaprobacion")]
        public DateTimeOffset? FechaAprobacion { get; set; }
        [Required]
        [Column("activo")]
        public bool Activo { get; set; }
    }

    [Table("tbl_time_report_empleado_proyecto", Schema = "time_report")]
    public class TimeReportEmpleadoProyecto
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }
        [Column("idempleado")]
        public int? IdEmpleado { get; set; }
        [ForeignKey("IdEmpleado")]
        public AdministracionEmpleado? Empleado { get; set; }
        [Required]
        [Column("idproyecto")]
        public int IdProyecto { get; set; }
        [ForeignKey("IdProyecto")]
        public TimeReportProyecto Proyecto { get; set; } = null!;
        [Column("idproveedor")]
        public int? IdProveedor { get; set; }
        [Column("fechaasignacion")]
        public DateTime? FechaAsignacion { get; set; }
        [Column("fechafinasignacion")]
        public DateTime? FechaFinAsignacion { get; set; }
        [Column("rolasignado")]
        public string? RolAsignado { get; set; }
        [Column("costoporhora")]
        public decimal? CostoPorHora { get; set; }
        [Column("horasasignadas")]
        public decimal? HorasAsignadas { get; set; }
        [Required]
        [Column("activo")]
        public bool Activo { get; set; }
    }

    [Table("tbl_time_report_permiso", Schema = "time_report")]
    public class TimeReportPermiso
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }
        [Required]
        [Column("idempleado")]
        public int IdEmpleado { get; set; }
        [ForeignKey("IdEmpleado")]
        public AdministracionEmpleado Empleado { get; set; } = null!;
        [Required]
        [Column("idtipopermiso")]
        public int IdTipoPermiso { get; set; }
        [Required]
        [Column("idestadoaprobacion")]
        public int IdEstadoAprobacion { get; set; }
        [Required]
        [Column("fechainicio")]
        public DateTime FechaInicio { get; set; }
        [Required]
        [Column("fechafin")]
        public DateTime FechaFin { get; set; }
        [Required]
        [Column("totaldias")]
        public decimal TotalDias { get; set; }
        [Column("totalhoras")]
        public decimal? TotalHoras { get; set; }
        [Column("espagado")]
        public bool? EsPagado { get; set; }
        [Column("descripcion")]
        public string? Descripcion { get; set; }
        [Column("aprobadopor")]
        public int? AprobadoPor { get; set; }
        [Column("fechaaprobacion")]
        public DateTimeOffset? FechaAprobacion { get; set; }
        [Column("observacion")]
        public string? Observacion { get; set; }
        [Required]
        [Column("activo")]
        public bool Activo { get; set; }
    }

    [Table("tbl_time_report_feriado", Schema = "time_report")]
    public class TimeReportFeriado
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }
        [Required]
        [Column("nombreferiado")]
        public string NombreFeriado { get; set; } = null!;
        [Required]
        [Column("fechaferiado")]
        public DateTime FechaFeriado { get; set; }
        [Column("esrecurrente")]
        public bool? EsRecurrente { get; set; }
        [Column("tipoferiado")]
        public string? TipoFeriado { get; set; }
        [Column("descripcion")]
        public string? Descripcion { get; set; }
        [Required]
        [Column("activo")]
        public bool Activo { get; set; }
    }
}
