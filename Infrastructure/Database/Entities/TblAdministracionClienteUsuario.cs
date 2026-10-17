using System;
using System.Collections.Generic;

namespace tmr_backend.Infrastructure.Database.Entities;

public partial class TblAdministracionClienteUsuario
{
    public int Id { get; set; }

    public int Idcliente { get; set; }

    public int Idusuario { get; set; }

    public bool Activo { get; set; }

    public string Usuariocreacion { get; set; } = null!;

    public DateTime Fechacreacion { get; set; }

    public string? Usuariomodificacion { get; set; }

    public DateTime? Fechamodificacion { get; set; }

    public string Ipcreacion { get; set; } = null!;

    public string? Ipmodificacion { get; set; }

    public virtual TblAdministracionCliente IdclienteNavigation { get; set; } = null!;

    public virtual TblAutenticacionUsuario IdusuarioNavigation { get; set; } = null!;
}
