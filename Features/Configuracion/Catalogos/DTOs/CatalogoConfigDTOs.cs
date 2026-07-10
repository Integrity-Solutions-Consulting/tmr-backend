namespace tmr_backend.Features.Configuracion.Catalogos.DTOs;

public record CreateCatalogoDetalleRequest(
    int idCatalogo,
    string codigoValor,
    string valor,
    string? descripcion,
    short? orden,
    string? valorExtra
);

public record UpdateCatalogoDetalleRequest(
    string valor,
    string? descripcion,
    short? orden,
    string? valorExtra,
    bool? activo,
    int? idCatalogo = null
);

public record CatalogoDetalleConfigResponse(
    int id,
    int idCatalogo,
    string codigoValor,
    string valor,
    string? descripcion,
    short? orden,
    string? valorExtra,
    bool activo
);

public record CatalogoMasterResponse(
    int id,
    string tipoCatalogo,
    string codigo,
    string? descripcion,
    bool activo
);

public record SuccessResponse(string Mensaje);
