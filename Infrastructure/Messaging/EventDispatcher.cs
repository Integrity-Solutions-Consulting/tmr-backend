using System.Reflection;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using tmr_backend.Infrastructure.Database;
using tmr_backend.Infrastructure.Messaging;

namespace tmr_backend.Infrastructure.Messaging;

public class EventDispatcher : BackgroundService
{
    public const int MaxIntentos = 5;
    private static readonly TimeSpan EsperaPorDefecto = TimeSpan.FromSeconds(30);

    private readonly TimeSpan _esperaEntreReintentos;
    private readonly Channel<Guid> _canal;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IReadOnlyDictionary<string, Type> _tiposDeEvento;
    private readonly ILogger<EventDispatcher> _logger;

    public EventDispatcher(
        Channel<Guid> canal,
        IServiceScopeFactory scopeFactory,
        IReadOnlyDictionary<string, Type> tiposDeEvento,
        ILogger<EventDispatcher> logger,
        TimeSpan? esperaEntreReintentos = null)
    {
        _esperaEntreReintentos = esperaEntreReintentos ?? EsperaPorDefecto;
        _canal = canal;
        _scopeFactory = scopeFactory;
        _tiposDeEvento = tiposDeEvento;
        _logger = logger;
    }

    // .NET llama a este método al arrancar el backend, y vive mientras el backend esté encendido
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 1. Recuperar los eventos que quedaron pendientes si el backend se cayó
        await ReencolarPendientesAsync(stoppingToken);

        // 2. Escuchar el canal: espera sin consumir CPU hasta que llegue un Id
        await foreach (var id in _canal.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcesarAsync(id, stoppingToken);
            }
            catch (Exception ex)
            {
                // Un error inesperado no debe detener el dispatcher: se registra y sigue con el siguiente
                _logger.LogError(ex, "Error inesperado procesando el evento {Id}", id);
            }
        }
    }

    private async Task ProcesarAsync(Guid id, CancellationToken ct)
    {
        // Un scope nuevo por evento: cada evento tiene su propio DbContext y sus propios handlers
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var fila = await db.Set<EventoOutbox>().FirstOrDefaultAsync(e => e.Id == id, ct);
        if (fila is null || fila.Estado != EstadoEvento.Pendiente)
            return;   // ya se procesó o no existe: no hay nada que hacer

        // 1. Reconstruir el evento a partir del nombre y el JSON guardados
        if (!_tiposDeEvento.TryGetValue(fila.TipoEvento, out var tipo))
        {
            fila.Estado = EstadoEvento.Fallido;
            fila.UltimoError = $"Tipo de evento desconocido: {fila.TipoEvento}";
            await db.SaveChangesAsync(ct);
            _logger.LogError("Tipo de evento desconocido {Tipo} en el evento {Id}", fila.TipoEvento, id);
            return;
        }
        var evento = JsonSerializer.Deserialize(fila.Payload, tipo)!;

        // 2. Buscar los handlers registrados para ese tipo: IEventHandler<CorteDeHorasAlcanzado>, etc.
        var tipoHandler = typeof(IEventHandler<>).MakeGenericType(tipo);
        var metodo = tipoHandler.GetMethod(nameof(IEventHandler<IEvento>.HandleAsync))!;
        var handlers = scope.ServiceProvider.GetServices(tipoHandler).ToList();

        if (handlers.Count == 0)
            _logger.LogWarning("El evento {Tipo} no tiene handlers registrados", fila.TipoEvento);

        string? falloDefinitivo = null;

        try
        {
            // 3. Entregar el evento a cada handler
            foreach (var handler in handlers)
                await (Task)metodo.Invoke(handler, [evento, ct])!;

            fila.Estado = EstadoEvento.Procesado;
            fila.FechaProcesado = DateTimeOffset.UtcNow;
        }
        catch (Exception ex)
        {
            var error = ex is TargetInvocationException tie ? tie.InnerException ?? ex : ex;

            // Lo que el handler dejó a medias (p. ej. una fila que violó un índice) sigue en el DbContext:
            // si no se descarta, el SaveChanges de abajo vuelve a fallar, Intentos nunca se guarda y el
            // evento se reintenta para siempre. Se limpia y se vuelve a enganchar solo la fila del Outbox.
            db.ChangeTracker.Clear();
            db.Attach(fila);

            fila.Intentos++;
            fila.UltimoError = error.Message;
            _logger.LogError(error, "Falló el evento {Id} ({Tipo}), intento {Intento}",
                fila.Id, fila.TipoEvento, fila.Intentos);

            if (fila.Intentos >= MaxIntentos)
            {
                fila.Estado = EstadoEvento.Fallido;   // se rinde: queda para revisión manual
                falloDefinitivo = error.Message;
            }
            else
            {
                ReintentarMasTarde(id, ct);           // lo vuelve a intentar más tarde
            }
        }

        await db.SaveChangesAsync(ct);

        if (falloDefinitivo is not null)
            await AvisarFalloDefinitivoAsync(tipo, evento, fila, falloDefinitivo, ct);
    }

    // Avisa a los IEventoFallidoHandler<T> del tipo (en un scope limpio) para que el resultado no se pierda:
    // p. ej. el aviso de horas queda registrado como Fallido y se ve en el popup del corte o en el toast.
    private async Task AvisarFalloDefinitivoAsync(Type tipo, object evento, EventoOutbox fila, string error, CancellationToken ct)
    {
        _logger.LogError("El evento {Id} ({Tipo}) agotó sus {Max} intentos y quedó como Fallido: {Error}",
            fila.Id, fila.TipoEvento, MaxIntentos, error);

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var tipoHandler = typeof(IEventoFallidoHandler<>).MakeGenericType(tipo);
            var metodo = tipoHandler.GetMethod(nameof(IEventoFallidoHandler<IEvento>.HandleFalloDefinitivoAsync))!;
            foreach (var handler in scope.ServiceProvider.GetServices(tipoHandler))
                await (Task)metodo.Invoke(handler, [evento, error, ct])!;
        }
        catch (Exception ex)
        {
            var real = ex is TargetInvocationException tie ? tie.InnerException ?? ex : ex;
            _logger.LogError(real, "No se pudo registrar el fallo definitivo del evento {Id} ({Tipo})", fila.Id, fila.TipoEvento);
        }
    }

    private void ReintentarMasTarde(Guid id, CancellationToken ct) =>
        _ = Task.Delay(_esperaEntreReintentos, ct)
                .ContinueWith(_ => _canal.Writer.TryWrite(id), ct,
                              TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Default);

    private async Task ReencolarPendientesAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var pendientes = await db.Set<EventoOutbox>()
                .Where(e => e.Estado == EstadoEvento.Pendiente)
                .OrderBy(e => e.FechaCreacion)
                .Select(e => e.Id)
                .ToListAsync(ct);

            foreach (var id in pendientes)
                await _canal.Writer.WriteAsync(id, ct);

            if (pendientes.Count > 0)
                _logger.LogInformation("Se reencolaron {Cantidad} eventos pendientes", pendientes.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al reencolar eventos pendientes del Outbox al iniciar el servicio");
        }
    }
}