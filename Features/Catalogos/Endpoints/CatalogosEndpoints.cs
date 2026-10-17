using Microsoft.EntityFrameworkCore;
using tmr_backend.Features.Catalogos.DTOs;
using tmr_backend.Infrastructure.Database;

namespace tmr_backend.Features.Catalogos;

public static class CatalogosEndpoints
{
    public static void MapCatalogosEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/catalogos").WithTags("Catalogos");

        group.MapGet("/{codigo}", async (string codigo, ApplicationDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(codigo)) return Results.BadRequest(new { message = "El código del catálogo es requerido." });

            var catalogo = await db.TblAdministracionCatalogos
                .FirstOrDefaultAsync(c => c.Codigo != null && c.Codigo.ToUpper() == codigo.ToUpper() && c.Activo);

            if (catalogo is null) return Results.NotFound(new { message = $"Catálogo '{codigo}' no encontrado." });

            var detalles = await db.TblAdministracionCatalogoDetalles
                .Where(d => d.Idcatalogo == catalogo.Id && d.Activo)
                .OrderBy(d => d.Orden)
                .Select(d => new CatalogoResponse(d.Id, d.Codigovalor ?? string.Empty, d.Valor ?? string.Empty))
                .ToListAsync();

            return Results.Ok(detalles);
        });
    }
}
