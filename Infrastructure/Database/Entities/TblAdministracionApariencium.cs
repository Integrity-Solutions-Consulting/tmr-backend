using System;
using System.Collections.Generic;

namespace tmr_backend.Infrastructure.Database.Entities;

public partial class TblAdministracionApariencium
{
    public int Id { get; set; }

    public string Fondologin { get; set; } = null!;

    public string Tipografia { get; set; } = null!;

    public bool Encabezadofijo { get; set; }

    public string Posicionmenu { get; set; } = null!;

    public bool Menucolapsado { get; set; }

    public string Colorfondo { get; set; } = null!;

    public int Bordercaja { get; set; }

    public string Fondocaja { get; set; } = null!;

    public bool Activo { get; set; }

    public string Usuariocreacion { get; set; } = null!;

    public DateTime Fechacreacion { get; set; }

    public string? Usuariomodificacion { get; set; }

    public DateTime? Fechamodificacion { get; set; }

    public string Ipcreacion { get; set; } = null!;

    public string? Ipmodificacion { get; set; }
}
