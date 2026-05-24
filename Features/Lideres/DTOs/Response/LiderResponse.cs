namespace tmr_backend.Features.Lideres.DTOs.Response;

public record LiderResponse(
    int Id,
    string Nombres,
    string Apellidos,
    string? Email,
    string? Telefono,
    string Tipopersona,
    int? Idtipo,
    string? TipoNombre,
    bool Activo,
    DateTime Fechacreacion
);

public record ContadoresLiderResponse(
    int Internos,
    int Externos,
    int Inactivos
);