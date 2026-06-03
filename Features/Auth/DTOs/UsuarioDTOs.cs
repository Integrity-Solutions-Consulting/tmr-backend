namespace tmr_backend.Features.Auth.DTOs;

public record CrearUsuarioRequest(string Nombre, string Descripcion);

public record UsuarioResponse(Guid Id, string Nombre, string Descripcion, bool Activo, DateTime FechaCreacion);

public record ActualizarUsuarioRequest(string Nombre, string Descripcion);
