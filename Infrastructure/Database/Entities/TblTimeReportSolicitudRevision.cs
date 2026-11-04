using System;

namespace tmr_backend.Infrastructure.Database.Entities;

public class TblTimeReportSolicitudRevision
{
    public int Id { get; set; }
    public int IdSolicitud { get; set; }
    public string EtapaRevision { get; set; } = null!; // JEFE_INMEDIATO | TALENTO_HUMANO
    public int? IdUsuarioRevisor { get; set; }
    public string MetodoRevision { get; set; } = "CUENTA_TMR"; // CUENTA_TMR | MAGIC_LINK_EXTERNO
    public string Decision { get; set; } = null!; // APROBADO | OBSERVADO | RECHAZADO
    public DateTime FechaRevision { get; set; }
    public string? Comentario { get; set; }

    public virtual TblTimeReportSolicitud Solicitud { get; set; } = null!;
    public virtual TblAutenticacionUsuario? UsuarioRevisor { get; set; }
}

