namespace tmr_backend.Features.Lideres.DTOs.Response;

public record LiderResponse(
    Guid Id,
    string Codigo,
    string Tipo,
    string PrimerNombre,
    string Apellidos,
    string CorreoElectronico,
    string Telefono,
    Guid? ClienteId,
    string? NombreCliente,
    bool Activo,
    DateTime FechaCreacion
);

public record ContadoresLiderResponse(
    int Internos,
    int Externos,
    int Inactivos
);