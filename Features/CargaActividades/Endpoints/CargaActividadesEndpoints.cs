using Microsoft.EntityFrameworkCore;
using tmr_backend.Features.CargaActividades.Domain;
using tmr_backend.Features.CargaActividades.DTOs;
using tmr_backend.Infrastructure.Database;

namespace tmr_backend.Features.CargaActividades;

public static class CargaActividadesEndpoints
{
    public static void MapCargaActividadesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/carga-actividades").WithTags("CargaActividades");

        group.MapGet("/", async (ApplicationDbContext db) =>
        {
            var actividades = await db.Actividades
                .Where(c => c.Activo)
                .Select(c => new ActividadResponse(c.Id, c.Nombre, c.Descripcion, c.Activo, c.FechaCreacion))
                .ToListAsync();

            return Results.Ok(actividades);
        });

        group.MapGet("/{id:guid}", async (Guid id, ApplicationDbContext db) =>
        {
            var actividad = await db.Actividades.FindAsync(id);

            if (actividad is null) return Results.NotFound();

            return Results.Ok(new ActividadResponse(actividad.Id, actividad.Nombre, actividad.Descripcion, actividad.Activo, actividad.FechaCreacion));
        });

        group.MapPost("/", async (CrearActividadRequest request, ApplicationDbContext db) =>
        {
            try
            {
                var nuevaActividad = Actividad.Crear(request.Nombre, request.Descripcion);
                
                db.Actividades.Add(nuevaActividad);
                await db.SaveChangesAsync();

                var response = new ActividadResponse(nuevaActividad.Id, nuevaActividad.Nombre, nuevaActividad.Descripcion, nuevaActividad.Activo, nuevaActividad.FechaCreacion);
                return Results.Created($"/api/carga-actividades/{nuevaActividad.Id}", response);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { Mensaje = ex.Message });
            }
        });

        group.MapPut("/{id:guid}", async (Guid id, ActualizarActividadRequest request, ApplicationDbContext db) =>
        {
            var actividad = await db.Actividades.FindAsync(id);

            if (actividad is null) return Results.NotFound();

            try
            {
                actividad.ActualizarDetalles(request.Nombre, request.Descripcion);
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
            var actividad = await db.Actividades.FindAsync(id);

            if (actividad is null) return Results.NotFound();

            actividad.Desactivar();
            await db.SaveChangesAsync();

            return Results.NoContent();
        });
    }
}
