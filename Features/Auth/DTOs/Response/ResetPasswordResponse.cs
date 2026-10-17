namespace tmr_backend.Features.Auth.DTOs.Response;

/// <summary>
/// Response para el reseteo de contraseña.
/// </summary>
public record ResetPasswordResponse(
    string Message,
    bool Success
);
