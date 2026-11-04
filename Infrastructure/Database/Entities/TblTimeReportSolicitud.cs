using System;
using System.Collections.Generic;

namespace tmr_backend.Infrastructure.Database.Entities;

public class TblTimeReportSolicitud
{
    public int Id { get; set; }
    public string Codigo { get; set; } = null!;
    public int IdEmpleado { get; set; }
    public int? IdAsignacionProyecto { get; set; }
    public int? IdProyecto { get; set; }
    public int? IdCliente { get; set; }
    public int? IdLider { get; set; }
    public int IdTipoPermiso { get; set; }
    public int IdEstado { get; set; }
    public DateOnly FechaSolicitud { get; set; }
    public string Motivo { get; set; } = null!;
    public DateOnly FechaInicio { get; set; }
    public DateOnly FechaFin { get; set; }
    public DateOnly FechaRegreso { get; set; }
    public decimal DiasSolicitados { get; set; }
    public string? Horario { get; set; } = "COMPLETA";
    public string? DetalleHorario { get; set; }
    public decimal? HorasSolicitadas { get; set; }
    public string ImpactoTimeReport { get; set; } = "RESTA_META";
    public string? TokenExterno { get; set; }
    public DateTime? TokenExternoExpira { get; set; }
    public DateTime? TokenExternoUsadoEn { get; set; }
    public bool Activo { get; set; } = true;
    public string UsuarioCreacion { get; set; } = null!;
    public DateTime FechaCreacion { get; set; }
    public string? UsuarioModificacion { get; set; }
    public DateTime? FechaModificacion { get; set; }
    public string IpCreacion { get; set; } = null!;
    public string? IpModificacion { get; set; }

    public virtual TblAdministracionEmpleado Empleado { get; set; } = null!;
    public virtual TblTimeReportAsignacionProyecto? AsignacionProyecto { get; set; }
    public virtual TblTimeReportProyecto? Proyecto { get; set; }
    public virtual TblAdministracionCliente? Cliente { get; set; }
    public virtual TblAdministracionLider? Lider { get; set; }
    public virtual TblTimeReportTipoPermiso TipoPermiso { get; set; } = null!;
    public virtual TblTimeReportEstadoSolicitud Estado { get; set; } = null!;

    public virtual ICollection<TblTimeReportSolicitudRevision> Revisiones { get; set; } = new List<TblTimeReportSolicitudRevision>();
    public virtual ICollection<TblTimeReportSolicitudAdjunto> Adjuntos { get; set; } = new List<TblTimeReportSolicitudAdjunto>();
}

