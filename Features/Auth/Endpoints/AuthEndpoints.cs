using Microsoft.EntityFrameworkCore;
using tmr_backend.Features.Auth.Domain;
using tmr_backend.Features.Auth.DTOs;
using tmr_backend.Infrastructure.Database;

namespace tmr_backend.Features.Auth;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapGet("/", async (ApplicationDbContext db) =>
        {
            var usuarios = await db.Usuarios
                .Where(c => c.Activo)
                .Select(c => new UsuarioResponse(c.Id, c.Nombre, c.Descripcion, c.Activo, c.FechaCreacion))
                .ToListAsync();

            return Results.Ok(usuarios);
        });

        group.MapGet("/{id:guid}", async (Guid id, ApplicationDbContext db) =>
        {
            var usuario = await db.Usuarios.FindAsync(id);

            if (usuario is null) return Results.NotFound();

            return Results.Ok(new UsuarioResponse(usuario.Id, usuario.Nombre, usuario.Descripcion, usuario.Activo, usuario.FechaCreacion));
        });

        group.MapPost("/", async (CrearUsuarioRequest request, ApplicationDbContext db) =>
        {
            try
            {
                var nuevoUsuario = Usuario.Crear(request.Nombre, request.Descripcion);
                
                db.Usuarios.Add(nuevoUsuario);
                await db.SaveChangesAsync();

                var response = new UsuarioResponse(nuevoUsuario.Id, nuevoUsuario.Nombre, nuevoUsuario.Descripcion, nuevoUsuario.Activo, nuevoUsuario.FechaCreacion);
                return Results.Created($"/api/auth/{nuevoUsuario.Id}", response);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { Mensaje = ex.Message });
            }
        });

        group.MapPut("/{id:guid}", async (Guid id, ActualizarUsuarioRequest request, ApplicationDbContext db) =>
        {
            var usuario = await db.Usuarios.FindAsync(id);

            if (usuario is null) return Results.NotFound();

            try
            {
                usuario.ActualizarDetalles(request.Nombre, request.Descripcion);
                await db.SaveChangesAsync();

                return Results.NoContent();
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { Mensaje = ex.Message });
            }
        });

        group.MapDelete("/{id:guid}", async (Guid id, ApplicationDbContext db) =>
        {
            var usuario = await db.Usuarios.FindAsync(id);

            if (usuario is null) return Results.NotFound();

            usuario.Desactivar();
            await db.SaveChangesAsync();

            return Results.NoContent();
        });
    }
}
