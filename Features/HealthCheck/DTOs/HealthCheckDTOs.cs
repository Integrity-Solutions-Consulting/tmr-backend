namespace tmr_backend.Features.HealthCheck.DTOs;

public class HealthCheckResponse
{
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public DatabaseHealthCheckDetails Database { get; set; } = new();
}

public class DatabaseHealthCheckDetails
{
    public bool IsConnected { get; set; }
    public string ConnectionStatus { get; set; } = string.Empty;
    public Dictionary<string, int> TableRecordCounts { get; set; } = new();
    public string? ErrorMessage { get; set; }
}

public class HealthCheckLiveResponse
{
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
