using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace tmr_backend.Infrastructure.Database.Entities;

public partial class TblAdministracionPersona
{
    public int Id { get; set; }

    public string Numeroidentificacion { get; set; } = null!;

    public int? Idtipoidentificacion { get; set; }

    public int? Idgenero { get; set; }

    public int? Idnacionalidad { get; set; }

    public string Tipopersona { get; set; } = null!;

    [Column("nombres")]
    public string Nombres { get; set; } = null!;

    [Column("apellidos")]
    public string Apellidos { get; set; } = null!;

    public DateOnly? Fechanacimiento { get; set; }

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

    public virtual TblAdministracionCatalogoDetalle? IdgeneroNavigation { get; set; }

    public virtual TblAdministracionCatalogoDetalle? IdnacionalidadNavigation { get; set; }

    public virtual TblAdministracionCatalogoDetalle? IdtipoidentificacionNavigation { get; set; }

    public virtual ICollection<TblAdministracionEmpleado> TblAdministracionEmpleados { get; set; } = new List<TblAdministracionEmpleado>();

    public virtual ICollection<TblAdministracionLider> TblAdministracionLiders { get; set; } = new List<TblAdministracionLider>();

    public virtual ICollection<TblAutenticacionUsuario> TblAutenticacionUsuarios { get; set; } = new List<TblAutenticacionUsuario>();

}
