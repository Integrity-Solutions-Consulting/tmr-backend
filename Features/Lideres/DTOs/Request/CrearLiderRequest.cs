namespace tmr_backend.Features.Lideres.DTOs.Request;

public record CrearLiderRequest(
    int? Idpersona,
    int Idtipo,
    string Nombres,
    string Apellidos,
    string? Email,
    string? Telefono,
    string? NumeroIdentificacion,
    string Usuariocreacion,
    string Ipcreacion
);