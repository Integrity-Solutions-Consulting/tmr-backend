using System;
using System.Collections.Generic;

namespace tmr_backend.Infrastructure.Database.Entities;

public partial class TblInventarioFactura
{
    public int Id { get; set; }

    public string Numerofactura { get; set; } = null!;

    public int? Idproveedor { get; set; }

    public DateOnly Fechafactura { get; set; }

    public bool Activo { get; set; }

    public string Usuariocreacion { get; set; } = null!;

    public DateTime Fechacreacion { get; set; }

    public string? Usuariomodificacion { get; set; }

    public DateTime? Fechamodificacion { get; set; }

    public string Ipcreacion { get; set; } = null!;

    public string? Ipmodificacion { get; set; }

    public virtual TblInventarioProveedor? IdproveedorNavigation { get; set; }

    public virtual ICollection<TblInventarioDetalleFactura> TblInventarioDetalleFacturas { get; set; } = new List<TblInventarioDetalleFactura>();

    public virtual ICollection<TblInventarioEquipo> TblInventarioEquipos { get; set; } = new List<TblInventarioEquipo>();
}
