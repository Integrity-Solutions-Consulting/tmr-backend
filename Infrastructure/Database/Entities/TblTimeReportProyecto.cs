using System;
using System.Collections.Generic;

namespace tmr_backend.Infrastructure.Database.Entities;

public partial class TblTimeReportProyecto
{
    public int Id { get; set; }

    public int? Idcliente { get; set; }

    public int Idestadoproyecto { get; set; }

    public int? Idtipoproyecto { get; set; }

    public int? Idlider { get; set; }

    public string? Codigo { get; set; }

    public string Nombre { get; set; } = null!;

    public string? Descripcion { get; set; }

    public DateOnly? Fechainicioplaneada { get; set; }

    public DateOnly? Fechafinplaneada { get; set; }

    public DateOnly? Fechainicioreal { get; set; }

    public DateOnly? Fechafinreal { get; set; }

    public DateOnly? Fechainicioespera { get; set; }

    public DateOnly? Fechafinespera { get; set; }

    public string? Observacion { get; set; }

    public decimal? Presupuesto { get; set; }

    public decimal? Horasasignadas { get; set; }

    public decimal? Lidercosto { get; set; }   // NUEVO

    public decimal? Liderhoras { get; set; }   // NUEVO

    public bool Activo { get; set; }

    public string Usuariocreacion { get; set; } = null!;

    public DateTime Fechacreacion { get; set; }

    public string? Usuariomodificacion { get; set; }

    public DateTime? Fechamodificacion { get; set; }

    public string Ipcreacion { get; set; } = null!;

    public string? Ipmodificacion { get; set; }

    public virtual TblAdministracionCliente? IdclienteNavigation { get; set; }

    public virtual TblAdministracionCatalogoDetalle IdestadoproyectoNavigation { get; set; } = null!;

    public virtual TblAdministracionLider? IdliderNavigation { get; set; }

    public virtual TblTimeReportTipoProyecto? IdtipoproyectoNavigation { get; set; }

    public virtual ICollection<TblTimeReportActividadDiarium> TblTimeReportActividadDiaria { get; set; } = new List<TblTimeReportActividadDiarium>();

    public virtual ICollection<TblTimeReportEmpleadoProyecto> TblTimeReportEmpleadoProyectos { get; set; } = new List<TblTimeReportEmpleadoProyecto>();

    public virtual ICollection<TblTimeReportProyeccionHorasProyecto> TblTimeReportProyeccionHorasProyectos { get; set; } = new List<TblTimeReportProyeccionHorasProyecto>();
}