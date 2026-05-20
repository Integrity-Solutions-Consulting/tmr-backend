using Microsoft.EntityFrameworkCore;
using tmr_backend.Features.Dashboard.Domain;
using tmr_backend.Features.Dashboard.DTOs;
using tmr_backend.Infrastructure.Database;

namespace tmr_backend.Features.Dashboard;

public static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/dashboard").WithTags("Dashboard");

        group.MapGet("/", async (ApplicationDbContext db) =>
        {
            var items = await db.DashboardItems
                .Where(c => c.Activo)
                .Select(c => new DashboardItemResponse(c.Id, c.Nombre, c.Descripcion, c.Activo, c.FechaCreacion))
                .ToListAsync();

            return Results.Ok(items);
        });

        group.MapGet("/{id:guid}", async (Guid id, ApplicationDbContext db) =>
        {
            var item = await db.DashboardItems.FindAsync(id);

            if (item is null) return Results.NotFound();

            return Results.Ok(new DashboardItemResponse(item.Id, item.Nombre, item.Descripcion, item.Activo, item.FechaCreacion));
        });

        group.MapPost("/", async (CrearDashboardItemRequest request, ApplicationDbContext db) =>
        {
            try
            {
                var nuevoItem = DashboardItem.Crear(request.Nombre, request.Descripcion);
                
                db.DashboardItems.Add(nuevoItem);
                await db.SaveChangesAsync();

                var response = new DashboardItemResponse(nuevoItem.Id, nuevoItem.Nombre, nuevoItem.Descripcion, nuevoItem.Activo, nuevoItem.FechaCreacion);
                return Results.Created($"/api/dashboard/{nuevoItem.Id}", response);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { Mensaje = ex.Message });
            }
        });

        group.MapPut("/{id:guid}", async (Guid id, ActualizarDashboardItemRequest request, ApplicationDbContext db) =>
        {
            var item = await db.DashboardItems.FindAsync(id);

            if (item is null) return Results.NotFound();

            try
            {
                item.ActualizarDetalles(request.Nombre, request.Descripcion);
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
            var item = await db.DashboardItems.FindAsync(id);

            if (item is null) return Results.NotFound();

            item.Desactivar();
            await db.SaveChangesAsync();

            return Results.NoContent();
        });
    }
}
