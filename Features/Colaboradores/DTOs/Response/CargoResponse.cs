namespace tmr_backend.Features.Colaboradores.DTOs.Response;

// DTO para los cargos filtrados por departamento.
// Alimenta el segundo dropdown del flujo Departamento → Cargo.
public record CargoResponse(
    int Id,
    string NombreCargo
);    