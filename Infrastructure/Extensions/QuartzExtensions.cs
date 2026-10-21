using Quartz;
using tmr_backend.Features.Notificaciones.Domain;
using tmr_backend.Features.Notificaciones.Infrastructure.Jobs;

namespace tmr_backend.Infrastructure.Extensions;

/// <summary>
/// Extensión para registrar Quartz y el job de notificación de horas faltantes (flujos 1 y 3)
/// </summary>
public static class QuartzExtensions
{
    /// <summary>
    /// Registra Quartz y RevisarHorasFaltantesJob con un trigger que se ejecuta cada minuto.
    /// Las fechas de corte no están en el cron: el job las lee de notificacion.notificacion_fecha_corte,
    /// así se pueden cambiar desde la pantalla Fechas de Corte sin reiniciar el backend.
    /// Como el estado (ejecutada o no) queda en la BD, un corte que pasó con el backend apagado
    /// se ejecuta al volver, si fue hace menos de 24 h. Nunca impide el arranque del backend.
    /// Uso: builder.Services.AddNotificacionesQuartz(builder.Configuration);
    /// </summary>
    public static IServiceCollection AddNotificacionesQuartz(this IServiceCollection services, IConfiguration configuration)
    {
        var seccion = configuration.GetSection("NotificacionHoras");
        services.Configure<NotificacionHorasOptions>(seccion);

        var opciones = seccion.Get<NotificacionHorasOptions>() ?? new NotificacionHorasOptions();

        services.AddQuartz(q =>
        {
            var jobKey = new JobKey(nameof(RevisarHorasFaltantesJob));

            // StoreDurably: el job queda registrado aunque no tenga trigger (Habilitado = false)
            q.AddJob<RevisarHorasFaltantesJob>(j => j.WithIdentity(jobKey).StoreDurably());

            if (opciones.Habilitado)
            {
                // Una revisión perdida no importa: la siguiente (un minuto después) encuentra el mismo corte
                q.AddTrigger(t => t
                    .ForJob(jobKey)
                    .WithIdentity("revision-fechas-corte")
                    .WithCronSchedule(RevisarHorasFaltantesJob.CronRevision, c => c
                        .InTimeZone(TimeZoneInfo.Utc)
                        .WithMisfireInstruction(CronTriggerMisfireInstruction.DoNothing)));
            }
        });

        services.AddQuartzHostedService(o => o.WaitForJobsToComplete = true);
        services.AddHostedService(sp => new RegistroCorteAlArrancar(
            sp.GetRequiredService<ILogger<RegistroCorteAlArrancar>>(), opciones.Habilitado));

        return services;
    }

    /// <summary>
    /// Deja en el log, al arrancar, si la revisión de cortes quedó programada.
    /// </summary>
    private sealed class RegistroCorteAlArrancar(
        ILogger<RegistroCorteAlArrancar> logger,
        bool habilitado) : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken)
        {
            if (!habilitado)
                logger.LogWarning("Cortes de horas NO programados: NotificacionHoras:Habilitado = false");
            else
                logger.LogInformation(
                    "Revisión de fechas de corte programada cada minuto (tabla notificacion.notificacion_fecha_corte; " +
                    "se editan en Time Report > Fechas de Corte)");

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
