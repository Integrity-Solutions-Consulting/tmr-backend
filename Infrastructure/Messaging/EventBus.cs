using System.Text.Json;
using System.Threading.Channels;
using tmr_backend.Infrastructure.Database;
using tmr_backend.Infrastructure.Messaging;

namespace tmr_backend.Infrastructure.Messaging;

public class EventBus : IEventBus
{
    private readonly ApplicationDbContext _db;
    private readonly Channel<Guid> _canal;
    private readonly ILogger<EventBus> _logger;

    public EventBus(ApplicationDbContext db, Channel<Guid> canal, ILogger<EventBus> logger)
    {
        _db = db;
        _canal = canal;
        _logger = logger;
    }

    public async Task PublishAsync<T>(T evento, CancellationToken ct = default) where T : IEvento
    {
        // El tipo real del objeto (no solo T), por si alguien publica usando una variable de tipo IEvento
        var tipoReal = evento.GetType();

        var fila = new EventoOutbox
        {
            Id = Guid.NewGuid(),
            TipoEvento = tipoReal.Name,                               // "CorteDeHorasAlcanzado"
            Payload = JsonSerializer.Serialize(evento, tipoReal),     // los datos del evento en JSON
            Estado = EstadoEvento.Pendiente,
            Intentos = 0,
            FechaCreacion = DateTimeOffset.UtcNow
        };

        // 1. Primero se guarda en Postgres: a partir de aquí el evento ya no se pierde
        _db.Set<EventoOutbox>().Add(fila);
        await _db.SaveChangesAsync(ct);

        // 2. Después se avisa al dispatcher, enviando solo el Id por el canal en memoria
        await _canal.Writer.WriteAsync(fila.Id, ct);

        _logger.LogDebug("Evento {Tipo} publicado con Id {Id}", fila.TipoEvento, fila.Id);
    }
}