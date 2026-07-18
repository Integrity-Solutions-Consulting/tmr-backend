using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using tmr_backend.Features.Configuracion.DiasFestivos.Application;
using tmr_backend.Features.Configuracion.DiasFestivos.DTOs;

namespace tmr_backend.Features.Configuracion.DiasFestivos.Endpoints;

public static class DiasFestivosEndpoints
{
    public static void MapDiasFestivosEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/configuracion/dias-festivos").WithTags("Configuracion - Dias Festivos");

        // GET /api/configuracion/dias-festivos
        group.MapGet("/", async ([FromServices] IDiasFestivosService service) =>
        {
            var result = await service.ObtenerFeriadosAsync();
            return Results.Ok(result);
        })
        .WithName("ObtenerDiasFestivos")
        .WithDescription("Obtiene la lista de todos los dias festivos activos.");

        // GET /api/configuracion/dias-festivos/{id}
        group.MapGet("/{id:int}", async (int id, [FromServices] IDiasFestivosService service) =>
        {
            var result = await service.ObtenerFeriadoPorIdAsync(id);
            return Results.Ok(result);
        })
        .WithName("ObtenerDiaFestivoPorId")
        .WithDescription("Obtiene el detalle de un feriado por su ID.");

        // POST /api/configuracion/dias-festivos
        group.MapPost("/", async ([FromBody] CreateFeriadoRequest request, HttpContext context, [FromServices] IDiasFestivosService service) =>
        {
            var usuarioActual = ObtenerUsuarioActual(context);
            var ipActual = ObtenerIpActual(context);

            var result = await service.CrearFeriadoAsync(request, usuarioActual, ipActual);
            return Results.Ok(result);
        })
        .WithName("CrearDiaFestivo")
        .WithDescription("Crea un nuevo feriado.");

        // PUT /api/configuracion/dias-festivos/{id}
        group.MapPut("/{id:int}", async (int id, [FromBody] UpdateFeriadoRequest request, HttpContext context, [FromServices] IDiasFestivosService service) =>
        {
            var usuarioActual = ObtenerUsuarioActual(context);
            var ipActual = ObtenerIpActual(context);

            var result = await service.ActualizarFeriadoAsync(id, request, usuarioActual, ipActual);
            return Results.Ok(result);
        })
        .WithName("ActualizarDiaFestivo")
        .WithDescription("Actualiza un feriado existente.");

        // DELETE /api/configuracion/dias-festivos/{id}
        group.MapDelete("/{id:int}", async (int id, HttpContext context, [FromServices] IDiasFestivosService service) =>
        {
            var usuarioActual = ObtenerUsuarioActual(context);
            var ipActual = ObtenerIpActual(context);

            var result = await service.EliminarFeriadoAsync(id, usuarioActual, ipActual);
            return Results.Ok(result);
        })
        .WithName("EliminarDiaFestivo")
        .WithDescription("Elimina logicamente un feriado.");
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
