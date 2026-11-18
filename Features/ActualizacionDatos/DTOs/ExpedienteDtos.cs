namespace tmr_backend.Features.ActualizacionDatos.DTOs;

public sealed record ExpedienteResponse(
    int Id,
    string CodigoEmpleado,
    string NombreCompleto,
    string Cargo,
    string Departamento,
    bool Activo,
    string Estado,
    GeneralExpedienteResponse General,
    bool ExpedienteRestringido,
    IReadOnlyList<ExpedienteFormacionResponse> Formaciones,
    IReadOnlyList<ExpedienteCertificacionResponse> Certificaciones,
    IReadOnlyList<ExpedienteArchivoResponse> Archivos,
    IReadOnlyList<ExpedienteSolicitudResponse> Solicitudes,
    InformacionFamiliarResponse? Familia = null);

public sealed record GeneralExpedienteResponse(
    string Identificacion,
    string? FechaNacimiento,
    string? Genero,
    string? Nacionalidad,
    string? Email,
    string? Telefono,
    string? Direccion,
    string? Asociacion,
    string? FechaIngreso,
    string? Modalidad,
    string? Categoria,
    int? AniosExperiencia,
    IReadOnlyList<ProyectoExpedienteResponse> Proyectos);

public sealed record ProyectoExpedienteResponse(int Id, string Nombre, string Cliente, string Estado, bool Activo);
public sealed record ExpedienteFormacionResponse(long Id, string Nivel, string Institucion, string Titulo, DateOnly? FechaInicio, DateOnly? FechaFin, IReadOnlyList<ExpedienteArchivoResponse>? Archivos = null);
public sealed record ExpedienteCertificacionResponse(long Id, string Nombre, string EntidadEmisora, DateOnly FechaEmision, DateOnly? FechaVencimiento, IReadOnlyList<ExpedienteArchivoResponse>? Archivos = null);
public sealed record ExpedienteArchivoResponse(long Id, long IdSolicitud, string NombreOriginal, string ContentType, long Tamano, DateTime Fecha, string Seccion = "General", long? ReferenciaId = null);
public sealed record ExpedienteSolicitudResponse(long Id, string Estado, string? Observacion, DateTime Fecha, int Version, string? Asunto = null, string? Descripcion = null, bool Enviada = false, IReadOnlyList<ExpedienteArchivoResponse>? Archivos = null);
