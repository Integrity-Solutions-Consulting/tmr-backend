using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using tmr_backend.Infrastructure.Security;

namespace tmr_backend.Shared.Middleware;

/// <summary>
/// Enriquece el ClaimsPrincipal con los códigos de permiso del usuario
/// (ej: "PROYECTOS_CREATE") leídos desde IMemoryCache o BD.
/// Debe ejecutarse después de JwtBlacklistMiddleware para no cargar permisos
/// de tokens revocados.
/// </summary>
public sealed class PermissionEnrichmentMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext ctx, IPermissionService permissionService)
    {
        if (ctx.User.Identity?.IsAuthenticated == true)
        {
            var userIdClaim = ctx.User.FindFirst(JwtRegisteredClaimNames.Sub);

            if (userIdClaim is not null && int.TryParse(userIdClaim.Value, out var userId))
            {
                var permissions = await permissionService.GetUserPermissionsAsync(
                    userId, ctx.RequestAborted);

                var identity = new ClaimsIdentity();
                foreach (var code in permissions)
                    identity.AddClaim(new Claim("permission", code));

                ctx.User.AddIdentity(identity);
            }
        }

        await next(ctx);
    }
}
