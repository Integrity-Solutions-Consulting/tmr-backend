namespace tmr_backend.Features.Auth.DTOs.Request;

/// <summary>
/// Request para solicitar un enlace de recuperación de contraseña.
/// </summary>
public record ForgotPasswordRequest(string Email);
