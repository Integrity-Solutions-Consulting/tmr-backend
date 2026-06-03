namespace tmr_backend.Features.Lideres.DTOs.Request;

public record ActualizarLiderRequest(
    string Nombres,
    string Apellidos,
    string Email,
    string Telefono,
    int? Idtipo,
    bool Activo,
    string Usuariomodificacion,
    string Ipmodificacion
);