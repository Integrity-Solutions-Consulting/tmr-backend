using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using tmr_backend.Infrastructure.Database;
using tmr_backend.Infrastructure.Shared;
using tmr_backend.Features.Notificaciones.Domain;
using tmr_backend.Features.Notificaciones.Domain.Events;
using tmr_backend.Infrastructure.Messaging;
using tmr_backend.Features.Notificaciones.Infrastructure.Apprise;

namespace tmr_backend.Features.Notificaciones.Application.Handlers;

/// <summary>
/// Envía el correo de horas faltantes a un empleado y publica el resultado
/// (NotificacionEnviada o NotificacionFallida) para que RegistrarEnvioHandler lo guarde.
/// Antes de enviar revisa NotificacionEnvio para no duplicar avisos:
///  - Automático (flujos 1 y 3): máximo un aviso por empleado y corte (RF-007).
///  - Manual (flujo 2): respeta NotificacionHoras:UmbralNotificacionManual (RF-008).
/// </summary>
public sealed class EnviarCorreoHandler : IEventHandler<HorasFaltantesDetectadas>
{
    private static readonly CultureInfo CulturaEc = CultureInfo.GetCultureInfo("es-EC");

    private readonly ApplicationDbContext _db;
    private readonly IEmailService _emailService;
    private readonly IEventBus _bus;
    private readonly NotificacionHorasOptions _opciones;
    private readonly IConfiguration _configuration;
    private readonly ILogger<EnviarCorreoHandler> _logger;
    private readonly IAppriseNotificationService? _appriseNotificationService;

    public EnviarCorreoHandler(
        ApplicationDbContext db,
        IEmailService emailService,
        IEventBus bus,
        IOptions<NotificacionHorasOptions> opciones,
        IConfiguration configuration,
        ILogger<EnviarCorreoHandler> logger,
        IAppriseNotificationService? appriseNotificationService = null)
    {
        _db = db;
        _emailService = emailService;
        _bus = bus;
        _opciones = opciones.Value;
        _configuration = configuration;
        _logger = logger;
        _appriseNotificationService = appriseNotificationService;
    }

    public async Task HandleAsync(HorasFaltantesDetectadas evento, CancellationToken ct)
    {
        var ahora = DateTimeOffset.UtcNow;

        if (string.IsNullOrWhiteSpace(evento.Email))
        {
            _logger.LogWarning("Empleado {IdEmpleado} sin correo; no se puede notificar", evento.IdEmpleado);
            await PublicarFallidaAsync(evento, ahora, "Empleado sin correo registrado", ct);
            return;
        }

        if (evento.Origen == OrigenNotificacion.Automatico && evento.FechaCorte is null)
        {
            _logger.LogError("Evento automático sin FechaCorte para empleado {IdEmpleado}", evento.IdEmpleado);
            await PublicarFallidaAsync(evento, ahora, "Evento automático sin FechaCorte", ct);
            return;
        }

        if (!await DebeEnviarAsync(evento, ahora, ct))
        {
            if (evento.Origen == OrigenNotificacion.Manual)
            {
                await _bus.PublishAsync(new NotificacionOmitida(
                    evento.IdEmpleado,
                    evento.InicioPeriodo,
                    evento.FinPeriodo,
                    evento.IdUsuarioEjecutor,
                    ahora,
                    DetectarHorasFaltantesHandler.MotivoUmbral(evento.NombreProyecto, _opciones.UmbralNotificacionManual),
                    IdProyecto: evento.IdProyecto), ct);
            }
            return;
        }

        var (asunto, html) = ConstruirCorreo(evento);

        try
        {
            if (_appriseNotificationService is not null)
            {
                var destinatario = evento.Email;
                var overrideEmail = _configuration["EmailSettings:OverrideEmailRecipient"];
                if (!string.IsNullOrWhiteSpace(overrideEmail))
                {
                    _logger.LogInformation(
                        "REDIRECCIÓN DE CORREO: destinatario real {OriginalEmail} reemplazado por {OverrideEmail}",
                        destinatario, overrideEmail);
                    asunto = $"[PRUEBA para: {destinatario}] {asunto}";
                    destinatario = overrideEmail;
                }

                // Apprise no adjunta imágenes en línea como hace MailKit: el logo va por URL pública
                await _appriseNotificationService.SendEmailAsync(
                    destinatario, asunto, html.Replace("cid:logo_isc", UrlLogoPublica(), StringComparison.Ordinal), ct);
            }
            else
            {
                await _emailService.SendEmailAsync(evento.Email, asunto, html);
            }
        }
        catch (Exception ex)
        {
            // No se relanza: el fallo queda registrado como NotificacionFallida.
            // Si se relanzara, el dispatcher reintentaría y habría registros dobles.
            _logger.LogError(ex, "Falló el envío del correo de horas faltantes al empleado {IdEmpleado}", evento.IdEmpleado);
            await PublicarFallidaAsync(evento, ahora, ex.Message, ct);
            return;
        }

        _logger.LogInformation(
            "Correo de horas faltantes enviado al empleado {IdEmpleado} ({Origen}, {HorasFaltantes}h)",
            evento.IdEmpleado, evento.Origen, evento.HorasFaltantes);

        await _bus.PublishAsync(new NotificacionEnviada(
            evento.IdEmpleado,
            evento.HorasFaltantes,
            evento.InicioPeriodo,
            evento.FinPeriodo,
            evento.Origen,
            evento.FechaCorte,
            evento.IdUsuarioEjecutor,
            ahora,
            evento.IdProyecto,
            HorasProyecto.Describir(evento.Proyectos)), ct);
    }

    // ---------------------------------------------------------------------
    // Regla anti-duplicados
    // ---------------------------------------------------------------------

    private async Task<bool> DebeEnviarAsync(HorasFaltantesDetectadas evento, DateTimeOffset ahora, CancellationToken ct)
    {
        var envios = _db.Set<NotificacionEnvio>().AsNoTracking()
            .Where(n => n.IdEmpleado == evento.IdEmpleado
                     && n.Origen == evento.Origen
                     && n.Estado == EstadoEnvio.Enviado
                     && n.IdProyecto == evento.IdProyecto);

        bool yaEnviadoEnCorte = false;
        DateTimeOffset? ultimoEnvioManual = null;

        // Correos ya enviados cuyo NotificacionEnviada sigue en el Outbox sin registrar: si el mismo
        // empleado llega dos veces seguidas (doble clic, corte publicado dos veces, reintento del detector),
        // el segundo se procesa antes de que RegistrarEnvioHandler guarde el primero.
        var sinRegistrar = await EnviosSinRegistrarAsync(evento.IdEmpleado, evento.Origen, evento.IdProyecto, ct);

        if (evento.Origen == OrigenNotificacion.Automatico)
        {
            // Npgsql solo acepta DateTimeOffset en UTC para timestamptz
            var fechaCorteUtc = evento.FechaCorte!.Value.ToUniversalTime();
            yaEnviadoEnCorte = sinRegistrar.Any(e => e.FechaCorte == fechaCorteUtc)
                || await envios.AnyAsync(n => n.FechaCorte == fechaCorteUtc, ct);
        }
        else
        {
            ultimoEnvioManual = await envios
                .OrderByDescending(n => n.FechaEnvio)
                .Select(n => (DateTimeOffset?)n.FechaEnvio)
                .FirstOrDefaultAsync(ct);

            foreach (var pendiente in sinRegistrar)
            {
                if (ultimoEnvioManual is null || pendiente.FechaEnvio > ultimoEnvioManual)
                    ultimoEnvioManual = pendiente.FechaEnvio;
            }
        }

        var debeEnviar = DebeEnviar(evento.Origen, yaEnviadoEnCorte, ultimoEnvioManual, ahora, _opciones.UmbralNotificacionManual);

        _logger.LogDebug(
            "Regla anti-duplicados para el empleado {IdEmpleado} ({Origen}): ya enviado en el corte = {YaEnviadoEnCorte}, " +
            "último manual = {UltimoEnvioManual}, envíos sin registrar en el Outbox = {SinRegistrar}, umbral = {UmbralHoras} h. Enviar = {DebeEnviar}",
            evento.IdEmpleado, evento.Origen, yaEnviadoEnCorte, ultimoEnvioManual, sinRegistrar.Count,
            _opciones.UmbralNotificacionManual, debeEnviar);

        if (!debeEnviar)
        {
            _logger.LogInformation(
                "Empleado {IdEmpleado} ya notificado ({Origen}, corte {FechaCorte}, último manual {UltimoEnvioManual}); se omite el envío",
                evento.IdEmpleado, evento.Origen, evento.FechaCorte, ultimoEnvioManual);
        }

        return debeEnviar;
    }

    private async Task<List<NotificacionEnviada>> EnviosSinRegistrarAsync(
        int idEmpleado, OrigenNotificacion origen, int? idProyecto, CancellationToken ct)
    {
        // Normalmente son muy pocas filas (las que están en cola en este momento)
        var payloads = await _db.Set<EventoOutbox>().AsNoTracking()
            .Where(e => e.TipoEvento == nameof(NotificacionEnviada) && e.Estado == EstadoEvento.Pendiente)
            .Select(e => e.Payload)
            .ToListAsync(ct);

        return payloads
            .Select(p => JsonSerializer.Deserialize<NotificacionEnviada>(p))
            .Where(e => e is not null && e.IdEmpleado == idEmpleado && e.Origen == origen && e.IdProyecto == idProyecto)
            .Select(e => e!)
            .ToList();
    }

    /// <summary>
    /// Decisión pura (sin BD) de si se debe enviar el correo.
    /// El aviso manual no bloquea al automático del corte y viceversa.
    /// </summary>
    internal static bool DebeEnviar(
        OrigenNotificacion origen,
        bool yaEnviadoEnCorte,
        DateTimeOffset? ultimoEnvioManual,
        DateTimeOffset ahora,
        int umbralHorasManual)
    {
        return origen switch
        {
            OrigenNotificacion.Automatico => !yaEnviadoEnCorte,
            OrigenNotificacion.Manual => ultimoEnvioManual is null
                                         || ahora - ultimoEnvioManual.Value >= TimeSpan.FromHours(umbralHorasManual),
            _ => false
        };
    }

    // ---------------------------------------------------------------------
    // Plantilla del correo
    // ---------------------------------------------------------------------

    private (string Asunto, string Html) ConstruirCorreo(HorasFaltantesDetectadas evento)
    {
        var nombre = WebUtility.HtmlEncode(evento.Nombre);
        var horas = evento.HorasFaltantes.ToString("0.##", CulturaEc);
        var inicio = evento.InicioPeriodo.ToString("dd/MM/yyyy", CulturaEc);
        var fin = evento.FinPeriodo.ToString("dd/MM/yyyy", CulturaEc);
        var frontendUrl = (_configuration["EmailSettings:FrontendUrl"] ?? "http://localhost:3000").TrimEnd('/');
        var enlace = WebUtility.HtmlEncode($"{frontendUrl}/time-report/actividades");

        var proyecto = string.IsNullOrWhiteSpace(evento.NombreProyecto) ? null : evento.NombreProyecto.Trim();

        var asunto = $"Recordatorio: tienes {horas} horas pendientes de registrar" +
                     (proyecto is null ? " " : $" en {proyecto} ") +
                     $"({evento.InicioPeriodo:dd/MM} - {evento.FinPeriodo:dd/MM})";

        // Cada proyecto exige su propia jornada: el aviso manual de una fila de Seguimiento nombra el proyecto
        var enProyecto = proyecto is null ? "" : $" en el proyecto <strong>{WebUtility.HtmlEncode(proyecto)}</strong>";
        var mensaje = evento.Origen == OrigenNotificacion.Manual
            ? $"Tu líder técnico te recuerda que tienes horas pendientes de registrar{enProyecto} en el período del <strong>{inicio}</strong> al <strong>{fin}</strong>."
            : $"Se realizó el {DescribirCorte(evento)} y detectamos que tienes horas pendientes de registrar en el período del <strong>{inicio}</strong> al <strong>{fin}</strong>.";

        // Corte: cada proyecto exige su propia jornada, así que se detalla lo que falta en cada uno
        var detalleProyectos = ConstruirDetalleProyectos(evento.Proyectos);

        var html = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #e5e7eb; border-radius: 8px;'>
                    <div style='text-align: center; margin-bottom: 24px;'>
                        <img src='cid:logo_isc' alt='Integrity Solutions' style='max-height: 48px; width: auto; margin-bottom: 8px;' />
                        <h2 style='color: #163572; margin: 0;'>ISC Time Report</h2>
                        <p style='color: #64748b; margin: 4px 0 0 0;'>Notificación de Horas Faltantes</p>
                    </div>
                    <div style='background-color: #f8fafc; padding: 16px; border-radius: 6px; margin-bottom: 20px; border-left: 4px solid #ef4444;'>
                        <p style='margin: 0; font-size: 16px; color: #1e293b;'>Hola <strong>{nombre}</strong>,</p>
                        <p style='margin: 12px 0 0 0; font-size: 14px; color: #475569; line-height: 1.5;'>
                            {mensaje}
                        </p>
                        <p style='margin: 12px 0 0 0; font-size: 14px; color: #475569;'>
                            Horas faltantes: <strong style='color: #ef4444; font-size: 16px;'>{horas} horas</strong>
                        </p>
                        {detalleProyectos}
                    </div>
                    <div style='text-align: center; margin-bottom: 20px;'>
                        <a href='{enlace}' style='display: inline-block; background-color: #163572; color: #ffffff; text-decoration: none; padding: 12px 24px; border-radius: 6px; font-size: 14px; font-weight: bold;'>Registrar mis horas</a>
                    </div>
                    <hr style='border: 0; border-top: 1px solid #e5e7eb; margin: 20px 0;' />
                    <p style='font-size: 11px; color: #94a3b8; text-align: center; margin: 0;'>
                        Este es un correo automático, por favor no respondas a este mensaje.<br/>
                        © {DateTime.UtcNow.Year} ISC Time Report
                    </p>
                </div>";

        return (asunto, html);
    }

    // Apprise:LogoUrl si está configurado; si no, el logo que publica el frontend en /assets/img
    private string UrlLogoPublica()
    {
        var configurada = _configuration["Apprise:LogoUrl"];
        var url = string.IsNullOrWhiteSpace(configurada)
            ? $"{(_configuration["EmailSettings:FrontendUrl"] ?? "http://localhost:3000").TrimEnd('/')}/assets/img/logo-isc.png"
            : configurada;
        return WebUtility.HtmlEncode(url);
    }

    // Tabla "Proyecto | Horas faltantes"; vacía si no hay proyectos (colaborador sin proyecto asignado)
    private static string ConstruirDetalleProyectos(IReadOnlyList<HorasProyecto>? proyectos)
    {
        if (proyectos is null || proyectos.Count == 0 || proyectos.All(p => p.IdProyecto is null))
            return "";

        var filas = string.Concat(proyectos.Select(p =>
            $"<tr><td style='padding: 6px 8px; border-top: 1px solid #e5e7eb; color: #1e293b;'>{WebUtility.HtmlEncode(p.Nombre)}</td>" +
            $"<td style='padding: 6px 8px; border-top: 1px solid #e5e7eb; color: #ef4444; text-align: right; font-weight: bold;'>" +
            $"{p.HorasFaltantes.ToString("0.##", CulturaEc)} h</td></tr>"));

        return $@"<table style='width: 100%; margin-top: 12px; border-collapse: collapse; font-size: 13px;'>
                            <tr><th style='padding: 6px 8px; text-align: left; color: #64748b;'>Proyecto</th>
                                <th style='padding: 6px 8px; text-align: right; color: #64748b;'>Horas faltantes</th></tr>
                            {filas}
                        </table>";
    }

    private static string DescribirCorte(HorasFaltantesDetectadas evento) =>
        evento.InicioPeriodo.Day == 1 && evento.FinPeriodo.Day == 15
            ? "cierre de quincena"
            : evento.FinPeriodo.AddDays(1).Day == 1
                ? "cierre de fin de mes"
                : "cierre del período";

    private Task PublicarFallidaAsync(HorasFaltantesDetectadas evento, DateTimeOffset fecha, string error, CancellationToken ct) =>
        _bus.PublishAsync(new NotificacionFallida(
            evento.IdEmpleado,
            evento.HorasFaltantes,
            evento.InicioPeriodo,
            evento.FinPeriodo,
            evento.Origen,
            evento.FechaCorte,
            evento.IdUsuarioEjecutor,
            fecha,
            error,
            evento.IdProyecto), ct);
}
