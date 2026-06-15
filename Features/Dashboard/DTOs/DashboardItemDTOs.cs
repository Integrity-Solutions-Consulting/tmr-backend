namespace tmr_backend.Features.Dashboard.DTOs;

public record CrearDashboardItemRequest(string Nombre, string Descripcion);

public record DashboardItemResponse(Guid Id, string Nombre, string Descripcion, bool Activo, DateTime FechaCreacion);

public record ActualizarDashboardItemRequest(string Nombre, string Descripcion);

// --- Real Dashboard Data DTOs ---

public record DashboardMetricasResponse(
    int TotalProyectos,
    decimal HorasReportadas,
    int ColaboradoresActivos,
    int ClientesActivos
);

public record DashboardProyectoResumenResponse(
    string Codigo,
    string Nombre,
    string Cliente,
    string Estado,
    decimal Horas,
    decimal Presupuesto,
    DateOnly? FechaFinPlaneada
);

public record DashboardHorasPorProyectoResponse(
    string Proyecto,
    decimal Horas,
    string Codigo,
    decimal HorasAsignadas
);

public record DashboardDataResponse(
    DashboardMetricasResponse Metricas,
    System.Collections.Generic.IEnumerable<DashboardProyectoResumenResponse> ProximosACerrar,
    System.Collections.Generic.IEnumerable<DashboardHorasPorProyectoResponse> HorasPorProyecto
);
