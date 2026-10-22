using tmr_backend.Infrastructure.Messaging;

namespace tmr_backend.Features.Notificaciones.Domain.Events;

/// <summary>
/// Un aviso que correspondía pero no se envió por una regla de negocio
/// (sin horas pendientes, sin correo, dentro del umbral). Se registra para que el líder pueda saber el motivo.
/// Los automáticos llevan la fecha del corte para aparecer en el resumen del corte.
/// </summary>
public record NotificacionOmitida(
    int IdEmpleado,
    DateOnly InicioPeriodo,
    DateOnly FinPeriodo,
    int? IdUsuarioEjecutor,
    DateTimeOffset FechaEnvio,
    string Motivo,
    OrigenNotificacion Origen = OrigenNotificacion.Manual,
    DateTimeOffset? FechaCorte = null,
    decimal HorasFaltantes = 0m,
    int? IdProyecto = null) : IEvento;
