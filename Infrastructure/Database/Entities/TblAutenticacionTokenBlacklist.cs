using System;
using System.Collections.Generic;

namespace tmr_backend.Infrastructure.Database.Entities;

public partial class TblAutenticacionTokenBlacklist
{
    public string Jti { get; set; } = null!;

    public int? Idusuario { get; set; }

    public DateTime Fecharevocado { get; set; }

    public DateTime? Fechaexpiracion { get; set; }

    public bool Activo { get; set; }

    public string Usuariocreacion { get; set; } = null!;

    public DateTime Fechacreacion { get; set; }

    public virtual TblAutenticacionUsuario? IdusuarioNavigation { get; set; }
}
