namespace tmr_backend.Features.Lideres.DTOs.Request;

public record CrearLiderRequest(
    string Codigo,
    string Tipo,
    string PrimerNombre,
    string Apellidos,
    string CorreoElectronico,
    string Telefono,
    Guid? ClienteId
);