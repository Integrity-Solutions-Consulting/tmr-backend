namespace tmr_backend.Features.Colaboradores.DTOs.Response;

// DTO de salida para el DETALLE de un colaborador.
// Trae todos los datos personales, laborales y la lista de proyectos.
// También devuelve IDs necesarios para precargar correctamente el modal editar.
public record ColaboradorDetalleResponse(
    int Id,
    string CodigoEmpleado,
    string Asociacion,
    string TipoContrato,
    bool Activo,
    
    // ── IDs necesarios para editar ─────────────────────
    int? IdEmpresaCatalogo,
    string? TipoPersona,
    int? IdTipoIdentificacion,
    int? IdGenero,
    int? IdNacionalidad,
    int? IdTipoContrato,
    int? IdDepartamento,
    int? IdCargo,
    int? IdModoTrabajo,
    int? IdCategoriaEmpleado,

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
    string? Nacionalidad,

    // ── Datos de contacto ─────────────────────────────
    string Email,
    string Telefono,
    string Direccion,

    // ── Proyectos asignados ───────────────────────────
    List<ProyectoAsignadoResponse> Proyectos,

    // ── CAMPOS PARA DATOS DE SALIDA ───────────────────────────
    DateOnly? FechaSalida,
    string? TipoSalida,
    string? CausaSalida,
    string? ComentarioSalida,
    string? ReemplazoNombre

);

// Sub-DTO para cada proyecto que aparece en el detalle.
public record ProyectoAsignadoResponse(
    int Id,
    string Nombre,
    string Cliente,
    string Estado
);