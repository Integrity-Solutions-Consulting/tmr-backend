using System;
using System.Collections.Generic;

namespace tmr_backend.Infrastructure.Database.Entities;

public partial class TblTimeReportHomologacionBanco
{
    public int Id { get; set; }

    public int Idempleado { get; set; }

    public string? Nombrecompletotr { get; set; }

    public string? Cedulatr { get; set; }

    public string? Proyectotr { get; set; }

    public string? Clientetr { get; set; }

    public string? Nombrecompletobanco { get; set; }

    public string? Observacion { get; set; }

    public DateTime Fecharegistro { get; set; }

    public virtual TblAdministracionEmpleado IdempleadoNavigation { get; set; } = null!;
}
