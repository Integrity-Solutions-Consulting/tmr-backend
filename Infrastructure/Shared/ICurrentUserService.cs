using System.Security.Claims;

namespace tmr_backend.Infrastructure.Shared;

public interface ICurrentUserService
{
    int UserId { get; }
    string Email { get; }
    IEnumerable<string> Roles { get; }
    string? Jti { get; }
}
