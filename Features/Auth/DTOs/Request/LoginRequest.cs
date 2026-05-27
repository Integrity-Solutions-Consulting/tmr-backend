namespace tmr_backend.Features.Auth.DTOs.Request;

/// <summary>
/// Request para login con credenciales (email y password).
/// </summary>
public record LoginRequest(string Email, string Password);
