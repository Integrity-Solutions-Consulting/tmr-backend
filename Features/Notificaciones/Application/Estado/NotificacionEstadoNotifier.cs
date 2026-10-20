using System.Collections.Concurrent;
using System.Threading.Channels;
using tmr_backend.Features.Notificaciones.Domain;

namespace tmr_backend.Features.Notificaciones.Application.Estado;

/// <summary>
/// Resultado de un aviso manual recién registrado en NotificacionEnvio.
/// </summary>
public sealed record CambioEstadoNotificacion(
    int IdUsuarioEjecutor,
    int IdEmpleado,
    EstadoEnvio Estado,
    string? Detalle,
    DateTimeOffset FechaEnvio,
    int? IdProyecto = null);

/// <summary>
/// Avisa en memoria a las conexiones SSE abiertas cuando RegistrarEnvioHandler guarda el resultado
/// de un aviso manual, para que el frontend no tenga que consultar /estado una y otra vez.
/// Solo llega a las conexiones de esta instancia del backend: el endpoint SSE además vuelve a
/// consultar la BD cada pocos segundos por si el evento se procesó en otra instancia.
/// </summary>
public interface INotificacionEstadoNotifier
{
    SuscripcionEstado Suscribir(int idUsuario);
    void Publicar(CambioEstadoNotificacion cambio);
}

public sealed class SuscripcionEstado : IDisposable
{
    private readonly Action _alCerrar;

    internal SuscripcionEstado(ChannelReader<CambioEstadoNotificacion> lector, Action alCerrar)
    {
        Lector = lector;
        _alCerrar = alCerrar;
    }

    public ChannelReader<CambioEstadoNotificacion> Lector { get; }

    public void Dispose() => _alCerrar();
}

public sealed class NotificacionEstadoNotifier : INotificacionEstadoNotifier
{
    private readonly ConcurrentDictionary<Guid, (int IdUsuario, Channel<CambioEstadoNotificacion> Canal)> _suscriptores = new();

    public SuscripcionEstado Suscribir(int idUsuario)
    {
        var id = Guid.NewGuid();
        // Acotado y descartando lo más viejo: un cliente lento nunca frena al dispatcher
        var canal = Channel.CreateBounded<CambioEstadoNotificacion>(new BoundedChannelOptions(500)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true
        });
        _suscriptores[id] = (idUsuario, canal);

        return new SuscripcionEstado(canal.Reader, () =>
        {
            if (_suscriptores.TryRemove(id, out var s))
                s.Canal.Writer.TryComplete();
        });
    }

    public void Publicar(CambioEstadoNotificacion cambio)
    {
        foreach (var (idUsuario, canal) in _suscriptores.Values)
        {
            if (idUsuario == cambio.IdUsuarioEjecutor)
                canal.Writer.TryWrite(cambio);
        }
    }
}
