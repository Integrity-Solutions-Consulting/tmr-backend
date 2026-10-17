using System;
using System.Collections.Generic;

namespace tmr_backend.Infrastructure.Database.Entities;

public partial class TblInventarioAsignacionEquipo
{
    public int Id { get; set; }

    public int Idequipo { get; set; }

    public int Idempleado { get; set; }

    public DateTime Fechaasignacion { get; set; }

    public DateTime? Fechadevolucion { get; set; }

    public string? Observacion { get; set; }

    public bool Activo { get; set; }

    public string Usuariocreacion { get; set; } = null!;

    public DateTime Fechacreacion { get; set; }

    public string? Usuariomodificacion { get; set; }

    public DateTime? Fechamodificacion { get; set; }

    public string Ipcreacion { get; set; } = null!;

    public string? Ipmodificacion { get; set; }

    public virtual TblAdministracionEmpleado IdempleadoNavigation { get; set; } = null!;

    public virtual TblInventarioEquipo IdequipoNavigation { get; set; } = null!;
}
