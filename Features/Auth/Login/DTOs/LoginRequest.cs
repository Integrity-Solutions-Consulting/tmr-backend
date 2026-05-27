namespace tmr_backend.Features.Auth.Login.DTOs;

/// <summary>
/// Request para login con credenciales (email y password).
/// </summary>
public record LoginRequest(string Email, string Password);
