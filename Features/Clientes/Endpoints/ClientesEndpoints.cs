using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using tmr_backend.Features.Clientes.DTOs.Request;
using tmr_backend.Features.Clientes.DTOs.Response;
using tmr_backend.Features.Clientes.Mappings;
using tmr_backend.Features.Clientes.Services;
using tmr_backend.Infrastructure.Database;

namespace tmr_backend.Features.Clientes;

public static class ClientesEndpoints
{
    public static void MapClientesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/clientes")
                       .WithTags("Clientes")
                       .RequireAuthorization();  // JWT: protege TODOS los endpoints del grupo

        // =====================================================================
        // GET /api/clientes  - lista 
        // =====================================================================
        group.MapGet("/", async (
            string? busqueda,
            bool? activo,
            IClienteService service,
            CancellationToken ct) =>
        {
            var lista = await service.ListarAsync(busqueda, activo, ct);
            return Results.Ok(lista);
        });

        // =====================================================================
        // GET /api/clientes/{id}  - detalle
        // =====================================================================
        group.MapGet("/{id:int}", async (
            int id,
            IClienteService service,
            CancellationToken ct) =>
        {
            var detalle = await service.ObtenerPorIdAsync(id, ct);
            return detalle is null ? Results.NotFound() : Results.Ok(detalle);
        });

        // =====================================================================
        // POST /api/clientes  - crear
        // =====================================================================
        group.MapPost("/", async (
            CrearClienteRequest request,
            IClienteService service,
            CancellationToken ct) =>
        {
            try
            {
                var nuevoId = await service.CrearAsync(request, ct);
                return Results.Created($"/api/clientes/{nuevoId}", new { Id = nuevoId });
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
                    Title = "Conflicto",
                    Detail = ex.Message,
                    Status = StatusCodes.Status409Conflict
                });
            }
        });

        // =====================================================================
        // PUT /api/clientes/{id}  - actualizar
        // =====================================================================
        group.MapPut("/{id:int}", async (
            int id,
            ActualizarClienteRequest request,
            IClienteService service,
            CancellationToken ct) =>
        {
            try
            {
                await service.ActualizarAsync(id, request, ct);
                return Results.NoContent();
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
                    Title = "Conflicto",
                    Detail = ex.Message,
                    Status = StatusCodes.Status409Conflict
                });
            }
        });

        // =====================================================================
        // DELETE /api/clientes/{id}  - eliminación lógica
        // =====================================================================
        group.MapDelete("/{id:int}", async (
            int id,
            IClienteService service,
            CancellationToken ct) =>
        {
            try
            {
                await service.EliminarAsync(id, ct);
                return Results.NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return Results.NotFound(new ProblemDetails
                {
                    Title = "No encontrado",
                    Detail = ex.Message,
                    Status = StatusCodes.Status404NotFound
                });
            }
        });

        // =====================================================================
        // GET /api/clientes/tipos-identificacion
        //   → dropdown de Tipo de Identificación (catálogo TID)
        // =====================================================================
        group.MapGet("/tipos-identificacion", async (
            ApplicationDbContext db,
            CancellationToken ct) =>
        {
            // Buscamos la cabecera del catálogo TID.
            var cabecera = await db.TblAdministracionCatalogos
                .FirstOrDefaultAsync(c =>
                    c.Codigo == "TID" && c.Tipocatalogo == "ADM", ct);

            if (cabecera is null)
                return Results.NotFound(new { Mensaje = "No existe el catálogo 'TID'." });

            // Traemos los detalles activos de ese catálogo, ordenados.
            var tipos = await db.TblAdministracionCatalogoDetalles
                .Where(d => d.Idcatalogo == cabecera.Id && d.Activo)
                .OrderBy(d => d.Orden)
                .Select(d => d.ToTipoIdentificacionResponse())
                .ToListAsync(ct);

            return Results.Ok(tipos);
        });
    }
}