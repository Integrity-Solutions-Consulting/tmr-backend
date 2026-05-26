namespace tmr_backend.Features.Lideres.DTOs.Request;

public record CrearLiderRequest(
    int Idpersona,
    int Idtipo,
    string Usuariocreacion,
    string Ipcreacion
);