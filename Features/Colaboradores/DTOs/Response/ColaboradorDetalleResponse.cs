namespace tmr_backend.Features.Colaboradores.DTOs.Response;

// DTO de salida para el DETALLE de un colaborador (modal "ver detalle").
// Trae todos los datos personales, laborales y la lista de proyectos.
public record ColaboradorDetalleResponse(
    int Id,
    string CodigoEmpleado,
    string Asociacion,
    string TipoContrato,
    bool Activo,

    // ── Datos laborales ───────────────────────────────
    string Departamento,
    DateOnly? FechaIngreso,
    string Cargo,
    int? AniosExperiencia,
    string Modalidad,
    string Categoria,

    // ── Datos personales ──────────────────────────────
    int IdPersona,
    string Nombres,
    string Apellidos,
    string NumeroIdentificacion,
    DateOnly? FechaNacimiento,
    string Genero,

    // ── Datos de contacto ─────────────────────────────
    string Email,
    string Telefono,
    string Direccion,

    // ── Proyectos asignados ───────────────────────────
    List<ProyectoAsignadoResponse> Proyectos
);

// Sub-DTO para cada proyecto que aparece en el detalle.
public record ProyectoAsignadoResponse(
    int Id,
    string Nombre,
    string Cliente,
    string Estado    
);