using System;
using tmr_backend.Infrastructure.Database.Entities;

namespace tmr_backend.Features.Notificaciones.Domain;

public class NotificacionUsuario
{
    public long Id { get; set; }
    public int IdUsuarioDestino { get; set; }
    public int? IdSolicitud { get; set; }
    public string Titulo { get; set; } = null!;
    public string Mensaje { get; set; } = null!;
    public string? UrlDestino { get; set; }
    public bool Leida { get; set; }
    public DateTime? FechaLectura { get; set; }
    public DateTime FechaCreacion { get; set; }

    public virtual TblAutenticacionUsuario UsuarioDestino { get; set; } = null!;
    public virtual TblTimeReportSolicitud? Solicitud { get; set; }
}

