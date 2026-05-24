using tmr_backend.Features.Lideres.DTOs.Request;
using tmr_backend.Features.Lideres.DTOs.Response;
using tmr_backend.Features.Lideres.Services;

namespace tmr_backend.Features.Lideres;

public static class LideresEndpoints
{
    public static void MapLideresEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/lideres").WithTags("Lideres").RequireAuthorization();

        group.MapGet("/", async (ILiderService service, bool? activo, CancellationToken ct) =>
        {
            var lideres = await service.ObtenerTodosAsync(activo, ct);
            return Results.Ok(lideres);
        });

        group.MapGet("/contadores", async (ILiderService service, CancellationToken ct) =>
        {
            var contadores = await service.ObtenerContadoresAsync(ct);
            return Results.Ok(contadores);
        });

        group.MapGet("/{id:int}", async (int id, ILiderService service, CancellationToken ct) =>
        {
            var lider = await service.ObtenerPorIdAsync(id, ct);
            return lider is null ? Results.NotFound() : Results.Ok(lider);
        });

        group.MapPost("/", async (CrearLiderRequest request, ILiderService service, CancellationToken ct) =>
        {
            var lider = await service.CrearAsync(request, ct);
            return Results.Created($"/api/lideres/{lider.Id}", lider);
        });

        group.MapPut("/{id:int}", async (int id, ActualizarLiderRequest request, ILiderService service, CancellationToken ct) =>
        {
            var lider = await service.ActualizarAsync(id, request, ct);
            return lider is null ? Results.NotFound() : Results.Ok(lider);
        });

        group.MapDelete("/{id:int}", async (int id, ILiderService service, CancellationToken ct) =>
        {
            var resultado = await service.DesactivarAsync(id, ct);
            return resultado ? Results.NoContent() : Results.NotFound();
        });
    }
}