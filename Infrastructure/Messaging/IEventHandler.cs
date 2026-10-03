namespace TmrBackend.Infrastructure.Messaging;

/// <summary>
/// Procesa un tipo de evento.
/// Cada handler implementa esta interfaz para el evento que escucha.
/// </summary>
public interface IEventHandler<in T> where T : IEvento
{
    Task HandleAsync(T evento, CancellationToken ct);
}