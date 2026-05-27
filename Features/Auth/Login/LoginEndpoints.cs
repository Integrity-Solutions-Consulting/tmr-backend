using tmr_backend.Features.Auth.Login.DTOs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace tmr_backend.Features.Auth.Login;

/// <summary>
/// Endpoints para el feature de Login.
/// </summary>
public static class LoginEndpoints
{
    public static void MapLoginEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth")
            .WithName("Auth");

        group.MapPost("/login", LoginAsync)
            .WithName("Login")
            .WithDescription("Autentica un usuario con email y contraseña, retorna access y refresh tokens.")
            .AllowAnonymous();
    }

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
        catch (Exception ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }
}
