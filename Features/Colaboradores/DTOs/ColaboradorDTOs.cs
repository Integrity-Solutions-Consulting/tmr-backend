namespace tmr_backend.Features.Colaboradores.DTOs;

public record CrearColaboradorRequest(string Nombre, string Descripcion);

public record ColaboradorResponse(Guid Id, string Nombre, string Descripcion, bool Activo, DateTime FechaCreacion);

public record ActualizarColaboradorRequest(string Nombre, string Descripcion);
