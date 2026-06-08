namespace tmr_backend.Features.Auth.DTOs.Response;

public record UserResponse(int Id, string Email, string Name, DateTime CreatedAt);