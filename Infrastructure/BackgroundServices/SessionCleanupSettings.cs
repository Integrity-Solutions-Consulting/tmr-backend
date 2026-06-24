namespace tmr_backend.Infrastructure.BackgroundServices;

/// <summary>
/// Configuración del servicio de limpieza periódica de sesiones huérfanas.
/// Se lee desde la sección "SessionCleanup" en appsettings.json.
/// </summary>
public sealed class SessionCleanupSettings
{
    /// <summary>
    /// Intervalo en minutos entre cada ciclo de limpieza. Default: 60 min.
    /// </summary>
    public int IntervalMinutes { get; init; } = 60;
}
