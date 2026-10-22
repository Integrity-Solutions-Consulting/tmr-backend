using tmr_backend.Infrastructure.Messaging;

namespace tmr_backend.Features.Notificaciones.Domain.Events;

public record NotificacionManualSolicitada(
    IReadOnlyList<int> IdsEmpleados,
    DateOnly InicioPeriodo,
    DateOnly FinPeriodo,
    int IdUsuarioEjecutor,
    int? IdProyecto = null) : IEvento;      // proyecto de la fila de Seguimiento; null = todas las horas