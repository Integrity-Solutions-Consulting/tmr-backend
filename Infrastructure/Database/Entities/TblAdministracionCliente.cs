using System;
using System.Collections.Generic;

namespace tmr_backend.Infrastructure.Database.Entities;

public partial class TblAdministracionCliente
{
    public int Id { get; set; }

    public string Numeroidentificacion { get; set; } = null!;

    public int? Idtipoidentificacion { get; set; }

    public string? Nombrecomercial { get; set; }

    public string? Razonsocial { get; set; }

    public string? Email { get; set; }

    public string? Telefono { get; set; }

    public string? Direccion { get; set; }

    public bool Activo { get; set; }

    public string Usuariocreacion { get; set; } = null!;

    public DateTime Fechacreacion { get; set; }

    public string? Usuariomodificacion { get; set; }

    public DateTime? Fechamodificacion { get; set; }

    public string Ipcreacion { get; set; } = null!;

    public string? Ipmodificacion { get; set; }

    public virtual TblAdministracionCatalogoDetalle? IdtipoidentificacionNavigation { get; set; }

    public virtual ICollection<TblAdministracionClienteUsuario> TblAdministracionClienteUsuarios { get; set; } = new List<TblAdministracionClienteUsuario>();

    public virtual ICollection<TblTimeReportProyecto> TblTimeReportProyectos { get; set; } = new List<TblTimeReportProyecto>();
}
