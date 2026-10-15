using System.Diagnostics;
using System.Globalization;

namespace tmr_backend.Shared.Middleware;

/// <summary>
/// Expone el tiempo real del servidor mediante Server-Timing y registra las
/// solicitudes lentas sin modificar el cuerpo de la respuesta.
/// </summary>
public sealed class RequestPerformanceMiddleware(
    RequestDelegate next,
    ILogger<RequestPerformanceMiddleware> logger,
    IConfiguration configuration)
{
    private readonly double _slowRequestThresholdMs = Math.Max(
        1,
        configuration.GetValue("Performance:SlowRequestThresholdMs", 750d));

    public async Task InvokeAsync(HttpContext context)
    {
        var startedAt = Stopwatch.GetTimestamp();

        context.Response.OnStarting(() =>
        {
            var elapsedMs = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
            context.Response.Headers.Append(
                "Server-Timing",
                $"app;dur={elapsedMs.ToString("F1", CultureInfo.InvariantCulture)}");
            return Task.CompletedTask;
        });

        await next(context);

        var totalMs = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
        if (totalMs >= _slowRequestThresholdMs)
        {
            logger.LogWarning(
                "Solicitud lenta {Method} {Path} respondió {StatusCode} en {ElapsedMs:F1} ms (TraceId: {TraceId})",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                totalMs,
                context.TraceIdentifier);
        }
    }
}
