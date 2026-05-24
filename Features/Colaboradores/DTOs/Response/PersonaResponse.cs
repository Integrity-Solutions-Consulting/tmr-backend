namespace tmr_backend.Features.Colaboradores.DTOs.Response;

// DTO para el ComboBox "Persona" del frontend.
// Al seleccionar una persona existente, se autocompletan sus datos personales.
public record PersonaResponse(
    int Id,
    string Nombres,
    string Apellidos,
    string NumeroIdentificacion,
    DateOnly? FechaNacimiento,
    int? IdGenero
);