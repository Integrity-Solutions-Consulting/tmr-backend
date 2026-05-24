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
            var lideres = await db.Lideres
                .Where(c => c.Activo)
                .Select(c => new LiderResponse(
                    c.Id, c.Codigo, c.Tipo, c.PrimerNombre, c.Apellidos,
                    c.CorreoElectronico, c.Telefono, c.ClienteId, null,
                    c.Activo, c.FechaCreacion))
                .ToListAsync();

            return Results.Ok(lideres);
        });

        group.MapGet("/{id:guid}", async (Guid id, ApplicationDbContext db) =>
        {
            var lider = await db.Lideres.FindAsync(id);
            if (lider is null) return Results.NotFound();

            return Results.Ok(new LiderResponse(
                lider.Id, lider.Codigo, lider.Tipo, lider.PrimerNombre, lider.Apellidos,
                lider.CorreoElectronico, lider.Telefono, lider.ClienteId, null,
                lider.Activo, lider.FechaCreacion));
        });

        group.MapPost("/", async (CrearLiderRequest request, ApplicationDbContext db) =>
        {
            try
            {
                var nuevoLider = Lider.Crear(
                    request.Codigo, request.Tipo, request.PrimerNombre,
                    request.Apellidos, request.CorreoElectronico,
                    request.Telefono, request.ClienteId);

                db.Lideres.Add(nuevoLider);
                await db.SaveChangesAsync();

                var response = new LiderResponse(
                    nuevoLider.Id, nuevoLider.Codigo, nuevoLider.Tipo,
                    nuevoLider.PrimerNombre, nuevoLider.Apellidos,
                    nuevoLider.CorreoElectronico, nuevoLider.Telefono,
                    nuevoLider.ClienteId, null, nuevoLider.Activo, nuevoLider.FechaCreacion);

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
                lider.ActualizarDetalles(
                    request.Tipo, request.PrimerNombre, request.Apellidos,
                    request.CorreoElectronico, request.Telefono,
                    request.ClienteId, request.Activo);

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