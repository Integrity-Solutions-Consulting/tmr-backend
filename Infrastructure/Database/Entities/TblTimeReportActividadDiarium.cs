using System;
using System.Collections.Generic;

namespace tmr_backend.Infrastructure.Database.Entities;

public partial class TblTimeReportActividadDiarium
{
    public int Id { get; set; }

    public int Idempleado { get; set; }

    public int? Idproyecto { get; set; }

    public int Idtipoactividad { get; set; }

    public string? Codigorequerimiento { get; set; }

    public decimal Cantidadhoras { get; set; }

    public DateOnly Fechaactividad { get; set; }

    public string Descripcionactividad { get; set; } = null!;

    public string? Notas { get; set; }

    public bool? Esbillable { get; set; }

    public int? Aprobadopor { get; set; }

    public DateTime? Fechaaprobacion { get; set; }

    public bool Activo { get; set; }

    public string Usuariocreacion { get; set; } = null!;

    public DateTime Fechacreacion { get; set; }

    public string? Usuariomodificacion { get; set; }

    public DateTime? Fechamodificacion { get; set; }

    public string Ipcreacion { get; set; } = null!;

    public string? Ipmodificacion { get; set; }

    public virtual TblAdministracionEmpleado? AprobadoporNavigation { get; set; }

    public virtual TblAdministracionEmpleado IdempleadoNavigation { get; set; } = null!;

    public virtual TblTimeReportProyecto? IdproyectoNavigation { get; set; }

    public virtual TblTimeReportTipoActividad IdtipoactividadNavigation { get; set; } = null!;
}
