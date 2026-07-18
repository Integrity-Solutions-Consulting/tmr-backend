namespace tmr_backend.Features.Auth.Register.DTOs;

public sealed record RegisterUserRequest(
    int IdRol,
    int IdGenero,
    int IdNacionalidad,
    int IdTipoIdentificacion,
    string TipoIdentificacion,
    string Numeroidentificacion,
    string Nombres,
    string Apellidos,
    string CorreoContacto,
    string TipoPersona,
    string FechaNacimiento,
    string Telefono,
    string Direccion,
    string Email,
    string Usuario);
