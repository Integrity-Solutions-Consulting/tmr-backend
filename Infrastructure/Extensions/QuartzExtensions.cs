using Quartz;
using tmr_backend.Features.Notificaciones.Domain;
using tmr_backend.Features.Notificaciones.Infrastructure.Jobs;

namespace tmr_backend.Infrastructure.Extensions;

/// <summary>
/// Extensión para registrar Quartz y el job de notificación de horas faltantes (flujo 1)
/// </summary>
public static class QuartzExtensions
{
    // Si el backend estaba apagado a la hora de un corte, al arrancar se ejecuta ese corte
    // solo si pasó hace menos de esta ventana; si no, se descarta.
    private static readonly TimeSpan VentanaCorteAtrasado = TimeSpan.FromHours(24);

    /// <summary>
    /// Registra Quartz, RevisarHorasFaltantesJob y dos triggers que se repiten cada mes:
    /// el día 15 y el último día del mes a las 18:00 de Ecuador. Si al arrancar el último
    /// corte pasó hace menos de 24 h, agrega un disparo único para recuperarlo.
    /// Nunca impide el arranque del backend.
    /// Uso: builder.Services.AddNotificacionesQuartz(builder.Configuration);
    /// </summary>
    public static IServiceCollection AddNotificacionesQuartz(this IServiceCollection services, IConfiguration configuration)
    {
        var seccion = configuration.GetSection("NotificacionHoras");
        services.Configure<NotificacionHorasOptions>(seccion);

        var opciones = seccion.Get<NotificacionHorasOptions>() ?? new NotificacionHorasOptions();
        var ahora = DateTimeOffset.UtcNow;

        var ultimoCorte = RevisarHorasFaltantesJob.UltimoCorte(ahora);
        DateTimeOffset? corteARecuperar = opciones.Habilitado && ahora - ultimoCorte < VentanaCorteAtrasado
            ? ultimoCorte
            : null;

        services.AddQuartz(q =>
        {
            var jobKey = new JobKey(nameof(RevisarHorasFaltantesJob));

            // StoreDurably: el job queda registrado aunque no tenga trigger (Habilitado = false)
            q.AddJob<RevisarHorasFaltantesJob>(j => j.WithIdentity(jobKey).StoreDurably());

            if (opciones.Habilitado)
            {
                // Las expresiones cron están en UTC (18:00 Ecuador = 23:00 UTC; Ecuador no tiene horario de verano).
                // Se usa TimeZoneInfo.Utc porque la imagen Alpine del contenedor no trae la base de zonas horarias.
                q.AddTrigger(t => t
                    .ForJob(jobKey)
                    .WithIdentity("corte-quincena")
                    .WithCronSchedule(RevisarHorasFaltantesJob.CronQuincena, c => c
                        .InTimeZone(TimeZoneInfo.Utc)
                        .WithMisfireInstruction(CronTriggerMisfireInstruction.FireAndProceed)));

                q.AddTrigger(t => t
                    .ForJob(jobKey)
                    .WithIdentity("corte-fin-de-mes")
                    .WithCronSchedule(RevisarHorasFaltantesJob.CronFinDeMes, c => c
                        .InTimeZone(TimeZoneInfo.Utc)
                        .WithMisfireInstruction(CronTriggerMisfireInstruction.FireAndProceed)));
            }

            if (corteARecuperar is { } corte)
            {
                // Quartz guarda la programación en memoria: un disparo perdido con el backend apagado
                // no se recupera solo. Se pasa la fecha del corte para que el evento sea el mismo
                // que habría publicado el cron y el control de duplicados funcione.
                q.AddTrigger(t => t
                    .ForJob(jobKey)
                    .WithIdentity($"corte-recuperado-{corte:yyyyMMddHHmm}")
                    .UsingJobData(RevisarHorasFaltantesJob.ClaveFechaCorte, corte.ToString("O"))
                    .StartNow()
                    .WithSimpleSchedule(s => s.WithRepeatCount(0)));
            }

            // Pendiente: cuando existan las tablas QRTZ_* en Postgres, usar el JobStore
            // persistente (en memoria por ahora):
            // q.UsePersistentStore(s => s.UsePostgres(configuration.GetConnectionString("DefaultConnection")!));
        });

        services.AddQuartzHostedService(o => o.WaitForJobsToComplete = true);
        services.AddHostedService(sp => new RegistroCorteAlArrancar(
            sp.GetRequiredService<ILogger<RegistroCorteAlArrancar>>(), opciones.Habilitado, corteARecuperar));

        return services;
    }

    /// <summary>
    /// Deja en el log, al arrancar, si los cortes quedaron programados y si se recupera uno atrasado.
    /// </summary>
    private sealed class RegistroCorteAlArrancar(
        ILogger<RegistroCorteAlArrancar> logger,
        bool habilitado,
        DateTimeOffset? corteARecuperar) : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken)
        {
            if (!habilitado)
            {
                logger.LogWarning("Cortes de horas NO programados: NotificacionHoras:Habilitado = false");
                return Task.CompletedTask;
            }

            logger.LogInformation(
                "Cortes de horas programados: día 15 y último día de cada mes a las 18:00 (Ecuador). Próximo corte: {ProximoCorte}",
                RevisarHorasFaltantesJob.ProximoCorte(DateTimeOffset.UtcNow));

            if (corteARecuperar is { } corte)
                logger.LogInformation(
                    "El corte {FechaCorte} pasó hace menos de {Horas} h; se ejecuta ahora por si el backend estaba apagado",
                    corte, VentanaCorteAtrasado.TotalHours);

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
