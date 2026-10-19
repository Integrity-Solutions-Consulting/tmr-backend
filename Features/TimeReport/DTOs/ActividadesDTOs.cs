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

// sm - Se comenta el resumen anterior (horas de hoy, semana y mes): Actividades ahora muestra las mismas métricas
// que Seguimiento, calculadas para el mes del calendario.
// public record ResumenHorasDto(
//     decimal HorasPorRegistrar,
//     decimal HorasRegistradas,
//     decimal HorasSemana,
//     decimal HorasMes
// );
public record ResumenHorasDto(
    decimal HorasPorRegistrar,
    decimal HorasRegistradas,
    decimal PromedioPorDia
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