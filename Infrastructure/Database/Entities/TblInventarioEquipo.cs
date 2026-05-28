using System;
using System.Collections.Generic;

namespace tmr_backend.Infrastructure.Database.Entities;

public partial class TblInventarioEquipo
{
    public int Id { get; set; }

    public int Idcategoria { get; set; }

    public int Idestado { get; set; }

    public int? Idcondicion { get; set; }

    public int? Idtipogarantia { get; set; }

    public int? Idfactura { get; set; }

    public string Marca { get; set; } = null!;

    public string Modelo { get; set; } = null!;

    public string Numeroserie { get; set; } = null!;

    public DateOnly? Fechaadquisicion { get; set; }

    public DateOnly? Fechavencimientogarantia { get; set; }

    public decimal? Valoradquisicion { get; set; }

    public string? Descripcion { get; set; }

    public string? Datosadicionales { get; set; }

    public bool Activo { get; set; }

    public string Usuariocreacion { get; set; } = null!;

    public DateTime Fechacreacion { get; set; }

    public string? Usuariomodificacion { get; set; }

    public DateTime? Fechamodificacion { get; set; }

    public string Ipcreacion { get; set; } = null!;

    public string? Ipmodificacion { get; set; }

    public virtual TblAdministracionCatalogoDetalle IdcategoriaNavigation { get; set; } = null!;

    public virtual TblAdministracionCatalogoDetalle? IdcondicionNavigation { get; set; }

    public virtual TblAdministracionCatalogoDetalle IdestadoNavigation { get; set; } = null!;

    public virtual TblInventarioFactura? IdfacturaNavigation { get; set; }

    public virtual TblAdministracionCatalogoDetalle? IdtipogarantiaNavigation { get; set; }

    public virtual ICollection<TblInventarioAsignacionEquipo> TblInventarioAsignacionEquipos { get; set; } = new List<TblInventarioAsignacionEquipo>();

    public virtual ICollection<TblInventarioBajaEquipo> TblInventarioBajaEquipos { get; set; } = new List<TblInventarioBajaEquipo>();

    public virtual ICollection<TblInventarioCaracteristicaEquipo> TblInventarioCaracteristicaEquipos { get; set; } = new List<TblInventarioCaracteristicaEquipo>();

    public virtual ICollection<TblInventarioReparacionEquipo> TblInventarioReparacionEquipos { get; set; } = new List<TblInventarioReparacionEquipo>();
}
