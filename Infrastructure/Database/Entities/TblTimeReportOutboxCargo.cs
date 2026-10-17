using System;
using System.Collections.Generic;

namespace tmr_backend.Infrastructure.Database.Entities;

public partial class TblTimeReportOutboxCargo
{
    public Guid Idoutbox { get; set; }

    public int Idagregado { get; set; }

    public char Operacion { get; set; }

    public string? Payloadjson { get; set; }

    public short Intentos { get; set; }

    public DateTime Proximointento { get; set; }

    public DateTime? Procesadoen { get; set; }

    public string? Mensajeerror { get; set; }

    public DateTime Creadoen { get; set; }
}
