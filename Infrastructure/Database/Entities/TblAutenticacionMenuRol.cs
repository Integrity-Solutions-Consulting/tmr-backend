using System;
using System.Collections.Generic;

namespace tmr_backend.Infrastructure.Database.Entities;

public partial class TblAutenticacionMenuRol
{
    public int Id { get; set; }

    public int Idmenu { get; set; }

    public int Idrol { get; set; }

    public bool Activo { get; set; }

    public string Usuariocreacion { get; set; } = null!;

    public DateTime Fechacreacion { get; set; }

    public string Ipcreacion { get; set; } = null!;

    public virtual TblAutenticacionMenu IdmenuNavigation { get; set; } = null!;

    public virtual TblAdministracionCatalogoDetalle IdrolNavigation { get; set; } = null!;
}
