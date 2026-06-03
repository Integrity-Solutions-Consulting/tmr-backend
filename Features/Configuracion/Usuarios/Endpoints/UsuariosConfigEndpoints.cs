using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using tmr_backend.Features.Configuracion.Usuarios.Application;
using tmr_backend.Features.Configuracion.Usuarios.DTOs;

namespace tmr_backend.Features.Configuracion.Usuarios.Endpoints;

public static class UsuariosConfigEndpoints
{
    public static void MapUsuariosConfigEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/configuracion/usuarios").WithTags("Configuracion - Usuarios");

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
        .WithDescription("Obtiene el detalle completo de un usuario por el ID de Persona.");

        // POST /api/configuracion/usuarios
        group.MapPost("/", async ([FromBody] CreateUsuarioRequest request, [FromServices] IUsuariosConfigService service) =>
        {
            // Nota: En un caso real, usuarioActual e ipActual vienen del HttpContext (JWT / Request)
            var usuarioActual = "SYSTEM";
            var ipActual = "127.0.0.1";
            
            var result = await service.CrearUsuarioAsync(request, usuarioActual, ipActual);
            return Results.Created($"/api/configuracion/usuarios/{result.id}", result);
        })
        .WithName("CrearUsuarioConfig")
        .WithDescription("Crea un nuevo usuario asignándole roles.");

        // PUT /api/configuracion/usuarios/{id}
        group.MapPut("/{id:int}", async (int id, [FromBody] UpdateUsuarioRequest request, [FromServices] IUsuariosConfigService service) =>
        {
            var usuarioActual = "SYSTEM";
            var ipActual = "127.0.0.1";

            var result = await service.ActualizarUsuarioAsync(id, request, usuarioActual, ipActual);
            return Results.Ok(result);
        })
        .WithName("ActualizarUsuarioConfig")
        .WithDescription("Actualiza datos personales y roles de un usuario.");

        // DELETE /api/configuracion/usuarios/{id}
        group.MapDelete("/{id:int}", async (int id, [FromServices] IUsuariosConfigService service) =>
        {
            var usuarioActual = "SYSTEM";
            var ipActual = "127.0.0.1";

            var result = await service.DesactivarUsuarioAsync(id, usuarioActual, ipActual);
            return Results.Ok(result);
        })
        .WithName("DesactivarUsuarioConfig")
        .WithDescription("Desactiva lógicamente a un usuario.");
    }
}
