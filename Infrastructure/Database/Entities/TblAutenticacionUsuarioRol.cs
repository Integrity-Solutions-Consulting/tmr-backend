using System;
using System.Collections.Generic;

namespace tmr_backend.Infrastructure.Database.Entities;

public partial class TblAutenticacionUsuarioRol
{
    public int Id { get; set; }

    public int Idusuario { get; set; }

    public int Idrol { get; set; }

    public bool Activo { get; set; }

    public string Usuariocreacion { get; set; } = null!;

    public DateTime Fechacreacion { get; set; }

    public string Ipcreacion { get; set; } = null!;

    public virtual TblAdministracionCatalogoDetalle IdrolNavigation { get; set; } = null!;

    public virtual TblAutenticacionUsuario IdusuarioNavigation { get; set; } = null!;
}
