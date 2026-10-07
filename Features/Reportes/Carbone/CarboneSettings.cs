namespace tmr_backend.Features.Reportes.Carbone;

// Configuración del microservicio de plantillas (tmr-carbone-service). Sección "Carbone" de appsettings.
// ApiKey nunca va en el repositorio: se define con la variable de entorno Carbone__ApiKey.
public sealed class CarboneSettings
{
    public string BaseUrl { get; set; } = "http://localhost:4000";
    public string ApiKey { get; set; } = "";
    public string TemplateSeguimiento { get; set; } = "seguimiento";
    public int TimeoutSeconds { get; set; } = 90;
}
