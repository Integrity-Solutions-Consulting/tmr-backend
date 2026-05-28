namespace tmr_backend.Features.Auth.ChangePassword;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using tmr_backend.Features.Auth.ChangePassword.DTOs;

/// <summary>
/// Endpoints para el feature de ChangePassword.
/// </summary>
public static class ChangePasswordEndpoints
{
    public static void MapChangePasswordEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth")
            .WithName("Auth");

        group.MapPost("/change-password", ChangePasswordAsync)
            .WithName("ChangePassword")
            .WithDescription("Cambia la contraseña del usuario autenticado.")
            .RequireAuthorization();
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
        catch (Exception ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }
}
