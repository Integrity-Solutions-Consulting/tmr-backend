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
    string NumeroIdentificacion,
    bool Activo,
    DateTime Fechacreacion,
    // Correo institucional, si el líder también es colaborador. Solo para mostrar: Email sigue siendo el que se edita.
    string? EmailCorporativo = null
);

public record ContadoresLiderResponse(
    int Internos,
    int Externos,
    int Inactivos
);
public record PersonaDisponibleResponse(
    int Id,
    string Nombres,
    string Apellidos,
    string? Email,
    string? Telefono
);