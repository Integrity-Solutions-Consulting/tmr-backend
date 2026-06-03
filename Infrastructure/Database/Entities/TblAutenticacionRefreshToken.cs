using System;
using System.Collections.Generic;

namespace tmr_backend.Infrastructure.Database.Entities;

public partial class TblAutenticacionRefreshToken
{
    public long Id { get; set; }

    public long Idsesion { get; set; }

    public int Idusuario { get; set; }

    public string Tokenhash { get; set; } = null!;

    public Guid Familiatoken { get; set; }

    public bool Estausado { get; set; }

    public bool Estarevocado { get; set; }

    public DateTime Fechaexpiracion { get; set; }

    public DateTime? Fechausado { get; set; }

    public bool Activo { get; set; }

    public string Usuariocreacion { get; set; } = null!;

    public DateTime Fechacreacion { get; set; }

    public virtual TblAutenticacionSesion IdsesionNavigation { get; set; } = null!;

    public virtual TblAutenticacionUsuario IdusuarioNavigation { get; set; } = null!;
}
