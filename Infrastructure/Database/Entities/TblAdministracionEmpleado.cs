using System;
using System.Collections.Generic;

namespace tmr_backend.Infrastructure.Database.Entities;

public partial class TblAdministracionEmpleado
{
    public int Id { get; set; }

    public int Idpersona { get; set; }

    public int? Idcargo { get; set; }

    public int? Idmodotrabajo { get; set; }

    public int? Idcategoriaempleado { get; set; }

    public int? Idempresacatalogo { get; set; }

    public string Codigoempleado { get; set; } = null!;

    public DateOnly? Fechaingreso { get; set; }

    public DateOnly? Fechaterminacion { get; set; }

    public int? Idtipocontrato { get; set; }

    public string? Emailcorporativo { get; set; }

    public decimal? Salario { get; set; }

    // Años de experiencia del empleado (columna nueva agregada por script).
    public int? Aniosexperiencia { get; set; }

    public bool Activo { get; set; }

    public string Usuariocreacion { get; set; } = null!;

    public DateTime Fechacreacion { get; set; }

    public string? Usuariomodificacion { get; set; }

    public DateTime? Fechamodificacion { get; set; }

    public string Ipcreacion { get; set; } = null!;

    public string? Ipmodificacion { get; set; }

    public int? IdTipoSalida { get; set; }

    public int? IdCausaSalida { get; set; }

    public string? ComentarioSalida { get; set; }

    public int? IdEmpleadoReemplazo { get; set; }

    public virtual TblAdministracionCargo? IdcargoNavigation { get; set; }

    public virtual TblAdministracionCatalogoDetalle? IdcategoriaempleadoNavigation { get; set; }

    public virtual TblAdministracionCatalogoDetalle? IdempresacatalogoNavigation { get; set; }

    public virtual TblAdministracionCatalogoDetalle? IdmodotrabajoNavigation { get; set; }

    public virtual TblAdministracionPersona IdpersonaNavigation { get; set; } = null!;

    public virtual TblAdministracionCatalogoDetalle? IdtipocontratoNavigation { get; set; }

    public virtual TblAdministracionCatalogoDetalle? TipoSalidaNavigation { get; set; }
    
    public virtual TblAdministracionCatalogoDetalle? CausaSalidaNavigation { get; set; }
   
    public virtual TblAdministracionEmpleado? EmpleadoReemplazoNavigation { get; set; }
   
    public virtual ICollection<TblAdministracionEmpleado>? EmpleadosReemplazados { get; set; }

    public virtual ICollection<TblAdministracionRegistroAsignacion> TblAdministracionRegistroAsignacions { get; set; } = new List<TblAdministracionRegistroAsignacion>();

    public virtual ICollection<TblInventarioAsignacionEquipo> TblInventarioAsignacionEquipos { get; set; } = new List<TblInventarioAsignacionEquipo>();

    public virtual ICollection<TblTimeReportActividadDiarium> TblTimeReportActividadDiariumAprobadoporNavigations { get; set; } = new List<TblTimeReportActividadDiarium>();

    public virtual ICollection<TblTimeReportActividadDiarium> TblTimeReportActividadDiariumIdempleadoNavigations { get; set; } = new List<TblTimeReportActividadDiarium>();

    public virtual ICollection<TblTimeReportAsignacionProyecto> TblTimeReportAsignacionProyectos { get; set; } = new List<TblTimeReportAsignacionProyecto>();

    public virtual ICollection<TblTimeReportHomologacionBanco> TblTimeReportHomologacionBancos { get; set; } = new List<TblTimeReportHomologacionBanco>();

    public virtual ICollection<TblTimeReportPermiso> TblTimeReportPermisoAprobadoporNavigations { get; set; } = new List<TblTimeReportPermiso>();

    public virtual ICollection<TblTimeReportPermiso> TblTimeReportPermisoIdempleadoNavigations { get; set; } = new List<TblTimeReportPermiso>();
}
