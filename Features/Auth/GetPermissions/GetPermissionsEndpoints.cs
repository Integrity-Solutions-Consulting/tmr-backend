namespace tmr_backend.Features.Auth.GetPermissions;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using tmr_backend.Features.Auth.GetPermissions.DTOs;

/// <summary>
/// Endpoints para obtener permisos del usuario autenticado.
/// </summary>
public static class GetPermissionsEndpoints
{
    public static void MapGetPermissionsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth")
            .WithName("Auth");

        group.MapGet("/permissions", GetPermissionsAsync)
            .WithName("GetPermissions")
            .WithDescription("Obtiene permisos granulares y menú dinámico del usuario autenticado.")
            .RequireAuthorization();
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
