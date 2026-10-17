using System;
using System.Collections.Generic;

namespace tmr_backend.Infrastructure.Database.Entities;

public partial class TblTimeReportTipoActividad
{
    public int Id { get; set; }

    public string Nombretipo { get; set; } = null!;

    public string? Descripcion { get; set; }

    public string? Codigocolor { get; set; }

    public bool Activo { get; set; }

    public string Usuariocreacion { get; set; } = null!;

    public DateTime Fechacreacion { get; set; }

    public string? Usuariomodificacion { get; set; }

    public DateTime? Fechamodificacion { get; set; }

    public string Ipcreacion { get; set; } = null!;

    public string? Ipmodificacion { get; set; }

    public virtual ICollection<TblTimeReportActividadDiarium> TblTimeReportActividadDiaria { get; set; } = new List<TblTimeReportActividadDiarium>();
}
