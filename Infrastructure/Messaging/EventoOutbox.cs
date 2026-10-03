namespace TmrBackend.Infrastructure.Messaging;

public class EventoOutbox
{
    public Guid Id { get; set; }
    public string TipoEvento { get; set; } = "";          // ej. "CorteDeHorasAlcanzado"
    public string Payload { get; set; } = "";             // el evento serializado en JSON
    public EstadoEvento Estado { get; set; }
    public int Intentos { get; set; }
    public string? UltimoError { get; set; }
    public DateTimeOffset FechaCreacion { get; set; }
    public DateTimeOffset? FechaProcesado { get; set; }
}