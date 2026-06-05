using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using tmr_backend.Features.Configuracion.Roles.Application;
using tmr_backend.Features.Configuracion.Roles.DTOs;

namespace tmr_backend.Features.Configuracion.Roles.Endpoints;

public static class RolesConfigEndpoints
{
    public static void MapRolesConfigEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/configuracion/roles").WithTags("Configuracion - Roles");

        // GET /api/configuracion/roles
        group.MapGet("/", async ([FromServices] IRolesConfigService service) =>
        {
            var result = await service.ObtenerRolesAsync();
            return Results.Ok(result);
        })
        .WithName("ObtenerRolesConfig")
        .WithDescription("Obtiene la lista completa de roles activos y sus modulos.");

        // GET /api/configuracion/roles/{id}
        group.MapGet("/{id:int}", async (int id, [FromServices] IRolesConfigService service) =>
        {
            var result = await service.ObtenerRolPorIdAsync(id);
            return Results.Ok(result);
        })
        .WithName("ObtenerRolPorIdConfig")
        .WithDescription("Obtiene el detalle completo de un rol por su ID.");

        // GET /api/configuracion/roles/modulos
        group.MapGet("/modulos", async ([FromServices] IRolesConfigService service) =>
        {
            var result = await service.ObtenerModulosAsync();
            return Results.Ok(result);
        })
        .WithName("ObtenerModulosDisponibles")
        .WithDescription("Obtiene la lista de todos los modulos disponibles para asignar a los roles.");

        // POST /api/configuracion/roles
        group.MapPost("/", async ([FromBody] CreateRolRequest request, HttpContext context, [FromServices] IRolesConfigService service) =>
        {
            var usuarioActual = ObtenerUsuarioActual(context);
            var ipActual = ObtenerIpActual(context);

            var result = await service.CrearRolAsync(request, usuarioActual, ipActual);
            return Results.Ok(result);
        })
        .WithName("CrearRolConfig")
        .WithDescription("Crea un nuevo rol y le asigna modulos.");

        // PUT /api/configuracion/roles/{id}
        group.MapPut("/{id:int}", async (int id, [FromBody] UpdateRolRequest request, HttpContext context, [FromServices] IRolesConfigService service) =>
        {
            var usuarioActual = ObtenerUsuarioActual(context);
            var ipActual = ObtenerIpActual(context);

            var result = await service.ActualizarRolAsync(id, request, usuarioActual, ipActual);
            return Results.Ok(result);
        })
        .WithName("ActualizarRolConfig")
        .WithDescription("Actualiza datos de un rol y sus modulos asignados.");

        // PATCH /api/configuracion/roles/{id}
        group.MapPatch("/{id:int}", async (int id, [FromBody] ActivarRolRequest request, HttpContext context, [FromServices] IRolesConfigService service) =>
        {
            var usuarioActual = ObtenerUsuarioActual(context);
            var ipActual = ObtenerIpActual(context);

            var result = await service.ActualizarEstadoAsync(id, request, usuarioActual, ipActual);
            return Results.Ok(result);
        })
        .WithName("ActualizarEstadoRolConfig")
        .WithDescription("Activa o desactiva logicamente un rol.");

        // DELETE /api/configuracion/roles/{id}
        group.MapDelete("/{id:int}", async (int id, HttpContext context, [FromServices] IRolesConfigService service) =>
        {
            var usuarioActual = ObtenerUsuarioActual(context);
            var ipActual = ObtenerIpActual(context);

            var result = await service.EliminarRolAsync(id, usuarioActual, ipActual);
            return Results.Ok(result);
        })
        .WithName("EliminarRolConfig")
        .WithDescription("Elimina logicamente un rol sin crear tablas nuevas.");
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
}
