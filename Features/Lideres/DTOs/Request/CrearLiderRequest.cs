namespace tmr_backend.Features.Lideres.DTOs.Request;

public record CrearLiderRequest(
    string Nombres,
    string Apellidos,
    string Email,
    string Telefono,
    string Tipopersona,
    int Idtipo,
    string Usuariocreacion,
    string Ipcreacion
);