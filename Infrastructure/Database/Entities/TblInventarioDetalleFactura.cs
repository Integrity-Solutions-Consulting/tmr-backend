using System;
using System.Collections.Generic;

namespace tmr_backend.Infrastructure.Database.Entities;

public partial class TblInventarioDetalleFactura
{
    public int Id { get; set; }

    public int Idfactura { get; set; }

    public string? Descripcion { get; set; }

    public int Cantidad { get; set; }

    public decimal Preciounitario { get; set; }

    public decimal Subtotal { get; set; }

    public decimal Iva { get; set; }

    public decimal Total { get; set; }

    public string? Observacion { get; set; }

    public bool Activo { get; set; }

    public string Usuariocreacion { get; set; } = null!;

    public DateTime Fechacreacion { get; set; }

    public string? Usuariomodificacion { get; set; }

    public DateTime? Fechamodificacion { get; set; }

    public string Ipcreacion { get; set; } = null!;

    public string? Ipmodificacion { get; set; }

    public virtual TblInventarioFactura IdfacturaNavigation { get; set; } = null!;
}
