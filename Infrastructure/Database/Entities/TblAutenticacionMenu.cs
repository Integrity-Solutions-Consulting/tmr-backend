using System;
using System.Collections.Generic;

namespace tmr_backend.Infrastructure.Database.Entities;

public partial class TblAutenticacionMenu
{
    public int Id { get; set; }

    public int? Idaplicacion { get; set; }

    public string Nombremenu { get; set; } = null!;

    public string? Rutamenu { get; set; }

    public string? Icono { get; set; }

    public int? Ordenvisualizacion { get; set; }

    public int? Idmenupadre { get; set; }

    public bool Activo { get; set; }

    public string Usuariocreacion { get; set; } = null!;

    public DateTime Fechacreacion { get; set; }

    public string? Usuariomodificacion { get; set; }

    public DateTime? Fechamodificacion { get; set; }

    public string Ipcreacion { get; set; } = null!;

    public string? Ipmodificacion { get; set; }

    public virtual TblAutenticacionAplicacion? IdaplicacionNavigation { get; set; }

    public virtual TblAutenticacionMenu? IdmenupadreNavigation { get; set; }

    public virtual ICollection<TblAutenticacionMenu> InverseIdmenupadreNavigation { get; set; } = new List<TblAutenticacionMenu>();

    public virtual ICollection<TblAutenticacionMenuRol> TblAutenticacionMenuRols { get; set; } = new List<TblAutenticacionMenuRol>();

    public virtual ICollection<TblAutenticacionMenuUsuario> TblAutenticacionMenuUsuarios { get; set; } = new List<TblAutenticacionMenuUsuario>();
}
