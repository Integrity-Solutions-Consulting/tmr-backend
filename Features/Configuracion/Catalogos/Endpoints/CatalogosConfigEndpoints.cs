using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using tmr_backend.Features.Configuracion.Catalogos.Application;
using tmr_backend.Features.Configuracion.Catalogos.DTOs;

namespace tmr_backend.Features.Configuracion.Catalogos.Endpoints;

public static class CatalogosConfigEndpoints
{
    public static void MapCatalogosConfigEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/configuracion/catalogos").WithTags("Configuracion - Catalogos");

        // GET /api/configuracion/catalogos
        group.MapGet("/", async ([FromServices] ICatalogosConfigService service) =>
        {
            var result = await service.ObtenerCatalogosAsync();
            return Results.Ok(result);
        })
        .WithName("ObtenerCatalogosMaestros")
        .WithDescription("Obtiene la lista de todos los catálogos maestros.");

        // GET /api/configuracion/catalogos/{idCatalogo}/detalles
        group.MapGet("/{idCatalogo:int}/detalles", async (int idCatalogo, [FromServices] ICatalogosConfigService service) =>
        {
            var result = await service.ObtenerDetallesPorCatalogoIdAsync(idCatalogo);
            return Results.Ok(result);
        })
        .WithName("ObtenerDetallesPorCatalogoId")
        .WithDescription("Obtiene la lista de detalles asociados a un ID de catálogo maestro.");

        // GET /api/configuracion/catalogos/codigo/{codigoCatalogo}/detalles
        group.MapGet("/codigo/{codigoCatalogo}/detalles", async (string codigoCatalogo, [FromServices] ICatalogosConfigService service) =>
        {
            var result = await service.ObtenerDetallesPorCatalogoCodigoAsync(codigoCatalogo);
            return Results.Ok(result);
        })
        .WithName("ObtenerDetallesPorCatalogoCodigo")
        .WithDescription("Obtiene la lista de detalles asociados a un código de catálogo maestro.");

        // POST /api/configuracion/catalogos/detalles
        group.MapPost("/detalles", async ([FromBody] CreateCatalogoDetalleRequest request, HttpContext context, [FromServices] ICatalogosConfigService service) =>
        {
            var usuarioActual = ObtenerUsuarioActual(context);
            var ipActual = ObtenerIpActual(context);

            var result = await service.CrearDetalleAsync(request, usuarioActual, ipActual);
            return Results.Ok(result);
        })
        .WithName("CrearCatalogoDetalle")
        .WithDescription("Crea un nuevo detalle de catálogo.");

        // PUT /api/configuracion/catalogos/detalles/{id}
        group.MapPut("/detalles/{id:int}", async (int id, [FromBody] UpdateCatalogoDetalleRequest request, HttpContext context, [FromServices] ICatalogosConfigService service) =>
        {
            var usuarioActual = ObtenerUsuarioActual(context);
            var ipActual = ObtenerIpActual(context);

            var result = await service.ActualizarDetalleAsync(id, request, usuarioActual, ipActual);
            return Results.Ok(result);
        })
        .WithName("ActualizarCatalogoDetalle")
        .WithDescription("Actualiza un detalle de catálogo existente.");

        // DELETE /api/configuracion/catalogos/detalles/{id}
        group.MapDelete("/detalles/{id:int}", async (int id, [FromQuery] int? idCatalogo, HttpContext context, [FromServices] ICatalogosConfigService service) =>
        {
            var usuarioActual = ObtenerUsuarioActual(context);
            var ipActual = ObtenerIpActual(context);

            var result = await service.EliminarDetalleAsync(id, idCatalogo, usuarioActual, ipActual);
            return Results.Ok(result);
        })
        .WithName("EliminarCatalogoDetalle")
        .WithDescription("Elimina lógicamente un detalle de catálogo (Soft Delete).");
    }

    private static string ObtenerUsuarioActual(HttpContext context)
    {
        var email = context.User.FindFirstValue(JwtRegisteredClaimNames.Email)
            ?? context.User.FindFirstValue(ClaimTypes.Email);

        if (!string.IsNullOrWhiteSpace(email))
            return email.Contains('@') ? email.Split('@')[0] : email;

        return context.User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? "SYSTEM";
    }

    private static string ObtenerIpActual(HttpContext context)
    {
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwardedFor))
            return forwardedFor.Split(',')[0].Trim();

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
