using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using tmr_backend.Features.Auth.Login;
using tmr_backend.Features.Auth.Login.DTOs;
using tmr_backend.Features.Auth.Refresh;
using tmr_backend.Features.Auth.Refresh.DTOs;
using tmr_backend.Features.Auth.Logout;
using tmr_backend.Features.Auth.Logout.DTOs;
using tmr_backend.Features.Auth.ChangePassword;
using tmr_backend.Features.Auth.ChangePassword.DTOs;
using tmr_backend.Features.Auth.GetCurrentUser;
using tmr_backend.Features.Auth.GetCurrentUser.DTOs;
using tmr_backend.Features.Auth.GetPermissions;
using tmr_backend.Features.Auth.GetPermissions.DTOs;

namespace tmr_backend.Features.Auth;

/// <summary>
/// Centralizador de todos los endpoints de autenticación.
/// </summary>
public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth")
            .WithName("Auth")
            .WithTags("Auth");

        // ─────────────────────────────────────────────
        // Endpoints de Login (Fase 2)
        // ─────────────────────────────────────────────
        group.MapPost("/login", LoginAsync)
            .WithName("Login")
            .WithDescription("Autentica un usuario con email y contraseña, retorna access y refresh tokens.")
            .AllowAnonymous();

        // ─────────────────────────────────────────────
        // Endpoints de Refresh/Logout (Fase 3)
        // ─────────────────────────────────────────────
        group.MapPost("/refresh", RefreshAsync)
            .WithName("Refresh")
            .WithDescription("Refresca el access token usando el refresh token.")
            .AllowAnonymous();

        group.MapPost("/logout", LogoutAsync)
            .WithName("Logout")
            .WithDescription("Cierra la sesión del usuario, invalida el refresh token.")
            .RequireAuthorization();

        // ─────────────────────────────────────────────
        // Endpoints de ChangePassword (Fase 3)
        // ─────────────────────────────────────────────
        group.MapPost("/change-password", ChangePasswordAsync)
            .WithName("ChangePassword")
            .WithDescription("Cambia la contraseña del usuario autenticado.")
            .RequireAuthorization();

        // ─────────────────────────────────────────────
        // Endpoints de GetCurrentUser / GetPermissions (Fase 3)
        // ─────────────────────────────────────────────
        group.MapGet("/me", GetCurrentUserAsync)
            .WithName("GetCurrentUser")
            .WithDescription("Obtiene datos actualizados del usuario autenticado.")
            .RequireAuthorization();

        group.MapGet("/permissions", GetPermissionsAsync)
            .WithName("GetPermissions")
            .WithDescription("Obtiene permisos granulares y menú dinámico del usuario autenticado.")
            .RequireAuthorization();
    }

    // ════════════════════════════════════════════════════════════
    // HANDLERS DE ENDPOINTS
    // ════════════════════════════════════════════════════════════

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        LoginHandler handler,
        CancellationToken ct)
    {
        try
        {
            var response = await handler.Handle(request, ct);
            return Results.Ok(response);
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Unauthorized();
        }
        catch (DbUpdateException dbEx)
        {
            var innerError = dbEx.InnerException?.Message ?? dbEx.Message;
            return Results.BadRequest(new { error = "Database error during login", details = innerError });
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { error = ex.Message, innerException = ex.InnerException?.Message });
        }
    }

    private static async Task<IResult> RefreshAsync(
        RefreshTokenRequest request,
        RefreshHandler handler,
        CancellationToken ct)
    {
        try
        {
            var response = await handler.Handle(request, ct);
            return Results.Ok(response);
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Unauthorized();
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> LogoutAsync(
        LogoutRequest request,
        LogoutHandler handler,
        CancellationToken ct)
    {
        try
        {
            await handler.Handle(request, ct);
            return Results.Ok(new { message = "Sesión cerrada exitosamente." });
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> ChangePasswordAsync(
        ChangePasswordRequest request,
        ChangePasswordHandler handler,
        CancellationToken ct)
    {
        try
        {
            await handler.Handle(request, ct);
            return Results.Ok(new { message = "Contraseña actualizada exitosamente." });
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Unauthorized();
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> GetCurrentUserAsync(
        GetCurrentUserHandler handler,
        CancellationToken ct)
    {
        try
        {
            var response = await handler.Handle(ct);
            return Results.Ok(response);
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Unauthorized();
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> GetPermissionsAsync(
        GetPermissionsHandler handler,
        CancellationToken ct)
    {
        try
        {
            var response = await handler.Handle(ct);
            return Results.Ok(response);
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Unauthorized();
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }
}
