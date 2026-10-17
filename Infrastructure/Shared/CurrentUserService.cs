using System.Security.Claims;

namespace tmr_backend.Infrastructure.Shared;

public sealed class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public int UserId
    {
        get
        {
            var claim = _httpContextAccessor.HttpContext?.User.FindFirst("sub");
            return claim != null && int.TryParse(claim.Value, out var userId) 
                ? userId 
                : 0;
        }
    }

    public string Email
    {
        get
        {
            return _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.Email)?.Value 
                ?? string.Empty;
        }
    }

    public IEnumerable<string> Roles
    {
        get
        {
            return _httpContextAccessor.HttpContext?.User.FindAll(ClaimTypes.Role) 
                ?.Select(c => c.Value) 
                ?? Enumerable.Empty<string>();
        }
    }

    public string? Jti
    {
        get
        {
            return _httpContextAccessor.HttpContext?.User.FindFirst("jti")?.Value;
        }
    }
}
