using System;

namespace tmr_backend.Infrastructure.Database.Entities;

public partial class TblAutenticacionPasswordReset
{
    public int Id { get; set; }

    public int IdUsuario { get; set; }

    public string TokenHash { get; set; } = null!;

    public DateTime FechaExpiracion { get; set; }

    public bool Utilizado { get; set; }

    public DateTime? FechaUtilizacion { get; set; }

    public bool Activo { get; set; } = true;

    public string UsuarioCreacion { get; set; } = null!;

    public DateTime FechaCreacion { get; set; }

    public string IpCreacion { get; set; } = null!;

    public virtual TblAutenticacionUsuario IdUsuarioNavigation { get; set; } = null!;
}
