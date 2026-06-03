namespace tmr_backend.Features.Auth.DTOs.Request;

public record RegisterRequest(
    int IdGenero,
    int IdNacionalidad,
    int IdTipoIdentificacion,
    string TipoIdentificacion,
    string? Numeroidentificacion,
    string Nombres,
    string Apellidos,
    string CorreoContacto,
    string TipoPersona,
    string FechaNacimiento,
    string? Telefono,
    string? Direccion,
    string Email,
    //string Password,
    string Usuario);
