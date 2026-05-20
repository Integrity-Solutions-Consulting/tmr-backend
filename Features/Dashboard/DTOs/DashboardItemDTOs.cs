namespace tmr_backend.Features.Dashboard.DTOs;

public record CrearDashboardItemRequest(string Nombre, string Descripcion);

public record DashboardItemResponse(Guid Id, string Nombre, string Descripcion, bool Activo, DateTime FechaCreacion);

public record ActualizarDashboardItemRequest(string Nombre, string Descripcion);
