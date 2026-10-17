namespace tmr_backend.Features.Auth.DTOs.Request;

/// <summary>
/// Request para resetear la contraseña usando el token de recuperación.
/// </summary>
public record ResetPasswordRequest(string Token, string NewPassword, string ConfirmPassword);
