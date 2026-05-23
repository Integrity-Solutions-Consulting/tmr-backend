using System;
using System.Collections.Generic;

namespace tmr_backend.Infrastructure.Database.Entities;

public partial class TblAdministracionCargo
{
    public int Id { get; set; }

    public int? Iddepartamento { get; set; }

    public string Nombrecargo { get; set; } = null!;

    public string? Descripcion { get; set; }

    public bool Activo { get; set; }

    public string Usuariocreacion { get; set; } = null!;

    public DateTime Fechacreacion { get; set; }

    public string? Usuariomodificacion { get; set; }

    public DateTime? Fechamodificacion { get; set; }

    public string Ipcreacion { get; set; } = null!;

    public string? Ipmodificacion { get; set; }

    public virtual TblAdministracionCatalogoDetalle? IddepartamentoNavigation { get; set; }

    public virtual ICollection<TblAdministracionEmpleado> TblAdministracionEmpleados { get; set; } = new List<TblAdministracionEmpleado>();

    public virtual ICollection<TblTimeReportProyeccionHora> TblTimeReportProyeccionHoras { get; set; } = new List<TblTimeReportProyeccionHora>();

    public virtual ICollection<TblTimeReportProyeccionHorasProyecto> TblTimeReportProyeccionHorasProyectos { get; set; } = new List<TblTimeReportProyeccionHorasProyecto>();
}
