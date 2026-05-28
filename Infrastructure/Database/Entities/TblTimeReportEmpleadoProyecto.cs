using System;
using System.Collections.Generic;

namespace tmr_backend.Infrastructure.Database.Entities;

public partial class TblTimeReportEmpleadoProyecto
{
    public int Id { get; set; }

    public int? Idempleado { get; set; }

    public int Idproyecto { get; set; }

    public int? Idproveedor { get; set; }

    public DateOnly? Fechaasignacion { get; set; }

    public DateOnly? Fechafinasignacion { get; set; }

    public string? Rolasignado { get; set; }

    public decimal? Costoporhora { get; set; }

    public decimal? Horasasignadas { get; set; }

    public bool Activo { get; set; }

    public string Usuariocreacion { get; set; } = null!;

    public DateTime Fechacreacion { get; set; }

    public string? Usuariomodificacion { get; set; }

    public DateTime? Fechamodificacion { get; set; }

    public string Ipcreacion { get; set; } = null!;

    public string? Ipmodificacion { get; set; }

    public virtual TblAdministracionEmpleado? IdempleadoNavigation { get; set; }

    public virtual TblTimeReportProyecto IdproyectoNavigation { get; set; } = null!;
}
