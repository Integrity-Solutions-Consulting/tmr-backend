using Serilog.Core;
using Serilog.Events;

namespace tmr_backend.Infrastructure.Logging;

/// <summary>
/// Agrega la propiedad {Nivel} con la abreviatura de 3 letras del nivel del log.
/// Usa los nombres de los métodos de ILogger (LogTrace = TRC, LogCritical = CRT)
/// en lugar de los de Serilog (VRB y FTL). La plantilla de salida la muestra con {Nivel:l}
/// (la "l" evita que salga entre comillas).
/// </summary>
public sealed class NivelAbreviadoEnricher : ILogEventEnricher
{
    public const string NombrePropiedad = "Nivel";

    // Se crean una sola vez porque el enricher se ejecuta en cada log
    private static readonly Dictionary<LogEventLevel, LogEventProperty> Niveles = new()
    {
        [LogEventLevel.Verbose] = Crear("TRC"),
        [LogEventLevel.Debug] = Crear("DBG"),
        [LogEventLevel.Information] = Crear("INF"),
        [LogEventLevel.Warning] = Crear("WRN"),
        [LogEventLevel.Error] = Crear("ERR"),
        [LogEventLevel.Fatal] = Crear("CRT")
    };

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory) =>
        logEvent.AddPropertyIfAbsent(Niveles[logEvent.Level]);

    private static LogEventProperty Crear(string abreviatura) =>
        new(NombrePropiedad, new ScalarValue(abreviatura));
}
