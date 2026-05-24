using System;
using System.Collections.Generic;

namespace tmr_backend.Infrastructure.Database.Entities;

public partial class TblAutenticacionSesionApp
{
    public int Id { get; set; }

    public int Idusuario { get; set; }

    public int Idaplicacion { get; set; }

    public string Tokenapp { get; set; } = null!;

    public DateTime? Fechaexpiracion { get; set; }

    public bool Activo { get; set; }

    public string Usuariocreacion { get; set; } = null!;

    public DateTime Fechacreacion { get; set; }

    public string Ipcreacion { get; set; } = null!;

    public virtual TblAutenticacionAplicacion IdaplicacionNavigation { get; set; } = null!;

    public virtual TblAutenticacionUsuario IdusuarioNavigation { get; set; } = null!;
}
