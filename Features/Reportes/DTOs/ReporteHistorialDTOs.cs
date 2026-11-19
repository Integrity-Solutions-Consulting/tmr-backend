using System;
using System.Collections.Generic;

namespace tmr_backend.Features.Reportes.DTOs;

// --- Reporte por Colaborador / por Cliente (historial de asignaciones) ---

/// <summary>
/// Una asignación de un colaborador a un proyecto (una fila de tbl_time_report_asignacion_proyecto).
/// Se usa en el historial del reporte por Colaborador y en "Colaboradores en proyectos" del reporte por Cliente.
/// </summary>
public record AsignacionHistorialDto(
    int IdAsignacion,
    int IdEmpleado,
    string Colaborador,
    string Identificacion,
    string? Cargo,
    bool ColaboradorActivo,
    int? IdCliente,
    string Cliente,
    int IdProyecto,
    string? CodigoProyecto,
    string Proyecto,
    string? EstadoProyecto,
    bool ProyectoActivo,
    string? Rol,
    string? Lider,
    DateOnly? FechaInicio,
    DateOnly? FechaFin,
    // Vigente: la asignación sigue abierta hoy (activa, sin salida pasada y el colaborador sigue en la empresa).
    bool Vigente,
    // Días calendario entre la entrada y la salida (o hoy / salida de la empresa / fin real del proyecto si sigue abierta).
    int DiasTrabajados,
    // Horas registradas en actividades de ese proyecto dentro de las fechas de la asignación.
    decimal HorasRegistradas
);

/// <summary>Tiempo total de un colaborador con un cliente (todas sus asignaciones en proyectos de ese cliente).</summary>
public record ColaboradorClienteResumenDto(
    int IdEmpleado,
    string Colaborador,
    string Identificacion,
    string? Cargo,
    bool ColaboradorActivo,
    int? IdCliente,
    string Cliente,
    int Proyectos,
    string NombresProyectos,
    DateOnly? FechaInicio,
    // Null cuando la relación sigue en curso (EnCurso = true).
    DateOnly? FechaFin,
    bool EnCurso,
    // Días calendario sin contar dos veces los periodos en que tuvo asignaciones superpuestas.
    int DiasTrabajados,
    decimal HorasRegistradas
);

public record ReporteColaboradoresResponse(
    List<ColaboradorClienteResumenDto> Resumen,
    List<AsignacionHistorialDto> Asignaciones
);

public record ClienteResumenDto(
    int? IdCliente,
    string Cliente,
    string? Identificacion,
    bool ClienteActivo,
    int TotalProyectos,
    int ProyectosActivos,
    string NombresProyectos,
    // Colaboradores distintos que han estado en algún proyecto del cliente.
    int Colaboradores,
    int ColaboradoresVigentes
);

public record ClienteProyectoDto(
    int IdProyecto,
    int? IdCliente,
    string Cliente,
    string? Codigo,
    string Proyecto,
    string? Tipo,
    string? EstadoProyecto,
    bool ProyectoActivo,
    DateOnly? FechaInicio,
    DateOnly? FechaFin,
    int TotalColaboradores,
    int ColaboradoresVigentes,
    string NombresColaboradores
);

public record ReporteClientesResponse(
    List<ClienteResumenDto> Clientes,
    List<ClienteProyectoDto> Proyectos,
    List<AsignacionHistorialDto> Colaboradores
);
