using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
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
        .WithDescription("Obtiene la lista completa de roles activos y sus módulos.");

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
        .WithDescription("Obtiene la lista de todos los módulos disponibles para asignar a los roles.");

        // POST /api/configuracion/roles
        group.MapPost("/", async ([FromBody] CreateRolRequest request, [FromServices] IRolesConfigService service) =>
        {
            // Nota: usuarioActual e ipActual deberían obtenerse del HttpContext en una implementación real
            var usuarioActual = "SYSTEM";
            var ipActual = "127.0.0.1";
            
            var result = await service.CrearRolAsync(request, usuarioActual, ipActual);
            return Results.Ok(result);
        })
        .WithName("CrearRolConfig")
        .WithDescription("Crea un nuevo rol y le asigna módulos.");

        // PUT /api/configuracion/roles/{id}
        group.MapPut("/{id:int}", async (int id, [FromBody] UpdateRolRequest request, [FromServices] IRolesConfigService service) =>
        {
            var usuarioActual = "SYSTEM";
            var ipActual = "127.0.0.1";

            var result = await service.ActualizarRolAsync(id, request, usuarioActual, ipActual);
            return Results.Ok(result);
        })
        .WithName("ActualizarRolConfig")
        .WithDescription("Actualiza datos de un rol y sus módulos asignados.");
    }
}
