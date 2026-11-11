using System.Net;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using tmr_backend.Features.Notificaciones.Infrastructure.Apprise;
using tmr_backend.Features.Permisos.DTOs;
using tmr_backend.Features.Permisos.Endpoints;
using tmr_backend.Infrastructure.Database;
using tmr_backend.Infrastructure.Messaging;
using tmr_backend.Infrastructure.Shared;

namespace tmr_backend.Features.Permisos.Services;

public enum EventoNotificacionPermiso
{
    SolicitudCreada,
    AprobadaPorLider,
    ObservadaPorLider,
    RechazadaPorLider,
    ConfirmadaPorRrhh,
    DevueltaPorRrhh,
    ReenvioMagicLink
}

public enum RolDestinatarioCorreoPermiso
{
    Colaborador,
    JefeInmediato,
    TalentoHumano
}

/// <summary>
/// Evento liviano del Outbox (~180 bytes) para despachar correos de Permisos de forma asíncrona
/// con reintentos automáticos sin almacenar HTML renderizado en PostgreSQL.
/// </summary>
public sealed record CorreoPermisoEncolado(
    int IdSolicitud,
    string FolioSolicitud,
    string DestinatarioOriginal,
    string NombreDestinatario,
    RolDestinatarioCorreoPermiso RolDestinatario,
    EventoNotificacionPermiso Evento,
    bool IncluirTokenExterno = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Asunto = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Html = null,
    PermisoItemDto? Instantanea = null,
    DateTime? VersionSolicitud = null,
    string? EstadoSolicitud = null,
    string? ReintentadoPor = null,
    DateTimeOffset? FechaReintento = null,
    Guid? EnvioAnterior = null) : IEvento;

/// <summary>
/// Handler del bus de mensajería (Outbox + EventDispatcher) que consulta la solicitud actualizada,
/// renderiza la plantilla HTML en memoria y despacha el correo vía Apprise o SMTP con reintentos automáticos.
/// </summary>
public sealed class EnviarCorreoPermisoHandler : IEventHandler<CorreoPermisoEncolado>
{
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<EnviarCorreoPermisoHandler> _logger;
    private readonly IAppriseNotificationService? _appriseService;
    private readonly IEmailService? _emailService;

    public EnviarCorreoPermisoHandler(
        ApplicationDbContext db,
        IConfiguration configuration,
        IWebHostEnvironment env,
        ILogger<EnviarCorreoPermisoHandler> logger,
        IAppriseNotificationService? appriseService = null,
        IEmailService? emailService = null)
    {
        _db = db;
        _configuration = configuration;
        _env = env;
        _logger = logger;
        _appriseService = appriseService;
        _emailService = emailService;
    }

    public async Task HandleAsync(CorreoPermisoEncolado evento, CancellationToken ct)
    {
        var destinatario = evento.DestinatarioOriginal.Trim();
        if (string.IsNullOrWhiteSpace(destinatario))
            return;

        var vigente = await PermisosEndpoints.ConsultarSolicitudesBase(_db)
            .FirstOrDefaultAsync(s => s.Id == evento.IdSolicitud && s.Activo, ct);
        if (!PermisoAvisoVigencia.EsVigente(evento, vigente))
            throw new EventoObsoletoException();

        string asunto;
        string html;

        // Compatibilidad hacia atrás por si existiera algún evento histórico con HTML pre-renderizado
        if (!string.IsNullOrWhiteSpace(evento.Html) && !string.IsNullOrWhiteSpace(evento.Asunto))
        {
            asunto = evento.Asunto;
            html = evento.Html;
        }
        else
        {
            var solicitud = await PermisosEndpoints.ConsultarSolicitudesBase(_db)
                .FirstOrDefaultAsync(s => s.Id == evento.IdSolicitud && s.Activo, ct);

            if (solicitud is null)
            {
                _logger.LogWarning(
                    "Solicitud {IdSolicitud} ({Folio}) no encontrada o inactiva al procesar correo del Outbox.",
                    evento.IdSolicitud,
                    evento.FolioSolicitud);
                return;
            }

            var dto = evento.Instantanea ?? PermisosEndpoints.MapearSolicitudDto(solicitud, null);
            if (evento.IncluirTokenExterno)
            {
                dto = dto with { TokenExterno = solicitud.TokenExterno };
            }

            var plantilla = await PermisosNotificacionService.CargarPlantillaHtmlCoreAsync(_env, ct);
            var frontendUrl = (_configuration["EmailSettings:FrontendUrl"] ?? "http://localhost:4200").TrimEnd('/');
            var logoUrl = PermisosNotificacionService.ObtenerLogoUrlCore(_configuration, frontendUrl);

            (asunto, html) = PermisosNotificacionService.ConstruirMensajeCorreo(
                plantilla,
                dto,
                evento.RolDestinatario,
                evento.NombreDestinatario,
                evento.Evento,
                frontendUrl,
                logoUrl);
        }

        var overrideEmail = _configuration["EmailSettings:OverrideEmailRecipient"];
        if (!string.IsNullOrWhiteSpace(overrideEmail))
        {
            _logger.LogInformation(
                "REDIRECCIÓN DE CORREO PERMISOS ({Folio}): destinatario {Original} reemplazado por {Override}",
                evento.FolioSolicitud,
                destinatario,
                overrideEmail);
            asunto = $"[PRUEBA para: {destinatario}] {asunto}";
            destinatario = overrideEmail;
        }

        if (_appriseService is not null)
        {
            try
            {
                await _appriseService.SendEmailAsync(destinatario, asunto, html, ct);
                return;
            }
            catch (Exception ex) when (_emailService is not null)
            {
                _logger.LogWarning(
                    ex,
                    "Fallo al enviar correo de permiso ({Folio}) por Apprise a {Destinatario}; intentando respaldo por SMTP.",
                    evento.FolioSolicitud,
                    destinatario);
            }
        }

        if (_emailService is null)
            throw new InvalidOperationException("No hay un proveedor de correo disponible.");
        await _emailService.SendEmailAsync(destinatario, asunto, html);
    }
}

public interface IPermisosNotificacionService
{
    Task<bool> NotificarCambioEstadoAsync(
        PermisoItemDto permiso,
        string? correoColaborador,
        EventoNotificacionPermiso evento,
        CancellationToken ct = default,
        int? idUsuarioRrhhElegido = null);
}

public sealed class PermisosNotificacionService : IPermisosNotificacionService
{
    private readonly ApplicationDbContext _db;
    private readonly IEventBus _eventBus;
    private readonly HashSet<string> _destinatariosEncolados = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<PermisosNotificacionService> _logger;

    public PermisosNotificacionService(
        ApplicationDbContext db,
        IEventBus eventBus,
        ILogger<PermisosNotificacionService> logger)
    {
        _db = db;
        _eventBus = eventBus;
        _logger = logger;
    }

    public async Task<bool> NotificarCambioEstadoAsync(
        PermisoItemDto permiso,
        string? correoColaborador,
        EventoNotificacionPermiso evento,
        CancellationToken ct = default,
        int? idUsuarioRrhhElegido = null)
    {
        var folio = permiso.FolioOrCodigo();
        var todosEncolados = true;
        _destinatariosEncolados.Clear();

        // Resolver destinatario(s) de Talento Humano respetando la selección del colaborador (o Líder de TH por defecto)
        var usuariosRrhh = await ObtenerDestinatariosRrhhAsync(permiso.Id, idUsuarioRrhhElegido, ct);

        switch (evento)
        {
            case EventoNotificacionPermiso.SolicitudCreada:
            {
                if (!string.IsNullOrWhiteSpace(correoColaborador))
                {
                    todosEncolados &= await EncolarCorreoIndividualAsync(
                        permiso.Id,
                        folio,
                        correoColaborador,
                        permiso.Colaborador,
                        RolDestinatarioCorreoPermiso.Colaborador,
                        evento,
                        incluirTokenExterno: false,
                        ct);
                }

                if (!string.IsNullOrWhiteSpace(permiso.LiderCorreo))
                {
                    var esExterno = string.Equals(permiso.LiderTipo, "EXT", StringComparison.OrdinalIgnoreCase) && permiso.EstadoClase != "confirmado";
                    todosEncolados &= await EncolarCorreoIndividualAsync(
                        permiso.Id,
                        folio,
                        permiso.LiderCorreo,
                        permiso.LiderNombre,
                        RolDestinatarioCorreoPermiso.JefeInmediato,
                        evento,
                        incluirTokenExterno: esExterno,
                        ct);
                }

                foreach (var rrhh in usuariosRrhh.Where(u => !string.IsNullOrWhiteSpace(u.Email)))
                {
                    todosEncolados &= await EncolarCorreoIndividualAsync(
                        permiso.Id,
                        folio,
                        rrhh.Email,
                        string.IsNullOrWhiteSpace(rrhh.Nombre) ? "Talento Humano ISC" : rrhh.Nombre,
                        RolDestinatarioCorreoPermiso.TalentoHumano,
                        evento,
                        incluirTokenExterno: false,
                        ct);
                }
                break;
            }

            case EventoNotificacionPermiso.ReenvioMagicLink:
            {
                if (!string.IsNullOrWhiteSpace(permiso.LiderCorreo))
                {
                    todosEncolados &= await EncolarCorreoIndividualAsync(
                        permiso.Id,
                        folio,
                        permiso.LiderCorreo,
                        permiso.LiderNombre,
                        RolDestinatarioCorreoPermiso.JefeInmediato,
                        evento,
                        incluirTokenExterno: true,
                        ct);
                }
                break;
            }

            case EventoNotificacionPermiso.AprobadaPorLider:
            case EventoNotificacionPermiso.ObservadaPorLider:
            case EventoNotificacionPermiso.RechazadaPorLider:
            {
                if (!string.IsNullOrWhiteSpace(correoColaborador))
                {
                    todosEncolados &= await EncolarCorreoIndividualAsync(
                        permiso.Id,
                        folio,
                        correoColaborador,
                        permiso.Colaborador,
                        RolDestinatarioCorreoPermiso.Colaborador,
                        evento,
                        incluirTokenExterno: false,
                        ct);
                }

                foreach (var rrhh in usuariosRrhh.Where(u => !string.IsNullOrWhiteSpace(u.Email)))
                {
                    todosEncolados &= await EncolarCorreoIndividualAsync(
                        permiso.Id,
                        folio,
                        rrhh.Email,
                        string.IsNullOrWhiteSpace(rrhh.Nombre) ? "Talento Humano ISC" : rrhh.Nombre,
                        RolDestinatarioCorreoPermiso.TalentoHumano,
                        evento,
                        incluirTokenExterno: false,
                        ct);
                }
                break;
            }

            case EventoNotificacionPermiso.ConfirmadaPorRrhh:
            case EventoNotificacionPermiso.DevueltaPorRrhh:
            {
                if (!string.IsNullOrWhiteSpace(correoColaborador))
                {
                    todosEncolados &= await EncolarCorreoIndividualAsync(
                        permiso.Id,
                        folio,
                        correoColaborador,
                        permiso.Colaborador,
                        RolDestinatarioCorreoPermiso.Colaborador,
                        evento,
                        incluirTokenExterno: false,
                        ct);
                }

                if (!string.IsNullOrWhiteSpace(permiso.LiderCorreo))
                {
                    todosEncolados &= await EncolarCorreoIndividualAsync(
                        permiso.Id,
                        folio,
                        permiso.LiderCorreo,
                        permiso.LiderNombre,
                        RolDestinatarioCorreoPermiso.JefeInmediato,
                        evento,
                        incluirTokenExterno: false,
                        ct);
                }
                break;
            }
        }

        return todosEncolados;
    }

    internal static (string Asunto, string Html) ConstruirMensajeCorreo(
        string plantilla,
        PermisoItemDto permiso,
        RolDestinatarioCorreoPermiso rolDestinatario,
        string nombreDestinatario,
        EventoNotificacionPermiso evento,
        string frontendUrl,
        string logoUrl)
    {
        var folio = permiso.FolioOrCodigo();
        var nombreEfectivo = string.IsNullOrWhiteSpace(nombreDestinatario) ? "Colaborador" : nombreDestinatario;

        switch (evento)
        {
            case EventoNotificacionPermiso.SolicitudCreada:
            {
                if (rolDestinatario == RolDestinatarioCorreoPermiso.Colaborador)
                {
                    var html = RenderizarCorreo(
                        plantilla,
                        permiso,
                        logoUrl,
                        nombreDestinatario: nombreEfectivo,
                        rolDestinatario: "Colaborador Solicitante",
                        badgeRolBg: "#eff6ff",
                        badgeRolColor: "#1d4ed8",
                        subtituloCabecera: permiso.EstadoClase == "confirmado" ? "Registro de Permiso Informativo" : "Confirmación de Registro de Solicitud",
                        mensajePersonalizado: permiso.EstadoClase == "confirmado" ? $"Tu permiso por <strong>{WebUtility.HtmlEncode(permiso.Tipo)}</strong> ({WebUtility.HtmlEncode(folio)}) ha sido registrado automáticamente. Este tipo de permiso no requiere aprobación y se ha enviado una copia informativa a tu Jefe Inmediato (<strong>{WebUtility.HtmlEncode(permiso.LiderNombre)}</strong>) y a <strong>Talento Humano (RRHH)</strong>." : $"Tu solicitud de <strong>{WebUtility.HtmlEncode(permiso.Tipo)}</strong> ({WebUtility.HtmlEncode(folio)}) ha sido registrada exitosamente y se envió a tu Jefe Inmediato (<strong>{WebUtility.HtmlEncode(permiso.LiderNombre)}</strong>) para su revisión.",
                        ctaUrl: $"{frontendUrl}/permisos?solicitud={permiso.Id}",
                        ctaTexto: "Ver seguimiento en Mis Permisos",
                        notaPieBoton: "Recibirás un correo personalizado en cada cambio de estado de tu solicitud.");

                    return ($"[Permisos ISC] Solicitud {folio} registrada ({permiso.Tipo})", html);
                }

                if (rolDestinatario == RolDestinatarioCorreoPermiso.JefeInmediato)
                {
                    var esExterno = string.Equals(permiso.LiderTipo, "EXT", StringComparison.OrdinalIgnoreCase) && permiso.EstadoClase != "confirmado";
                    var ctaLiderUrl = esExterno && !string.IsNullOrWhiteSpace(permiso.TokenExterno)
                        ? $"{frontendUrl}/permisos/revision-externa?token={Uri.EscapeDataString(permiso.TokenExterno)}"
                        : $"{frontendUrl}/permisos/equipo?solicitud={permiso.Id}";

                    var html = RenderizarCorreo(
                        plantilla,
                        permiso,
                        logoUrl,
                        nombreDestinatario: nombreEfectivo,
                        rolDestinatario: esExterno ? "Jefe Inmediato Externo · Enlace Directo" : "Jefe Inmediato / Líder TMR",
                        badgeRolBg: esExterno ? "#f3e8ff" : "#e0f2fe",
                        badgeRolColor: esExterno ? "#7e22ce" : "#0369a1",
                        subtituloCabecera: permiso.EstadoClase == "confirmado" ? "Copia Informativa de Permiso (No requiere aprobación)" : "Nueva Solicitud Pendiente de tu Aprobación",
                        mensajePersonalizado: esExterno
                            ? $"El colaborador <strong>{WebUtility.HtmlEncode(permiso.Colaborador)}</strong> asignado a <strong>{WebUtility.HtmlEncode(permiso.Cliente)}</strong> ha registrado una solicitud de <strong>{WebUtility.HtmlEncode(permiso.Tipo)}</strong>. Puedes revisarla y aprobarla o rechazarla directamente mediante el siguiente enlace seguro sin necesidad de iniciar sesión:"
                            : permiso.EstadoClase == "confirmado" ? $"El colaborador <strong>{WebUtility.HtmlEncode(permiso.Colaborador)}</strong> de tu equipo ha registrado un permiso por <strong>{WebUtility.HtmlEncode(permiso.Tipo)}</strong> para el período <strong>{WebUtility.HtmlEncode(permiso.Fechas)}</strong>. Este tipo de permiso no requiere aprobación; se envía esta copia informativa para tu conocimiento." : $"El colaborador <strong>{WebUtility.HtmlEncode(permiso.Colaborador)}</strong> de tu equipo ha registrado una solicitud de <strong>{WebUtility.HtmlEncode(permiso.Tipo)}</strong> que requiere tu revisión y aprobación.",
                        ctaUrl: ctaLiderUrl,
                        ctaTexto: esExterno ? "Revisar y Aprobar Solicitud" : "Revisar Solicitud en Aprobación Líder",
                        notaPieBoton: esExterno
                            ? "Enlace único de aprobación externa (válido por 72 horas)."
                            : "Ingresa a la bandeja de Aprobación Líder en TMR para gestionar la solicitud.");

                    return (permiso.EstadoClase == "confirmado" ? $"[Copia Informativa] Permiso {folio} ({permiso.Tipo}) — {permiso.Colaborador}" : $"[Acción Requerida] Aprobación de permiso {folio} — {permiso.Colaborador}", html);
                }

                var htmlRrhh = RenderizarCorreo(
                    plantilla,
                    permiso,
                    logoUrl,
                    nombreDestinatario: nombreEfectivo,
                    rolDestinatario: "Talento Humano (RRHH)",
                    badgeRolBg: "#fef3c7",
                    badgeRolColor: "#b45309",
                    subtituloCabecera: permiso.EstadoClase == "confirmado" ? "Copia Informativa para Talento Humano" : "Nueva Solicitud en Flujo de Aprobación",
                    mensajePersonalizado: permiso.EstadoClase == "confirmado" ? $"Se ha registrado un permiso por <strong>{WebUtility.HtmlEncode(permiso.Tipo)}</strong> por parte de <strong>{WebUtility.HtmlEncode(permiso.Colaborador)}</strong> para el período <strong>{WebUtility.HtmlEncode(permiso.Fechas)}</strong>. Este tipo de permiso no requiere aprobación ni descuenta días de vacaciones; se envía esta copia para registro de Talento Humano." : $"Se ha registrado una nueva solicitud de <strong>{WebUtility.HtmlEncode(permiso.Tipo)}</strong> por parte de <strong>{WebUtility.HtmlEncode(permiso.Colaborador)}</strong>. Actualmente se encuentra en espera de aprobación de su Jefe Inmediato (<strong>{WebUtility.HtmlEncode(permiso.LiderNombre)}</strong>).",
                    ctaUrl: $"{frontendUrl}/permisos/rrhh?solicitud={permiso.Id}",
                    ctaTexto: "Ver Bandeja de Validación RRHH",
                    notaPieBoton: "Serás notificado nuevamente apenas el Jefe Inmediato registre su aprobación.");

                return (permiso.EstadoClase == "confirmado" ? $"[Copia Informativa RRHH] Permiso {folio} ({permiso.Tipo}) — {permiso.Colaborador}" : $"[Talento Humano] Nueva solicitud {folio} en curso — {permiso.Colaborador}", htmlRrhh);
            }

            case EventoNotificacionPermiso.ReenvioMagicLink:
            {
                var ctaLiderUrl = !string.IsNullOrWhiteSpace(permiso.TokenExterno)
                    ? $"{frontendUrl}/permisos/revision-externa?token={Uri.EscapeDataString(permiso.TokenExterno)}"
                    : $"{frontendUrl}/permisos/revision-externa";

                var html = RenderizarCorreo(
                    plantilla,
                    permiso,
                    logoUrl,
                    nombreDestinatario: nombreEfectivo,
                    rolDestinatario: "Jefe Inmediato Externo · Enlace Directo",
                    badgeRolBg: "#f3e8ff",
                    badgeRolColor: "#7e22ce",
                    subtituloCabecera: "Recordatorio de Aprobación Pendiente",
                    mensajePersonalizado: $"Talento Humano de Integrity Solutions te reenvía el enlace directo para revisar y confirmar la solicitud de <strong>{WebUtility.HtmlEncode(permiso.Tipo)}</strong> de <strong>{WebUtility.HtmlEncode(permiso.Colaborador)}</strong>.",
                    ctaUrl: ctaLiderUrl,
                    ctaTexto: "Revisar solicitud",
                    notaPieBoton: "Enlace renovado por 72 horas. No requiere usuario ni contraseña.");

                return ($"[Recordatorio] Aprobación pendiente {folio} — {permiso.Colaborador}", html);
            }

            case EventoNotificacionPermiso.AprobadaPorLider:
            case EventoNotificacionPermiso.ObservadaPorLider:
            case EventoNotificacionPermiso.RechazadaPorLider:
            {
                var accionTexto = evento switch
                {
                    EventoNotificacionPermiso.AprobadaPorLider => "aprobada por tu Jefe Inmediato",
                    EventoNotificacionPermiso.ObservadaPorLider => "observada por tu Jefe Inmediato",
                    _ => "rechazada por tu Jefe Inmediato"
                };

                if (rolDestinatario == RolDestinatarioCorreoPermiso.Colaborador)
                {
                    var html = RenderizarCorreo(
                        plantilla,
                        permiso,
                        logoUrl,
                        nombreDestinatario: nombreEfectivo,
                        rolDestinatario: "Colaborador Solicitante",
                        badgeRolBg: "#eff6ff",
                        badgeRolColor: "#1d4ed8",
                        subtituloCabecera: "Actualización de Estado · Revisión de Jefe Inmediato",
                        mensajePersonalizado: $"Tu solicitud <strong>{WebUtility.HtmlEncode(folio)}</strong> ha sido <strong>{accionTexto}</strong> ({WebUtility.HtmlEncode(permiso.LiderNombre)})." +
                            (evento == EventoNotificacionPermiso.AprobadaPorLider
                                ? " Ahora ha pasado a la bandeja de <strong>Talento Humano (RRHH)</strong> para su confirmación final."
                                : ""),
                        ctaUrl: $"{frontendUrl}/permisos?solicitud={permiso.Id}",
                        ctaTexto: "Consultar mi Solicitud y PDF",
                        notaPieBoton: "Puedes visualizar el documento oficial actualizado desde tu bandeja Mis Permisos.");

                    return ($"[Permisos ISC] Solicitud {folio} {accionTexto}", html);
                }

                var htmlRrhh = RenderizarCorreo(
                    plantilla,
                    permiso,
                    logoUrl,
                    nombreDestinatario: nombreEfectivo,
                    rolDestinatario: "Talento Humano (RRHH)",
                    badgeRolBg: "#fef3c7",
                    badgeRolColor: "#b45309",
                    subtituloCabecera: evento == EventoNotificacionPermiso.AprobadaPorLider
                        ? "Solicitud Lista para Confirmación de RRHH"
                        : "Actualización de Solicitud por Jefe Inmediato",
                    mensajePersonalizado: evento == EventoNotificacionPermiso.AprobadaPorLider
                        ? $"El Jefe Inmediato (<strong>{WebUtility.HtmlEncode(permiso.LiderNombre)}</strong>) aprobó la solicitud <strong>{WebUtility.HtmlEncode(folio)}</strong> de <strong>{WebUtility.HtmlEncode(permiso.Colaborador)}</strong> ({WebUtility.HtmlEncode(permiso.MetodoLider)}). Ya puedes revisarla y dar la confirmación definitiva de Talento Humano."
                        : $"El Jefe Inmediato (<strong>{WebUtility.HtmlEncode(permiso.LiderNombre)}</strong>) marcó la solicitud <strong>{WebUtility.HtmlEncode(folio)}</strong> de <strong>{WebUtility.HtmlEncode(permiso.Colaborador)}</strong> como <strong>{WebUtility.HtmlEncode(permiso.EstadoTexto)}</strong>.",
                    ctaUrl: $"{frontendUrl}/permisos/rrhh?solicitud={permiso.Id}",
                    ctaTexto: "Revisar Solicitud en Validación RRHH",
                    notaPieBoton: "Al confirmar se actualizará el formulario oficial PDF y su impacto en Time Report.");

                var asuntoRrhh = evento == EventoNotificacionPermiso.AprobadaPorLider
                    ? $"[Confirmación RRHH Requerida] Solicitud {folio} — {permiso.Colaborador}"
                    : $"[Talento Humano] Solicitud {folio} {permiso.EstadoTexto} — {permiso.Colaborador}";

                return (asuntoRrhh, htmlRrhh);
            }

            default:
            {
                var confirmada = evento == EventoNotificacionPermiso.ConfirmadaPorRrhh;

                if (rolDestinatario == RolDestinatarioCorreoPermiso.Colaborador)
                {
                    var html = RenderizarCorreo(
                        plantilla,
                        permiso,
                        logoUrl,
                        nombreDestinatario: nombreEfectivo,
                        rolDestinatario: "Colaborador Solicitante",
                        badgeRolBg: "#eff6ff",
                        badgeRolColor: "#1d4ed8",
                        subtituloCabecera: confirmada
                            ? "Confirmación Definitiva de Talento Humano"
                            : "Solicitud Observada / Devuelta por Talento Humano",
                        mensajePersonalizado: confirmada
                            ? $"¡Tu solicitud <strong>{WebUtility.HtmlEncode(folio)}</strong> ({WebUtility.HtmlEncode(permiso.Tipo)}) ha sido <strong>confirmada definitivamente por Talento Humano</strong>! Puedes consultar el resultado y descargar el PDF desde TMR."
                            : $"Talento Humano ha revisado tu solicitud <strong>{WebUtility.HtmlEncode(folio)}</strong> y ha registrado una observación para subsanación.",
                        ctaUrl: $"{frontendUrl}/permisos?solicitud={permiso.Id}",
                        ctaTexto: confirmada ? "Descargar PDF Oficial en Mis Permisos" : "Revisar Observación en Mis Permisos",
                        notaPieBoton: confirmada ? "La solicitud está confirmada." : "Consulta el comentario de Talento Humano en tu solicitud.");

                    var asuntoColab = confirmada
                        ? $"[Confirmado] Solicitud {folio} aprobada por Talento Humano"
                        : $"[Observado] Solicitud {folio} devuelta por Talento Humano";

                    return (asuntoColab, html);
                }

                var htmlLider = RenderizarCorreo(
                    plantilla,
                    permiso,
                    logoUrl,
                    nombreDestinatario: nombreEfectivo,
                    rolDestinatario: "Jefe Inmediato / Líder",
                    badgeRolBg: "#e0f2fe",
                    badgeRolColor: "#0369a1",
                    subtituloCabecera: confirmada
                        ? "Constancia de Confirmación RRHH"
                        : "Aviso de Devolución por RRHH",
                    mensajePersonalizado: confirmada
                        ? $"Te informamos que Talento Humano ha confirmado definitivamente la solicitud <strong>{WebUtility.HtmlEncode(folio)}</strong> del colaborador <strong>{WebUtility.HtmlEncode(permiso.Colaborador)}</strong> para el período <strong>{WebUtility.HtmlEncode(permiso.Fechas)}</strong>."
                        : $"Talento Humano devolvió con observaciones la solicitud <strong>{WebUtility.HtmlEncode(folio)}</strong> del colaborador <strong>{WebUtility.HtmlEncode(permiso.Colaborador)}</strong>.",
                    ctaUrl: permiso.LiderTipo == "EXT" ? "" : $"{frontendUrl}/permisos/equipo?solicitud={permiso.Id}",
                    ctaTexto: "Ver solicitud",
                    notaPieBoton: "Notificación de seguimiento para coordinación del equipo.");

                return ($"[Seguimiento Equipo] Solicitud {folio} ({permiso.Colaborador}) — {permiso.EstadoTexto}", htmlLider);
            }
        }
    }

    private static string RenderizarCorreo(
        string plantilla,
        PermisoItemDto p,
        string logoUrl,
        string nombreDestinatario,
        string rolDestinatario,
        string badgeRolBg,
        string badgeRolColor,
        string subtituloCabecera,
        string mensajePersonalizado,
        string ctaUrl,
        string ctaTexto,
        string notaPieBoton)
    {
        var (estadoBg, estadoColor, estadoBorder) = p.EstadoClase switch
        {
            "confirmado" => ("#dcfce7", "#166534", "#86efac"),
            "rechazado" => ("#fee2e2", "#991b1b", "#fca5a5"),
            "rrhh" => ("#dbeafe", "#1e40af", "#93c5fd"),
            _ => ("#fef3c7", "#92400e", "#fcd34d")
        };

        const string paso1Bg = "#16a34a";
        const string paso1Color = "#ffffff";
        var paso1Detalle = $"Registrado ({p.FechaSolicitud})";

        string paso2Bg;
        string paso2Color = "#ffffff";
        string paso2Detalle;
        if (p.AprobadoLider)
        {
            paso2Bg = "#16a34a";
            paso2Detalle = $"Aprobado ({p.FechaAprobacionLider ?? p.FechaAprobacion})";
        }
        else if (p.EstadoClase == "rechazado")
        {
            paso2Bg = "#dc2626";
            paso2Detalle = "Rechazado";
        }
        else if (p.EstadoCodigo == "OBS")
        {
            paso2Bg = "#d97706";
            paso2Detalle = "Observado";
        }
        else
        {
            paso2Bg = "#f59e0b";
            paso2Detalle = "En espera de aprobación";
        }

        string paso3Bg;
        string paso3Color = "#ffffff";
        string paso3Detalle;
        if (p.ConfirmadoRrhh)
        {
            paso3Bg = "#16a34a";
            paso3Detalle = $"Confirmado ({p.FechaConfirmacionRrhh ?? p.FechaAprobacion})";
        }
        else if (p.EstadoCodigo == "OBS")
        {
            paso3Bg = "#d97706";
            paso3Detalle = "Observado / Devuelto";
        }
        else if (p.EstadoClase == "rechazado")
        {
            paso3Bg = "#94a3b8";
            paso3Detalle = "Cancelado";
        }
        else if (p.AprobadoLider)
        {
            paso3Bg = "#2563eb";
            paso3Detalle = "En revisión de RRHH";
        }
        else
        {
            paso3Bg = "#cbd5e1";
            paso3Color = "#475569";
            paso3Detalle = "Pendiente";
        }

        var comentarios = p.ObservacionRrhh ?? p.ComentarioLider ?? p.Comentarios;
        var filaComentarios = string.IsNullOrWhiteSpace(comentarios)
            ? ""
            : $@"<tr style='background-color: #fffbeb;'>
                  <td style='padding: 8px 12px; border-top: 1px solid #e2e8f0; color: #92400e; font-weight: 600;'>Comentarios / Observaciones</td>
                  <td style='padding: 8px 12px; border-top: 1px solid #e2e8f0; color: #78350f;'>{WebUtility.HtmlEncode(comentarios)}</td>
                </tr>";

        var clienteProyecto = string.IsNullOrWhiteSpace(p.Proyecto) || p.Proyecto == p.Cliente
            ? p.Cliente
            : $"{p.Cliente} ({p.Proyecto})";

        if (string.IsNullOrWhiteSpace(ctaUrl))
        {
            var inicio = plantilla.IndexOf("<a href=\"{{CTA_URL}}\"", StringComparison.Ordinal);
            var fin = inicio < 0 ? -1 : plantilla.IndexOf("</a>", inicio, StringComparison.Ordinal);
            if (fin >= inicio && inicio >= 0) plantilla = plantilla.Remove(inicio, fin + 4 - inicio);
        }
        return plantilla
            .Replace("{{TITULO_CORREO}}", WebUtility.HtmlEncode(subtituloCabecera))
            .Replace("{{LOGO_URL}}", logoUrl)
            .Replace("{{SUBTITULO_CABECERA}}", WebUtility.HtmlEncode(subtituloCabecera))
            .Replace("{{FOLIO}}", WebUtility.HtmlEncode(p.FolioOrCodigo()))
            .Replace("{{ROL_DESTINATARIO}}", WebUtility.HtmlEncode(rolDestinatario))
            .Replace("{{BADGE_ROL_BG}}", badgeRolBg)
            .Replace("{{BADGE_ROL_COLOR}}", badgeRolColor)
            .Replace("{{ESTADO_BG}}", estadoBg)
            .Replace("{{ESTADO_COLOR}}", estadoColor)
            .Replace("{{ESTADO_BORDER}}", estadoBorder)
            .Replace("{{ESTADO_ACTUAL_TEXTO}}", WebUtility.HtmlEncode(p.EstadoTexto))
            .Replace("{{NOMBRE_DESTINATARIO}}", WebUtility.HtmlEncode(nombreDestinatario))
            .Replace("{{MENSAJE_PERSONALIZADO}}", mensajePersonalizado)
            .Replace("{{PASO1_BG}}", paso1Bg)
            .Replace("{{PASO1_COLOR}}", paso1Color)
            .Replace("{{PASO1_DETALLE}}", WebUtility.HtmlEncode(paso1Detalle))
            .Replace("{{PASO2_BG}}", paso2Bg)
            .Replace("{{PASO2_COLOR}}", paso2Color)
            .Replace("{{PASO2_DETALLE}}", WebUtility.HtmlEncode(paso2Detalle))
            .Replace("{{PASO3_BG}}", paso3Bg)
            .Replace("{{PASO3_COLOR}}", paso3Color)
            .Replace("{{PASO3_DETALLE}}", WebUtility.HtmlEncode(paso3Detalle))
            .Replace("{{COLABORADOR}}", WebUtility.HtmlEncode(p.Colaborador))
            .Replace("{{CEDULA}}", WebUtility.HtmlEncode(p.Cedula))
            .Replace("{{CARGO}}", WebUtility.HtmlEncode(p.Cargo))
            .Replace("{{CLIENTE_PROYECTO}}", WebUtility.HtmlEncode(clienteProyecto))
            .Replace("{{LIDER_NOMBRE}}", WebUtility.HtmlEncode(p.LiderNombre))
            .Replace("{{TIPO_PERMISO}}", WebUtility.HtmlEncode(p.Tipo))
            .Replace("{{FECHAS}}", WebUtility.HtmlEncode(p.Fechas))
            .Replace("{{FECHA_REGRESO}}", WebUtility.HtmlEncode(p.FechaRegreso))
            .Replace("{{DURACION}}", WebUtility.HtmlEncode($"{p.DiasSolicitados} ({p.HorasSolicitadas})"))
            .Replace("{{HORARIO}}", WebUtility.HtmlEncode(p.Horario))
            .Replace("{{MOTIVO}}", WebUtility.HtmlEncode(p.Motivo))
            .Replace("{{FILA_COMENTARIOS}}", filaComentarios)
            .Replace("{{CTA_URL}}", WebUtility.HtmlEncode(ctaUrl))
            .Replace("{{CTA_TEXTO}}", WebUtility.HtmlEncode(ctaTexto))
            .Replace("{{NOTA_PIE_BOTON}}", WebUtility.HtmlEncode(notaPieBoton))
            .Replace("{{ANIO_ACTUAL}}", DateTime.UtcNow.Year.ToString());
    }

    private sealed record DestinatarioRrhh(string Email, string Nombre);

    private async Task<List<DestinatarioRrhh>> ObtenerDestinatariosRrhhAsync(
        int idSolicitud,
        int? idUsuarioRrhhElegido,
        CancellationToken ct)
    {
        var idTarget = idUsuarioRrhhElegido;
        if (idTarget is null or <= 0 && idSolicitud > 0)
        {
            idTarget = await _db.NotificacionesUsuario
                .AsNoTracking()
                .Where(n => n.IdSolicitud == idSolicitud && n.UrlDestino == "/permisos/rrhh")
                .OrderBy(n => n.Id)
                .Select(n => (int?)n.IdUsuarioDestino)
                .FirstOrDefaultAsync(ct);
        }

        if (idTarget is > 0)
        {
            var usuarioElegido = await _db.TblAutenticacionUsuarios
                .AsNoTracking()
                .Include(u => u.IdpersonaNavigation)
                .FirstOrDefaultAsync(u => u.Id == idTarget.Value && u.Activo, ct);

            if (usuarioElegido is not null && await PermisosEndpoints.EsUsuarioRrhhValidoAsync(_db, usuarioElegido.Id, ct))
            {
                string? emailCorp = null;
                if (usuarioElegido.Idpersona.HasValue)
                {
                    emailCorp = await _db.TblAdministracionEmpleados
                        .AsNoTracking()
                        .Where(e => e.Idpersona == usuarioElegido.Idpersona.Value && e.Activo)
                        .Select(e => e.Emailcorporativo)
                        .FirstOrDefaultAsync(ct);
                }

                var emailFinal = !string.IsNullOrWhiteSpace(emailCorp)
                    ? emailCorp.Trim()
                    : !string.IsNullOrWhiteSpace(usuarioElegido.Email)
                        ? usuarioElegido.Email.Trim()
                        : usuarioElegido.IdpersonaNavigation?.Email?.Trim() ?? "";

                if (!string.IsNullOrWhiteSpace(emailFinal))
                {
                    var nombreFinal = usuarioElegido.IdpersonaNavigation is { } p
                        ? $"{p.Nombres} {p.Apellidos}".Trim()
                        : "Talento Humano ISC";
                    return [new DestinatarioRrhh(emailFinal, nombreFinal)];
                }
            }
        }

        var lideresTh = await _db.TblAdministracionEmpleados
            .AsNoTracking()
            .Include(e => e.IdpersonaNavigation)
                .ThenInclude(p => p.TblAutenticacionUsuarios)
            .Include(e => e.IdcargoNavigation)
            .Where(e => e.Activo
                        && e.IdcargoNavigation != null
                        && e.IdcargoNavigation.Activo
                        && (e.IdcargoNavigation.Nombrecargo.ToUpper() == "LÍDER DE TALENTO HUMANO"
                            || e.IdcargoNavigation.Nombrecargo.ToUpper() == "LIDER DE TALENTO HUMANO"))
            .ToListAsync(ct);

        var resultadoLiderTh = lideresTh
            .Select(e =>
            {
                var emailUsuario = e.IdpersonaNavigation?.TblAutenticacionUsuarios
                    .FirstOrDefault(u => u.Activo)?.Email;
                var email = !string.IsNullOrWhiteSpace(e.Emailcorporativo)
                    ? e.Emailcorporativo.Trim()
                    : !string.IsNullOrWhiteSpace(emailUsuario)
                        ? emailUsuario.Trim()
                        : e.IdpersonaNavigation?.Email?.Trim() ?? "";
                var nombre = e.IdpersonaNavigation is not null
                    ? $"{e.IdpersonaNavigation.Nombres} {e.IdpersonaNavigation.Apellidos}".Trim()
                    : "Líder de Talento Humano";
                return new DestinatarioRrhh(email, nombre);
            })
            .Where(d => !string.IsNullOrWhiteSpace(d.Email))
            .DistinctBy(d => d.Email.ToLowerInvariant())
            .ToList();

        if (resultadoLiderTh.Count > 0)
            return resultadoLiderTh;

        return await _db.TblAutenticacionUsuarioRols
            .AsNoTracking()
            .Where(ur => ur.Activo
                         && ur.IdusuarioNavigation.Activo
                         && ur.IdrolNavigation.Activo
                         && (ur.IdrolNavigation.Nombre.ToUpper() == "RECURSOS HUMANOS"
                             || ur.IdrolNavigation.Nombre.ToUpper() == "RECURSOS_HUMANOS"))
            .Select(ur => new DestinatarioRrhh(
                ur.IdusuarioNavigation.Email,
                ur.IdusuarioNavigation.IdpersonaNavigation != null
                    ? (ur.IdusuarioNavigation.IdpersonaNavigation.Nombres + " " + ur.IdusuarioNavigation.IdpersonaNavigation.Apellidos).Trim()
                    : ""))
            .Distinct()
            .ToListAsync(ct);
    }

    private async Task<bool> EncolarCorreoIndividualAsync(
        int idSolicitud,
        string folio,
        string destinatarioOriginal,
        string nombreDestinatario,
        RolDestinatarioCorreoPermiso rolDestinatario,
        EventoNotificacionPermiso evento,
        bool incluirTokenExterno,
        CancellationToken ct)
    {
        try
        {
            if (!_destinatariosEncolados.Add(destinatarioOriginal.Trim())) return true;
            var solicitud = await PermisosEndpoints.ConsultarSolicitudesBase(_db)
                .FirstAsync(s => s.Id == idSolicitud, ct);
            var snapshot = PermisosEndpoints.MapearSolicitudDto(solicitud, null);
            if (incluirTokenExterno) snapshot = snapshot with { TokenExterno = solicitud.TokenExterno };
            await _eventBus.PublishAsync(
                new CorreoPermisoEncolado(
                    IdSolicitud: idSolicitud,
                    FolioSolicitud: folio,
                    DestinatarioOriginal: destinatarioOriginal.Trim(),
                    NombreDestinatario: nombreDestinatario,
                    RolDestinatario: rolDestinatario,
                    Evento: evento,
                    IncluirTokenExterno: incluirTokenExterno,
                    Instantanea: snapshot,
                    VersionSolicitud: solicitud.FechaModificacion ?? solicitud.FechaCreacion,
                    EstadoSolicitud: solicitud.Estado.Codigo),
                ct);
            return true;
        }
        catch (Exception ex)
        {
            if (_db.Database.CurrentTransaction is not null) throw;
            _logger.LogWarning(
                ex,
                "No se pudo encolar notificación de permiso {Folio} ({Evento}) para {Destinatario}",
                folio,
                evento,
                destinatarioOriginal);
            return false;
        }
    }

    internal static string ObtenerLogoUrlCore(IConfiguration configuration, string frontendUrl)
    {
        var configurada = configuration["Apprise:LogoUrl"];
        var url = string.IsNullOrWhiteSpace(configurada)
            ? $"{frontendUrl}/assets/img/logo-isc.png"
            : configurada;
        return WebUtility.HtmlEncode(url);
    }

    internal static async Task<string> CargarPlantillaHtmlCoreAsync(IWebHostEnvironment env, CancellationToken ct)
    {
        var candidatos = new[]
        {
            Path.Combine(env.ContentRootPath, "Features", "Permisos", "Templates", "notificacion-permiso.html"),
            Path.Combine(AppContext.BaseDirectory, "Features", "Permisos", "Templates", "notificacion-permiso.html")
        };

        foreach (var ruta in candidatos)
        {
            if (File.Exists(ruta))
                return await File.ReadAllTextAsync(ruta, ct);
        }

        return "<div style='font-family:Arial,sans-serif;'><h2>{{SUBTITULO_CABECERA}} ({{FOLIO}})</h2><p>Hola <strong>{{NOMBRE_DESTINATARIO}}</strong>,</p><p>{{MENSAJE_PERSONALIZADO}}</p><p><a href='{{CTA_URL}}'>{{CTA_TEXTO}}</a></p></div>";
    }
}

internal static class PermisoItemExtensions
{
    public static string FolioOrCodigo(this PermisoItemDto p) =>
        string.IsNullOrWhiteSpace(p.Codigo) ? $"PER-{p.Id:D4}" : p.Codigo;
}
