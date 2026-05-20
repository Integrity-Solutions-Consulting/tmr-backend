namespace tmr_backend.Features.TimeReport.DTOs;

public record CrearRegistroTiempoRequest(string Nombre, string Descripcion);

public record RegistroTiempoResponse(Guid Id, string Nombre, string Descripcion, bool Activo, DateTime FechaCreacion);

public record ActualizarRegistroTiempoRequest(string Nombre, string Descripcion);
