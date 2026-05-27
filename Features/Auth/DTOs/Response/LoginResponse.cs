namespace tmr_backend.Features.Auth.DTOs.Response;

/// <summary>
/// Response después de login exitoso, contiene tokens y datos del usuario.
/// </summary>
public record LoginResponse(
    string AccessToken,
    string RefreshToken,
    int ExpiresIn,        // segundos
    UserDto User
);

/// <summary>
/// Información básica del usuario autenticado.
/// </summary>
public record UserDto(
    int Id,
    string Nombre,
    string Email,
    string[] Roles,
    int? EmployeeId = null
);
