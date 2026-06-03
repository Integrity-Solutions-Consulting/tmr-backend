using System;
using System.Collections.Generic;

namespace tmr_backend.Infrastructure.Database.Entities;

public partial class TblAutenticacionModulo
{
    public int Id { get; set; }

    public string Nombremodulo { get; set; } = null!;

    public string? Rutamodulo { get; set; }

    public string? Icono { get; set; }

    public int? Ordenvisualizacion { get; set; }

    public int Idmodulopadre { get; set; }

    public bool Activo { get; set; }

    public string Usuariocreacion { get; set; } = null!;

    public DateTime Fechacreacion { get; set; }

    public string? Usuariomodificacion { get; set; }

    public DateTime? Fechamodificacion { get; set; }

    public string Ipcreacion { get; set; } = null!;

    public string? Ipmodificacion { get; set; }

    public virtual ICollection<TblAutenticacionRolModulo> TblAutenticacionRolModulos { get; set; } = new List<TblAutenticacionRolModulo>();

    public virtual ICollection<TblAutenticacionUsuarioModulo> TblAutenticacionUsuarioModulos { get; set; } = new List<TblAutenticacionUsuarioModulo>();
}
