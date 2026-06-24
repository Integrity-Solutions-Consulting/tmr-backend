using System;
using System.Collections.Generic;

namespace tmr_backend.Infrastructure.Database.Entities;

public partial class TblAutenticacionSesion
{
    public long Id { get; set; }

    public int Idusuario { get; set; }

    public string? Dispositivoinfo { get; set; }

    public string? Direccionip { get; set; }

    public string? Agenteusuario { get; set; }

    public string? Ubicacioninfo { get; set; }

    public bool Estaactiva { get; set; }

    public DateTime Ultimaactividad { get; set; }

    public DateTime? Fechaexpiracion { get; set; }

    public DateTime? Revocadofecha { get; set; }
    public string? Revocadorazon { get; set; }

    public bool Activo { get; set; }

    public string Usuariocreacion { get; set; } = null!;

    public DateTime Fechacreacion { get; set; }

    public string? Usuariomodificacion { get; set; }

    public DateTime? Fechamodificacion { get; set; }

    public string Ipcreacion { get; set; } = null!;

    public string? Ipmodificacion { get; set; }

    public virtual TblAutenticacionUsuario IdusuarioNavigation { get; set; } = null!;

    public virtual ICollection<TblAutenticacionRefreshToken> TblAutenticacionRefreshTokens { get; set; } = new List<TblAutenticacionRefreshToken>();
}
