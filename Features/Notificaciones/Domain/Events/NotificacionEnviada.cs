using tmr_backend.Infrastructure.Messaging;

namespace tmr_backend.Features.Notificaciones.Domain.Events;

public record NotificacionEnviada(
    int IdEmpleado,
    decimal HorasFaltantes,
    DateOnly InicioPeriodo,
    DateOnly FinPeriodo,
    OrigenNotificacion Origen,
    DateTimeOffset? FechaCorte,
    int? IdUsuarioEjecutor,
    DateTimeOffset FechaEnvio,
    int? IdProyecto = null,
    string? Detalle = null) : IEvento;      // corte: "Proyecto A: 16 h · Proyecto B: 8 h"