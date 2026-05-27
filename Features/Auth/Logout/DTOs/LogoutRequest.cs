namespace tmr_backend.Features.Auth.Logout.DTOs;

/// <summary>
/// Request para logout. El refresh token es opcional.
/// </summary>
public record LogoutRequest(string? RefreshToken = null);
