using TmrBackend.Infrastructure.Messaging;

namespace TmrBackend.Features.Notificaciones.Domain.Events;

public record HorasFaltantesDetectadas(
    int IdEmpleado,
    string Nombre,
    string Email,
    decimal HorasFaltantes,
    DateOnly InicioPeriodo,
    DateOnly FinPeriodo,
    OrigenNotificacion Origen,
    DateTimeOffset? FechaCorte,
    int? IdUsuarioEjecutor) : IEvento;