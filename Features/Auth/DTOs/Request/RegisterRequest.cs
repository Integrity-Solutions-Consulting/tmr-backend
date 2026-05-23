namespace tmr_backend.Features.Auth.DTOs.Request;
public record RegisterRequest(string Email, string Password, string ConfirmPassword);
