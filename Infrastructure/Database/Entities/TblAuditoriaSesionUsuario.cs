using System;
using System.Collections.Generic;

namespace tmr_backend.Infrastructure.Database.Entities;

public partial class TblAuditoriaSesionUsuario
{
    public long Id { get; set; }

    public int Idusuario { get; set; }

    public string Tipocambio { get; set; } = null!;

    public string? Direccionip { get; set; }

    public string? Agenteusuario { get; set; }

    public DateTime Fechacreacion { get; set; }
}
