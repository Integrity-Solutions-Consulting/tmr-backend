using System;
using System.Collections.Generic;

namespace tmr_backend.Infrastructure.Database.Entities;

public partial class TblAutenticacionPermiso
{
    public int Id { get; set; }

    public int Idmodulo { get; set; }

    public string Codigo { get; set; } = null!;

    public string Accion { get; set; } = null!;

    public string? Descripcion { get; set; }

    public bool Activo { get; set; }

    public string Usuariocreacion { get; set; } = null!;

    public DateTime Fechacreacion { get; set; }

    public string Ipcreacion { get; set; } = null!;

    public virtual ICollection<TblAutenticacionRolPermiso> TblAutenticacionRolPermisos { get; set; } = [];
}
