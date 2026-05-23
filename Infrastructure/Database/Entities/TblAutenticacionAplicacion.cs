using System;
using System.Collections.Generic;

namespace tmr_backend.Infrastructure.Database.Entities;

public partial class TblAutenticacionAplicacion
{
    public int Id { get; set; }

    public string Nombreaplicacion { get; set; } = null!;

    public string? Descripcion { get; set; }

    public string? Urlbase { get; set; }

    public bool Activo { get; set; }

    public string Usuariocreacion { get; set; } = null!;

    public DateTime Fechacreacion { get; set; }

    public string? Usuariomodificacion { get; set; }

    public DateTime? Fechamodificacion { get; set; }

    public string Ipcreacion { get; set; } = null!;

    public string? Ipmodificacion { get; set; }

    public virtual ICollection<TblAutenticacionMenu> TblAutenticacionMenus { get; set; } = new List<TblAutenticacionMenu>();

    public virtual ICollection<TblAutenticacionSesionApp> TblAutenticacionSesionApps { get; set; } = new List<TblAutenticacionSesionApp>();

    public virtual ICollection<TblAutenticacionUsuarioAplicacion> TblAutenticacionUsuarioAplicacions { get; set; } = new List<TblAutenticacionUsuarioAplicacion>();
}
