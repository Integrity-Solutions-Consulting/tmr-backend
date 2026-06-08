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

public record ProyectoLookupDto(
    int Id,
    string Nombre
);

public record CalendarioActividadDto(
    int Id,
    int IdEmpleado,
    int? IdProyecto,
    string ProyectoNombre,
    int IdTipoActividad,
    string TipoActividadNombre,
    string? CodigoRequerimiento,
    decimal CantidadHoras,
    DateOnly FechaActividad,
    string DescripcionActividad,
    string? Notas,
    bool? EsBillable
);

public record ActualizarActividadDto(
    int? IdProyecto,
    int IdTipoActividad,
    string? CodigoRequerimiento,
    decimal CantidadHoras,
    DateOnly FechaActividad,
    string DescripcionActividad,
    string? Notas,
    bool? EsBillable
);