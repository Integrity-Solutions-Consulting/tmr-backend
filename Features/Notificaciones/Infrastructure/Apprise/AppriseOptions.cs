namespace tmr_backend.Features.Notificaciones.Infrastructure.Apprise;

public sealed class AppriseOptions
{
    public bool Enabled { get; set; }
    public string ApiUrl { get; set; } = "";
    public string Tag { get; set; } = "notificaciones";
    public List<string> UrlTemplates { get; set; } = [];
    public string? Username { get; set; }
    public string? Password { get; set; }
    public int TimeoutSeconds { get; set; } = 30;
    // URL pública del logo del correo (Apprise no adjunta imágenes en línea). Vacío = {FrontendUrl}/assets/img/logo-isc.png
    public string? LogoUrl { get; set; }
}
