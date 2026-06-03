using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using tmr_backend.Features.Colaboradores.DTOs.Request;
using tmr_backend.Features.Colaboradores.DTOs.Response;
using tmr_backend.Features.Colaboradores.Mappings;
using tmr_backend.Features.Colaboradores.Services;
using tmr_backend.Infrastructure.Database;

namespace tmr_backend.Features.Colaboradores;


public static class ColaboradoresEndpoints
{
    public static void MapColaboradoresEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/colaboradores")
                       .WithTags("Colaboradores")
                       .RequireAuthorization();  // JWT: protege TODOS los endpoints del grupo

        // =====================================================================
        // GET /api/colaboradores  → lista (con filtros opcionales)
        // =====================================================================
        group.MapGet("/", async (
            string? busqueda,
            bool? activo,
            int? asignacion,
            IColaboradorService service,
            CancellationToken ct) =>
        {
            var lista = await service.ListarAsync(busqueda, activo, asignacion, ct);
            return Results.Ok(lista);
        });
        

        // =====================================================================
        // GET /api/colaboradores/{id}  → detalle
        // =====================================================================
        group.MapGet("/{id:int}", async (
            int id,
            IColaboradorService service,
            CancellationToken ct) =>
        {
            var detalle = await service.ObtenerPorIdAsync(id, ct);
            return detalle is null ? Results.NotFound() : Results.Ok(detalle);
        });
        

        // =====================================================================
        // POST /api/colaboradores  → crear
        // =====================================================================
        group.MapPost("/", async (
            CrearColaboradorRequest request,
            IColaboradorService service,
            CancellationToken ct) =>
        {
            try
            {
                var nuevoId = await service.CrearAsync(request, ct);
                return Results.Created($"/api/colaboradores/{nuevoId}", new { Id = nuevoId });
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
        // PUT /api/colaboradores/{id}  → actualizar
        // =====================================================================
        group.MapPut("/{id:int}", async (
            int id,
            ActualizarColaboradorRequest request,
            IColaboradorService service,
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
        // DELETE /api/colaboradores/{id}  → eliminación lógica
        // =====================================================================
        group.MapDelete("/{id:int}", async (
            int id,
            IColaboradorService service,
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
        // GET /api/colaboradores/personas  → ComboBox de personas existentes
        // =====================================================================
        group.MapGet("/personas", async (
            ApplicationDbContext db,
            CancellationToken ct) =>
        {
            var personas = await db.TblAdministracionPersonas
                .Where(p => p.Activo)
                .OrderBy(p => p.Nombres)
                .Select(p => p.ToPersonaResponse())
                .ToListAsync(ct);

            return Results.Ok(personas);
        });
      

        // =====================================================================
        // GET /api/colaboradores/cargos?idDepartamento=5
        //   → cargos filtrados por departamento (flujo Departamento → Cargo)
        // =====================================================================
        group.MapGet("/cargos", async (
            int idDepartamento,
            ApplicationDbContext db,
            CancellationToken ct) =>
        {
            var cargos = await db.TblAdministracionCargos
                .Where(c => c.Activo && c.Iddepartamento == idDepartamento)
                .OrderBy(c => c.Nombrecargo)
                .Select(c => c.ToCargoResponse())
                .ToListAsync(ct);

            return Results.Ok(cargos);
        });
        

        // =====================================================================
        // GET /api/colaboradores/catalogos/{codigo}
        //   → dropdowns. codigo = GEN, DEP, MDT, CAT, EMP, TCT, TID
        // =====================================================================
        group.MapGet("/catalogos/{codigo}", async (
            string codigo,
            ApplicationDbContext db,
            CancellationToken ct) =>
        {
            // Buscamos la cabecera del catálogo por su código (DEP, GEN, etc.).
            var cabecera = await db.TblAdministracionCatalogos
                .FirstOrDefaultAsync(c =>
                    c.Codigo == codigo.ToUpper() && c.Tipocatalogo == "ADM", ct);

            if (cabecera is null)
                return Results.NotFound(new { Mensaje = $"No existe el catálogo '{codigo}'." });

            // Traemos los detalles activos de ese catálogo, ordenados.
            var detalles = await db.TblAdministracionCatalogoDetalles
                .Where(d => d.Idcatalogo == cabecera.Id && d.Activo)
                .OrderBy(d => d.Orden)
                .Select(d => d.ToCatalogoResponse())
                .ToListAsync(ct);

            return Results.Ok(detalles);
        });
        
    }
}
