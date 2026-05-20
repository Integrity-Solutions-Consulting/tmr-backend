using Microsoft.EntityFrameworkCore;
using tmr_backend.Features.Proyectos.Domain;
using tmr_backend.Features.Proyectos.DTOs;
using tmr_backend.Infrastructure.Database;

namespace tmr_backend.Features.Proyectos;

public static class ProyectosEndpoints
{
    public static void MapProyectosEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/proyectos").WithTags("Proyectos");

        group.MapGet("/", async (ApplicationDbContext db) =>
        {
            var proyectos = await db.Proyectos
                .Where(c => c.Activo)
                .Select(c => new ProyectoResponse(c.Id, c.Nombre, c.Descripcion, c.Activo, c.FechaCreacion))
                .ToListAsync();

            return Results.Ok(proyectos);
        });

        group.MapGet("/{id:guid}", async (Guid id, ApplicationDbContext db) =>
        {
            var proyecto = await db.Proyectos.FindAsync(id);

            if (proyecto is null) return Results.NotFound();

            return Results.Ok(new ProyectoResponse(proyecto.Id, proyecto.Nombre, proyecto.Descripcion, proyecto.Activo, proyecto.FechaCreacion));
        });

        group.MapPost("/", async (CrearProyectoRequest request, ApplicationDbContext db) =>
        {
            try
            {
                var nuevoProyecto = Proyecto.Crear(request.Nombre, request.Descripcion);
                
                db.Proyectos.Add(nuevoProyecto);
                await db.SaveChangesAsync();

                var response = new ProyectoResponse(nuevoProyecto.Id, nuevoProyecto.Nombre, nuevoProyecto.Descripcion, nuevoProyecto.Activo, nuevoProyecto.FechaCreacion);
                return Results.Created($"/api/proyectos/{nuevoProyecto.Id}", response);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { Mensaje = ex.Message });
            }
        });

        group.MapPut("/{id:guid}", async (Guid id, ActualizarProyectoRequest request, ApplicationDbContext db) =>
        {
            var proyecto = await db.Proyectos.FindAsync(id);

            if (proyecto is null) return Results.NotFound();

            try
            {
                proyecto.ActualizarDetalles(request.Nombre, request.Descripcion);
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
            var proyecto = await db.Proyectos.FindAsync(id);

            if (proyecto is null) return Results.NotFound();

            proyecto.Desactivar();
            await db.SaveChangesAsync();

            return Results.NoContent();
        });
    }
}
