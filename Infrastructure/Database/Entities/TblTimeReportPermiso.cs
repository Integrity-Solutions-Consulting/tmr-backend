using System;
using System.Collections.Generic;

namespace tmr_backend.Infrastructure.Database.Entities;

public partial class TblTimeReportPermiso
{
    public int Id { get; set; }

    public int Idempleado { get; set; }

    public int Idtipopermiso { get; set; }

    public int Idestadoaprobacion { get; set; }

    public DateOnly Fechainicio { get; set; }

    public DateOnly Fechafin { get; set; }

    public decimal Totaldias { get; set; }

    public decimal? Totalhoras { get; set; }

    public bool? Espagado { get; set; }

    public string? Descripcion { get; set; }

    public int? Aprobadopor { get; set; }

    public DateTime? Fechaaprobacion { get; set; }

    public string? Observacion { get; set; }

    public bool Activo { get; set; }

    public string Usuariocreacion { get; set; } = null!;

    public DateTime Fechacreacion { get; set; }

    public string? Usuariomodificacion { get; set; }

    public DateTime? Fechamodificacion { get; set; }

    public string Ipcreacion { get; set; } = null!;

    public string? Ipmodificacion { get; set; }

    public virtual TblAdministracionEmpleado? AprobadoporNavigation { get; set; }

    public virtual TblAdministracionEmpleado IdempleadoNavigation { get; set; } = null!;

    public virtual TblAdministracionCatalogoDetalle IdestadoaprobacionNavigation { get; set; } = null!;

    public virtual TblAdministracionCatalogoDetalle IdtipopermisoNavigation { get; set; } = null!;
}
