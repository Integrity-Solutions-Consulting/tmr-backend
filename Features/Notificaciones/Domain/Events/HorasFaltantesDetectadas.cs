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
    IReadOnlyList<HorasProyecto>? Proyectos = null,              // formato anterior (faltantes por proyecto), ya no se llena
    IReadOnlyList<ProyectoColaborador>? ProyectosColaborador = null,   // todos sus proyectos del período con sus horas
    decimal HorasEsperadas = 0m,
    decimal HorasRegistradas = 0m,
    decimal JornadaDiaria = 0m) : IEvento;