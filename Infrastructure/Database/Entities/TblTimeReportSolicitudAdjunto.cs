using System;

namespace tmr_backend.Infrastructure.Database.Entities;

public class TblTimeReportSolicitudAdjunto
{
    public int Id { get; set; }
    public int IdSolicitud { get; set; }
    public string NombreOriginal { get; set; } = null!;
    public string BucketName { get; set; } = null!;
    public string ObjectKey { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public long TamanoBytes { get; set; }
    public string? Sha256 { get; set; }
    public int SubidoPorUsuarioId { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime FechaCreacion { get; set; }

    public virtual TblTimeReportSolicitud Solicitud { get; set; } = null!;
    public virtual TblAutenticacionUsuario SubidoPorUsuario { get; set; } = null!;
}

