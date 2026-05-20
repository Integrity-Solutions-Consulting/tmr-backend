using Microsoft.EntityFrameworkCore;
using tmr_backend.Features.Reportes.Domain;
using tmr_backend.Features.Reportes.DTOs;
using tmr_backend.Infrastructure.Database;

namespace tmr_backend.Features.Reportes;

public static class ReportesEndpoints
{
    public static void MapReportesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/reportes").WithTags("Reportes");

        group.MapGet("/", async (ApplicationDbContext db) =>
        {
            var reportes = await db.Reportes
                .Where(c => c.Activo)
                .Select(c => new ReporteResponse(c.Id, c.Nombre, c.Descripcion, c.Activo, c.FechaCreacion))
                .ToListAsync();

            return Results.Ok(reportes);
        });

        group.MapGet("/{id:guid}", async (Guid id, ApplicationDbContext db) =>
        {
            var reporte = await db.Reportes.FindAsync(id);

            if (reporte is null) return Results.NotFound();

            return Results.Ok(new ReporteResponse(reporte.Id, reporte.Nombre, reporte.Descripcion, reporte.Activo, reporte.FechaCreacion));
        });

        group.MapPost("/", async (CrearReporteRequest request, ApplicationDbContext db) =>
        {
            try
            {
                var nuevoReporte = Reporte.Crear(request.Nombre, request.Descripcion);
                
                db.Reportes.Add(nuevoReporte);
                await db.SaveChangesAsync();

                var response = new ReporteResponse(nuevoReporte.Id, nuevoReporte.Nombre, nuevoReporte.Descripcion, nuevoReporte.Activo, nuevoReporte.FechaCreacion);
                return Results.Created($"/api/reportes/{nuevoReporte.Id}", response);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { Mensaje = ex.Message });
            }
        });

        group.MapPut("/{id:guid}", async (Guid id, ActualizarReporteRequest request, ApplicationDbContext db) =>
        {
            var reporte = await db.Reportes.FindAsync(id);

            if (reporte is null) return Results.NotFound();

            try
            {
                reporte.ActualizarDetalles(request.Nombre, request.Descripcion);
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
            var reporte = await db.Reportes.FindAsync(id);

            if (reporte is null) return Results.NotFound();

            reporte.Desactivar();
            await db.SaveChangesAsync();

            return Results.NoContent();
        });
    }
}
