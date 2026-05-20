namespace tmr_backend.Features.Reportes.DTOs;

public record CrearReporteRequest(string Nombre, string Descripcion);

public record ReporteResponse(Guid Id, string Nombre, string Descripcion, bool Activo, DateTime FechaCreacion);

public record ActualizarReporteRequest(string Nombre, string Descripcion);
