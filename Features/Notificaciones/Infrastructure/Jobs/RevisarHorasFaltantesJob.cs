using Microsoft.Extensions.Options;
using Quartz;
using tmr_backend.Features.Notificaciones.Application.Cortes;
using tmr_backend.Features.Notificaciones.Application.Estado;
using tmr_backend.Features.Notificaciones.Domain;
using tmr_backend.Features.Notificaciones.Domain.Events;
using tmr_backend.Infrastructure.Messaging;

namespace tmr_backend.Features.Notificaciones.Infrastructure.Jobs;

/// <summary>
/// Job de Quartz de los flujos 1 y 3. Se ejecuta cada minuto y revisa notificacion.notificacion_fecha_corte:
/// por cada corte cuya fecha ya llegó y que nadie ejecutó, publica CorteDeHorasAlcanzado y termina.
/// El cálculo y el envío los hacen DetectarHorasFaltantesHandler y EnviarCorreoHandler.
/// Las fechas por defecto (día 15 y último día del mes, 18:00 de Ecuador) se crean solas, y se
/// pueden cambiar desde la pantalla Fechas de Corte (permiso NOTIFICACIONES_CONFIGURAR).
/// </summary>
[DisallowConcurrentExecution]
public sealed class RevisarHorasFaltantesJob : IJob
{
    // Segundo 0 de cada minuto (formato de Quartz: seg min hora díaMes mes díaSemana)
    public const string CronRevision = "0 * * * * ?";

    private readonly IEventBus _bus;
    private readonly ICalendarioCortesService _calendario;
    private readonly NotificacionHorasOptions _opciones;
    private readonly ILogger<RevisarHorasFaltantesJob> _logger;
    private readonly ICorteNotifier? _corteNotifier;

    public RevisarHorasFaltantesJob(
        IEventBus bus,
        ICalendarioCortesService calendario,
        IOptions<NotificacionHorasOptions> opciones,
        ILogger<RevisarHorasFaltantesJob> logger,
        ICorteNotifier? corteNotifier = null)
    {
        _bus = bus;
        _calendario = calendario;
        _opciones = opciones.Value;
        _logger = logger;
        _corteNotifier = corteNotifier;
    }

    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken)
    {
        if (!_opciones.Habilitado)
            return;

        try
        {
            await _calendario.AsegurarCortesAsync(cancellationToken);

            var ahora = DateTimeOffset.UtcNow;
            IReadOnlyList<NotificacionFechaCorte> pendientes = await _calendario.PendientesAsync(ahora, cancellationToken);

            // Corre cada minuto: sin pendientes va en Trace para no llenar la consola
            if (pendientes.Count == 0)
                _logger.LogTrace("Revisión de fechas de corte: sin cortes pendientes");
            else
                _logger.LogDebug("Revisión de fechas de corte: {Cantidad} corte(s) pendiente(s)", pendientes.Count);

            foreach (NotificacionFechaCorte corte in pendientes)
                await EjecutarCorteAsync(corte, ahora, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Se vuelve a intentar en el siguiente minuto
            _logger.LogError(ex, "Error al revisar las fechas de corte de horas");
        }
    }

    private async Task EjecutarCorteAsync(NotificacionFechaCorte corte, DateTimeOffset ahora, CancellationToken ct)
    {
        // Marca primero: si hay varias instancias del backend, solo una publica el corte
        if (!await _calendario.TomarParaEjecutarAsync(corte.Id, ahora, ct))
        {
            _logger.LogDebug("El corte {IdCorte} ({Tipo} {Mes}/{Anio}) ya lo tomó otra instancia; se omite",
                corte.Id, corte.Tipo, corte.Mes, corte.Anio);
            return;
        }

        var evento = new CorteDeHorasAlcanzado(
            corte.InicioPeriodo,
            corte.FinPeriodo,
            corte.FechaCorte.ToOffset(CalendarioCortes.OffsetEcuador));

        try
        {
            await _bus.PublishAsync(evento, ct);
        }
        catch
        {
            // El detalle del error lo registra el catch de Execute
            _logger.LogWarning("No se pudo publicar el corte {IdCorte} ({Tipo} {Mes}/{Anio}); se libera para reintentarlo en la próxima revisión",
                corte.Id, corte.Tipo, corte.Mes, corte.Anio);
            await _calendario.LiberarAsync(corte.Id, CancellationToken.None);
            throw;
        }

        // Admin y líderes conectados ven aparecer el corte "en proceso" de inmediato
        _corteNotifier?.NotificarCambio();

        _logger.LogInformation(
            "Publicado CorteDeHorasAlcanzado ({Tipo} {Mes}/{Anio}): período {InicioPeriodo} - {FinPeriodo}, corte {FechaCorte}",
            corte.Tipo, corte.Mes, corte.Anio, evento.InicioPeriodo, evento.FinPeriodo, evento.FechaCorte);
    }
}
