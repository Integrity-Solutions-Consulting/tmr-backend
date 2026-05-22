namespace tmr_backend.Features.Lideres.DTOs;

public record CrearLiderRequest(
    string Codigo,
    string Tipo,
    string PrimerNombre,
    string Apellidos,
    string CorreoElectronico,
    string Telefono,
    Guid? ClienteId
);

public record ActualizarLiderRequest(
    string Tipo,
    string PrimerNombre,
    string Apellidos,
    string CorreoElectronico,
    string Telefono,
    Guid? ClienteId,
    bool Activo
);

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