using System;
using System.Collections.Generic;

namespace tmr_backend.Infrastructure.Database.Entities;

public partial class TblAuditoriaEmpleado
{
    public long Id { get; set; }

    public int Idempleado { get; set; }

    public string Tipocambio { get; set; } = null!;

    public string? Camposmodificados { get; set; }

    public string? Valoresanteriores { get; set; }

    public string? Valoresnuevos { get; set; }

    public string? Cambiadopor { get; set; }

    public string? Direccionip { get; set; }

    public string? Agenteusuario { get; set; }

    public string? Usuariocreacion { get; set; }

    public string? Ipcreacion { get; set; }

    public DateTime Fechacreacion { get; set; }
}
