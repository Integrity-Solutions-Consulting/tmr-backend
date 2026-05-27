namespace tmr_backend.Features.Auth.Refresh.DTOs;

/// <summary>
/// Request para refrescar access token usando refresh token.
/// </summary>
public record RefreshTokenRequest(string RefreshToken);
