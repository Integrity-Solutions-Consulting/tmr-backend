using System.Net;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using tmr_backend.Features.Notificaciones.Infrastructure.Apprise;
using tmr_backend.Features.Permisos.DTOs;
using tmr_backend.Features.Permisos.Endpoints;
using tmr_backend.Infrastructure.Database;
using tmr_backend.Infrastructure.Database.Entities;
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
/// Evento liviano del Outbox para despachar correos de Permisos de forma asíncrona
/// con reintentos automáticos y control de vigencia.
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
/// Handler del bus de mensajería (Outbox + EventDispatcher) que verifica vigencia,
/// renderiza la plantilla HTML en memoria y despacha el correo vía Apprise o SMTP.
/// </summary>
public sealed class EnviarCorreoPermisoHandler(
    ApplicationDbContext db,
    IConfiguration configuration,
    IWebHostEnvironment env,
    ILogger<EnviarCorreoPermisoHandler> logger,
    IAppriseNotificationService? appriseService = null,
    IEmailService? emailService = null) : IEventHandler<CorreoPermisoEncolado>
{
    public async Task HandleAsync(CorreoPermisoEncolado evento, CancellationToken ct)
    {
        var destinatario = evento.DestinatarioOriginal.Trim();
        if (string.IsNullOrWhiteSpace(destinatario))
            return;

        var solicitud = await PermisosEndpoints.ConsultarSolicitudesBase(db)
            .FirstOrDefaultAsync(s => s.Id == evento.IdSolicitud && s.Activo, ct);

        if (!PermisoAvisoVigencia.EsVigente(evento, solicitud))
            throw new EventoObsoletoException();

        string asunto;
        string html;

        if (!string.IsNullOrWhiteSpace(evento.Html) && !string.IsNullOrWhiteSpace(evento.Asunto))
        {
            asunto = evento.Asunto;
            html = evento.Html;
        }
        else
        {
            var dto = evento.Instantanea ?? PermisosEndpoints.MapearSolicitudDto(solicitud!, null);
            if (evento.IncluirTokenExterno)
                dto = dto with { TokenExterno = solicitud!.TokenExterno };

            var plantilla = await PermisosNotificacionService.CargarPlantillaHtmlCoreAsync(env, ct);
            var frontendUrl = (configuration["EmailSettings:FrontendUrl"] ?? "http://localhost:4200").TrimEnd('/');
            var logoUrl = PermisosNotificacionService.ObtenerLogoUrlCore(configuration, frontendUrl);

            (asunto, html) = PermisosNotificacionService.ConstruirMensajeCorreo(
                plantilla,
                dto,
                evento.RolDestinatario,
                evento.NombreDestinatario,
                evento.Evento,
                frontendUrl,
                logoUrl);
        }

        var overrideEmail = configuration["EmailSettings:OverrideEmailRecipient"];
        if (!string.IsNullOrWhiteSpace(overrideEmail))
        {
            logger.LogInformation(
                "REDIRECCIÓN DE CORREO PERMISOS ({Folio}): destinatario {Original} reemplazado por {Override}",
                evento.FolioSolicitud,
                destinatario,
                overrideEmail);
            asunto = $"[PRUEBA para: {destinatario}] {asunto}";
            destinatario = overrideEmail;
        }

        if (appriseService is not null)
        {
            try
            {
                await appriseService.SendEmailAsync(destinatario, asunto, html, ct);
                return;
            }
            catch (Exception ex) when (emailService is not null)
            {
                logger.LogWarning(
                    ex,
                    "Fallo al enviar correo de permiso ({Folio}) por Apprise a {Destinatario}; intentando respaldo por SMTP.",
                    evento.FolioSolicitud,
                    destinatario);
            }
        }

        if (emailService is null)
            throw new InvalidOperationException("No hay un proveedor de correo disponible.");

        await emailService.SendEmailAsync(destinatario, asunto, html);
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

public sealed class PermisosNotificacionService(
    ApplicationDbContext db,
    IEventBus eventBus,
    ILogger<PermisosNotificacionService> logger) : IPermisosNotificacionService
{
    private sealed record DestinatarioCorreo(
        string Email,
        string Nombre,
        RolDestinatarioCorreoPermiso Rol,
        bool IncluirTokenExterno = false);

    public async Task<bool> NotificarCambioEstadoAsync(
        PermisoItemDto permiso,
        string? correoColaborador,
        EventoNotificacionPermiso evento,
        CancellationToken ct = default,
        int? idUsuarioRrhhElegido = null)
    {
        var destinatarios = await ResolverDestinatariosEventoAsync(permiso, correoColaborador, evento, idUsuarioRrhhElegido, ct);
        if (destinatarios.Count == 0)
            return true;

        var solicitud = await PermisosEndpoints.ConsultarSolicitudesBase(db)
            .FirstOrDefaultAsync(s => s.Id == permiso.Id, ct);
        if (solicitud is null)
            return false;

        var snapshotBase = PermisosEndpoints.MapearSolicitudDto(solicitud, null);
        var version = solicitud.FechaModificacion ?? solicitud.FechaCreacion;
        var estadoCodigo = solicitud.Estado.Codigo;
        var folio = permiso.FolioOrCodigo();

        var enviados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var todosOk = true;

        foreach (var dest in destinatarios)
        {
            var emailLimpio = dest.Email?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(emailLimpio) || !enviados.Add(emailLimpio))
                continue;

            try
            {
                var snapshot = dest.IncluirTokenExterno
                    ? snapshotBase with { TokenExterno = solicitud.TokenExterno }
                    : snapshotBase;

                await eventBus.PublishAsync(
                    new CorreoPermisoEncolado(
                        IdSolicitud: permiso.Id,
                        FolioSolicitud: folio,
                        DestinatarioOriginal: emailLimpio,
                        NombreDestinatario: dest.Nombre,
                        RolDestinatario: dest.Rol,
                        Evento: evento,
                        IncluirTokenExterno: dest.IncluirTokenExterno,
                        Instantanea: snapshot,
                        VersionSolicitud: version,
                        EstadoSolicitud: estadoCodigo),
                    ct);
            }
            catch (Exception ex)
            {
                if (db.Database.CurrentTransaction is not null)
                    throw;

                logger.LogWarning(
                    ex,
                    "No se pudo encolar notificación de permiso {Folio} ({Evento}) para {Destinatario}",
                    folio,
                    evento,
                    emailLimpio);
                todosOk = false;
            }
        }

        return todosOk;
    }

    private async Task<List<DestinatarioCorreo>> ResolverDestinatariosEventoAsync(
        PermisoItemDto p,
        string? correoColab,
        EventoNotificacionPermiso evento,
        int? idUsuarioRrhhElegido,
        CancellationToken ct)
    {
        var lista = new List<DestinatarioCorreo>();

        void AgregarColab()
        {
            if (!string.IsNullOrWhiteSpace(correoColab))
                lista.Add(new(correoColab, p.Colaborador, RolDestinatarioCorreoPermiso.Colaborador));
        }

        void AgregarLider(bool incluirToken)
        {
            if (!string.IsNullOrWhiteSpace(p.LiderCorreo))
                lista.Add(new(p.LiderCorreo, p.LiderNombre, RolDestinatarioCorreoPermiso.JefeInmediato, incluirToken));
        }

        async Task AgregarRrhhAsync()
        {
            var rrhhList = await ObtenerDestinatariosRrhhAsync(p.Id, idUsuarioRrhhElegido, ct);
            foreach (var r in rrhhList.Where(u => !string.IsNullOrWhiteSpace(u.Email)))
            {
                var nombre = string.IsNullOrWhiteSpace(r.Nombre) ? "Talento Humano ISC" : r.Nombre;
                lista.Add(new(r.Email, nombre, RolDestinatarioCorreoPermiso.TalentoHumano));
            }
        }

        switch (evento)
        {
            case EventoNotificacionPermiso.SolicitudCreada:
                var esExt = string.Equals(p.LiderTipo, "EXT", StringComparison.OrdinalIgnoreCase) && p.EstadoClase != "confirmado";
                AgregarColab();
                AgregarLider(esExt);
                await AgregarRrhhAsync();
                break;

            case EventoNotificacionPermiso.ReenvioMagicLink:
                AgregarLider(incluirToken: true);
                break;

            case EventoNotificacionPermiso.AprobadaPorLider:
            case EventoNotificacionPermiso.ObservadaPorLider:
            case EventoNotificacionPermiso.RechazadaPorLider:
                AgregarColab();
                await AgregarRrhhAsync();
                break;

            case EventoNotificacionPermiso.ConfirmadaPorRrhh:
            case EventoNotificacionPermiso.DevueltaPorRrhh:
                AgregarColab();
                AgregarLider(incluirToken: false);
                break;
        }

        return lista;
    }

    public static (string Asunto, string Html) ConstruirMensajeCorreo(
        string plantilla,
        PermisoItemDto p,
        RolDestinatarioCorreoPermiso rol,
        string nombreDestinatario,
        EventoNotificacionPermiso evento,
        string frontendUrl,
        string logoUrl)
    {
        var folio = p.FolioOrCodigo();
        var nombre = string.IsNullOrWhiteSpace(nombreDestinatario) ? "Colaborador" : nombreDestinatario;
        var tipoHtml = WebUtility.HtmlEncode(p.Tipo);
        var colabHtml = WebUtility.HtmlEncode(p.Colaborador);
        var liderHtml = WebUtility.HtmlEncode(p.LiderNombre);
        var folioHtml = WebUtility.HtmlEncode(folio);
        var esInformativo = p.EstadoClase == "confirmado";
        var esExterno = string.Equals(p.LiderTipo, "EXT", StringComparison.OrdinalIgnoreCase) && !esInformativo;

        var urlColab = $"{frontendUrl}/permisos?solicitud={p.Id}";
        var urlRrhh = $"{frontendUrl}/permisos/rrhh?solicitud={p.Id}";
        var urlLider = esExterno && !string.IsNullOrWhiteSpace(p.TokenExterno)
            ? $"{frontendUrl}/permisos/revision-externa?token={Uri.EscapeDataString(p.TokenExterno)}"
            : $"{frontendUrl}/permisos/equipo?solicitud={p.Id}";

        (string Asunto, string Titulo, string Mensaje, string CtaUrl, string CtaTexto) config = (evento, rol) switch
        {
            (EventoNotificacionPermiso.SolicitudCreada, RolDestinatarioCorreoPermiso.Colaborador) => (
                Asunto: $"[Permisos ISC] Solicitud {folio} registrada · {p.Tipo}",
                Titulo: esInformativo ? "Registro de Permiso Informativo" : "Confirmación de Solicitud",
                Mensaje: esInformativo
                    ? $"Tu permiso por <strong>{tipoHtml}</strong> ha sido registrado automáticamente en TMR y enviado como copia informativa a Talento Humano."
                    : p.IdLider.HasValue
                        ? $"Tu solicitud de <strong>{tipoHtml}</strong> ha sido registrada y enviada a tu Jefe Inmediato, <strong>{liderHtml}</strong>, para su revisión."
                        : $"Tu solicitud de <strong>{tipoHtml}</strong> ha sido registrada y enviada directamente a <strong>Talento Humano</strong> para su confirmación.",
                CtaUrl: urlColab,
                CtaTexto: "Ver en Mis Permisos"),

            (EventoNotificacionPermiso.SolicitudCreada, RolDestinatarioCorreoPermiso.JefeInmediato) => (
                Asunto: esInformativo
                    ? $"[Copia Informativa] Permiso {folio} · {p.Colaborador}"
                    : $"[Acción Requerida] Aprobación de permiso {folio} · {p.Colaborador}",
                Titulo: esInformativo ? "Copia Informativa de Permiso" : "Solicitud Pendiente de Aprobación",
                Mensaje: esExterno
                    ? $"El colaborador <strong>{colabHtml}</strong> ha registrado una solicitud de <strong>{tipoHtml}</strong>. Puedes revisarla y registrar tu decisión directamente desde el siguiente enlace seguro:"
                    : esInformativo
                        ? $"El colaborador <strong>{colabHtml}</strong> registró un permiso informativo de <strong>{tipoHtml}</strong>. No requiere aprobación; se envía esta copia para tu conocimiento."
                        : $"El colaborador <strong>{colabHtml}</strong> ha registrado una solicitud de <strong>{tipoHtml}</strong> que requiere tu revisión en TMR.",
                CtaUrl: urlLider,
                CtaTexto: esExterno ? "Revisar y Aprobar Solicitud" : "Revisar en TMR"),

            (EventoNotificacionPermiso.SolicitudCreada, _) => (
                Asunto: esInformativo
                    ? $"[Copia Informativa RRHH] Permiso {folio} · {p.Colaborador}"
                    : !p.IdLider.HasValue
                        ? $"[Confirmación RRHH] Solicitud directa {folio} · {p.Colaborador}"
                        : $"[Talento Humano] Nueva solicitud {folio} · {p.Colaborador}",
                Titulo: esInformativo ? "Copia Informativa para Talento Humano" : "Nueva Solicitud Registrada",
                Mensaje: esInformativo
                    ? $"<strong>{colabHtml}</strong> registró un permiso informativo por <strong>{tipoHtml}</strong>. Se envía esta copia para conocimiento y registro de Talento Humano."
                    : !p.IdLider.HasValue
                        ? $"<strong>{colabHtml}</strong> registró una solicitud de <strong>{tipoHtml}</strong> que pasa directamente a <strong>Talento Humano</strong> para su confirmación."
                        : $"<strong>{colabHtml}</strong> registró una solicitud de <strong>{tipoHtml}</strong>. Actualmente se encuentra pendiente de revisión por su Jefe Inmediato, <strong>{liderHtml}</strong>.",
                CtaUrl: urlRrhh,
                CtaTexto: "Ver en Validación RRHH"),

            (EventoNotificacionPermiso.ReenvioMagicLink, _) => (
                Asunto: $"[Recordatorio] Aprobación pendiente {folio} · {p.Colaborador}",
                Titulo: "Recordatorio de Aprobación Pendiente",
                Mensaje: $"Talento Humano de Integrity Solutions te reenvía el enlace directo para revisar la solicitud de <strong>{tipoHtml}</strong> de <strong>{colabHtml}</strong>.",
                CtaUrl: urlLider,
                CtaTexto: "Revisar Solicitud"),

            (EventoNotificacionPermiso.AprobadaPorLider or EventoNotificacionPermiso.ObservadaPorLider or EventoNotificacionPermiso.RechazadaPorLider, RolDestinatarioCorreoPermiso.Colaborador) => (
                Asunto: $"[Permisos ISC] Solicitud {folio} {TextoAccionLider(evento)}",
                Titulo: "Actualización de tu Solicitud",
                Mensaje: $"Tu solicitud <strong>{folioHtml}</strong> fue <strong>{TextoAccionLider(evento)}</strong> por <strong>{liderHtml}</strong>." +
                         (evento == EventoNotificacionPermiso.AprobadaPorLider
                             ? " Ahora se encuentra en la bandeja de <strong>Talento Humano</strong> para su confirmación final."
                             : ""),
                CtaUrl: urlColab,
                CtaTexto: "Ver en Mis Permisos"),

            (EventoNotificacionPermiso.AprobadaPorLider or EventoNotificacionPermiso.ObservadaPorLider or EventoNotificacionPermiso.RechazadaPorLider, _) => (
                Asunto: evento == EventoNotificacionPermiso.AprobadaPorLider
                    ? $"[Confirmación RRHH] Solicitud {folio} · {p.Colaborador}"
                    : $"[Talento Humano] Solicitud {folio} {p.EstadoTexto} · {p.Colaborador}",
                Titulo: evento == EventoNotificacionPermiso.AprobadaPorLider
                    ? "Solicitud Lista para Confirmación RRHH"
                    : "Actualización de Jefe Inmediato",
                Mensaje: evento == EventoNotificacionPermiso.AprobadaPorLider
                    ? $"El Jefe Inmediato, <strong>{liderHtml}</strong>, aprobó la solicitud de <strong>{colabHtml}</strong>. Ya puedes revisarla y confirmar en Talento Humano."
                    : $"El Jefe Inmediato, <strong>{liderHtml}</strong>, marcó la solicitud de <strong>{colabHtml}</strong> como <strong>{WebUtility.HtmlEncode(p.EstadoTexto)}</strong>.",
                CtaUrl: urlRrhh,
                CtaTexto: "Revisar en Validación RRHH"),

            (_, RolDestinatarioCorreoPermiso.Colaborador) => (
                Asunto: evento == EventoNotificacionPermiso.ConfirmadaPorRrhh
                    ? $"[Confirmado] Solicitud {folio} aprobada por Talento Humano"
                    : $"[Observado] Solicitud {folio} devuelta por Talento Humano",
                Titulo: evento == EventoNotificacionPermiso.ConfirmadaPorRrhh
                    ? "Confirmación de Talento Humano"
                    : "Solicitud Observada por Talento Humano",
                Mensaje: evento == EventoNotificacionPermiso.ConfirmadaPorRrhh
                    ? $"Tu solicitud <strong>{folioHtml}</strong> por <strong>{tipoHtml}</strong> fue <strong>confirmada por Talento Humano</strong>. Puedes consultar el detalle y descargar el PDF desde TMR."
                    : $"Talento Humano revisó tu solicitud <strong>{folioHtml}</strong> y registró una observación. Por favor revísala en TMR.",
                CtaUrl: urlColab,
                CtaTexto: "Ver en Mis Permisos"),

            _ => (
                Asunto: $"[Seguimiento Equipo] Solicitud {folio} · {p.Colaborador} · {p.EstadoTexto}",
                Titulo: evento == EventoNotificacionPermiso.ConfirmadaPorRrhh
                    ? "Constancia de Confirmación RRHH"
                    : "Aviso de Devolución por RRHH",
                Mensaje: evento == EventoNotificacionPermiso.ConfirmadaPorRrhh
                    ? $"Talento Humano confirmó definitivamente la solicitud <strong>{folioHtml}</strong> de <strong>{colabHtml}</strong>."
                    : $"Talento Humano devolvió con observaciones la solicitud <strong>{folioHtml}</strong> de <strong>{colabHtml}</strong>.",
                CtaUrl: p.LiderTipo == "EXT" ? "" : urlLider,
                CtaTexto: "Ver en TMR")
        };

        var html = RenderizarCorreo(plantilla, p, logoUrl, nombre, config.Titulo, config.Mensaje, config.CtaUrl, config.CtaTexto);
        return (config.Asunto, html);
    }

    private static string TextoAccionLider(EventoNotificacionPermiso evento) => evento switch
    {
        EventoNotificacionPermiso.AprobadaPorLider => "aprobada",
        EventoNotificacionPermiso.ObservadaPorLider => "observada",
        _ => "rechazada"
    };

    private static string RenderizarCorreo(
        string plantilla,
        PermisoItemDto p,
        string logoUrl,
        string nombreDestinatario,
        string tituloCorreo,
        string mensajePersonalizado,
        string ctaUrl,
        string ctaTexto)
    {
        var comentarios = p.ObservacionRrhh ?? p.ComentarioLider ?? p.Comentarios;
        var filaComentarios = string.IsNullOrWhiteSpace(comentarios)
            ? ""
            : $@"<div style='margin-top: 12px; padding: 10px 12px; background-color: #fffbeb; border-left: 3px solid #f59e0b; border-radius: 4px; font-size: 13px; color: #92400e; line-height: 1.45;'>
                  <strong>Observación:</strong> {WebUtility.HtmlEncode(comentarios)}
                </div>";

        if (string.IsNullOrWhiteSpace(ctaUrl))
        {
            var inicio = plantilla.IndexOf("<a href=\"{{CTA_URL}}\"", StringComparison.Ordinal);
            var fin = inicio < 0 ? -1 : plantilla.IndexOf("</a>", inicio, StringComparison.Ordinal);
            if (fin >= inicio && inicio >= 0)
                plantilla = plantilla.Remove(inicio, fin + 4 - inicio);
        }

        var duracion = string.IsNullOrWhiteSpace(p.Duracion)
            ? $"{p.DiasSolicitados} · {p.HorasSolicitadas}"
            : p.Duracion;

        return plantilla
            .Replace("{{TITULO_CORREO}}", WebUtility.HtmlEncode(tituloCorreo))
            .Replace("{{SUBTITULO_CABECERA}}", WebUtility.HtmlEncode(tituloCorreo))
            .Replace("{{LOGO_URL}}", logoUrl)
            .Replace("{{FOLIO}}", WebUtility.HtmlEncode(p.FolioOrCodigo()))
            .Replace("{{NOMBRE_DESTINATARIO}}", WebUtility.HtmlEncode(nombreDestinatario))
            .Replace("{{MENSAJE_PERSONALIZADO}}", mensajePersonalizado)
            .Replace("{{TIPO_PERMISO}}", WebUtility.HtmlEncode(p.Tipo))
            .Replace("{{COLABORADOR}}", WebUtility.HtmlEncode(p.Colaborador))
            .Replace("{{FECHAS}}", WebUtility.HtmlEncode(p.Fechas))
            .Replace("{{DURACION}}", WebUtility.HtmlEncode(duracion))
            .Replace("{{FILA_COMENTARIOS}}", filaComentarios)
            .Replace("{{CTA_URL}}", WebUtility.HtmlEncode(ctaUrl))
            .Replace("{{CTA_TEXTO}}", WebUtility.HtmlEncode(ctaTexto))
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
            idTarget = await db.NotificacionesUsuario
                .AsNoTracking()
                .Where(n => n.IdSolicitud == idSolicitud && n.UrlDestino == "/permisos/rrhh")
                .OrderBy(n => n.Id)
                .Select(n => (int?)n.IdUsuarioDestino)
                .FirstOrDefaultAsync(ct);
        }

        if (idTarget is > 0)
        {
            var usuarioElegido = await db.TblAutenticacionUsuarios
                .AsNoTracking()
                .Include(u => u.IdpersonaNavigation)
                .FirstOrDefaultAsync(u => u.Id == idTarget.Value && u.Activo, ct);

            if (usuarioElegido is not null && await PermisosEndpoints.EsUsuarioRrhhValidoAsync(db, usuarioElegido.Id, ct))
            {
                var emailFinal = await ResolverEmailUsuarioRrhhAsync(usuarioElegido, ct);
                if (!string.IsNullOrWhiteSpace(emailFinal))
                {
                    var nombreFinal = usuarioElegido.IdpersonaNavigation is { } p
                        ? $"{p.Nombres} {p.Apellidos}".Trim()
                        : "Talento Humano ISC";
                    return [new DestinatarioRrhh(emailFinal, nombreFinal)];
                }
            }
        }

        var catalogoRrhh = await PermisosEndpoints.ObtenerCatalogoUsuariosRrhhAsync(db, ct);
        var principales = catalogoRrhh.Where(r => r.EsPrincipal && !string.IsNullOrWhiteSpace(r.Correo)).ToList();
        var origen = principales.Count > 0 ? principales : catalogoRrhh.Where(r => !string.IsNullOrWhiteSpace(r.Correo)).ToList();

        return origen
            .Select(r => new DestinatarioRrhh(r.Correo!.Trim(), r.Nombre))
            .DistinctBy(d => d.Email.ToLowerInvariant())
            .ToList();
    }

    private async Task<string> ResolverEmailUsuarioRrhhAsync(TblAutenticacionUsuario usuario, CancellationToken ct)
    {
        if (usuario.Idpersona.HasValue)
        {
            var emailCorp = await db.TblAdministracionEmpleados
                .AsNoTracking()
                .Where(e => e.Idpersona == usuario.Idpersona.Value && e.Activo)
                .Select(e => e.Emailcorporativo)
                .FirstOrDefaultAsync(ct);

            if (!string.IsNullOrWhiteSpace(emailCorp))
                return emailCorp.Trim();
        }

        return !string.IsNullOrWhiteSpace(usuario.Email)
            ? usuario.Email.Trim()
            : usuario.IdpersonaNavigation?.Email?.Trim() ?? "";
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

        return "<div style='font-family:Arial,sans-serif;'><h2>{{SUBTITULO_CABECERA}}</h2><p>Hola <strong>{{NOMBRE_DESTINATARIO}}</strong>,</p><p>{{MENSAJE_PERSONALIZADO}}</p><p><a href='{{CTA_URL}}'>{{CTA_TEXTO}}</a></p></div>";
    }
}

internal static class PermisoItemExtensions
{
    public static string FolioOrCodigo(this PermisoItemDto p) =>
        string.IsNullOrWhiteSpace(p.Codigo) ? $"PER-{p.Id:D4}" : p.Codigo;
}
