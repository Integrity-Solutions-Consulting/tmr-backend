namespace tmr_backend.Features.Auth.Refresh.DTOs;

/// <summary>
/// Response después de refrescar tokens exitosamente.
/// </summary>
public record RefreshTokenResponse(
    string NewAccessToken,
    string NewRefreshToken,
    int ExpiresIn  // segundos
);
