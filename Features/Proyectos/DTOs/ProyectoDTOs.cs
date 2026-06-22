namespace tmr_backend.Features.Proyectos.DTOs;

public record CrearProyectoRequest(
    string? Codigo,
    string Nombre,
    string? Descripcion,
    int? IdCliente,
    string? Cliente,
    int? IdTipoProyecto,
    string? Tipo,
    int? IdLider,
    string? Lider,
    int? IdEstadoProyecto,    // ← nullable
    string? Estado,
    DateOnly? FechaInicio,
    DateOnly? FechaFin,
    decimal? Presupuesto,
    decimal? Horas,
    decimal? LiderCosto,
    decimal? LiderHoras,
    List<ProyectoRecursoRequest>? Recursos,
    List<ProyectoLiderRequest>? Lideres
);

public record ActualizarProyectoRequest(
    string? Codigo,
    string Nombre,
    string? Descripcion,
    int? IdCliente,
    string? Cliente,
    int? IdTipoProyecto,
    string? Tipo,
    int? IdLider,
    string? Lider,
    int? IdEstadoProyecto,    // ← nullable
    string? Estado,
    bool? Activo,
    DateOnly? FechaInicio,
    DateOnly? FechaFin,
    decimal? Presupuesto,
    decimal? Horas,
    decimal? LiderCosto,
    decimal? LiderHoras,
    List<ProyectoRecursoRequest>? Recursos,
    List<ProyectoLiderRequest>? Lideres
);

public record ProyectoLiderRequest(
    int? IdLider,
    string? Lider,
    decimal? LiderCosto,
    decimal? LiderHoras,
    List<ProyectoRecursoRequest>? Recursos
);

public record ProyectoResponse(
    int Id,
    string Codigo,
    string Nombre,
    string Descripcion,
    int? IdCliente,
    string Cliente,
    int? IdTipoProyecto,
    string Tipo,
    int? IdLider,
    string Lider,
    string CargoLider,
    decimal? CostoHoraLider,
    decimal? HorasLider,
    int IdEstadoProyecto,
    string Estado,
    DateOnly? FechaInicio,
    DateOnly? FechaFin,
    decimal? Presupuesto,
    decimal? Horas,
    int NumeroRecursos,
    bool Activo,
    DateTime FechaCreacion,
    List<ProyectoRecursoResponse> Recursos,
    List<ProyectoLiderResponse> Lideres
);

public record ProyectoLiderResponse(
    int? IdLider,
    string Lider,
    string CargoLider,
    decimal? CostoHoraLider,
    decimal? HorasLider,
    List<ProyectoRecursoResponse> Recursos
);

public record ProyectoRecursoRequest(
    int? IdEmpleado,
    string? Tipo,
    string Nombre,
    string? Rol,
    DateOnly? Entrada,
    DateOnly? Salida,
    decimal? CostoHora,
    decimal? Horas
);

public record ProyectoRecursoResponse(
    int Id,
    int? IdEmpleado,
    string Tipo,
    string Nombre,
    string Rol,
    DateOnly? Entrada,
    DateOnly? Salida,
    decimal? CostoHora,
    decimal? Horas,
    int? IdDepartamento
);

public record LookupDto(int Id, string Nombre);
public record CargoLookupDto(int Id, string Nombre, int? IdDepartamento);