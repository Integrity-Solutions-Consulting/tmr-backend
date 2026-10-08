using Microsoft.Extensions.Options;
using Quartz;
using TmrBackend.Features.Notificaciones.Domain;
using TmrBackend.Features.Notificaciones.Domain.Events;
using TmrBackend.Infrastructure.Messaging;

namespace TmrBackend.Features.Notificaciones.Infrastructure.Jobs;

/// <summary>
/// Job de Quartz de los flujos 1 y 3. Solo publica CorteDeHorasAlcanzado y termina:
/// el cálculo y el envío los hacen DetectarHorasFaltantesHandler y EnviarCorreoHandler.
/// Flujo 1: el corte sale de appsettings.json (NotificacionHoras).
/// Flujo 3 (futuro): basta con cambiar ObtenerCortesPendientes para leer NotificacionFechaCorte.
/// </summary>
[DisallowConcurrentExecution]
public sealed class RevisarHorasFaltantesJob : IJob
{
    private readonly IEventBus _bus;
    private readonly NotificacionHorasOptions _opciones;
    private readonly ILogger<RevisarHorasFaltantesJob> _logger;

    public RevisarHorasFaltantesJob(
        IEventBus bus,
        IOptions<NotificacionHorasOptions> opciones,
        ILogger<RevisarHorasFaltantesJob> logger)
    {
        _bus = bus;
        _opciones = opciones.Value;
        _logger = logger;
    }

    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken)
    {
        if (!_opciones.Habilitado)
        {
            _logger.LogInformation("Notificación de horas faltantes deshabilitada (NotificacionHoras:Habilitado = false)");
            return;
        }

        foreach (var corte in ObtenerCortesPendientes())
        {
            await _bus.PublishAsync(corte, cancellationToken);

            _logger.LogInformation(
                "Publicado CorteDeHorasAlcanzado: período {InicioPeriodo} - {FinPeriodo}, corte {FechaCorte}",
                corte.InicioPeriodo, corte.FinPeriodo, corte.FechaCorte);
        }
    }

    private IEnumerable<CorteDeHorasAlcanzado> ObtenerCortesPendientes()
    {
        yield return new CorteDeHorasAlcanzado(_opciones.InicioPeriodo, _opciones.FinPeriodo, _opciones.FechaCorte);
    }
}
