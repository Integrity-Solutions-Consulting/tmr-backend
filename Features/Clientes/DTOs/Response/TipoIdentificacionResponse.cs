namespace tmr_backend.Features.Clientes.DTOs.Response;

// DTO para el dropdown de Tipo de Identificación (catálogo TID).
public record TipoIdentificacionResponse(
    int Id,
    string Valor
);