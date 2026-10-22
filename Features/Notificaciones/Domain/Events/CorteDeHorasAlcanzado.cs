using tmr_backend.Infrastructure.Messaging;

namespace tmr_backend.Features.Notificaciones.Domain.Events;

public record CorteDeHorasAlcanzado(
    DateOnly InicioPeriodo,
    DateOnly FinPeriodo,
    DateTimeOffset FechaCorte) : IEvento;