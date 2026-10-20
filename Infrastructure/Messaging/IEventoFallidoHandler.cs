namespace tmr_backend.Infrastructure.Messaging;

/// <summary>
/// Reacciona cuando un evento agotó sus reintentos y quedó como Fallido en el Outbox,
/// para que el resultado no se pierda (p. ej. registrar el aviso como fallido).
/// </summary>
public interface IEventoFallidoHandler<in T> where T : IEvento
{
    Task HandleFalloDefinitivoAsync(T evento, string error, CancellationToken ct);
}
