using tmr_backend.Infrastructure.Database.Entities;

namespace tmr_backend.Features.Permisos.Services;

public static class PermisoAvisoVigencia
{
    public static bool EsVigente(CorreoPermisoEncolado evento, TblTimeReportSolicitud? solicitud)
    {
        if (solicitud is null || !solicitud.Activo) return false;
        if (evento.VersionSolicitud.HasValue &&
            evento.VersionSolicitud.Value.Ticks / 10 != (solicitud.FechaModificacion ?? solicitud.FechaCreacion).Ticks / 10) return false;
        var esperado = evento.EstadoSolicitud ?? evento.Evento switch
        {
            EventoNotificacionPermiso.SolicitudCreada or EventoNotificacionPermiso.ReenvioMagicLink => "PENDIENTE_JEFE",
            EventoNotificacionPermiso.AprobadaPorLider => "PENDIENTE_TH",
            EventoNotificacionPermiso.RechazadaPorLider => "RECHAZADA",
            EventoNotificacionPermiso.ConfirmadaPorRrhh => "APROBADA",
            _ => "OBSERVADA"
        };
        if (solicitud.Estado.Codigo != esperado
            && !(evento.Evento == EventoNotificacionPermiso.SolicitudCreada && solicitud.Estado.Codigo == "APROBADA"))
            return false;
        if (evento.IncluirTokenExterno)
            return !solicitud.TokenExternoUsadoEn.HasValue && solicitud.TokenExternoExpira > DateTime.UtcNow
                && !string.IsNullOrWhiteSpace(solicitud.TokenExterno)
                && (evento.Instantanea is null || evento.Instantanea.TokenExterno == solicitud.TokenExterno);
        return true;
    }
}
