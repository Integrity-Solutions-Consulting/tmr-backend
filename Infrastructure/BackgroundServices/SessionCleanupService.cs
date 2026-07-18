using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using tmr_backend.Infrastructure.Database;
using tmr_backend.Infrastructure.Database.Entities;
using tmr_backend.Infrastructure.Security;

namespace tmr_backend.Infrastructure.BackgroundServices;

/// <summary>
/// Servicio en segundo plano que desactiva periódicamente las sesiones huérfanas:
/// 1. Sesiones cuya Fechaexpiracion (absolute timeout) ya pasó.
/// 2. Sesiones cuyo RT más reciente está expirado o revocado (y sin un RT activo válido).
/// Solo desactiva registros — nunca los elimina — para preservar la trazabilidad de auditoría.
/// </summary>
public sealed class SessionCleanupService(
    IServiceScopeFactory scopeFactory,
    IOptions<SessionCleanupSettings> options,
    ILogger<SessionCleanupService> logger) : BackgroundService
{
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(options.Value.IntervalMinutes);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "SessionCleanupService iniciado. Intervalo: {IntervalMinutes} min.",
            options.Value.IntervalMinutes);

        // Espera inicial para no correr al mismo tiempo que el startup de la app
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunCleanupAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Cancelación solicitada — salir silenciosamente
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error durante la limpieza de sesiones. Se reintentará en el próximo ciclo.");
            }

            await Task.Delay(_interval, stoppingToken);
        }

        logger.LogInformation("SessionCleanupService detenido.");
    }

    private async Task RunCleanupAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var now   = DateTime.UtcNow;
        var fecha = now;
        int totalDesactivadas = 0;

        // ── 1. Sesiones cuyo absolute timeout ya pasó ────────────────────────
        var sesionesExpiradas = await db.TblAutenticacionSesions
            .Where(s => s.Estaactiva && s.Activo
                     && s.Fechaexpiracion.HasValue
                     && s.Fechaexpiracion.Value < now)
            .ToListAsync(ct);

        foreach (var sesion in sesionesExpiradas)
        {
            DeactivateSession(sesion, RevocacionRazonEnum.SESSION_EXPIRED, fecha);
        }

        // ── 2. Sesiones activas cuyo RT más reciente ya expiró o está revocado ──
        // Una sesión "huérfana por RT" es aquella que:
        //   - Está activa.
        //   - No tiene ningún RT válido (no expirado, no revocado, no usado).
        var sesionIdsConRtValido = await db.TblAutenticacionRefreshTokens
            .Where(rt => !rt.Estarevocado && !rt.Estausado && rt.Fechaexpiracion > now && rt.Activo)
            .Select(rt => rt.Idsesion)
            .Distinct()
            .ToListAsync(ct);

        // Sesiones activas que NO tienen ningún RT válido
        var sesionesHuerfanas = await db.TblAutenticacionSesions
            .Where(s => s.Estaactiva && s.Activo
                     && !sesionIdsConRtValido.Contains(s.Id))
            .ToListAsync(ct);

        foreach (var sesion in sesionesHuerfanas)
        {
            // Solo desactivar si la sesión tiene al menos un RT asociado (evitar tocar sesiones recién creadas)
            var tieneRt = await db.TblAutenticacionRefreshTokens
                .AnyAsync(rt => rt.Idsesion == sesion.Id, ct);

            if (tieneRt)
                DeactivateSession(sesion, RevocacionRazonEnum.EXPIRED, fecha);
        }

        totalDesactivadas = sesionesExpiradas.Count + sesionesHuerfanas.Count(s => !s.Estaactiva);

        if (totalDesactivadas > 0)
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation(
                "SessionCleanup: {Total} sesión(es) desactivada(s) " +
                "[absolute-timeout: {PorExpiracion}, RT sin validez: {PorRtExpirado}].",
                totalDesactivadas,
                sesionesExpiradas.Count,
                sesionesHuerfanas.Count(s => !s.Estaactiva));
        }
        else
        {
            logger.LogDebug("SessionCleanup: no se encontraron sesiones huérfanas en este ciclo.");
        }
    }

    private static void DeactivateSession(
        TblAutenticacionSesion sesion,
        RevocacionRazonEnum razon,
        DateTime fecha)
    {
        sesion.Estaactiva          = false;
        sesion.Activo              = false;
        sesion.Revocadofecha       = fecha;
        sesion.Revocadorazon       = razon.ToString();
        sesion.Usuariomodificacion = "system-cleanup";
        sesion.Fechamodificacion   = fecha;
        sesion.Ipmodificacion      = "system";
    }
}
