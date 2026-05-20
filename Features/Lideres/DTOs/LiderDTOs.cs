namespace tmr_backend.Features.Lideres.DTOs;

public record CrearLiderRequest(string Nombre, string Descripcion);

public record LiderResponse(Guid Id, string Nombre, string Descripcion, bool Activo, DateTime FechaCreacion);

public record ActualizarLiderRequest(string Nombre, string Descripcion);
