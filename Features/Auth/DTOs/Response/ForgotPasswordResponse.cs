namespace tmr_backend.Features.Auth.DTOs.Response;

/// <summary>
/// Response para la solicitud de recuperación de contraseña.
/// Utiliza un mensaje genérico por seguridad (no revela si el email existe).
/// </summary>
public record ForgotPasswordResponse(
    string Message,
    DateTime ExpirationTime
);
