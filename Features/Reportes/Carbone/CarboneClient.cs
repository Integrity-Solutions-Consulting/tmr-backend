using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace tmr_backend.Features.Reportes.Carbone;

public interface ICarboneClient
{
    /// <summary>Renderiza una plantilla ya registrada en el microservicio y devuelve el archivo en el formato pedido.</summary>
    Task<byte[]> RenderAsync(string templateId, object data, string format, CancellationToken ct);
}

// Error al generar un documento. StatusCode es el que debe devolver nuestra API al frontend.
// Message es seguro para mostrar al usuario: el detalle técnico solo va al log.
public sealed class CarboneException(string message, int statusCode, Exception? inner = null)
    : Exception(message, inner)
{
    public int StatusCode { get; } = statusCode;
}

// Cliente tipado hacia tmr-carbone-service. Solo el backend lo llama (red interna), el frontend nunca.
public sealed class CarboneClient(
    HttpClient http,
    IOptions<CarboneSettings> options,
    ILogger<CarboneClient> logger) : ICarboneClient
{
    public async Task<byte[]> RenderAsync(string templateId, object data, string format, CancellationToken ct)
    {
        var apiKey = options.Value.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            logger.LogError("Carbone__ApiKey no está configurada.");
            throw new CarboneException("El motor de reportes no está configurado.", StatusCodes.Status503ServiceUnavailable);
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "render")
        {
            Content = JsonContent.Create(new { templateId, data, convertTo = format })
        };
        request.Headers.Add("x-api-key", apiKey);

        try
        {
            using var response = await http.SendAsync(request, ct);
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadAsByteArrayAsync(ct);

            var detalle = await response.Content.ReadAsStringAsync(ct);
            logger.LogError("Carbone respondió {Status} al renderizar '{Plantilla}' a {Formato}: {Detalle}",
                (int)response.StatusCode, templateId, format, detalle);

            // 503/504 = el servicio está saturado o tardó demasiado: el usuario puede reintentar.
            throw response.StatusCode is HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout
                ? new CarboneException("El motor de reportes está ocupado. Intenta nuevamente en unos segundos.", StatusCodes.Status503ServiceUnavailable)
                : new CarboneException("No se pudo generar el reporte.", StatusCodes.Status502BadGateway);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "No se pudo contactar a Carbone en {BaseAddress}", http.BaseAddress);
            throw new CarboneException("El motor de reportes no está disponible.", StatusCodes.Status503ServiceUnavailable, ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Carbone no respondió dentro de {Segundos}s", http.Timeout.TotalSeconds);
            throw new CarboneException("El motor de reportes tardó demasiado en responder.", StatusCodes.Status504GatewayTimeout, ex);
        }
    }
}
