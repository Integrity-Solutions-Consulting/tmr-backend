namespace tmr_backend.Features.Notificaciones.Application.Queries;

/// <summary>
/// Resumen del último corte compartido por todas las conexiones SSE de /cortes/stream:
/// se arma como máximo una vez por segundo aunque haya muchos administradores y líderes conectados,
/// en vez de una consulta a la BD por conexión.
/// </summary>
public interface IResumenCorteCache
{
    Task<ResumenCorteDto?> ObtenerAsync(CancellationToken ct);
}

public sealed class ResumenCorteCache : IResumenCorteCache
{
    private static readonly TimeSpan Vigencia = TimeSpan.FromSeconds(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SemaphoreSlim _calculando = new(1, 1);
    private ResumenCorteDto? _ultimo;
    private DateTimeOffset _calculadoEn = DateTimeOffset.MinValue;

    public ResumenCorteCache(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    public async Task<ResumenCorteDto?> ObtenerAsync(CancellationToken ct)
    {
        if (DateTimeOffset.UtcNow - _calculadoEn < Vigencia)
            return _ultimo;

        await _calculando.WaitAsync(ct);
        try
        {
            // Otra conexión pudo haberlo calculado mientras esta esperaba
            if (DateTimeOffset.UtcNow - _calculadoEn < Vigencia)
                return _ultimo;

            using var scope = _scopeFactory.CreateScope();
            var consulta = scope.ServiceProvider.GetRequiredService<IConsultaResumenCorte>();
            // Sin el token de la conexión: si ese cliente se desconecta, el cálculo sirve igual a los demás
            _ultimo = await consulta.UltimoCorteAsync(CancellationToken.None);
            _calculadoEn = DateTimeOffset.UtcNow;
            return _ultimo;
        }
        finally
        {
            _calculando.Release();
        }
    }
}
