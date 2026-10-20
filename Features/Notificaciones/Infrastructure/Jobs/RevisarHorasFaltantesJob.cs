using System.Globalization;
using Microsoft.Extensions.Options;
using Quartz;
using tmr_backend.Features.Notificaciones.Application.Estado;
using tmr_backend.Features.Notificaciones.Domain;
using tmr_backend.Features.Notificaciones.Domain.Events;
using tmr_backend.Infrastructure.Messaging;

namespace tmr_backend.Features.Notificaciones.Infrastructure.Jobs;

/// <summary>
/// Job de Quartz de los flujos 1 y 3. Solo publica CorteDeHorasAlcanzado y termina:
/// el cálculo y el envío los hacen DetectarHorasFaltantesHandler y EnviarCorreoHandler.
/// Flujo 1: se dispara el día 15 y el último día de cada mes a las 18:00 de Ecuador
/// (triggers cron de QuartzExtensions); el período sale de la fecha del corte.
/// Flujo 3 (futuro): basta con cambiar de dónde sale la fecha del corte para leer NotificacionFechaCorte.
/// </summary>
[DisallowConcurrentExecution]
public sealed class RevisarHorasFaltantesJob : IJob
{
    // 18:00 en Ecuador (UTC−5, sin horario de verano) = 23:00 UTC. "L" = último día del mes.
    public const string CronQuincena = "0 0 23 15 * ?";
    public const string CronFinDeMes = "0 0 23 L * ?";

    // Clave del JobDataMap con la que el trigger de recuperación indica qué corte ejecutar.
    public const string ClaveFechaCorte = "fechaCorte";

    private static readonly TimeSpan OffsetEcuador = TimeSpan.FromHours(-5);
    private const int HoraCorte = 18;

    private readonly IEventBus _bus;
    private readonly NotificacionHorasOptions _opciones;
    private readonly ILogger<RevisarHorasFaltantesJob> _logger;
    private readonly ICorteNotifier? _corteNotifier;

    public RevisarHorasFaltantesJob(
        IEventBus bus,
        IOptions<NotificacionHorasOptions> opciones,
        ILogger<RevisarHorasFaltantesJob> logger,
        ICorteNotifier? corteNotifier = null)
    {
        _bus = bus;
        _opciones = opciones.Value;
        _logger = logger;
        _corteNotifier = corteNotifier;
    }

    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken)
    {
        if (!_opciones.Habilitado)
        {
            _logger.LogInformation("Notificación de horas faltantes deshabilitada (NotificacionHoras:Habilitado = false)");
            return;
        }

        await PublicarCorteAsync(FechaCorteDelDisparo(context), cancellationToken);
    }

    /// <summary>
    /// Publica CorteDeHorasAlcanzado con el período (quincena o fin de mes) al que pertenece la fecha del corte.
    /// </summary>
    public async Task PublicarCorteAsync(DateTimeOffset fechaCorte, CancellationToken cancellationToken)
    {
        var (inicio, fin) = PeriodoDelCorte(fechaCorte);
        var corte = new CorteDeHorasAlcanzado(inicio, fin, fechaCorte.ToOffset(OffsetEcuador));

        await _bus.PublishAsync(corte, cancellationToken);

        // Admin y líderes conectados ven aparecer el corte "en proceso" de inmediato
        _corteNotifier?.NotificarCambio();

        _logger.LogInformation(
            "Publicado CorteDeHorasAlcanzado: período {InicioPeriodo} - {FinPeriodo}, corte {FechaCorte}",
            corte.InicioPeriodo, corte.FinPeriodo, corte.FechaCorte);
    }

    // ---------------------------------------------------------------------
    // Calendario de cortes (funciones puras, sin Quartz ni BD)
    // ---------------------------------------------------------------------

    /// <summary>
    /// Período que cierra el corte: 1–15 si el corte cae en la primera quincena, 16–fin de mes si no.
    /// Se usa el día en Ecuador, no en UTC.
    /// </summary>
    public static (DateOnly Inicio, DateOnly Fin) PeriodoDelCorte(DateTimeOffset fechaCorte) =>
        CalculadorHorasFaltantes.PeriodoEnCurso(DateOnly.FromDateTime(fechaCorte.ToOffset(OffsetEcuador).DateTime));

    /// <summary>
    /// Último corte (día 15 o último día del mes, 18:00 Ecuador) que ocurrió en o antes de <paramref name="ahora"/>.
    /// </summary>
    public static DateTimeOffset UltimoCorte(DateTimeOffset ahora)
    {
        var mes = new DateOnly(ahora.ToOffset(OffsetEcuador).Year, ahora.ToOffset(OffsetEcuador).Month, 1);
        var anterior = mes.AddMonths(-1);

        return new[] { CorteFinDeMes(anterior), CorteQuincena(mes), CorteFinDeMes(mes) }
            .Last(c => c <= ahora);
    }

    /// <summary>
    /// Siguiente corte después de <paramref name="ahora"/>. Solo se usa para el log al arrancar.
    /// </summary>
    public static DateTimeOffset ProximoCorte(DateTimeOffset ahora)
    {
        var mes = new DateOnly(ahora.ToOffset(OffsetEcuador).Year, ahora.ToOffset(OffsetEcuador).Month, 1);
        var siguiente = mes.AddMonths(1);

        return new[] { CorteQuincena(mes), CorteFinDeMes(mes), CorteQuincena(siguiente) }
            .First(c => c > ahora);
    }

    private static DateTimeOffset CorteQuincena(DateOnly mes) =>
        new(mes.Year, mes.Month, 15, HoraCorte, 0, 0, OffsetEcuador);

    private static DateTimeOffset CorteFinDeMes(DateOnly mes) =>
        new(mes.Year, mes.Month, DateTime.DaysInMonth(mes.Year, mes.Month), HoraCorte, 0, 0, OffsetEcuador);

    /// <summary>
    /// Fecha del corte que se está ejecutando: la del trigger de recuperación si viene en el JobDataMap,
    /// o la hora programada del cron (no "ahora"), para que un mismo corte tenga siempre la misma FechaCorte
    /// y el control de duplicados de NotificacionEnvio funcione.
    /// </summary>
    private static DateTimeOffset FechaCorteDelDisparo(IJobExecutionContext context)
    {
        if (context.MergedJobDataMap.TryGetValue(ClaveFechaCorte, out var valor) && valor is string texto)
            return DateTimeOffset.Parse(texto, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

        return context.ScheduledFireTimeUtc ?? context.FireTimeUtc;
    }
}
