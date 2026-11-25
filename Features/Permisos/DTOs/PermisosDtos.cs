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

public sealed record RrhhDisponiblePermisoDto(
    int IdUsuario,
    string Nombre,
    string? Cargo,
    string? Correo,
    bool EsPrincipal = false);

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
    IReadOnlyList<TipoPermisoCatalogoDto>? TiposPermiso = null,
    IReadOnlyList<RrhhDisponiblePermisoDto>? RrhhDisponibles = null,
    string RegimenVacaciones = "AFILIADO",
    bool TieneDerechoVacaciones = true,
    string? FechaInicioVacaciones = null,
    decimal DiasGanadosAcumulados = 0m,
    decimal DiasConsumidosVacaciones = 0m,
    decimal DiasAjusteVacaciones = 0m,
    decimal DiasProporcionalesCiclo = 0m,
    decimal MaximoAnticipoDias = 15m);

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
    IReadOnlyList<AdjuntoPermisoDto>? Adjuntos = null,
    string? AvisoNotificacion = null,
    string? VersionSolicitud = null,
    bool DescuentaVacaciones = false,
    bool EsRecuperable = false,
    bool EsAnticipada = false,
    bool EsHistorico = false,
    bool VerificadoRrhh = true,
    string? ObservacionMigracion = null);

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
    string Motivo,
    int? IdUsuarioRrhh = null,
    int? IdSolicitudObservada = null,
    string? VersionSolicitud = null);

public sealed record DecisionLiderRequest(
    string Accion, // "aprobar" | "observar" | "rechazar"
    string? Comentario, string? VersionSolicitud = null);

public sealed record DecisionRrhhRequest(
    string Accion, // "confirmar" | "devolver"
    string? ImpactoTimeReport, // "RESTA_META" | "SUMA_REPORTADAS" | "SIN_IMPACTO"
    string? Observacion, string? VersionSolicitud = null);

public sealed record DecisionExternaRequest(
    string Accion, // "aprobar" | "observar" | "rechazar"
    string? Comentario, string? VersionSolicitud = null);

public sealed record ColaboradorSelectorPermisoDto(
    int IdEmpleado,
    string CodigoEmpleado,
    string Cedula,
    string ApellidosNombres,
    string Regimen,
    string TipoContrato,
    string? Empresa,
    bool Activo);

public sealed record GuardarPermisoRrhhRequest(
    int IdEmpleado,
    string TipoOficial,
    string FechaInicio,         // YYYY-MM-DD o DD/MM/YYYY
    string FechaFin,            // YYYY-MM-DD o DD/MM/YYYY
    string? FechaRegreso,       // opcional
    decimal? TotalDias,         // opcional: si no se envía, se calcula automáticamente
    string? Horario,            // COMPLETA | MANANA | TARDE | OTROS
    string? DetalleHorario,
    string Motivo,
    bool DescuentaVacaciones,
    bool EsRecuperable,
    bool EsAnticipada,
    bool EsHistorico,
    bool VerificadoRrhh,
    string? ObservacionMigracion,
    string? EstadoCodigo = null,       // APROBADA, PENDIENTE_TH, etc. (opcional)
    string? ImpactoTimeReport = null); // RESTA_META, SUMA_REPORTADAS, SIN_IMPACTO

public sealed record AjusteVacacionesItemDto(
    int Id,
    string TipoAjuste,
    decimal DiasAjuste,
    bool? PermiteVacaciones,
    string Motivo,
    string UsuarioCreacion,
    string FechaCreacion,
    string? FechaAfiliacionBase = null);

public sealed record SaldoVacacionesColaboradorDto(
    int IdEmpleado,
    string CodigoEmpleado,
    string Cedula,
    string ApellidosNombres,
    string Cargo,
    string? Empresa,
    string TipoContrato,
    string Regimen, // AFILIADO | MIXTO | FACTURA
    bool TieneDerechoVacaciones,
    bool ExcepcionFacturaActiva,
    string? FechaIngresoOriginal,
    string? FechaInicioVacaciones,
    int AniosAntiguedad,
    decimal DiasGanadosAcumulados,
    decimal DiasProporcionalesCiclo,
    decimal DiasConsumidos,
    decimal DiasAjuste,
    decimal SaldoDisponible,
    int TotalRegistrosHistorial,
    int RegistrosPorVerificar,
    IReadOnlyList<AjusteVacacionesItemDto> Ajustes,
    string? OrigenFechaInicioVacaciones);

public sealed record CrearAjusteVacacionesRequest(
    string TipoAjuste, // "AJUSTE_DIAS" | "EXCEPCION_FACTURA" | "FECHA_AFILIACION"
    decimal DiasAjuste,
    bool? PermiteVacaciones,
    string Motivo,
    string? FechaAfiliacionBase = null); // YYYY-MM-DD o DD/MM/YYYY (solo FECHA_AFILIACION)
