namespace tmr_backend.Features.Auth.DTOs.Request;

/// <summary>
/// Request para cambiar contraseña del usuario autenticado.
/// </summary>
public record ChangePasswordRequest(string OldPassword, string NewPassword, string ConfirmPassword);