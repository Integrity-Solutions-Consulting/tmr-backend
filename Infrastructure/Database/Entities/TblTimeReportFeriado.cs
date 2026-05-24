using System;
using System.Collections.Generic;

namespace tmr_backend.Infrastructure.Database.Entities;

public partial class TblTimeReportFeriado
{
    public int Id { get; set; }

    public string Nombreferiado { get; set; } = null!;

    public DateOnly Fechaferiado { get; set; }

    public bool? Esrecurrente { get; set; }

    public string? Tipoferiado { get; set; }

    public string? Descripcion { get; set; }

    public bool Activo { get; set; }

    public string Usuariocreacion { get; set; } = null!;

    public DateTime Fechacreacion { get; set; }

    public string? Usuariomodificacion { get; set; }

    public DateTime? Fechamodificacion { get; set; }

    public string Ipcreacion { get; set; } = null!;

    public string? Ipmodificacion { get; set; }
}
