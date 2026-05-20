namespace tmr_backend.Features.Configuracion.DTOs;

public record CrearConfiguracionSistemaRequest(string Nombre, string Descripcion);

public record ConfiguracionSistemaResponse(Guid Id, string Nombre, string Descripcion, bool Activo, DateTime FechaCreacion);

public record ActualizarConfiguracionSistemaRequest(string Nombre, string Descripcion);
