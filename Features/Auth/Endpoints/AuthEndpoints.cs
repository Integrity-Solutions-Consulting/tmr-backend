using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using tmr_backend.Features.Auth.DTOs.Request;
using tmr_backend.Features.Auth.DTOs.Response;
using tmr_backend.Features.Auth.Services;
using tmr_backend.Shared.Wrappers;

namespace tmr_backend.Features.Auth;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        // ── Endpoints públicos (sin autenticación) ─────────────────────────

        group.MapPost("/register", Register)
            .WithName("Register")
            .WithSummary("Registrar nuevo usuario")
            .Produces<ApiResponse<RegisterResponse>>(StatusCodes.Status201Created)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        group.MapPost("/login", Login)
            .WithName("Login")
            .WithSummary("Iniciar sesión — devuelve AT + RT + FamilyId")
            .Produces<ApiResponse<AuthResponse>>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized);

        group.MapPost("/refresh-token", RefreshToken)
            .WithName("RefreshToken")
            .WithSummary("Rotar RT expirado y obtener nuevo par AT + RT")
            .Produces<ApiResponse<AuthResponse>>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized);

        // ── Endpoints protegidos (requieren AT válido) ─────────────────────

        group.MapPost("/logout", Logout)
            .WithName("Logout")
            .WithSummary("Cerrar sesión — blacklist JTI + revocar RT")
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized);

        group.MapPost("/revoke-token", RevokeToken)
            .WithName("RevokeToken")
            .WithSummary("Revocar toda la familia de tokens (todos los dispositivos)")
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized);

        group.MapPost("/change-password", ChangePassword)
            .WithName("ChangePassword")
            .WithSummary("Cambia la contraseña")
            .RequireAuthorization()
            .Produces(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Handlers
    // ─────────────────────────────────────────────────────────────────────────

    private static async Task<IResult> Register(
        RegisterRequest request,
        HttpContext context,
        IAuthService authService,
        CancellationToken ct)
    {
        var result = await authService.RegisterAsync(request, context, ct);
        return Results.Created(
            $"/api/auth/{result.Id}",
            ApiResponse<RegisterResponse>.Ok(result, "Usuario registrado correctamente. Verifique su email."));
    }

    private static async Task<IResult> Login(
        LoginRequest request,
        HttpContext context,
        IAuthService authService,
        CancellationToken ct)
    {
        var clientIp   = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var userAgent  = context.Request.Headers.UserAgent.ToString();
        var deviceInfo = context.Request.Headers["X-Device-Info"].ToString();

        var result = await authService.LoginAsync(
            request, clientIp, userAgent,
            string.IsNullOrEmpty(deviceInfo) ? null : deviceInfo, ct);

        return Results.Ok(ApiResponse<AuthResponse>.Ok(result, "Sesión iniciada correctamente."));
    }

    private static async Task<IResult> RefreshToken(
        RefreshTokenRequest request,
        HttpContext context,
        IAuthService authService,
        CancellationToken ct)
    {
        var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var result   = await authService.RefreshTokenAsync(request, clientIp, ct);

        return Results.Ok(ApiResponse<AuthResponse>.Ok(result, "Tokens renovados correctamente."));
    }

    private static async Task<IResult> Logout(
        LogoutRequest request,
        HttpContext context,
        IAuthService authService,
        CancellationToken ct)
    {
        var jti    = context.User.FindFirstValue(JwtRegisteredClaimNames.Jti);
        var subRaw = context.User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        var expRaw = context.User.FindFirstValue("exp");

        if (jti is null || !int.TryParse(subRaw, out var idUsuario))
            return Results.Unauthorized();

        var atExpiry = long.TryParse(expRaw, out var expSeconds)
            ? DateTimeOffset.FromUnixTimeSeconds(expSeconds).UtcDateTime
            : DateTime.UtcNow;

        await authService.LogoutAsync(jti, idUsuario, atExpiry, request.RefreshToken, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> RevokeToken(
        RevokeTokenRequest request,
        HttpContext context,
        IAuthService authService,
        CancellationToken ct)
    {
        var subRaw = context.User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (!int.TryParse(subRaw, out var idUsuario))
            return Results.Unauthorized();

        await authService.RevokeTokenAsync(request, idUsuario, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> ChangePassword(
        ChangePasswordRequest request,
        HttpContext context,
        IAuthService authService,
        CancellationToken ct)
    {
        await authService.ChangePasswordAsync(request, context, ct);
        return Results.NoContent();
    }
}
