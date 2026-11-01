using System;
using System.Collections.Generic;

namespace tmr_backend.Infrastructure.Database.Entities;

public partial class TblTimeReportPermiso
{
    public int Id { get; set; }

    public string? Codigo { get; set; }

    public int Idempleado { get; set; }

    public int? Idproyecto { get; set; }

    public int? Idcliente { get; set; }

    public int? Idlider { get; set; }

    public int Idtipopermiso { get; set; }

    public int Idestadoaprobacion { get; set; }

    public DateOnly Fechainicio { get; set; }

    public DateOnly Fechafin { get; set; }

    public DateOnly? Fecharegreso { get; set; }

    public string Horario { get; set; } = "COMPLETA";

    public string? Detallehorario { get; set; }

    public decimal Totaldias { get; set; }

    public decimal? Totalhoras { get; set; }

    public bool? Espagado { get; set; }

    public string? Descripcion { get; set; }

    public string? Evidencianombre { get; set; }

    public string? Evidenciaurl { get; set; }

    public string? Evidenciamimetype { get; set; }

    public int? Aprobadopor { get; set; }

    public DateTime? Fechaaprobacion { get; set; }

    public bool Aprobadoporlider { get; set; }

    public DateTime? Fechaaprobacionlider { get; set; }

    public string? Metodolider { get; set; }

    public string? Comentariolider { get; set; }

    public string? Tokenexterno { get; set; }

    public DateTime? Tokenexternoexpira { get; set; }

    public bool Confirmadoporrrhh { get; set; }

    public int? Idempleadorrhh { get; set; }

    public DateTime? Fechaconfirmacionrrhh { get; set; }

    public string? Observacionrrhh { get; set; }

    public string Impactotimereport { get; set; } = "RESTA_META";

    public string? Observacion { get; set; }

    public bool Activo { get; set; }

    public string Usuariocreacion { get; set; } = null!;

    public DateTime Fechacreacion { get; set; }

    public string? Usuariomodificacion { get; set; }

    public DateTime? Fechamodificacion { get; set; }

    public string Ipcreacion { get; set; } = null!;

    public string? Ipmodificacion { get; set; }

    public virtual TblAdministracionEmpleado? AprobadoporNavigation { get; set; }

    public virtual TblAdministracionEmpleado IdempleadoNavigation { get; set; } = null!;

    public virtual TblTimeReportProyecto? IdproyectoNavigation { get; set; }

    public virtual TblAdministracionCliente? IdclienteNavigation { get; set; }

    public virtual TblAdministracionLider? IdliderNavigation { get; set; }

    public virtual TblAdministracionEmpleado? IdempleadorrhhNavigation { get; set; }

    public virtual TblAdministracionCatalogoDetalle IdestadoaprobacionNavigation { get; set; } = null!;

    public virtual TblAdministracionCatalogoDetalle IdtipopermisoNavigation { get; set; } = null!;
}
