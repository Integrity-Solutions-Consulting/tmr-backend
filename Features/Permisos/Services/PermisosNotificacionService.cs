using System.Net;
using Microsoft.EntityFrameworkCore;
using tmr_backend.Features.Notificaciones.Infrastructure.Apprise;
using tmr_backend.Features.Permisos.DTOs;
using tmr_backend.Infrastructure.Database;
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

public interface IPermisosNotificacionService
{
    Task NotificarCambioEstadoAsync(
        PermisoItemDto permiso,
        string? correoColaborador,
        EventoNotificacionPermiso evento,
        CancellationToken ct = default);
}

public sealed class PermisosNotificacionService : IPermisosNotificacionService
{
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<PermisosNotificacionService> _logger;
    private readonly IAppriseNotificationService? _appriseService;
    private readonly IEmailService? _emailService;

    public PermisosNotificacionService(
        ApplicationDbContext db,
        IConfiguration configuration,
        IWebHostEnvironment env,
        ILogger<PermisosNotificacionService> logger,
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

    public async Task NotificarCambioEstadoAsync(
        PermisoItemDto permiso,
        string? correoColaborador,
        EventoNotificacionPermiso evento,
        CancellationToken ct = default)
    {
        var plantilla = await CargarPlantillaHtmlAsync(ct);
        var frontendUrl = (_configuration["EmailSettings:FrontendUrl"] ?? "http://localhost:3000").TrimEnd('/');

        // Obtener usuarios activos con rol RECURSOS HUMANOS para notificar a Talento Humano
        var usuariosRrhh = await _db.TblAutenticacionUsuarioRols
            .AsNoTracking()
            .Where(ur => ur.Activo &&
                         ur.IdusuarioNavigation.Activo &&
                         ur.IdrolNavigation.Activo &&
                         (ur.IdrolNavigation.Nombre.ToUpper() == "RECURSOS HUMANOS" ||
                          ur.IdrolNavigation.Nombre.ToUpper() == "RECURSOS_HUMANOS"))
            .Select(ur => new
            {
                Email = ur.IdusuarioNavigation.Email,
                Nombre = (ur.IdusuarioNavigation.IdpersonaNavigation.Nombres + " " + ur.IdusuarioNavigation.IdpersonaNavigation.Apellidos).Trim()
            })
            .Distinct()
            .ToListAsync(ct);

        switch (evento)
        {
            case EventoNotificacionPermiso.SolicitudCreada:
            {
                // 1. Correo personalizado al Colaborador
                if (!string.IsNullOrWhiteSpace(correoColaborador))
                {
                    var htmlColab = RenderizarCorreo(
                        plantilla,
                        permiso,
                        nombreDestinatario: permiso.Colaborador,
                        rolDestinatario: "Colaborador Solicitante",
                        badgeRolBg: "#eff6ff",
                        badgeRolColor: "#1d4ed8",
                        subtituloCabecera: "Confirmación de Registro de Solicitud",
                        mensajePersonalizado: $"Tu solicitud de <strong>{WebUtility.HtmlEncode(permiso.Tipo)}</strong> ({WebUtility.HtmlEncode(permiso.FolioOrCodigo())}) ha sido registrada exitosamente y se envió a tu Jefe Inmediato (<strong>{WebUtility.HtmlEncode(permiso.LiderNombre)}</strong>) para su revisión.",
                        ctaUrl: $"{frontendUrl}/permisos",
                        ctaTexto: "Ver seguimiento en Mis Permisos",
                        notaPieBoton: "Recibirás un correo personalizado en cada cambio de estado de tu solicitud.");

                    await EnviarCorreoIndividualAsync(
                        correoColaborador,
                        $"[Permisos ISC] Solicitud {permiso.FolioOrCodigo()} registrada ({permiso.Tipo})",
                        htmlColab,
                        ct);
                }

                // 2. Correo personalizado al Jefe Inmediato (Interno o Externo con Magic Link)
                if (!string.IsNullOrWhiteSpace(permiso.LiderCorreo))
                {
                    var esExterno = string.Equals(permiso.LiderTipo, "EXT", StringComparison.OrdinalIgnoreCase);
                    var ctaLiderUrl = esExterno && !string.IsNullOrWhiteSpace(permiso.TokenExterno)
                        ? $"{frontendUrl}/permisos/revision-externa?token={Uri.EscapeDataString(permiso.TokenExterno)}"
                        : $"{frontendUrl}/permisos/equipo";

                    var htmlLider = RenderizarCorreo(
                        plantilla,
                        permiso,
                        nombreDestinatario: permiso.LiderNombre,
                        rolDestinatario: esExterno ? "Jefe Inmediato Externo · Magic Link" : "Jefe Inmediato / Líder TMR",
                        badgeRolBg: esExterno ? "#f3e8ff" : "#e0f2fe",
                        badgeRolColor: esExterno ? "#7e22ce" : "#0369a1",
                        subtituloCabecera: "Nueva Solicitud Pendiente de tu Aprobación",
                        mensajePersonalizado: esExterno
                            ? $"El colaborador <strong>{WebUtility.HtmlEncode(permiso.Colaborador)}</strong> asignado a <strong>{WebUtility.HtmlEncode(permiso.Cliente)}</strong> ha registrado una solicitud de <strong>{WebUtility.HtmlEncode(permiso.Tipo)}</strong>. Puedes revisarla y aprobarla o rechazarla directamente mediante el siguiente enlace seguro sin necesidad de iniciar sesión:"
                            : $"El colaborador <strong>{WebUtility.HtmlEncode(permiso.Colaborador)}</strong> de tu equipo ha registrado una solicitud de <strong>{WebUtility.HtmlEncode(permiso.Tipo)}</strong> que requiere tu revisión y aprobación.",
                        ctaUrl: ctaLiderUrl,
                        ctaTexto: esExterno ? "Revisar y Aprobar Solicitud (Magic Link)" : "Revisar Solicitud en Aprobación Líder",
                        notaPieBoton: esExterno
                            ? "Enlace único de aprobación externa (válido por 48 horas)."
                            : "Ingresa a la bandeja de Aprobación Líder en TMR para gestionar la solicitud.");

                    await EnviarCorreoIndividualAsync(
                        permiso.LiderCorreo,
                        $"[Acción Requerida] Aprobación de permiso {permiso.FolioOrCodigo()} — {permiso.Colaborador}",
                        htmlLider,
                        ct);
                }

                // 3. Correo personalizado a Talento Humano (RRHH)
                foreach (var rrhh in usuariosRrhh.Where(u => !string.IsNullOrWhiteSpace(u.Email)))
                {
                    var htmlRrhh = RenderizarCorreo(
                        plantilla,
                        permiso,
                        nombreDestinatario: string.IsNullOrWhiteSpace(rrhh.Nombre) ? "Talento Humano ISC" : rrhh.Nombre,
                        rolDestinatario: "Talento Humano (RRHH)",
                        badgeRolBg: "#fef3c7",
                        badgeRolColor: "#b45309",
                        subtituloCabecera: "Nueva Solicitud en Flujo de Aprobación",
                        mensajePersonalizado: $"Se ha registrado una nueva solicitud de <strong>{WebUtility.HtmlEncode(permiso.Tipo)}</strong> por parte de <strong>{WebUtility.HtmlEncode(permiso.Colaborador)}</strong>. Actualmente se encuentra en espera de aprobación de su Jefe Inmediato (<strong>{WebUtility.HtmlEncode(permiso.LiderNombre)}</strong>).",
                        ctaUrl: $"{frontendUrl}/permisos/rrhh",
                        ctaTexto: "Ver Bandeja de Validación RRHH",
                        notaPieBoton: "Serás notificado nuevamente apenas el Jefe Inmediato registre su aprobación.");

                    await EnviarCorreoIndividualAsync(
                        rrhh.Email,
                        $"[Talento Humano] Nueva solicitud {permiso.FolioOrCodigo()} en curso — {permiso.Colaborador}",
                        htmlRrhh,
                        ct);
                }
                break;
            }

            case EventoNotificacionPermiso.ReenvioMagicLink:
            {
                if (!string.IsNullOrWhiteSpace(permiso.LiderCorreo))
                {
                    var ctaLiderUrl = !string.IsNullOrWhiteSpace(permiso.TokenExterno)
                        ? $"{frontendUrl}/permisos/revision-externa?token={Uri.EscapeDataString(permiso.TokenExterno)}"
                        : $"{frontendUrl}/permisos/revision-externa";

                    var htmlLider = RenderizarCorreo(
                        plantilla,
                        permiso,
                        nombreDestinatario: permiso.LiderNombre,
                        rolDestinatario: "Jefe Inmediato Externo · Magic Link",
                        badgeRolBg: "#f3e8ff",
                        badgeRolColor: "#7e22ce",
                        subtituloCabecera: "Recordatorio de Aprobación Pendiente (Magic Link)",
                        mensajePersonalizado: $"Talento Humano de Integrity Solutions te reenvía el enlace directo para revisar y confirmar la solicitud de <strong>{WebUtility.HtmlEncode(permiso.Tipo)}</strong> de <strong>{WebUtility.HtmlEncode(permiso.Colaborador)}</strong>.",
                        ctaUrl: ctaLiderUrl,
                        ctaTexto: "Abrir Formulario Oficial y Responder",
                        notaPieBoton: "Enlace renovado por 48 horas. No requiere usuario ni contraseña.");

                    await EnviarCorreoIndividualAsync(
                        permiso.LiderCorreo,
                        $"[Recordatorio] Aprobación pendiente {permiso.FolioOrCodigo()} — {permiso.Colaborador}",
                        htmlLider,
                        ct);
                }
                break;
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

                // 1. Notificar al Colaborador
                if (!string.IsNullOrWhiteSpace(correoColaborador))
                {
                    var htmlColab = RenderizarCorreo(
                        plantilla,
                        permiso,
                        nombreDestinatario: permiso.Colaborador,
                        rolDestinatario: "Colaborador Solicitante",
                        badgeRolBg: "#eff6ff",
                        badgeRolColor: "#1d4ed8",
                        subtituloCabecera: "Actualización de Estado · Revisión de Jefe Inmediato",
                        mensajePersonalizado: $"Tu solicitud <strong>{WebUtility.HtmlEncode(permiso.FolioOrCodigo())}</strong> ha sido <strong>{accionTexto}</strong> ({WebUtility.HtmlEncode(permiso.LiderNombre)})." +
                            (evento == EventoNotificacionPermiso.AprobadaPorLider
                                ? " Ahora ha pasado a la bandeja de <strong>Talento Humano (RRHH)</strong> para su confirmación final."
                                : ""),
                        ctaUrl: $"{frontendUrl}/permisos",
                        ctaTexto: "Consultar mi Solicitud y PDF",
                        notaPieBoton: "Puedes visualizar el documento oficial actualizado desde tu bandeja Mis Permisos.");

                    await EnviarCorreoIndividualAsync(
                        correoColaborador,
                        $"[Permisos ISC] Solicitud {permiso.FolioOrCodigo()} {accionTexto}",
                        htmlColab,
                        ct);
                }

                // 2. Notificar a Talento Humano (RRHH) para que confirme
                foreach (var rrhh in usuariosRrhh.Where(u => !string.IsNullOrWhiteSpace(u.Email)))
                {
                    var htmlRrhh = RenderizarCorreo(
                        plantilla,
                        permiso,
                        nombreDestinatario: string.IsNullOrWhiteSpace(rrhh.Nombre) ? "Talento Humano ISC" : rrhh.Nombre,
                        rolDestinatario: "Talento Humano (RRHH)",
                        badgeRolBg: "#fef3c7",
                        badgeRolColor: "#b45309",
                        subtituloCabecera: evento == EventoNotificacionPermiso.AprobadaPorLider
                            ? "Solicitud Lista para Confirmación de RRHH"
                            : "Actualización de Solicitud por Jefe Inmediato",
                        mensajePersonalizado: evento == EventoNotificacionPermiso.AprobadaPorLider
                            ? $"El Jefe Inmediato (<strong>{WebUtility.HtmlEncode(permiso.LiderNombre)}</strong>) aprobó la solicitud <strong>{WebUtility.HtmlEncode(permiso.FolioOrCodigo())}</strong> de <strong>{WebUtility.HtmlEncode(permiso.Colaborador)}</strong> ({WebUtility.HtmlEncode(permiso.MetodoLider)}). Ya puedes revisarla y dar la confirmación definitiva de Talento Humano."
                            : $"El Jefe Inmediato (<strong>{WebUtility.HtmlEncode(permiso.LiderNombre)}</strong>) marcó la solicitud <strong>{WebUtility.HtmlEncode(permiso.FolioOrCodigo())}</strong> de <strong>{WebUtility.HtmlEncode(permiso.Colaborador)}</strong> como <strong>{WebUtility.HtmlEncode(permiso.EstadoTexto)}</strong>.",
                        ctaUrl: $"{frontendUrl}/permisos/rrhh",
                        ctaTexto: "Revisar Solicitud en Validación RRHH",
                        notaPieBoton: "Al confirmar se actualizará el formulario oficial PDF y su impacto en Time Report.");

                    await EnviarCorreoIndividualAsync(
                        rrhh.Email,
                        evento == EventoNotificacionPermiso.AprobadaPorLider
                            ? $"[Confirmación RRHH Requerida] Solicitud {permiso.FolioOrCodigo()} — {permiso.Colaborador}"
                            : $"[Talento Humano] Solicitud {permiso.FolioOrCodigo()} {permiso.EstadoTexto} — {permiso.Colaborador}",
                        htmlRrhh,
                        ct);
                }
                break;
            }

            case EventoNotificacionPermiso.ConfirmadaPorRrhh:
            case EventoNotificacionPermiso.DevueltaPorRrhh:
            {
                var confirmada = evento == EventoNotificacionPermiso.ConfirmadaPorRrhh;

                // 1. Notificar al Colaborador
                if (!string.IsNullOrWhiteSpace(correoColaborador))
                {
                    var htmlColab = RenderizarCorreo(
                        plantilla,
                        permiso,
                        nombreDestinatario: permiso.Colaborador,
                        rolDestinatario: "Colaborador Solicitante",
                        badgeRolBg: "#eff6ff",
                        badgeRolColor: "#1d4ed8",
                        subtituloCabecera: confirmada
                            ? "Confirmación Definitiva de Talento Humano"
                            : "Solicitud Observada / Devuelta por Talento Humano",
                        mensajePersonalizado: confirmada
                            ? $"¡Tu solicitud <strong>{WebUtility.HtmlEncode(permiso.FolioOrCodigo())}</strong> ({WebUtility.HtmlEncode(permiso.Tipo)}) ha sido <strong>confirmada definitivamente por Talento Humano</strong>! El formulario oficial PDF con todas las firmas digitales de aprobación ya está disponible para su descarga."
                            : $"Talento Humano ha revisado tu solicitud <strong>{WebUtility.HtmlEncode(permiso.FolioOrCodigo())}</strong> y ha registrado una observación para subsanación.",
                        ctaUrl: $"{frontendUrl}/permisos",
                        ctaTexto: confirmada ? "Descargar PDF Oficial en Mis Permisos" : "Revisar Observación en Mis Permisos",
                        notaPieBoton: "El seguimiento de tu solicitud ha concluido en el flujo de aprobaciones.");

                    await EnviarCorreoIndividualAsync(
                        correoColaborador,
                        confirmada
                            ? $"[Confirmado] Solicitud {permiso.FolioOrCodigo()} aprobada por Talento Humano"
                            : $"[Observado] Solicitud {permiso.FolioOrCodigo()} devuelta por Talento Humano",
                        htmlColab,
                        ct);
                }

                // 2. Notificar también al Jefe Inmediato para constancia
                if (!string.IsNullOrWhiteSpace(permiso.LiderCorreo))
                {
                    var htmlLider = RenderizarCorreo(
                        plantilla,
                        permiso,
                        nombreDestinatario: permiso.LiderNombre,
                        rolDestinatario: "Jefe Inmediato / Líder",
                        badgeRolBg: "#e0f2fe",
                        badgeRolColor: "#0369a1",
                        subtituloCabecera: confirmada
                            ? "Constancia de Confirmación RRHH"
                            : "Aviso de Devolución por RRHH",
                        mensajePersonalizado: confirmada
                            ? $"Te informamos que Talento Humano ha confirmado definitivamente la solicitud <strong>{WebUtility.HtmlEncode(permiso.FolioOrCodigo())}</strong> del colaborador <strong>{WebUtility.HtmlEncode(permiso.Colaborador)}</strong> para el período <strong>{WebUtility.HtmlEncode(permiso.Fechas)}</strong>."
                            : $"Talento Humano devolvió con observaciones la solicitud <strong>{WebUtility.HtmlEncode(permiso.FolioOrCodigo())}</strong> del colaborador <strong>{WebUtility.HtmlEncode(permiso.Colaborador)}</strong>.",
                        ctaUrl: $"{frontendUrl}/permisos/equipo",
                        ctaTexto: "Ver en TMR",
                        notaPieBoton: "Notificación de seguimiento para coordinación del equipo.");

                    await EnviarCorreoIndividualAsync(
                        permiso.LiderCorreo,
                        $"[Seguimiento Equipo] Solicitud {permiso.FolioOrCodigo()} ({permiso.Colaborador}) — {permiso.EstadoTexto}",
                        htmlLider,
                        ct);
                }
                break;
            }
        }
    }

    private string RenderizarCorreo(
        string plantilla,
        PermisoItemDto p,
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
        // Colores semáforo del estado actual
        var (estadoBg, estadoColor, estadoBorder) = p.EstadoClase switch
        {
            "confirmado" => ("#dcfce7", "#166534", "#86efac"),
            "rechazado" => ("#fee2e2", "#991b1b", "#fca5a5"),
            "rrhh" => ("#dbeafe", "#1e40af", "#93c5fd"),
            _ => ("#fef3c7", "#92400e", "#fcd34d") // pendiente u observado -> amarillo/ámbar
        };

        // Paso 1: Solicitud Creada (siempre completado)
        const string paso1Bg = "#16a34a";
        const string paso1Color = "#ffffff";
        var paso1Detalle = $"Registrado ({p.FechaSolicitud})";

        // Paso 2: Jefe Inmediato
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

        // Paso 3: Talento Humano (RRHH)
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

        return plantilla
            .Replace("{{TITULO_CORREO}}", WebUtility.HtmlEncode(subtituloCabecera))
            .Replace("{{LOGO_URL}}", ObtenerLogoUrl())
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

    private async Task EnviarCorreoIndividualAsync(string destinatarioOriginal, string asunto, string html, CancellationToken ct)
    {
        try
        {
            var destinatario = destinatarioOriginal.Trim();
            var overrideEmail = _configuration["EmailSettings:OverrideEmailRecipient"];
            if (!string.IsNullOrWhiteSpace(overrideEmail))
            {
                _logger.LogInformation(
                    "REDIRECCIÓN DE CORREO PERMISOS: destinatario {Original} reemplazado por {Override}",
                    destinatario, overrideEmail);
                asunto = $"[PRUEBA para: {destinatario}] {asunto}";
                destinatario = overrideEmail;
            }

            if (_appriseService is not null)
            {
                await _appriseService.SendEmailAsync(destinatario, asunto, html, ct);
                return;
            }

            if (_emailService is not null)
            {
                await _emailService.SendEmailAsync(destinatario, asunto, html);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo enviar notificación de permiso a {Destinatario} (Asunto: {Asunto})", destinatarioOriginal, asunto);
        }
    }

    private string ObtenerLogoUrl()
    {
        var configurada = _configuration["Apprise:LogoUrl"];
        var url = string.IsNullOrWhiteSpace(configurada)
            ? $"{(_configuration["EmailSettings:FrontendUrl"] ?? "http://localhost:3000").TrimEnd('/')}/assets/img/logo-isc.png"
            : configurada;
        return WebUtility.HtmlEncode(url);
    }

    private async Task<string> CargarPlantillaHtmlAsync(CancellationToken ct)
    {
        var candidatos = new[]
        {
            Path.Combine(_env.ContentRootPath, "Features", "Permisos", "Templates", "notificacion-permiso.html"),
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
