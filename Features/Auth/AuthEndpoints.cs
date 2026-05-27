using tmr_backend.Features.Auth.Login;
using tmr_backend.Features.Auth.Refresh;
using tmr_backend.Features.Auth.Logout;
using tmr_backend.Features.Auth.ChangePassword;
using tmr_backend.Features.Auth.GetCurrentUser;
using tmr_backend.Features.Auth.GetPermissions;

namespace tmr_backend.Features.Auth;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        // ─────────────────────────────────────────────
        // Endpoints de Login (Fase 2)
        // ─────────────────────────────────────────────
        LoginEndpoints.MapLoginEndpoints(app);

        // ─────────────────────────────────────────────
        // Endpoints de Refresh/Logout (Fase 3)
        // ─────────────────────────────────────────────
        RefreshEndpoints.MapRefreshEndpoints(app);
        LogoutEndpoints.MapLogoutEndpoints(app);

        // ─────────────────────────────────────────────
        // Endpoints de ChangePassword (Fase 3)
        // ─────────────────────────────────────────────
        ChangePasswordEndpoints.MapChangePasswordEndpoints(app);

        // ─────────────────────────────────────────────
        // Endpoints de GetCurrentUser / GetPermissions (Fase 3)
        // ─────────────────────────────────────────────
        GetCurrentUserEndpoints.MapGetCurrentUserEndpoints(app);
        GetPermissionsEndpoints.MapGetPermissionsEndpoints(app);
    }
}
