using System.Globalization;
using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using tmr_backend.Infrastructure.Database;
using tmr_backend.Infrastructure.Shared;
using TmrBackend.Features.Notificaciones.Domain;
using TmrBackend.Features.Notificaciones.Domain.Events;
using TmrBackend.Infrastructure.Messaging;

namespace TmrBackend.Features.Notificaciones.Application.Handlers;

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

    public EnviarCorreoHandler(
        ApplicationDbContext db,
        IEmailService emailService,
        IEventBus bus,
        IOptions<NotificacionHorasOptions> opciones,
        IConfiguration configuration,
        ILogger<EnviarCorreoHandler> logger)
    {
        _db = db;
        _emailService = emailService;
        _bus = bus;
        _opciones = opciones.Value;
        _configuration = configuration;
        _logger = logger;
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
            return;

        var (asunto, html) = ConstruirCorreo(evento);

        try
        {
            await _emailService.SendEmailAsync(evento.Email, asunto, html);
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
            ahora), ct);
    }

    // ---------------------------------------------------------------------
    // Regla anti-duplicados
    // ---------------------------------------------------------------------

    private async Task<bool> DebeEnviarAsync(HorasFaltantesDetectadas evento, DateTimeOffset ahora, CancellationToken ct)
    {
        var envios = _db.Set<NotificacionEnvio>().AsNoTracking()
            .Where(n => n.IdEmpleado == evento.IdEmpleado
                     && n.Origen == evento.Origen
                     && n.Estado == EstadoEnvio.Enviado);

        bool yaEnviadoEnCorte = false;
        DateTimeOffset? ultimoEnvioManual = null;

        if (evento.Origen == OrigenNotificacion.Automatico)
        {
            // Npgsql solo acepta DateTimeOffset en UTC para timestamptz
            var fechaCorteUtc = evento.FechaCorte!.Value.ToUniversalTime();
            yaEnviadoEnCorte = await envios.AnyAsync(n => n.FechaCorte == fechaCorteUtc, ct);
        }
        else
        {
            ultimoEnvioManual = await envios
                .OrderByDescending(n => n.FechaEnvio)
                .Select(n => (DateTimeOffset?)n.FechaEnvio)
                .FirstOrDefaultAsync(ct);
        }

        var debeEnviar = DebeEnviar(evento.Origen, yaEnviadoEnCorte, ultimoEnvioManual, ahora, _opciones.UmbralNotificacionManual);

        if (!debeEnviar)
        {
            _logger.LogInformation(
                "Empleado {IdEmpleado} ya notificado ({Origen}, corte {FechaCorte}, último manual {UltimoEnvioManual}); se omite el envío",
                evento.IdEmpleado, evento.Origen, evento.FechaCorte, ultimoEnvioManual);
        }

        return debeEnviar;
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

        var asunto = $"Recordatorio: tienes {horas} horas pendientes de registrar " +
                     $"({evento.InicioPeriodo:dd/MM} - {evento.FinPeriodo:dd/MM})";

        var mensaje = evento.Origen == OrigenNotificacion.Manual
            ? $"Tu líder técnico te recuerda que tienes horas pendientes de registrar en el período del <strong>{inicio}</strong> al <strong>{fin}</strong>."
            : $"Se realizó el {DescribirCorte(evento)} y detectamos que tienes horas pendientes de registrar en el período del <strong>{inicio}</strong> al <strong>{fin}</strong>.";

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
                    </div>
                    <div style='background-color: #ffffff; border: 1px solid #e2e8f0; border-radius: 6px; padding: 16px; margin-bottom: 20px;'>
                        <h4 style='margin: 0 0 10px 0; font-size: 14px; color: #163572;'>📌 Pasos para completar tu registro:</h4>
                        <ol style='margin: 0; padding-left: 20px; font-size: 13px; color: #475569; line-height: 1.8;'>
                            <li>Abre la plataforma <strong>TMR</strong> en tu navegador habitual.</li>
                            <li>Ingresa al módulo <strong>Time Report</strong> &gt; <strong>Actividades</strong>.</li>
                            <li>Registra y guarda tus horas pendientes para el proyecto correspondiente.</li>
                        </ol>
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
            error), ct);
}
