using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace tmr_backend.Infrastructure.Security;

/// <summary>
/// Genera políticas de autorización dinámicamente para cualquier código de permiso
/// (ej: "PROYECTOS_CREATE"). Evita tener que declarar cada permiso como policy
/// estática en Program.cs.
///
/// Uso en endpoints: [Authorize(Policy = "PROYECTOS_CREATE")]
/// </summary>
public sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallback = new(options);

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() =>
        _fallback.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() =>
        _fallback.GetFallbackPolicyAsync();

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        // Si el nombre de la policy es un código de permiso conocido del sistema
        // (todo en mayúsculas con guion bajo), genera la policy dinámicamente.
        if (IsPermissionCode(policyName))
        {
            var policy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireClaim("permission", policyName)
                .Build();

            return Task.FromResult<AuthorizationPolicy?>(policy);
        }

        // Para cualquier otra policy (ej: "Admin", "Lider") usa el proveedor base.
        return _fallback.GetPolicyAsync(policyName);
    }

    // Heurística: un código de permiso es todo mayúsculas con letras, dígitos y guiones bajos.
    private static bool IsPermissionCode(string name) =>
        name.Length > 0 && name == name.ToUpperInvariant() && name.Replace("_", "").Length > 0;
}
