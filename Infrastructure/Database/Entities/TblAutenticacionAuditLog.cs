using System;
using System.Collections.Generic;

namespace tmr_backend.Infrastructure.Database.Entities;

public partial class TblAutenticacionAuditLog
{
    public int Id { get; set; }

    public int? Idusuario { get; set; }

    public string? Agenteusuario { get; set; }

    public string? Detalles { get; set; }

    public string? Direccionip { get; set; }

    public DateTime Fechacreacion { get; set; }

    public virtual TblAutenticacionUsuario? IdusuarioNavigation { get; set; }
}
