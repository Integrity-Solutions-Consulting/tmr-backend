using System.Collections.Concurrent;
using System.Threading.Channels;

namespace tmr_backend.Features.Notificaciones.Application.Estado;

/// <summary>
/// Avisa en memoria a las conexiones SSE de /cortes/stream que el corte automático cambió
/// (empezó, se registró un envío, terminó). No lleva datos: cada conexión vuelve a armar el resumen
/// desde la BD, así que da igual cuántos avisos lleguen seguidos.
/// </summary>
public interface ICorteNotifier
{
    SuscripcionCorte Suscribir();
    void NotificarCambio();
}

public sealed class SuscripcionCorte : IDisposable
{
    private readonly Action _alCerrar;

    internal SuscripcionCorte(ChannelReader<bool> lector, Action alCerrar)
    {
        Lector = lector;
        _alCerrar = alCerrar;
    }

    public ChannelReader<bool> Lector { get; }

    public void Dispose() => _alCerrar();
}

public sealed class CorteNotifier : ICorteNotifier
{
    private readonly ConcurrentDictionary<Guid, Channel<bool>> _suscriptores = new();

    public SuscripcionCorte Suscribir()
    {
        var id = Guid.NewGuid();
        // Capacidad 1: varios avisos seguidos se juntan en uno solo
        var canal = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true
        });
        _suscriptores[id] = canal;

        return new SuscripcionCorte(canal.Reader, () =>
        {
            if (_suscriptores.TryRemove(id, out var c))
                c.Writer.TryComplete();
        });
    }

    public void NotificarCambio()
    {
        foreach (var canal in _suscriptores.Values)
            canal.Writer.TryWrite(true);
    }
}
