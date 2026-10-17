using System;
using System.Collections.Generic;

namespace tmr_backend.Infrastructure.Database.Entities;

public partial class TblInventarioReparacionEquipo
{
    public int Id { get; set; }

    public int Idequipo { get; set; }

    public DateOnly Fechainicio { get; set; }

    public DateOnly? Fechafin { get; set; }

    public string? Descripcion { get; set; }

    public decimal? Costoreparacion { get; set; }

    public bool Activo { get; set; }

    public string Usuariocreacion { get; set; } = null!;

    public DateTime Fechacreacion { get; set; }

    public string? Usuariomodificacion { get; set; }

    public DateTime? Fechamodificacion { get; set; }

    public string Ipcreacion { get; set; } = null!;

    public string? Ipmodificacion { get; set; }

    public virtual TblInventarioEquipo IdequipoNavigation { get; set; } = null!;
}
