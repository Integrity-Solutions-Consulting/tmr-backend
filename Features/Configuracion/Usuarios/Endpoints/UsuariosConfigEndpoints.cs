using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using tmr_backend.Features.Auth.DTOs.Response;
using tmr_backend.Features.Auth.Register;
using tmr_backend.Features.Auth.Register.DTOs;
using tmr_backend.Features.Configuracion.Usuarios.Application;
using tmr_backend.Features.Configuracion.Usuarios.DTOs;
using tmr_backend.Shared.Wrappers;

namespace tmr_backend.Features.Configuracion.Usuarios.Endpoints;

public static class UsuariosConfigEndpoints
{
    public static void MapUsuariosConfigEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/configuracion/usuarios")
            .WithTags("Configuracion - Usuarios")
            .RequireAuthorization();

        // GET /api/configuracion/usuarios
        group.MapGet("/", async ([AsParameters] ObtenerUsuariosQuery query, [FromServices] IUsuariosConfigService service) =>
        {
            var result = await service.ObtenerUsuariosPaginadosAsync(query);
            return Results.Ok(result);
        })
        .WithName("ObtenerUsuariosConfig")
        .WithDescription("Obtiene la lista paginada de usuarios.");

        // GET /api/configuracion/usuarios/{id}
        group.MapGet("/{id:int}", async (int id, [FromServices] IUsuariosConfigService service) =>
        {
            var result = await service.ObtenerUsuarioPorIdAsync(id);
            return Results.Ok(result);
        })
        .WithName("ObtenerUsuarioPorIdConfig")
        .WithDescription("Obtiene el detalle completo de un usuario por el ID de Usuario.");

        // POST /api/configuracion/usuarios
        group.MapPost("/", async ([FromBody] CrearUsuarioConfigRequest request, HttpContext context, [FromServices] IUsuariosConfigService service) =>
        {
            var usuarioActual = ObtenerUsuarioActual(context);
            var ipActual = ObtenerIpActual(context);
            var idUsuarioActual = ObtenerIdUsuarioActual(context);

            var result = await service.CrearUsuarioAsync(request, usuarioActual, ipActual, idUsuarioActual);
            return Results.Created($"/api/configuracion/usuarios/{result.idusuario}", result);
        })
        .WithName("CrearUsuarioConfig")
        .WithDescription("Crea un nuevo usuario asignandole roles y una persona opcional.");

        // PUT /api/configuracion/usuarios/{id}
        group.MapPut("/{id:int}", async (int id, [FromBody] UpdateUsuarioRequest request, HttpContext context, [FromServices] IUsuariosConfigService service) =>
        {
            var usuarioActual = ObtenerUsuarioActual(context);
            var ipActual = ObtenerIpActual(context);
            var idUsuarioActual = ObtenerIdUsuarioActual(context);

            var result = await service.ActualizarUsuarioAsync(id, request, usuarioActual, ipActual, idUsuarioActual);
            return Results.Ok(result);
        })
        .WithName("ActualizarUsuarioConfig")
        .WithDescription("Actualiza datos de autenticacion, roles y persona opcional de un usuario.");

        // PATCH /api/configuracion/usuarios/{id}
        group.MapPatch("/{id:int}", async (int id, [FromBody] ActivarUsuarioRequest request, HttpContext context, [FromServices] IUsuariosConfigService service) =>
        {
            var usuarioActual = ObtenerUsuarioActual(context);
            var ipActual = ObtenerIpActual(context);

            var result = await service.ActualizarEstadoUsuarioAsync(id, request, usuarioActual, ipActual);
            return Results.Ok(result);
        })
        .WithName("ActualizarEstadoUsuarioConfig")
        .WithDescription("Activa o desactiva logicamente a un usuario.");

        // DELETE /api/configuracion/usuarios/{id}
        group.MapDelete("/{id:int}", async (int id, HttpContext context, [FromServices] IUsuariosConfigService service) =>
        {
            var usuarioActual = ObtenerUsuarioActual(context);
            var ipActual = ObtenerIpActual(context);

            var result = await service.DesactivarUsuarioAsync(id, usuarioActual, ipActual);
            return Results.Ok(result);
        })
        .WithName("DesactivarUsuarioConfig")
        .WithDescription("Desactiva logicamente a un usuario.");

        // POST /api/configuracion/usuarios/register-user
        group.MapPost("/register-user", async (
            [FromBody] RegisterUserRequest request,
            HttpContext context,
            [FromServices] RegisterUserHandler handler,
            CancellationToken ct) =>
        {
            var result = await handler.Handle(request, context, ct);
            return Results.Created(
                $"/api/configuracion/usuarios/{result.Id}",
                ApiResponse<RegisterResponse>.Ok(result, "Usuario administrativo registrado correctamente."));
        })
        .WithName("RegisterUserConfig")
        .WithSummary("Registrar usuario administrativo")
        .WithDescription("Crea un usuario administrativo con contrasena temporal. Requiere autenticacion.")
        .Produces<ApiResponse<RegisterResponse>>(StatusCodes.Status201Created)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);
    }

    private static string ObtenerUsuarioActual(HttpContext context)
    {
        var email = context.User.FindFirstValue(JwtRegisteredClaimNames.Email)
            ?? context.User.FindFirstValue(ClaimTypes.Email);

        if (!string.IsNullOrWhiteSpace(email))
            return email.Contains('@') ? email.Split('@')[0] : email;

        return context.User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? "SYSTEM";
    }

    private static string ObtenerIpActual(HttpContext context)
    {
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwardedFor))
            return forwardedFor.Split(',')[0].Trim();

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private static int? ObtenerIdUsuarioActual(HttpContext context)
    {
        var sub = context.User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier);

        return int.TryParse(sub, out var idUsuario) ? idUsuario : null;
    }
}
