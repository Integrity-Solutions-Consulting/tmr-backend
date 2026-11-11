using System.Collections.Concurrent;
using System.Threading.Channels;

namespace tmr_backend.Features.Notificaciones.Application.Estado;

/// <summary>
/// Despierta en memoria las conexiones SSE abiertas en GET /api/notificaciones/internas/stream
/// cuando se inserta o actualiza una fila en notificacion.notificacion_usuario (RF-010).
/// </summary>
public interface INotificacionInternaNotifier
{
    SuscripcionAvisos Suscribir(int idUsuario);
    void NotificarUsuarios(IEnumerable<int> idsUsuario);
}

public sealed class NotificacionInternaNotifier : INotificacionInternaNotifier
{
    private readonly ConcurrentDictionary<Guid, (int IdUsuario, Channel<bool> Canal)> _suscriptores = new();

    public SuscripcionAvisos Suscribir(int idUsuario)
    {
        var id = Guid.NewGuid();
        var canal = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true
        });
        _suscriptores[id] = (idUsuario, canal);

        return new SuscripcionAvisos(canal.Reader, () =>
        {
            if (_suscriptores.TryRemove(id, out var s))
                s.Canal.Writer.TryComplete();
        });
    }

    public void NotificarUsuarios(IEnumerable<int> idsUsuario)
    {
        var destino = idsUsuario.Distinct().ToHashSet();
        if (destino.Count == 0) return;

        foreach (var (idUsuario, canal) in _suscriptores.Values)
        {
            if (destino.Contains(idUsuario))
                canal.Writer.TryWrite(true);
        }
    }
}

