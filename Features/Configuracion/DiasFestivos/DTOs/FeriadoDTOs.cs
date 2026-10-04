namespace tmr_backend.Features.Configuracion.DiasFestivos.DTOs;

public record CreateFeriadoRequest(string nombreFeriado, DateOnly fechaFeriado, string tipoFeriado, bool esRecurrente, string? descripcion);
public record UpdateFeriadoRequest(string nombreFeriado, DateOnly fechaFeriado, string tipoFeriado, bool esRecurrente, string? descripcion);

// sm - Resultado de importar los feriados de un año: los creados y los omitidos (ya existían).
public record ImportarFeriadosResponse(int anio, List<string> creados, List<string> omitidos);

public record FeriadoResponse(int id, string nombreFeriado, DateOnly fechaFeriado, string tipoFeriado, bool esRecurrente, string? descripcion, bool activo);

public record SuccessResponse(string Mensaje);
