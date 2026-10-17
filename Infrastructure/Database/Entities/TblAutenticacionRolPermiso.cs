using System;

namespace tmr_backend.Infrastructure.Database.Entities;

public partial class TblAutenticacionRolPermiso
{
    // PK compuesta (IdRol, IdPermiso) — sin Id surrogate
    public int Idrol { get; set; }

    public int Idpermiso { get; set; }

    public DateTime Otorgado { get; set; }

    public bool Activo { get; set; }

    public string Usuariocreacion { get; set; } = null!;

    public DateTime Fechacreacion { get; set; }

    public string Ipcreacion { get; set; } = null!;

    public virtual TblAutenticacionPermiso IdpermisoNavigation { get; set; } = null!;

    public virtual TblAutenticacionRol IdrolNavigation { get; set; } = null!;
}
