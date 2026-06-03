namespace tmr_backend.Features.Configuracion.DiasFestivos.DTOs;

public record CreateFeriadoRequest(string nombreFeriado, DateOnly fechaFeriado, string tipoFeriado, bool esRecurrente, string? descripcion);
public record UpdateFeriadoRequest(string nombreFeriado, DateOnly fechaFeriado, string tipoFeriado, bool esRecurrente, string? descripcion);

public record FeriadoResponse(int id, string nombreFeriado, DateOnly fechaFeriado, string tipoFeriado, bool esRecurrente, string? descripcion, bool activo);

public record SuccessResponse(string Mensaje);
