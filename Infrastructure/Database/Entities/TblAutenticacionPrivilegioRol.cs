using System;
using System.Collections.Generic;

namespace tmr_backend.Infrastructure.Database.Entities;

public partial class TblAutenticacionPrivilegioRol
{
    public int Id { get; set; }

    public int Idprivilegio { get; set; }

    public int Idrol { get; set; }

    public bool Activo { get; set; }

    public string Usuariocreacion { get; set; } = null!;

    public DateTime Fechacreacion { get; set; }

    public string Ipcreacion { get; set; } = null!;

    public virtual TblAdministracionCatalogoDetalle IdprivilegioNavigation { get; set; } = null!;

    public virtual TblAdministracionCatalogoDetalle IdrolNavigation { get; set; } = null!;
}
