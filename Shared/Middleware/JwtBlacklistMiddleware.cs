using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using tmr_backend.Infrastructure.Database;
using tmr_backend.Shared.Wrappers;

namespace tmr_backend.Shared.Middleware;

/// <summary>
/// Corre después de UseAuthentication(). Si el AT tiene firma válida y no expiró,
/// verifica que su JTI no esté en la blacklist (logout o token comprometido).
/// Si está blacklisted → 401. El refresh automático es responsabilidad del frontend.
/// </summary>
public class JwtBlacklistMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ApplicationDbContext db)
    {
        // Solo aplica a tokens que ya pasaron la validación JWT
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var jti = context.User.FindFirstValue(JwtRegisteredClaimNames.Jti);

            if (jti is not null)
            {
                var blacklisted = await db.TblAutenticacionTokenBlacklists
                    .AnyAsync(b => b.Jti == jti && b.Activo, context.RequestAborted);

                if (blacklisted)
                {
                    context.Response.StatusCode  = StatusCodes.Status401Unauthorized;
                    context.Response.ContentType = "application/json";

                    var response = ApiResponse<object>.Fail(
                        401,
                        "Token revocado.",
                        [new ApiError("token", "El token ha sido revocado. Inicia sesión nuevamente.")]);

                    await context.Response.WriteAsJsonAsync(response, context.RequestAborted);
                    return;
                }
            }
        }

        await next(context);
    }
}
