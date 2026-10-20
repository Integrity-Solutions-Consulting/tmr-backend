using tmr_backend.Infrastructure.Messaging;

namespace tmr_backend.Features.Notificaciones.Domain.Events;

public record HorasFaltantesDetectadas(
    int IdEmpleado,
    string Nombre,
    string Email,
    decimal HorasFaltantes,
    DateOnly InicioPeriodo,
    DateOnly FinPeriodo,
    OrigenNotificacion Origen,
    DateTimeOffset? FechaCorte,
    int? IdUsuarioEjecutor,
    int? IdProyecto = null,                 // solo avisos manuales por proyecto
    string? NombreProyecto = null,
    IReadOnlyList<HorasProyecto>? Proyectos = null) : IEvento;   // corte: detalle por proyecto (8 h cada uno)