using Microsoft.EntityFrameworkCore;
using tmr_backend.Features.Colaboradores.Domain;
using tmr_backend.Features.Colaboradores.DTOs;
using tmr_backend.Infrastructure.Database;

namespace tmr_backend.Features.Colaboradores;

public static class ColaboradoresEndpoints
{
    public static void MapColaboradoresEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/colaboradores").WithTags("Colaboradores");

        group.MapGet("/", async (ApplicationDbContext db) =>
        {
            var colaboradores = await db.Colaboradores
                .Where(c => c.Activo)
                .Select(c => new ColaboradorResponse(c.Id, c.Nombre, c.Descripcion, c.Activo, c.FechaCreacion))
                .ToListAsync();

            return Results.Ok(colaboradores);
        });

        group.MapGet("/{id:guid}", async (Guid id, ApplicationDbContext db) =>
        {
            var colaborador = await db.Colaboradores.FindAsync(id);

            if (colaborador is null) return Results.NotFound();

            return Results.Ok(new ColaboradorResponse(colaborador.Id, colaborador.Nombre, colaborador.Descripcion, colaborador.Activo, colaborador.FechaCreacion));
        });

        group.MapPost("/", async (CrearColaboradorRequest request, ApplicationDbContext db) =>
        {
            try
            {
                var nuevoColaborador = Colaborador.Crear(request.Nombre, request.Descripcion);
                
                db.Colaboradores.Add(nuevoColaborador);
                await db.SaveChangesAsync();

                var response = new ColaboradorResponse(nuevoColaborador.Id, nuevoColaborador.Nombre, nuevoColaborador.Descripcion, nuevoColaborador.Activo, nuevoColaborador.FechaCreacion);
                return Results.Created($"/api/colaboradores/{nuevoColaborador.Id}", response);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { Mensaje = ex.Message });
            }
        });

        group.MapPut("/{id:guid}", async (Guid id, ActualizarColaboradorRequest request, ApplicationDbContext db) =>
        {
            var colaborador = await db.Colaboradores.FindAsync(id);

            if (colaborador is null) return Results.NotFound();

            try
            {
                colaborador.ActualizarDetalles(request.Nombre, request.Descripcion);
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
            var colaborador = await db.Colaboradores.FindAsync(id);

            if (colaborador is null) return Results.NotFound();

            colaborador.Desactivar();
            await db.SaveChangesAsync();

            return Results.NoContent();
        });
    }
}
