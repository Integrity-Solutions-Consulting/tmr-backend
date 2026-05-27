namespace tmr_backend.Features.Auth.GetCurrentUser.DTOs;

/// <summary>
/// Response con datos del usuario autenticado (datos actualizados no presentes en JWT).
/// </summary>
public record CurrentUserResponse(
    int Id,
    string Nombre,
    string Email,
    string[] Roles,
    string? Foto = null,
    int? EmployeeId = null,
    DateTime? FechaCreacion = null
);
