namespace tmr_backend.Features.Colaboradores.DTOs.Request;

public record RegistrarSalidaRequest(
    DateOnly FechaSalida,
    int IdTipoSalida,
    int IdCausaSalida,
    string? Comentario,
    int? IdEmpleadoReemplazo
);