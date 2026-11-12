using Microsoft.AspNetCore.Mvc;
using tmr_backend.Features.Notificaciones.Application.Cortes;
using tmr_backend.Infrastructure.Shared;

namespace tmr_backend.Features.Notificaciones.Presentation.Endpoint;

/// <summary>
/// Flujo 3: consultar y mover las fechas de corte de la notificación automática de horas faltantes.
/// Requiere el permiso NOTIFICACIONES_CONFIGURAR (módulo "Fechas de Corte", script 18).
/// </summary>
public static class FechasCorteEndpoints
{
    private const string Permiso = "NOTIFICACIONES_CONFIGURAR";

    public static void MapFechasCorteEndpoints(this RouteGroupBuilder notificaciones)
    {
        var group = notificaciones.MapGroup("/cortes/programados").RequireAuthorization(Permiso);

        // GET /api/notificaciones/cortes/programados - Cortes del mes anterior, el actual y los dos siguientes
        group.MapGet("", async (
            [FromServices] ICalendarioCortesService calendario,
            CancellationToken ct) => Results.Ok(await calendario.ListarAsync(ct)))
        .WithName("GetCortesProgramados")
        .WithSummary("Fechas de corte de la notificación automática de horas faltantes");

        // PUT /api/notificaciones/cortes/programados/{id} - Cambia la fecha y hora de un corte que aún no se ejecuta
        group.MapPut("/{id:int}", async (
            int id,
            [FromBody] CambiarFechaCorteRequest request,
            [FromServices] ICalendarioCortesService calendario,
            [FromServices] ICurrentUserService usuario,
            CancellationToken ct) =>
        {
            if (request.FechaCorte is not { } fechaCorte)
                return Results.BadRequest(new { Mensaje = "Debe indicar la nueva fecha de corte." });

            return Responder(await calendario.CambiarFechaAsync(id, fechaCorte, usuario.UserId, ct));
        })
        .WithName("CambiarFechaCorte")
        .WithSummary("Cambia la fecha y hora de un corte programado");

        // POST /api/notificaciones/cortes/programados/{id}/restablecer - Vuelve a la fecha por defecto (15 o fin de mes, 18:00)
        group.MapPost("/{id:int}/restablecer", async (
            int id,
            [FromServices] ICalendarioCortesService calendario,
            [FromServices] ICurrentUserService usuario,
            CancellationToken ct) => Responder(await calendario.RestablecerAsync(id, usuario.UserId, ct)))
        .WithName("RestablecerFechaCorte")
        .WithSummary("Devuelve un corte programado a su fecha por defecto");
    }

    private static IResult Responder(ResultadoCambioCorte resultado)
    {
        if (resultado.NoEncontrado)
            return Results.NotFound(new { Mensaje = resultado.Error });
        if (resultado.Error is not null)
            return Results.BadRequest(new { Mensaje = resultado.Error });
        return Results.Ok(resultado.Cortes);
    }
}

/// <summary>Fecha con su zona, p. ej. "2026-10-30T18:00:00-05:00".</summary>
public sealed record CambiarFechaCorteRequest(DateTimeOffset? FechaCorte);
