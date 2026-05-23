using System;
using System.Collections.Generic;

namespace tmr_backend.Infrastructure.Database.Entities;

public partial class TblInventarioProveedor
{
    public int Id { get; set; }

    public int? Idtipoproveedor { get; set; }

    public string Nombreproveedor { get; set; } = null!;

    public string? Ruc { get; set; }

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

    public virtual TblAdministracionCatalogoDetalle? IdtipoproveedorNavigation { get; set; }

    public virtual ICollection<TblInventarioFactura> TblInventarioFacturas { get; set; } = new List<TblInventarioFactura>();
}
