using TmrBackend.Infrastructure.Messaging;

namespace TmrBackend.Features.Notificaciones.Domain.Events;

public record NotificacionFallida(
    int IdEmpleado,
    decimal HorasFaltantes,
    DateOnly InicioPeriodo,
    DateOnly FinPeriodo,
    OrigenNotificacion Origen,
    DateTimeOffset? FechaCorte,
    int? IdUsuarioEjecutor,
    DateTimeOffset FechaEnvio,
    string Error) : IEvento;