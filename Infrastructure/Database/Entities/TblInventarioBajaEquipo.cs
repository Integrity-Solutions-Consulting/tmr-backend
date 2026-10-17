using System;
using System.Collections.Generic;

namespace tmr_backend.Infrastructure.Database.Entities;

public partial class TblInventarioBajaEquipo
{
    public int Id { get; set; }

    public int Idequipo { get; set; }

    public int? Idtipobaja { get; set; }

    public DateOnly Fechabaja { get; set; }

    public string? Motivobaja { get; set; }

    public bool Activo { get; set; }

    public string Usuariocreacion { get; set; } = null!;

    public DateTime Fechacreacion { get; set; }

    public string? Usuariomodificacion { get; set; }

    public DateTime? Fechamodificacion { get; set; }

    public string Ipcreacion { get; set; } = null!;

    public string? Ipmodificacion { get; set; }

    public virtual TblInventarioEquipo IdequipoNavigation { get; set; } = null!;

    public virtual TblAdministracionCatalogoDetalle? IdtipobajaNavigation { get; set; }
}
