using System;

namespace tmr_backend.Features.Reportes.DTOs;

public record PaginatedResponse<T>(
    System.Collections.Generic.IEnumerable<T> Data,
    int Total,
    int? AnioMinimo = null,
    int? AnioMaximo = null
);

// --- Reporte por Horas ---
public record ReporteHorasFiltroRequest(
    string? Cliente,
    int? Mes,
    int? Anio,
    int Page = 1,
    int PageSize = 10
);

public record ReporteHorasResponse(
    string Id,
    string Cliente,
    int Recursos,
    decimal Horas,
    string Mes,
    string Anio
);

// --- Reporte por Fechas ---
public record ReporteFechasFiltroRequest(
    DateTime? FechaInicio,
    DateTime? FechaFin,
    string? Cliente,
    string? Lider,
    int Page = 1,
    int PageSize = 10
);

public record ReporteFechasResponse(
    string Id,
    string Cliente,
    string Lider,
    string Recurso,
    string Cargo,
    DateTime FechaInicio,
    DateTime FechaFin
);
