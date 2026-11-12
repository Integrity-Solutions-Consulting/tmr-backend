using System.Threading.Channels;
using tmr_backend.Infrastructure.Messaging;

namespace tmr_backend.Infrastructure.Extensions;

public static class MessagingExtensions
{
    public static IServiceCollection AddMessaging(this IServiceCollection services)
    {
        // 1. El canal en memoria: UNO solo para toda la aplicación (singleton)
        services.AddSingleton(Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions
        {
            SingleReader = true    // solo el EventDispatcher lee de él
        }));

        // 2. Diccionario "nombre del evento → clase", para que el dispatcher reconstruya el JSON
        var tiposDeEvento = typeof(IEvento).Assembly.GetTypes()
            .Where(t => typeof(IEvento).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
            .ToDictionary(t => t.Name);
        services.AddSingleton<IReadOnlyDictionary<string, Type>>(tiposDeEvento);

        // 3. El bus: scoped, porque usa el DbContext
        services.AddScoped<IEventBus, EventBus>();

        // 4. El dispatcher: .NET lo arranca solo en app.Run() y vive mientras el backend esté encendido
        services.AddHostedService<EventDispatcher>();

        return services;
    }
}