using Microsoft.EntityFrameworkCore;
using tmr_backend.Features.Configuracion.Domain;
using tmr_backend.Features.Configuracion.DTOs;
using tmr_backend.Infrastructure.Database;

namespace tmr_backend.Features.Configuracion;

public static class ConfiguracionEndpoints
{
    public static void MapConfiguracionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/configuracion").WithTags("Configuracion");

        group.MapGet("/", async (ApplicationDbContext db) =>
        {
            var configuraciones = await db.ConfiguracionesSistema
                .Where(c => c.Activo)
                .Select(c => new ConfiguracionSistemaResponse(c.Id, c.Nombre, c.Descripcion, c.Activo, c.FechaCreacion))
                .ToListAsync();

            return Results.Ok(configuraciones);
        });

        group.MapGet("/{id:guid}", async (Guid id, ApplicationDbContext db) =>
        {
            var configuracion = await db.ConfiguracionesSistema.FindAsync(id);

            if (configuracion is null) return Results.NotFound();

            return Results.Ok(new ConfiguracionSistemaResponse(configuracion.Id, configuracion.Nombre, configuracion.Descripcion, configuracion.Activo, configuracion.FechaCreacion));
        });

        group.MapPost("/", async (CrearConfiguracionSistemaRequest request, ApplicationDbContext db) =>
        {
            try
            {
                var nuevaConfig = ConfiguracionSistema.Crear(request.Nombre, request.Descripcion);
                
                db.ConfiguracionesSistema.Add(nuevaConfig);
                await db.SaveChangesAsync();

                var response = new ConfiguracionSistemaResponse(nuevaConfig.Id, nuevaConfig.Nombre, nuevaConfig.Descripcion, nuevaConfig.Activo, nuevaConfig.FechaCreacion);
                return Results.Created($"/api/configuracion/{nuevaConfig.Id}", response);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { Mensaje = ex.Message });
            }
        });

        group.MapPut("/{id:guid}", async (Guid id, ActualizarConfiguracionSistemaRequest request, ApplicationDbContext db) =>
        {
            var configuracion = await db.ConfiguracionesSistema.FindAsync(id);

            if (configuracion is null) return Results.NotFound();

            try
            {
                configuracion.ActualizarDetalles(request.Nombre, request.Descripcion);
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
            var configuracion = await db.ConfiguracionesSistema.FindAsync(id);

            if (configuracion is null) return Results.NotFound();

            configuracion.Desactivar();
            await db.SaveChangesAsync();

            return Results.NoContent();
        });
    }
}
