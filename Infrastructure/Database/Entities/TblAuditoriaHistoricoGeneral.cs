using System;
using System.Collections.Generic;

namespace tmr_backend.Infrastructure.Database.Entities;

public partial class TblAuditoriaHistoricoGeneral
{
    public long Id { get; set; }

    public string Nombretabla { get; set; } = null!;

    public string Idregistro { get; set; } = null!;

    public string Tipooperacion { get; set; } = null!;

    public string? Datosanteriores { get; set; }

    public string? Datosnuevos { get; set; }

    public string? Cambiadopor { get; set; }

    public DateTime Fechacambio { get; set; }

    public string? Direccionip { get; set; }
}
