using System;

namespace tmr_backend.Features.TimeReport.DTOs;

public record CrearActividadDto(
    int IdEmpleado,
    int? IdProyecto,
    int IdTipoActividad,
    string? CodigoRequerimiento,
    decimal CantidadHoras,
    DateOnly FechaActividad,
    string DescripcionActividad,
    string? Notas,
    bool? EsBillable
);

public record ActividadDiaDto(
    DateOnly Fecha,
    decimal TotalHoras
);

public record ResumenHorasDto(
    decimal HorasPorRegistrar,
    decimal HorasRegistradas,
    decimal HorasSemana,
    decimal HorasMes
);
