namespace tmr_backend.Features.Auth.GetCurrentUser;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using tmr_backend.Features.Auth.GetCurrentUser.DTOs;

/// <summary>
/// Endpoints para obtener datos del usuario autenticado.
/// </summary>
public static class GetCurrentUserEndpoints
{
    public static void MapGetCurrentUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth")
            .WithName("Auth");

        group.MapGet("/me", GetCurrentUserAsync)
            .WithName("GetCurrentUser")
            .WithDescription("Obtiene datos actualizados del usuario autenticado.")
            .RequireAuthorization();
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
}
