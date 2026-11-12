using System.Collections.Concurrent;
using System.Threading.Channels;

namespace tmr_backend.Features.Notificaciones.Application.Estado;

/// <summary>
/// Avisa en memoria a la conexión SSE de /mias/stream de un colaborador que se le acaba de enviar un correo
/// de horas faltantes (aviso manual del líder o del corte automático), para que aparezca en su campana.
/// No lleva datos: la conexión vuelve a leer sus avisos de la BD. Solo llega a las conexiones de esta
/// instancia del backend; el endpoint además revisa la BD cada cierto tiempo.
/// </summary>
public interface IAvisoColaboradorNotifier
{
    SuscripcionAvisos Suscribir(IReadOnlyCollection<int> idsEmpleado);
    void Notificar(int idEmpleado);
}

public sealed class SuscripcionAvisos : IDisposable
{
    private readonly Action _alCerrar;

    internal SuscripcionAvisos(ChannelReader<bool> lector, Action alCerrar)
    {
        Lector = lector;
        _alCerrar = alCerrar;
    }

    public ChannelReader<bool> Lector { get; }

    public void Dispose() => _alCerrar();
}

public sealed class AvisoColaboradorNotifier : IAvisoColaboradorNotifier
{
    private readonly ConcurrentDictionary<Guid, (IReadOnlyCollection<int> Empleados, Channel<bool> Canal)> _suscriptores = new();

    public SuscripcionAvisos Suscribir(IReadOnlyCollection<int> idsEmpleado)
    {
        var id = Guid.NewGuid();
        // Capacidad 1: varios avisos seguidos se juntan en uno solo
        var canal = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true
        });
        _suscriptores[id] = (idsEmpleado, canal);

        return new SuscripcionAvisos(canal.Reader, () =>
        {
            if (_suscriptores.TryRemove(id, out var s))
                s.Canal.Writer.TryComplete();
        });
    }

    public void Notificar(int idEmpleado)
    {
        foreach (var (empleados, canal) in _suscriptores.Values)
        {
            if (empleados.Contains(idEmpleado))
                canal.Writer.TryWrite(true);
        }
    }
}
