using TmrBackend.Infrastructure.Messaging;

namespace TmrBackend.Features.Notificaciones.Domain.Events;

public record CorteDeHorasAlcanzado(
    DateOnly InicioPeriodo,
    DateOnly FinPeriodo,
    DateTimeOffset FechaCorte) : IEvento;