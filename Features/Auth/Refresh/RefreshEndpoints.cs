namespace tmr_backend.Features.Auth.Refresh;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using tmr_backend.Features.Auth.Refresh.DTOs;

/// <summary>
/// Endpoints para el feature de Refresh de tokens.
/// </summary>
public static class RefreshEndpoints
{
    public static void MapRefreshEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth")
            .WithName("Auth");

        group.MapPost("/refresh", RefreshAsync)
            .WithName("Refresh")
            .WithDescription("Refresca el access token usando el refresh token.")
            .AllowAnonymous();
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
}
