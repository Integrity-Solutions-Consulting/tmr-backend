namespace tmr_backend.Features.Auth.ChangePassword.DTOs;

/// <summary>
/// Request para cambiar contraseña del usuario autenticado.
/// </summary>
public record ChangePasswordRequest(string OldPassword, string NewPassword, string ConfirmPassword);
