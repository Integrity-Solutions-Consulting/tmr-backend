namespace tmr_backend.Features.Proyectos.DTOs;

public record CrearProyectoRequest(string Nombre, string Descripcion);

public record ProyectoResponse(Guid Id, string Nombre, string Descripcion, bool Activo, DateTime FechaCreacion);

public record ActualizarProyectoRequest(string Nombre, string Descripcion);
