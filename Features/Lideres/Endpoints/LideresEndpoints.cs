using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using tmr_backend.Features.Lideres.Domain;
using tmr_backend.Features.Lideres.DTOs;
using tmr_backend.Features.Lideres.DTOs.Response;
using tmr_backend.Infrastructure.Database;
using tmr_backend.Features.Lideres.Services;
using tmr_backend.Shared.Wrappers;

using FluentValidation;

namespace tmr_backend.Features.Lideres;

public static class LideresEndpoints
{
    public static void MapLideresEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/lideres")
            .WithTags("Lideres")
            .RequireAuthorization();

        group.MapGet("/", async (ApplicationDbContext db) =>
        {
            var lideres = await db.Lideres
                .Where(c => c.Activo)
                .Select(c => new LiderResponse(c.Id, c.Nombre, c.Descripcion, c.Activo, c.FechaCreacion))
                .ToListAsync();

            return Results.Ok(lideres);
        }).RequireAuthorization("LIDERES_READ");

        group.MapGet("/{id:guid}", async (Guid id, ApplicationDbContext db) =>
        {
            var lider = await db.Lideres.FindAsync(id);

            if (lider is null) return Results.NotFound();

            return Results.Ok(new LiderResponse(lider.Id, lider.Nombre, lider.Descripcion, lider.Activo, lider.FechaCreacion));
        }).RequireAuthorization("LIDERES_READ");

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
        }).RequireAuthorization("LIDERES_CREATE");

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
        }).RequireAuthorization("LIDERES_UPDATE");

        group.MapDelete("/{id:guid}", async (Guid id, ApplicationDbContext db) =>
        {
            var lider = await db.Lideres.FindAsync(id);

            if (lider is null) return Results.NotFound();

            lider.Desactivar();
            await db.SaveChangesAsync();

            return Results.NoContent();
        }).RequireAuthorization("LIDERES_DELETE");;

        group.MapGet("/personasNoLideres", PersonasNoLideres)
        .WithName("PersonasNoLideres")
        .WithDisplayName("Obtener personas que no son líderes")
        .WithDescription("Obtiene una lista de personas que no están asignadas como líderes en el sistema.")
        .Produces<AuthResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status500InternalServerError) .RequireAuthorization("LIDERES_READ");

        group.MapGet("/personas", Personas)
        .WithName("Personas")
        .WithDisplayName("Obtener personas")
        .WithDescription("Obtiene una lista de personas en el sistema.")
        .Produces<AuthResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status500InternalServerError) .RequireAuthorization("LIDERES_READ");    
    }

    private static async Task<IResult> PersonasNoLideres(
        [FromServices] ILiderService liderService,
        CancellationToken ct)
    {
        try
        {
            var personas = await liderService.ObtenerPersonasNoLideresAsync(ct);

            return Results.Ok(ApiResponse<IEnumerable<PersonaResponse>>.Ok(
                data:personas,
                message:"Personas no líderes obtenidas exitosamente",
                meta: new PaginationMeta(personas.Count, 1, personas.Count)
            ));
        }
        catch (ValidationException ex)
        {
            return Results.BadRequest(new ProblemDetails
            {
                Title = "Validación fallida",
                Detail = string.Join(", ", ex.Errors.Select(e => e.ErrorMessage)),
                Status = StatusCodes.Status400BadRequest
            });
        }
        catch (InvalidOperationException ex)
        {
            return Results.Conflict(new ProblemDetails
            {
                Title = "Conflicto de datos",
                Detail = ex.Message,
                Status = StatusCodes.Status409Conflict
            });
        }
    }
    private static async Task<IResult> Personas(
        [FromServices] ILiderService liderService,
        CancellationToken ct)
    {
        try
        {
            var personas = await liderService.ObtenerPersonasNoLideresAsync(ct);

            return Results.Ok(ApiResponse<IEnumerable<PersonaResponse>>.Ok(
                data:personas,
                message:"Personas no líderes obtenidas exitosamente",
                meta: new PaginationMeta(personas.Count, 1, personas.Count)
            ));
            //return Results.Ok(personas);
        }
        catch (ValidationException ex)
        {
            return Results.BadRequest(new ProblemDetails
            {
                Title = "Validación fallida",
                Detail = string.Join(", ", ex.Errors.Select(e => e.ErrorMessage)),
                Status = StatusCodes.Status400BadRequest
            });
        }
        catch (InvalidOperationException ex)
        {
            return Results.Conflict(new ProblemDetails
            {
                Title = "Conflicto de datos",
                Detail = ex.Message,
                Status = StatusCodes.Status409Conflict
            });
        }
    }
}
