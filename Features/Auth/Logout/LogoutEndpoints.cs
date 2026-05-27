namespace tmr_backend.Features.Auth.Logout;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using tmr_backend.Features.Auth.Logout.DTOs;

/// <summary>
/// Endpoints para el feature de Logout.
/// </summary>
public static class LogoutEndpoints
{
    public static void MapLogoutEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth")
            .WithName("Auth");

        group.MapPost("/logout", LogoutAsync)
            .WithName("Logout")
            .WithDescription("Cierra la sesión del usuario, invalida el refresh token.")
            .RequireAuthorization();
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
}
