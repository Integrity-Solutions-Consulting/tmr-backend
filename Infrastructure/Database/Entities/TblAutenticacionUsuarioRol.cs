using System;
using System.Collections.Generic;

namespace tmr_backend.Infrastructure.Database.Entities;

public partial class TblAutenticacionUsuarioRol
{
    public int Idusuario { get; set; }

    public int Idrol { get; set; }

    public int? Asignadopor { get; set; }

    public DateTime Asignadoen { get; set; }

    public DateTime? Fechaexpiracion { get; set; }

    public bool Activo { get; set; }

    public string Usuariocreacion { get; set; } = null!;

    public DateTime Fechacreacion { get; set; }

    public string Ipcreacion { get; set; } = null!;

    public virtual TblAutenticacionRol IdrolNavigation { get; set; } = null!;

    public virtual TblAutenticacionUsuario IdusuarioNavigation { get; set; } = null!;
}
