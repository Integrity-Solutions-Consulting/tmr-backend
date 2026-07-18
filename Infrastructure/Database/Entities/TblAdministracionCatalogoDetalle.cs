using System;
using System.Collections.Generic;

namespace tmr_backend.Infrastructure.Database.Entities;

public partial class TblAdministracionCatalogoDetalle
{
    public int Id { get; set; }

    public int Idcatalogo { get; set; }

    public string Codigovalor { get; set; } = null!;

    public string Valor { get; set; } = null!;

    public string? Descripcion { get; set; }

    public short? Orden { get; set; }

    public string? Valorextra { get; set; }

    public bool Activo { get; set; }

    public string Usuariocreacion { get; set; } = null!;

    public DateTime Fechacreacion { get; set; }

    public string? Usuariomodificacion { get; set; }

    public DateTime? Fechamodificacion { get; set; }

    public string Ipcreacion { get; set; } = null!;

    public string? Ipmodificacion { get; set; }

    public virtual TblAdministracionCatalogo IdcatalogoNavigation { get; set; } = null!;

    public virtual ICollection<TblAdministracionCargo> TblAdministracionCargos { get; set; } = new List<TblAdministracionCargo>();

    public virtual ICollection<TblAdministracionCliente> TblAdministracionClientes { get; set; } = new List<TblAdministracionCliente>();

    public virtual ICollection<TblAdministracionEmpleado> TblAdministracionEmpleadoIdcategoriaempleadoNavigations { get; set; } = new List<TblAdministracionEmpleado>();

    public virtual ICollection<TblAdministracionEmpleado> TblAdministracionEmpleadoIdempresacatalogoNavigations { get; set; } = new List<TblAdministracionEmpleado>();

    public virtual ICollection<TblAdministracionEmpleado> TblAdministracionEmpleadoIdmodotrabajoNavigations { get; set; } = new List<TblAdministracionEmpleado>();

    public virtual ICollection<TblAdministracionEmpleado> TblAdministracionEmpleadoIdtipocontratoNavigations { get; set; } = new List<TblAdministracionEmpleado>();

    public virtual ICollection<TblAdministracionLider> TblAdministracionLiders { get; set; } = new List<TblAdministracionLider>();

    public virtual ICollection<TblAdministracionPersona> TblAdministracionPersonaIdgeneroNavigations { get; set; } = new List<TblAdministracionPersona>();

    public virtual ICollection<TblAdministracionPersona> TblAdministracionPersonaIdnacionalidadNavigations { get; set; } = new List<TblAdministracionPersona>();

    public virtual ICollection<TblAdministracionPersona> TblAdministracionPersonaIdtipoidentificacionNavigations { get; set; } = new List<TblAdministracionPersona>();

    public virtual ICollection<TblAutenticacionMenuRol> TblAutenticacionMenuRols { get; set; } = new List<TblAutenticacionMenuRol>();

    public virtual ICollection<TblAutenticacionPrivilegioRol> TblAutenticacionPrivilegioRolIdprivilegioNavigations { get; set; } = new List<TblAutenticacionPrivilegioRol>();

    public virtual ICollection<TblAutenticacionPrivilegioRol> TblAutenticacionPrivilegioRolIdrolNavigations { get; set; } = new List<TblAutenticacionPrivilegioRol>();

    public virtual ICollection<TblAutenticacionPrivilegioUsuario> TblAutenticacionPrivilegioUsuarios { get; set; } = new List<TblAutenticacionPrivilegioUsuario>();

    public virtual ICollection<TblInventarioBajaEquipo> TblInventarioBajaEquipos { get; set; } = new List<TblInventarioBajaEquipo>();

    public virtual ICollection<TblInventarioCaracteristicaEquipo> TblInventarioCaracteristicaEquipos { get; set; } = new List<TblInventarioCaracteristicaEquipo>();

    public virtual ICollection<TblInventarioEquipo> TblInventarioEquipoIdcategoriaNavigations { get; set; } = new List<TblInventarioEquipo>();

    public virtual ICollection<TblInventarioEquipo> TblInventarioEquipoIdcondicionNavigations { get; set; } = new List<TblInventarioEquipo>();

    public virtual ICollection<TblInventarioEquipo> TblInventarioEquipoIdestadoNavigations { get; set; } = new List<TblInventarioEquipo>();

    public virtual ICollection<TblInventarioEquipo> TblInventarioEquipoIdtipogarantiaNavigations { get; set; } = new List<TblInventarioEquipo>();

    public virtual ICollection<TblInventarioProveedor> TblInventarioProveedors { get; set; } = new List<TblInventarioProveedor>();

    public virtual ICollection<TblInventarioStockCategorium> TblInventarioStockCategoria { get; set; } = new List<TblInventarioStockCategorium>();

    public virtual ICollection<TblTimeReportPermiso> TblTimeReportPermisoIdestadoaprobacionNavigations { get; set; } = new List<TblTimeReportPermiso>();

    public virtual ICollection<TblTimeReportPermiso> TblTimeReportPermisoIdtipopermisoNavigations { get; set; } = new List<TblTimeReportPermiso>();

    public virtual ICollection<TblTimeReportProyecto> TblTimeReportProyectos { get; set; } = new List<TblTimeReportProyecto>();
}
