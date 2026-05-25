using tmr_backend.Features.Auth.DTOs.Request;

namespace tmr_backend.Features.Auth.Services;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct);
}