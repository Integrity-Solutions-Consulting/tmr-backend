using Microsoft.EntityFrameworkCore;
using tmr_backend.Features.Lideres.Domain;
using tmr_backend.Features.Lideres.DTOs;
using tmr_backend.Infrastructure.Database;

namespace tmr_backend.Features.Lideres;

public static class LideresEndpoints
{
    public static void MapLideresEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/lideres").WithTags("Lideres");

        group.MapGet("/", async (ApplicationDbContext db) =>
        {
            var lideres = await db.TblAdministracionLiders
                .Where(l => l.Activo)
                .OrderBy(l => l.IdpersonaNavigation.Nombres)
                .Select(l => new LiderLookupResponse(
                    l.Id,
                    ((l.IdpersonaNavigation.Nombres ?? string.Empty) + " " + (l.IdpersonaNavigation.Apellidos ?? string.Empty)).Trim()))
                .ToListAsync();

            return Results.Ok(lideres);
        });

        group.MapGet("/{id:int}", async (int id, ApplicationDbContext db) =>
        {
            var lider = await db.TblAdministracionLiders
                .Include(l => l.IdpersonaNavigation)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (lider is null) return Results.NotFound();

            return Results.Ok(new LiderLookupResponse(
                lider.Id,
                ((lider.IdpersonaNavigation.Nombres ?? string.Empty) + " " + (lider.IdpersonaNavigation.Apellidos ?? string.Empty)).Trim()));
        });

        group.MapPost("/", async (CrearLiderRequest request, ApplicationDbContext db) =>
        {
            try
            {
                var nuevoLider = Lider.Crear(request.Nombre, request.Descripcion);
                
                db.Lideres.Add(nuevoLider);
                await db.SaveChangesAsync();

                var response = new LiderResponse(nuevoLider.Id, nuevoLider.Nombre, nuevoLider.Descripcion, nuevoLider.Activo, nuevoLider.FechaCreacion);
                return Results.Created($"/api/lideres/{nuevoLider.Id}", response);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { Mensaje = ex.Message });
            }
        });

        group.MapPut("/{id:guid}", async (Guid id, ActualizarLiderRequest request, ApplicationDbContext db) =>
        {
            var lider = await db.Lideres.FindAsync(id);

            if (lider is null) return Results.NotFound();

            try
            {
                lider.ActualizarDetalles(request.Nombre, request.Descripcion);
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
            var lider = await db.Lideres.FindAsync(id);

            if (lider is null) return Results.NotFound();

            lider.Desactivar();
            await db.SaveChangesAsync();

            return Results.NoContent();
        });
    }
}
