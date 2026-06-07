using System;
using System.Collections.Generic;

namespace tmr_backend.Infrastructure.Database.Entities;

public partial class TblAdministracionLider
{
    //agregue bool es interno
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public bool EsInterno { get; set; }  // true = Interno, false = Externo
    public int Id { get; set; }

    public int Idpersona { get; set; }

    public int? Idtipo { get; set; }

    public bool Activo { get; set; }

    public string Usuariocreacion { get; set; } = null!;

    public DateTime Fechacreacion { get; set; }

    public string? Usuariomodificacion { get; set; }

    public DateTime? Fechamodificacion { get; set; }

    public string Ipcreacion { get; set; } = null!;

    public string? Ipmodificacion { get; set; }

    public virtual TblAdministracionPersona IdpersonaNavigation { get; set; } = null!;

    public virtual TblAdministracionCatalogoDetalle? IdtipoNavigation { get; set; }

    public virtual ICollection<TblTimeReportProyecto> TblTimeReportProyectos { get; set; } = new List<TblTimeReportProyecto>();
}
