using System;

namespace tmr_backend.Features.TimeReport.DTOs;

public record CrearActividadDto(
    int IdEmpleado,
    int? IdProyecto,
    int IdTipoActividad,
    string? CodigoRequerimiento,
    int CantidadHoras,
    DateOnly FechaActividad,
    string DescripcionActividad,
    string? Notas,
    bool? EsBillable
);

public record ActividadDiaDto(
    DateOnly Fecha,
    int TotalHoras
);

public record ResumenHorasDto(
    int HorasPorRegistrar,
    int HorasRegistradas,
    int HorasSemana,
    int HorasMes
);
