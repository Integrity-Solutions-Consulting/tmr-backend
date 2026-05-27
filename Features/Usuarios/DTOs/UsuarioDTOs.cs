namespace tmr_backend.Features.Usuarios.DTOs;

/// <summary>
/// DTO para crear un nuevo usuario.
/// </summary>
public record CrearUsuarioRequest(string Nombre, string Descripcion);

/// <summary>
/// DTO para actualizar un usuario existente.
/// </summary>
public record ActualizarUsuarioRequest(string Nombre, string Descripcion);

/// <summary>
/// DTO para retornar datos de un usuario.
/// </summary>
public record UsuarioResponse(Guid Id, string Nombre, string Descripcion, bool Activo, DateTime FechaCreacion);
