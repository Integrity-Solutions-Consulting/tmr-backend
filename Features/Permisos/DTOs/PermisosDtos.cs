namespace tmr_backend.Features.Permisos.DTOs;

public sealed record ProyectoAsignadoPermisoDto(
    int IdProyecto,
    string ProyectoNombre,
    int? IdCliente,
    string ClienteNombre,
    int? IdLider,
    string LiderNombre,
    string LiderTipo, // "INT" | "EXT"
    string? LiderCorreo,
    int? IdAsignacionProyecto = null,
    bool EsAsignacionPropia = false);

public sealed record LiderDisponiblePermisoDto(
    int IdLider,
    string Nombre,
    string Tipo, // "INT" | "EXT"
    string? Correo,
    int? IdCliente,
    string? ClienteNombre);

public sealed record TipoPermisoCatalogoDto(
    int Id,
    string Codigo,
    string CodigoCorto,
    string Nombre,
    bool RequiereAdjunto,
    short Orden);

public sealed record ContextoColaboradorPermisoDto(
    int IdEmpleado,
    string CodigoEmpleado,
    string ApellidosNombres,
    string NumeroCedula,
    string Cargo,
    string? Area,
    string? Correo,
    decimal DiasDisponibles,
    int EnProceso,
    int Aprobados,
    decimal HorasAcumuladas,
    IReadOnlyList<ProyectoAsignadoPermisoDto> ProyectosAsignados,
    IReadOnlyList<LiderDisponiblePermisoDto> LideresDisponibles,
    bool EsNomina = true,
    string TipoContrato = "Nómina",
    IReadOnlyList<TipoPermisoCatalogoDto>? TiposPermiso = null);

public sealed record AdjuntoPermisoDto(
    int Id,
    string NombreOriginal,
    string ContentType,
    long TamanoBytes,
    string FechaCreacion);

public sealed record PermisoItemDto(
    int Id,
    string Codigo,
    int IdEmpleado,
    string Colaborador,
    string Cedula,
    string CodigoColaborador,
    string Cargo,
    string Area,
    int? IdCliente,
    string Cliente,
    int? IdProyecto,
    string Proyecto,
    int? IdLider,
    string LiderNombre,
    string LiderTipo, // "INT" | "EXT"
    string? LiderCorreo,
    string? LiderEmpresa,
    string TipoCodigo, // VAC, ENF, CIT, MAT, NAC, OTR, MTR, FAL
    string Tipo,
    string TipoOficial, // VACACIONES, LICENCIA_ENFERMEDAD, etc.
    string Motivo,
    string FechaSolicitud, // DD/MM/YYYY
    string DiaSolicitud,
    string MesSolicitud,
    string AnioSolicitud,
    string FechaInicio, // DD/MM/YYYY
    string FechaFin, // DD/MM/YYYY
    string FechaRegreso, // DD/MM/YYYY
    string Fechas, // Rango legible
    string FechaAprobacion, // DD/MM/YYYY o "Pendiente"
    string DiasSolicitados, // ej. "5 días laborables"
    decimal TotalDias,
    string Horario, // COMPLETA, MANANA, TARDE, OTROS
    string? DetalleHorario,
    string HorasSolicitadas, // ej. "40 horas"
    decimal TotalHoras,
    string Duracion,
    string? EvidenciaNombre,
    string? EvidenciaUrl,
    bool TieneEvidencia,
    bool AprobadoLider,
    string? FechaAprobacionLider,
    string MetodoLider,
    string? ComentarioLider,
    bool ConfirmadoRrhh,
    string? ResponsableRrhh,
    string? FechaConfirmacionRrhh,
    string? ObservacionRrhh,
    string ImpactoTimeReport, // RESTA_META, SUMA_REPORTADAS, SIN_IMPACTO
    string EstadoCodigo, // PEN, RRH, CON, OBS, REC
    string EstadoClase, // pendiente, rrhh, confirmado, observado, rechazado
    string EstadoTexto, // Pendiente Jefe, En Talento Humano, Confirmado, Observado, Rechazado
    string EstadoRrhh, // CONFIRMADO_LIDER, PENDIENTE_LIDER_EXT, APROBADO_RRHH, OBSERVADO_RRHH, RECHAZADO
    string EstadoLider, // Pendiente, Aprobado, Observado, Rechazado
    string? Comentarios,
    string? TraslapeAlerta,
    string? TokenExterno,
    IReadOnlyList<AdjuntoPermisoDto>? Adjuntos = null);

public sealed record CrearPermisoRequest(
    int? IdProyecto,
    int? IdCliente,
    int? IdLider,
    string TipoOficial,
    string FechaInicio, // YYYY-MM-DD o DD/MM/YYYY
    string FechaFin, // YYYY-MM-DD o DD/MM/YYYY
    string? FechaRegreso, // YYYY-MM-DD o DD/MM/YYYY
    string Horario, // COMPLETA, MANANA, TARDE, OTROS
    string? DetalleHorario,
    decimal? TotalDias,
    decimal? TotalHoras,
    string Motivo);

public sealed record DecisionLiderRequest(
    string Accion, // "aprobar" | "observar" | "rechazar"
    string? Comentario);

public sealed record DecisionRrhhRequest(
    string Accion, // "confirmar" | "devolver"
    string? ImpactoTimeReport, // "RESTA_META" | "SUMA_REPORTADAS" | "SIN_IMPACTO"
    string? Observacion);

public sealed record DecisionExternaRequest(
    string Accion, // "aprobar" | "rechazar"
    string? Comentario);
