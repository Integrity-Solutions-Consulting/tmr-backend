using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace tmr_backend.Features.Notificaciones.Infrastructure.Apprise;

public sealed class AppriseNotificationService : IAppriseNotificationService
{
    private readonly HttpClient _httpClient;
    private readonly AppriseOptions _options;
    private readonly ILogger<AppriseNotificationService> _logger;

    public AppriseNotificationService(
        HttpClient httpClient,
        IOptions<AppriseOptions> options,
        ILogger<AppriseNotificationService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendEmailAsync(
        string toEmail,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiUrl))
            throw new InvalidOperationException("Apprise:ApiUrl no está configurado.");

        var endpoint = BuildEndpoint();
        var urls = BuildUrls(toEmail);
        if (urls.Count == 0)
        {
            throw new InvalidOperationException(
                "Apprise no tiene destinos configurados. Configure Apprise:UrlTemplates.");
        }

        var payload = new AppriseNotificationRequest
        {
            Urls = string.Join(",", urls),
            Body = htmlBody,
            Title = subject,
            Type = "info",
            Format = "html"
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(payload, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                }),
                Encoding.UTF8,
                "application/json")
        };

        if (!string.IsNullOrWhiteSpace(_options.Username))
        {
            var credentials = Convert.ToBase64String(
                System.Text.Encoding.UTF8.GetBytes($"{_options.Username}:{_options.Password}"));
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
        }

        _logger.LogInformation(
            "Enviando notificación mediante Apprise al destinatario {ToEmail} usando {DestinationCount} destino(s)",
            toEmail,
            urls.Count);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Apprise rechazó la notificación. Código HTTP {StatusCode}. Respuesta: {Response}",
                (int)response.StatusCode,
                responseBody);

            throw new HttpRequestException(
                $"Apprise devolvió {(int)response.StatusCode} ({response.ReasonPhrase}). " +
                $"Detalle: {responseBody}");
        }

        _logger.LogInformation("Notificación enviada correctamente mediante Apprise.");
    }

    private Uri BuildEndpoint()
    {
        var baseUri = _options.ApiUrl.TrimEnd('/');
        return new Uri($"{baseUri}/notify", UriKind.Absolute);
    }

    private IReadOnlyList<string> BuildUrls(string recipient)
    {
        if (_options.UrlTemplates.Count == 0)
            return [];

        return _options.UrlTemplates
            .Where(template => !string.IsNullOrWhiteSpace(template))
            .Select(template => template.Replace(
                "{recipient}",
                Uri.EscapeDataString(recipient),
                StringComparison.Ordinal))
            .ToArray();
    }

    private sealed class AppriseNotificationRequest
    {
        public string Urls { get; init; } = "";
        public string Body { get; init; } = "";
        public string Title { get; init; } = "";
        public string Type { get; init; } = "info";
        public string Format { get; init; } = "html";
    }
}
