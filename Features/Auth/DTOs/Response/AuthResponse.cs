using tmr_backend.Features.Auth.DTOs.Response;

public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    Guid TokenFamilyId,
    UserResponse User
);
