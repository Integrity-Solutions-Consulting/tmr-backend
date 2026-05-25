namespace tmr_backend.Features.Lideres.DTOs.Response;

public record TipoLiderResponse(
    int Id,
    string Codigo,
    string Valor,
    string? Descripcion
);