using tmr_backend.Features.Auth.DTOs.Request;
using tmr_backend.Features.Auth.DTOs.Response;
using tmr_backend.Features.Auth.Services;
using tmr_backend.Features.Auth.Login;
using FluentValidation;
using Microsoft.AspNetCore.Mvc; 

namespace tmr_backend.Features.Auth;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        // ─────────────────────────────────────────────
        // Endpoint de Registro (Fase 1)
        // ─────────────────────────────────────────────
        group.MapPost("/register", Register)
            .WithName("Register")
            .WithDisplayName("Registrar usuario")
            .Produces<AuthResponse>(StatusCodes.Status201Created)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        // ─────────────────────────────────────────────
        // Endpoints de Login (Fase 2)
        // ─────────────────────────────────────────────
        LoginEndpoints.MapLoginEndpoints(app);

        // ─────────────────────────────────────────────
        // Endpoints de Refresh/Logout (Fase 3)
        // ─────────────────────────────────────────────
        // RefreshEndpoints.MapRefreshEndpoints(app);
        // LogoutEndpoints.MapLogoutEndpoints(app);
    }

    /// <summary>
    /// Registra un nuevo usuario con email y contraseña.
    /// Crea la persona en administración y usuario en autenticación.
    /// </summary>
    private static async Task<IResult> Register(
        RegisterRequest request,
        IAuthService authService,
        CancellationToken ct)
    {
        try
        {
            var result = await authService.RegisterAsync(request, ct);
            return Results.Created($"/api/auth/login", result);
        }
        catch (ValidationException ex)
        {
            return Results.BadRequest(new ProblemDetails
            {
                Title = "Validación fallida",
                Detail = string.Join(", ", ex.Errors.Select(e => e.ErrorMessage)),
                Status = StatusCodes.Status400BadRequest
            });
        }
        catch (InvalidOperationException ex)
        {
            return Results.Conflict(new ProblemDetails
            {
                Title = "Conflicto",
                Detail = ex.Message,
                Status = StatusCodes.Status409Conflict
            });
        }
    }
}
