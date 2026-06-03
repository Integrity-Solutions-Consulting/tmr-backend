using System;
using System.Collections.Generic;

namespace tmr_backend.Infrastructure.Database.Entities;

public partial class TblAutenticacionUsuario
{
    public int Id { get; set; }

    public int? Idpersona { get; set; }

    public string Email { get; set; } = null!;

    public string Hashpassword { get; set; } = null!;

    public DateTime? Ultimologin { get; set; }

    public bool Emailverificado { get; set; }

    public short Intentosfallidos { get; set; }

    public DateTime? Bloqueadohasta { get; set; }

    public bool Debecambiarpassword { get; set; }

    public bool Activo { get; set; }

    public string Usuariocreacion { get; set; } = null!;

    public DateTime Fechacreacion { get; set; }

    public string? Usuariomodificacion { get; set; }

    public DateTime? Fechamodificacion { get; set; }

    public string Ipcreacion { get; set; } = null!;

    public string? Ipmodificacion { get; set; }

    public virtual TblAdministracionPersona? IdpersonaNavigation { get; set; }

    public virtual ICollection<TblAdministracionClienteUsuario> TblAdministracionClienteUsuarios { get; set; } = new List<TblAdministracionClienteUsuario>();

    public virtual ICollection<TblAutenticacionMenuUsuario> TblAutenticacionMenuUsuarios { get; set; } = new List<TblAutenticacionMenuUsuario>();

    public virtual ICollection<TblAutenticacionPasswordHistorial> TblAutenticacionPasswordHistorials { get; set; } = new List<TblAutenticacionPasswordHistorial>();

    public virtual ICollection<TblAutenticacionPreguntaUsuario> TblAutenticacionPreguntaUsuarios { get; set; } = new List<TblAutenticacionPreguntaUsuario>();

    public virtual ICollection<TblAutenticacionPrivilegioUsuario> TblAutenticacionPrivilegioUsuarios { get; set; } = new List<TblAutenticacionPrivilegioUsuario>();

    public virtual ICollection<TblAutenticacionSesionApp> TblAutenticacionSesionApps { get; set; } = new List<TblAutenticacionSesionApp>();

    public virtual ICollection<TblAutenticacionRefreshToken> TblAutenticacionRefreshTokens { get; set; } = new List<TblAutenticacionRefreshToken>();

    public virtual ICollection<TblAutenticacionSesion> TblAutenticacionSesions { get; set; } = new List<TblAutenticacionSesion>();

    public virtual ICollection<TblAutenticacionUsuarioAplicacion> TblAutenticacionUsuarioAplicacions { get; set; } = new List<TblAutenticacionUsuarioAplicacion>();

    public virtual ICollection<TblAutenticacionUsuarioModulo> TblAutenticacionUsuarioModulos { get; set; } = new List<TblAutenticacionUsuarioModulo>();

    public virtual ICollection<TblAutenticacionUsuarioRol> TblAutenticacionUsuarioRols { get; set; } = new List<TblAutenticacionUsuarioRol>();

    public virtual ICollection<TblAutenticacionAuditLog> TblAutenticacionAuditLogs { get; set; } = new List<TblAutenticacionAuditLog>();
}
