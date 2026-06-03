namespace tmr_backend.Features.Colaboradores.DTOs.Response;

// DTO genérico para los dropdowns (selects) del frontend.
// Se usa para género, departamento, modalidad, categoría, tipo contrato, asociación.
// El frontend solo necesita el Id (para guardar) y el Valor (para mostrar).
public record CatalogoResponse(
    int Id,
    string Valor
);