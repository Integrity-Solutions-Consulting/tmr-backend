using System.Reflection;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using tmr_backend.Infrastructure.Database;

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
        using var recoveryCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var recovery = RecuperarPeriodicamenteAsync(recoveryCancellation.Token);
        try
        {
            await foreach (var id in _canal.Reader.ReadAllAsync(stoppingToken))
            {
                try { await ProcesarAsync(id, stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception ex) { _logger.LogError(ex, "Error inesperado procesando el evento {Id}", id); }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        finally
        {
            await recoveryCancellation.CancelAsync();
            await recovery;
        }
    }

    private async Task ProcesarAsync(Guid id, CancellationToken ct)
    {
        // Un scope nuevo por evento: cada evento tiene su propio DbContext y sus propios handlers
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Serializa workers de varias réplicas. El segundo vuelve a leer el estado confirmado.
        var fila = await db.Set<EventoOutbox>()
            .FromSqlInterpolated($"SELECT * FROM notificacion.evento_outbox WHERE id = {id} FOR UPDATE")
            .FirstOrDefaultAsync(ct);
        if (fila is null || fila.Estado != EstadoEvento.Pendiente)
        {
            // ya se procesó o no existe: no hay nada que hacer
            _logger.LogDebug("Evento {Id} omitido: ya fue procesado o no existe", id);
            return;
        }

        // 1. Reconstruir el evento a partir del nombre y el JSON guardados
        if (!_tiposDeEvento.TryGetValue(fila.TipoEvento, out var tipo))
        {
            fila.Estado = EstadoEvento.Fallido;
            fila.UltimoError = $"Tipo de evento desconocido: {fila.TipoEvento}";
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            _logger.LogError("Tipo de evento desconocido {Tipo} en el evento {Id}", fila.TipoEvento, id);
            return;
        }
        object? evento = null;

        // 2. Buscar los handlers registrados para ese tipo: IEventHandler<CorteDeHorasAlcanzado>, etc.
        var tipoHandler = typeof(IEventHandler<>).MakeGenericType(tipo);
        var metodo = tipoHandler.GetMethod(nameof(IEventHandler<IEvento>.HandleAsync))!;
        var handlers = scope.ServiceProvider.GetServices(tipoHandler).ToList();

        if (handlers.Count == 0)
            _logger.LogWarning("El evento {Tipo} no tiene handlers registrados", fila.TipoEvento);

        _logger.LogDebug("Procesando evento {Tipo} ({Id}), intento {Intento}, con {CantidadHandlers} handler(s)",
            fila.TipoEvento, fila.Id, fila.Intentos + 1, handlers.Count);

        string? falloDefinitivo = null;
        await tx.CreateSavepointAsync("handler", ct);

        try
        {
            evento = JsonSerializer.Deserialize(fila.Payload, tipo) ?? throw new InvalidOperationException("Evento vacío.");
            if (handlers.Count == 0) throw new InvalidOperationException("No hay handler para este evento.");
            // 3. Entregar el evento a cada handler
            foreach (var handler in handlers)
                await (Task)metodo.Invoke(handler, [evento, ct])!;

            fila.Estado = EstadoEvento.Procesado;
            fila.FechaProcesado = DateTimeOffset.UtcNow;
        }
        catch (Exception ex)
        {
            await tx.RollbackToSavepointAsync("handler", ct);
            var error = ex is TargetInvocationException tie ? tie.InnerException ?? ex : ex;
            if (error is EventoObsoletoException)
            {
                db.ChangeTracker.Clear();
                db.Attach(fila);
                fila.Estado = EstadoEvento.Procesado;
                fila.UltimoError = "AVISO_NO_VIGENTE";
                fila.FechaProcesado = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return;
            }

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
            // La recuperación periódica vuelve a descubrir eventos pendientes.
            // No agregamos un segundo temporizador que duplique el mismo reintento.
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        if (fila.Estado == EstadoEvento.Procesado)
            _logger.LogDebug("Evento {Tipo} ({Id}) procesado correctamente", fila.TipoEvento, fila.Id);

        if (falloDefinitivo is not null && evento is not null)
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

    private async Task RecuperarPeriodicamenteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(_esperaEntreReintentos);
        try
        {
            while (await timer.WaitForNextTickAsync(ct)) await ReencolarPendientesAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    }

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