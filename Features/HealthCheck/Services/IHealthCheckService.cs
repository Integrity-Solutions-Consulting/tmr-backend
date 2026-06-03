using tmr_backend.Features.HealthCheck.DTOs;

namespace tmr_backend.Features.HealthCheck.Services;

public interface IHealthCheckService
{
    Task<HealthCheckResponse> CheckHealthAsync();
}
