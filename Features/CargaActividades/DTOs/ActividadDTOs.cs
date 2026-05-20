namespace tmr_backend.Features.CargaActividades.DTOs;

public record CrearActividadRequest(string Nombre, string Descripcion);

public record ActividadResponse(Guid Id, string Nombre, string Descripcion, bool Activo, DateTime FechaCreacion);

public record ActualizarActividadRequest(string Nombre, string Descripcion);
