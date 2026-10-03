namespace TmrBackend.Infrastructure.Messaging;

/// <summary>
/// Publica eventos para que sean procesados en segundo plano.
/// El evento se guarda primero en el Outbox y luego se entrega a sus handlers.
/// </summary>
public interface IEventBus
{
    Task PublishAsync<T>(T evento, CancellationToken ct = default) where T : IEvento;
}