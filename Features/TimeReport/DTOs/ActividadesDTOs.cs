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

// sm - Actividades muestra las mismas métricas que Seguimiento (antes mostraba horas de hoy, semana y mes),
// calculadas para el mes del calendario.
public record ResumenHorasDto(
    decimal HorasPorRegistrar,
    decimal HorasRegistradas,
    decimal PromedioPorDia,
    // sm - Jornada diaria del colaborador (8 h, o 6 h si es pasante), para que Actividades pueda mostrarle
    // su progreso de horas al registrar (ver CalculoHorasPeriodo).
    decimal HorasJornada = 8m
);

public record ProyectoLookupDto(
    int Id,
    string Nombre,
    string? Codigo,
    // sm - Vigencia del colaborador en el proyecto (para validar la fecha en Agregar actividad al elegirla):
    // entrada y salida de su asignación (null = sin límite) y su salida de la empresa si está Inactivo.
    DateOnly? FechaEntrada = null,
    DateOnly? FechaSalida = null,
    DateOnly? FechaSalidaColaborador = null
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