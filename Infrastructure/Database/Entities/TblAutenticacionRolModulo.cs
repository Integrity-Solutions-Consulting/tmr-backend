using System;
using System.Collections.Generic;

namespace tmr_backend.Infrastructure.Database.Entities;

public partial class TblAutenticacionRolModulo
{
    public int Id { get; set; }

    public int Idrol { get; set; }

    public int Idmodulo { get; set; }

    public bool? Puedever { get; set; }

    public bool? Puedecrear { get; set; }

    public bool? Puedeeditar { get; set; }

    public bool? Puedeeliminar { get; set; }

    public bool Activo { get; set; }

    public string Usuariocreacion { get; set; } = null!;

    public DateTime Fechacreacion { get; set; }

    public string? Usuariomodificacion { get; set; }

    public DateTime? Fechamodificacion { get; set; }

    public string Ipcreacion { get; set; } = null!;

    public string? Ipmodificacion { get; set; }

    public virtual TblAutenticacionModulo IdmoduloNavigation { get; set; } = null!;

    public virtual TblAdministracionCatalogoDetalle IdrolNavigation { get; set; } = null!;
}
