using tmr_backend.Features.Auth.DTOs.Request;
using tmr_backend.Features.Auth.DTOs.Response;

namespace tmr_backend.Features.Auth.Services;

public interface IAuthService
{
    Task<RegisterResponse> RegisterAsync(RegisterRequest request, HttpContext context, CancellationToken ct);

    Task<AuthResponse> LoginAsync(
        LoginRequest request,
        string clientIp,
        string? userAgent,
        string? deviceInfo,
        CancellationToken ct);

    Task<AuthResponse> RefreshTokenAsync(
        RefreshTokenRequest request,
        string clientIp,
        CancellationToken ct);

    Task LogoutAsync(
        string jti,
        int idUsuario,
        DateTime atExpiry,
        string? rawRefreshToken,
        CancellationToken ct);

    /// <summary>
    /// Cierra la sesión usando únicamente el Refresh Token, sin necesitar AT válido.
    /// Útil cuando el AT ya expiró y el frontend redirige al login.
    /// </summary>
    Task LogoutWithRefreshTokenAsync(string? rawRefreshToken, CancellationToken ct);

    Task RevokeTokenAsync(RevokeTokenRequest request, int idUsuario, CancellationToken ct);

    Task ChangePasswordAsync(ChangePasswordRequest request, HttpContext context, CancellationToken ct);

    Task<string[]> GetUserModulesAsync(int idUsuario, CancellationToken ct);
}
