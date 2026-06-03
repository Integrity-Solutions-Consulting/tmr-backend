using tmr_backend.Features.Lideres.DTOs.Request;
using tmr_backend.Features.Lideres.DTOs.Response;
using tmr_backend.Features.Lideres.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using tmr_backend.Infrastructure.Database;
using tmr_backend.Shared.Wrappers;
using FluentValidation;

namespace tmr_backend.Features.Lideres;

public static class LideresEndpoints
{
    public static void MapLideresEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/lideres").WithTags("Lideres").RequireAuthorization();

        // 1. Obtener todos los líderes para la grilla
        group.MapGet("/", async (ILiderService service, bool? activo, CancellationToken ct) =>
        {
            var lideres = await service.ObtenerTodosAsync(activo, ct);
            return Results.Ok(lideres);
        });

        // 2. Lookup de líderes activos (para combos en otros módulos)
        group.MapGet("/lookup", async (ApplicationDbContext db, CancellationToken ct) =>
        {
            var lookup = await db.TblAdministracionLiders
                .Where(l => l.Activo)
                .OrderBy(l => l.IdpersonaNavigation.Nombres)
                .Select(l => new LiderLookupResponse(
                    l.Id,
                    ((l.IdpersonaNavigation.Nombres ?? string.Empty) + " " + (l.IdpersonaNavigation.Apellidos ?? string.Empty)).Trim()))
                .ToListAsync(ct);

            return Results.Ok(lookup);
        });

        // 3. Contadores
        group.MapGet("/contadores", async (ILiderService service, CancellationToken ct) =>
        {
            var contadores = await service.ObtenerContadoresAsync(ct);
            return Results.Ok(contadores);
        });

        // 4. Personas disponibles para ser líderes
        group.MapGet("/personas-disponibles", async (ILiderService service, CancellationToken ct) =>
        {
            var personas = await service.ObtenerPersonasDisponiblesAsync(ct);
            return Results.Ok(personas);
        });

        // 5. Tipos de líderes (catálogo ADM/TLI)
        group.MapGet("/tipos", async (ILiderService service, CancellationToken ct) =>
        {
            var tipos = await service.ObtenerTiposAsync(ct);
            return Results.Ok(tipos);
        });

        // 6. Obtener por ID
        group.MapGet("/{id:int}", async (int id, ILiderService service, CancellationToken ct) =>
        {
            var lider = await service.ObtenerPorIdAsync(id, ct);
            return lider is null ? Results.NotFound() : Results.Ok(lider);
        });

        // 7. Crear líder
        group.MapPost("/", async (CrearLiderRequest request, ILiderService service, CancellationToken ct) =>
        {
            var lider = await service.CrearAsync(request, ct);
            return Results.Created($"/api/lideres/{lider.Id}", lider);
        });

        // 8. Actualizar líder
        group.MapPut("/{id:int}", async (int id, ActualizarLiderRequest request, ILiderService service, CancellationToken ct) =>
        {
            var lider = await service.ActualizarAsync(id, request, ct);
            return lider is null ? Results.NotFound() : Results.Ok(lider);
        });

        // 9. Eliminar (desactivar) líder
        group.MapDelete("/{id:int}", async (int id, ILiderService service, CancellationToken ct) =>
        {
            var resultado = await service.DesactivarAsync(id, ct);
            return resultado ? Results.NoContent() : Results.NotFound();
        });

        // 10. Personas no líderes (Elisa - redundancia)
        group.MapGet("/personasNoLideres", PersonasNoLideres)
             .WithName("PersonasNoLideres");

        group.MapGet("/personas", Personas)
             .WithName("Personas");
    }

    private static async Task<IResult> PersonasNoLideres(
        ILiderService liderService,
        CancellationToken ct)
    {
        var personas = await liderService.ObtenerPersonasNoLideresAsync(ct);
        return Results.Ok(ApiResponse<IEnumerable<PersonaResponse>>.Ok(
            data: personas,
            message: "Personas no líderes obtenidas exitosamente",
            meta: new PaginationMeta(personas.Count, 1, personas.Count)
        ));
    }

    private static async Task<IResult> Personas(
        ILiderService liderService,
        CancellationToken ct)
    {
        var personas = await liderService.ObtenerPersonasNoLideresAsync(ct);
        return Results.Ok(ApiResponse<IEnumerable<PersonaResponse>>.Ok(
            data: personas,
            message: "Personas no líderes obtenidas exitosamente",
            meta: new PaginationMeta(personas.Count, 1, personas.Count)
        ));
    }
}

public record LiderLookupResponse(int Id, string NombreCompleto);