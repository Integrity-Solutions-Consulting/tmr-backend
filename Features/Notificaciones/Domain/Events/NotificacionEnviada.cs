using TmrBackend.Infrastructure.Messaging;

namespace TmrBackend.Features.Notificaciones.Domain.Events;

public record NotificacionEnviada(
    int IdEmpleado,
    decimal HorasFaltantes,
    DateOnly InicioPeriodo,
    DateOnly FinPeriodo,
    OrigenNotificacion Origen,
    DateTimeOffset? FechaCorte,
    int? IdUsuarioEjecutor,
    DateTimeOffset FechaEnvio) : IEvento;