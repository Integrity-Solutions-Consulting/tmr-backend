namespace tmr_backend.Features.Colaboradores.DTOs.Response;

public record PersonaResponse(
    int Id,
    string Nombres,
    string Apellidos,
    string NumeroIdentificacion,
    DateOnly? FechaNacimiento,
    int? IdGenero,
    string? Email,       
    string? Telefono,    
    string? Direccion     
);