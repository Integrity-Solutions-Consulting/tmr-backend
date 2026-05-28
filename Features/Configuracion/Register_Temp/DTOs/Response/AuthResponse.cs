namespace tmr_backend.Features.Configuracion.Register_Temp.DTOs.Response;

public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    UserResponse User
);
