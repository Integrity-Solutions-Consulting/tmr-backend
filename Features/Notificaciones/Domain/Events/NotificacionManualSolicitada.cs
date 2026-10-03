using TmrBackend.Infrastructure.Messaging;

namespace TmrBackend.Features.Notificaciones.Domain.Events;

public record NotificacionManualSolicitada(
    IReadOnlyList<int> IdsEmpleados,
    DateOnly InicioPeriodo,
    DateOnly FinPeriodo,
    int IdUsuarioEjecutor) : IEvento;