using Quartz;
using TmrBackend.Features.Notificaciones.Domain;
using TmrBackend.Features.Notificaciones.Infrastructure.Jobs;

namespace tmr_backend.Infrastructure.Extensions;

/// <summary>
/// Extensión para registrar Quartz y el job de notificación de horas faltantes (flujo 1)
/// </summary>
public static class QuartzExtensions
{
    // Si el backend arranca más tarde que FechaCorte, el job se ejecuta al arrancar
    // solo si el corte pasó hace menos de esta ventana; si no, se descarta.
    private static readonly TimeSpan VentanaCorteAtrasado = TimeSpan.FromHours(24);

    /// <summary>
    /// Registra Quartz, RevisarHorasFaltantesJob y un trigger que se dispara una sola vez
    /// en NotificacionHoras:FechaCorte. Nunca impide el arranque del backend.
    /// Uso: builder.Services.AddNotificacionesQuartz(builder.Configuration);
    /// </summary>
    public static IServiceCollection AddNotificacionesQuartz(this IServiceCollection services, IConfiguration configuration)
    {
        var seccion = configuration.GetSection("NotificacionHoras");
        services.Configure<NotificacionHorasOptions>(seccion);

        var opciones = seccion.Get<NotificacionHorasOptions>() ?? new NotificacionHorasOptions();
        var motivoSinProgramar = ValidarCorte(opciones, DateTimeOffset.UtcNow);

        services.AddQuartz(q =>
        {
            var jobKey = new JobKey(nameof(RevisarHorasFaltantesJob));

            // StoreDurably: el job queda registrado aunque no tenga trigger (Habilitado = false)
            q.AddJob<RevisarHorasFaltantesJob>(j => j.WithIdentity(jobKey).StoreDurably());

            if (motivoSinProgramar is null)
            {
                q.AddTrigger(t => t
                    .ForJob(jobKey)
                    .WithIdentity($"corte-{opciones.FechaCorte.ToUniversalTime():yyyyMMddHHmm}")
                    .StartAt(opciones.FechaCorte)
                    .WithSimpleSchedule(s => s
                        .WithRepeatCount(0)
                        .WithMisfireInstruction(SimpleTriggerMisfireInstruction.FireNow)));
            }

            // Pendiente: cuando existan las tablas QRTZ_* en Postgres, usar el JobStore
            // persistente (en memoria por ahora):
            // q.UsePersistentStore(s => s.UsePostgres(configuration.GetConnectionString("DefaultConnection")!));
        });

        services.AddQuartzHostedService(o => o.WaitForJobsToComplete = true);
        services.AddHostedService(sp => new RegistroCorteAlArrancar(
            sp.GetRequiredService<ILogger<RegistroCorteAlArrancar>>(), opciones, motivoSinProgramar));

        return services;
    }

    /// <summary>
    /// Devuelve por qué no se debe programar el corte, o null si se puede programar.
    /// </summary>
    private static string? ValidarCorte(NotificacionHorasOptions o, DateTimeOffset ahora)
    {
        if (!o.Habilitado)
            return "NotificacionHoras:Habilitado = false";
        if (o.FechaCorte == default || o.InicioPeriodo == default || o.FinPeriodo == default)
            return "faltan InicioPeriodo, FinPeriodo o FechaCorte en NotificacionHoras";
        if (o.InicioPeriodo > o.FinPeriodo)
            return "InicioPeriodo es posterior a FinPeriodo";
        if (o.FechaCorte < ahora - VentanaCorteAtrasado)
            return $"FechaCorte ({o.FechaCorte:O}) ya pasó hace más de {VentanaCorteAtrasado.TotalHours} horas";
        return null;
    }

    /// <summary>
    /// Deja en el log, al arrancar, si el corte quedó programado o por qué no.
    /// </summary>
    private sealed class RegistroCorteAlArrancar(
        ILogger<RegistroCorteAlArrancar> logger,
        NotificacionHorasOptions opciones,
        string? motivoSinProgramar) : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken)
        {
            if (motivoSinProgramar is null)
                logger.LogInformation(
                    "Corte de horas programado para {FechaCorte} (período {InicioPeriodo} - {FinPeriodo})",
                    opciones.FechaCorte, opciones.InicioPeriodo, opciones.FinPeriodo);
            else
                logger.LogWarning("Corte de horas NO programado: {Motivo}", motivoSinProgramar);

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
