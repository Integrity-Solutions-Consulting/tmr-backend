using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using tmr_backend.Features.Notificaciones.Domain;
using tmr_backend.Features.Notificaciones.Domain.Events;
using tmr_backend.Features.Notificaciones.Application.Cortes;
using tmr_backend.Features.Notificaciones.Application.Estado;
using tmr_backend.Features.Notificaciones.Application.Queries;
using tmr_backend.Features.Notificaciones.Application.Handlers;
using tmr_backend.Infrastructure.Messaging;
using tmr_backend.Features.Notificaciones.Infrastructure.Apprise;

namespace tmr_backend.Features.Notificaciones;

public static class NotificacionesModule
{
    public static IServiceCollection AddNotificacionesModule(
        this IServiceCollection services, IConfiguration configuration)
    {
        // Configuración del módulo (appsettings.json → NotificacionHoras)
        services.Configure<NotificacionHorasOptions>(configuration.GetSection("NotificacionHoras"));
        services.Configure<AppriseOptions>(configuration.GetSection("Apprise"));

        // Sin ApiUrl no se registra: el handler cae a SMTP en vez de fallar en cada envío
        if (configuration.GetValue<bool>("Apprise:Enabled")
            && !string.IsNullOrWhiteSpace(configuration["Apprise:ApiUrl"]))
        {
            services.AddHttpClient<IAppriseNotificationService, AppriseNotificationService>((serviceProvider, client) =>
            {
                var options = serviceProvider.GetRequiredService<
                    Microsoft.Extensions.Options.IOptions<AppriseOptions>>().Value;
                client.Timeout = TimeSpan.FromSeconds(Math.Max(1, options.TimeoutSeconds));
            });
        }

        // Aviso en memoria a las conexiones SSE de /estado/stream (uno solo para toda la aplicación)
        services.AddSingleton<INotificacionEstadoNotifier, NotificacionEstadoNotifier>();
        // Aviso en memoria a las conexiones SSE de /cortes/stream (resumen del corte para admin y líderes)
        services.AddSingleton<ICorteNotifier, CorteNotifier>();
        // Aviso en memoria a la campana de cada colaborador (/mias/stream)
        services.AddSingleton<IAvisoColaboradorNotifier, AvisoColaboradorNotifier>();

        // Lógica de dominio y consultas
        services.AddScoped<CalculadorHorasFaltantes>();
        services.AddScoped<IConsultaHorasFaltantes, ConsultaHorasFaltantes>();
        services.AddScoped<IConsultaResumenCorte, ConsultaResumenCorte>();
        services.AddSingleton<IResumenCorteCache, ResumenCorteCache>();
        services.AddScoped<IConsultaMisNotificaciones, ConsultaMisNotificaciones>();
        // Fechas de corte editables (flujo 3): las lee el job y las cambia la pantalla Fechas de Corte
        services.AddScoped<ICalendarioCortesService, CalendarioCortesService>();

        // Handlers: qué handler escucha qué evento

        // Detección (flujos 1 y 2): escucha los dos eventos de entrada
        services.AddScoped<IEventHandler<CorteDeHorasAlcanzado>, DetectarHorasFaltantesHandler>();
        services.AddScoped<IEventHandler<NotificacionManualSolicitada>, DetectarHorasFaltantesHandler>();

        // Envío (flujos 1 y 2)
        services.AddScoped<IEventHandler<HorasFaltantesDetectadas>, EnviarCorreoHandler>();

        // Registro (flujos 1 y 2)
        services.AddScoped<IEventHandler<NotificacionEnviada>, RegistrarEnvioHandler>();
        services.AddScoped<IEventHandler<NotificacionFallida>, RegistrarEnvioHandler>();
        services.AddScoped<IEventHandler<NotificacionOmitida>, RegistrarEnvioHandler>();

        // Avisos que agotaron sus reintentos: quedan registrados como Fallido
        services.AddScoped<IEventoFallidoHandler<HorasFaltantesDetectadas>, FalloDefinitivoHandler>();
        services.AddScoped<IEventoFallidoHandler<NotificacionManualSolicitada>, FalloDefinitivoHandler>();

        return services;
    }
}