using System;
using System.Collections.Generic;

namespace tmr_backend.Infrastructure.Database.Entities;

/// <summary>Tipo de proveedor del esquema inventario.</summary>
public partial class TblInventarioTipoProveedor
{
    public int Id { get; set; }
    public string Nombre { get; set; } = null!;
    public bool Activo { get; set; }
    public string Usuariocreacion { get; set; } = null!;
    public string? Usuariomodificacion { get; set; }
    public DateTime Fechacreacion { get; set; }
    public DateTime? Fechamodificacion { get; set; }

    public virtual ICollection<TblInventarioProveedor> TblInventarioProveedors { get; set; } = new List<TblInventarioProveedor>();
}
