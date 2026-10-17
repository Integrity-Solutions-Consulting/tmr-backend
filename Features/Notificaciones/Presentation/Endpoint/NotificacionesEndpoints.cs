using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using tmr_backend.Features.Notificaciones.Application.Estado;
using tmr_backend.Features.Notificaciones.Application.Queries;
using tmr_backend.Features.Notificaciones.Domain.Events;
using tmr_backend.Features.Notificaciones.Domain;
using tmr_backend.Features.TimeReport.Services;
using tmr_backend.Infrastructure.Database;
using tmr_backend.Infrastructure.Messaging;
using tmr_backend.Infrastructure.Shared;

namespace tmr_backend.Features.Notificaciones.Presentation.Endpoint;

public static class NotificacionesEndpoints
{
    private const int MaxEmpleadosEstado = 200;
    private const int MaxDiasRango = 366;

    // SSE: cuánto vive como máximo una conexión y cada cuánto se revisa la BD (por si el evento
    // se procesó en otra instancia) y se manda un "ping" para que el proxy no corte la conexión.
    private static readonly TimeSpan DuracionMaximaStream = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan IntervaloRevisionStream = TimeSpan.FromSeconds(5);

    public static void MapNotificacionesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/notificaciones").WithTags("Notificaciones");

        // GET /pendientes - Consulta los empleados que deben horas
        group.MapGet("/pendientes", async (
            [FromServices] IConsultaHorasFaltantes consulta,
            CancellationToken ct) =>
        {
            var resultado = await consulta.ObtenerPendientesAsync(ct);
            return Results.Ok(resultado);
        })
        .RequireAuthorization("NOTIFICACIONES_EJECUTAR")
        .WithName("GetPendientesNotificacion")
        .WithSummary("Obtiene la lista de empleados que deben horas en el período en curso");

        // POST /notificar - Envía notificaciones a IDs específicos
        group.MapPost("/notificar", async (
            [FromBody] NotificarRequest request,
            [FromServices] IEventBus eventBus,
            [FromServices] ICurrentUserService currentUserService,
            CancellationToken ct) =>
        {
            if (request.EmpleadoIds == null || !request.EmpleadoIds.Any())
                return Results.BadRequest(new { Mensaje = "Debe seleccionar al menos un empleado para notificar." });

            var hoy = CalculoHorasPeriodo.HoyEcuador();
            DateOnly inicio, fin;

            if (request.FechaDesde is { } desde && request.FechaHasta is { } hasta)
            {
                // Rango del filtro de Seguimiento: el correo informa lo mismo que el líder ve en pantalla
                if (hasta < desde)
                    return Results.BadRequest(new { Mensaje = "La fecha hasta no puede ser anterior a la fecha desde." });
                if (desde > hoy)
                    return Results.BadRequest(new { Mensaje = "La fecha desde no puede ser futura." });
                if (hasta.DayNumber - desde.DayNumber > MaxDiasRango)
                    return Results.BadRequest(new { Mensaje = $"El rango no puede superar {MaxDiasRango} días." });

                (inicio, fin) = (desde, hasta);
            }
            else if (request.FechaDesde is not null || request.FechaHasta is not null)
            {
                return Results.BadRequest(new { Mensaje = "Debe indicar fecha desde y fecha hasta, o ninguna." });
            }
            else
            {
                // Sin rango: período en curso hasta el día anterior (propuesta original)
                (inicio, _) = CalculadorHorasFaltantes.PeriodoEnCurso(hoy);
                fin = hoy.AddDays(-1);
            }

            var evento = new NotificacionManualSolicitada(
                request.EmpleadoIds,
                inicio,
                fin,
                currentUserService.UserId,
                request.IdProyecto
            );

            var solicitadoEn = DateTimeOffset.UtcNow;
            await eventBus.PublishAsync(evento, ct);

            // 202: la solicitud quedó en cola; el resultado real llega por GET /estado/stream (o GET /estado)
            return Results.Accepted(value: new NotificarResponse(
                "Solicitud recibida. Los correos se procesan en segundo plano.",
                request.EmpleadoIds.Distinct().Count(),
                solicitadoEn.ToUnixTimeMilliseconds()));
        })
        .RequireAuthorization("NOTIFICACIONES_EJECUTAR")
        .WithName("NotificarEmpleadosSeleccionados")
        .WithSummary("El Líder Técnico fuerza el envío del correo de notificación a los empleados indicados");

        // GET /estado - Resultado de un aviso manual pedido por el usuario actual
        group.MapGet("/estado", async (
            [FromQuery] int[] empleadoIds,
            [FromQuery] long desdeMs,
            [FromQuery] int? idProyecto,
            [FromServices] ApplicationDbContext db,
            [FromServices] ICurrentUserService currentUserService,
            CancellationToken ct) =>
        {
            var error = ValidarEmpleados(empleadoIds);
            if (error is not null)
                return error;

            var ids = empleadoIds.Distinct().ToList();
            var resultados = await ConsultarEstadoAsync(
                db, currentUserService.UserId, ids, DateTimeOffset.FromUnixTimeMilliseconds(desdeMs), idProyecto, ct);

            return Results.Ok(new EstadoNotificacionResponse(
                resultados.All(r => r.Estado != Pendiente),
                resultados));
        })
        .RequireAuthorization("NOTIFICACIONES_EJECUTAR")
        .WithName("GetEstadoNotificacion")
        .WithSummary("Estado de los avisos manuales pedidos por el usuario actual desde el momento indicado");

        // GET /estado/stream - Lo mismo que /estado, pero por Server-Sent Events: el backend empuja
        // un evento "estado" por empleado en cuanto RegistrarEnvioHandler guarda su resultado,
        // y un evento "fin" cuando ya no queda ninguno pendiente (o se agota el tiempo máximo).
        group.MapGet("/estado/stream", (
            [FromQuery] int[] empleadoIds,
            [FromQuery] long desdeMs,
            [FromQuery] int? idProyecto,
            [FromServices] ApplicationDbContext db,
            [FromServices] ICurrentUserService currentUserService,
            [FromServices] INotificacionEstadoNotifier notifier,
            HttpContext http,
            CancellationToken ct) =>
        {
            var error = ValidarEmpleados(empleadoIds);
            if (error is not null)
                return error;

            // Que nginx/Traefik no acumulen la respuesta antes de reenviarla
            http.Response.Headers["X-Accel-Buffering"] = "no";

            var eventos = StreamEstadoAsync(
                db, notifier, currentUserService.UserId, empleadoIds.Distinct().ToList(),
                DateTimeOffset.FromUnixTimeMilliseconds(desdeMs), idProyecto, ct);

            return (IResult)TypedResults.ServerSentEvents(eventos);
        })
        .RequireAuthorization("NOTIFICACIONES_EJECUTAR")
        .WithName("StreamEstadoNotificacion")
        .WithSummary("Server-Sent Events con el resultado de los avisos manuales pedidos por el usuario actual");

        // GET /cortes/ultimo - Resumen del último corte automático (enviados, fallidos, omitidos y pendientes)
        group.MapGet("/cortes/ultimo", async (
            [FromServices] IConsultaResumenCorte consulta,
            CancellationToken ct) =>
        {
            var resumen = await consulta.UltimoCorteAsync(ct);
            return resumen is null ? Results.NoContent() : Results.Ok(resumen);
        })
        .RequireAuthorization("NOTIFICACIONES_EJECUTAR")
        .WithName("GetResumenUltimoCorte")
        .WithSummary("Resumen del último corte automático de horas");

        // GET /cortes/stream - Lo mismo por Server-Sent Events, abierto mientras el admin o líder use la app:
        // al conectarse recibe el último corte (aunque haya pasado mientras no estaba) y luego cada cambio.
        group.MapGet("/cortes/stream", (
            [FromServices] IResumenCorteCache resumenes,
            [FromServices] ICorteNotifier notifier,
            HttpContext http,
            CancellationToken ct) =>
        {
            http.Response.Headers["X-Accel-Buffering"] = "no";
            return TypedResults.ServerSentEvents(StreamCortesAsync(resumenes, notifier, ct));
        })
        .RequireAuthorization("NOTIFICACIONES_EJECUTAR")
        .WithName("StreamResumenCorte")
        .WithSummary("Server-Sent Events con el resumen del último corte automático y sus cambios");

        // GET /mias/stream - Campana de cualquier colaborador: los correos de horas faltantes que se le enviaron
        // (aviso de su líder o del corte). Al conectarse recibe los recientes y luego cada uno nuevo al instante.
        group.MapGet("/mias/stream", (
            [FromServices] IConsultaMisNotificaciones consulta,
            [FromServices] IAvisoColaboradorNotifier notifier,
            [FromServices] ICurrentUserService currentUserService,
            HttpContext http,
            CancellationToken ct) =>
        {
            http.Response.Headers["X-Accel-Buffering"] = "no";
            return TypedResults.ServerSentEvents(StreamMisAvisosAsync(consulta, notifier, currentUserService.UserId, ct));
        })
        .RequireAuthorization()
        .WithName("StreamMisNotificaciones")
        .WithSummary("Server-Sent Events con los avisos de horas faltantes enviados al usuario actual");
    }

    // Sin novedades en memoria se revisa la BD cada tanto (por si el envío se registró en otra instancia)
    // y se manda un "ping" para que el proxy no corte la conexión.
    private static readonly TimeSpan RevisionMisAvisos = TimeSpan.FromSeconds(60);

    private static async IAsyncEnumerable<SseItem<IReadOnlyList<AvisoColaboradorDto>?>> StreamMisAvisosAsync(
        IConsultaMisNotificaciones consulta,
        IAvisoColaboradorNotifier notifier,
        int idUsuario,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var empleados = await consulta.EmpleadosDelUsuarioAsync(idUsuario, ct);
        using var suscripcion = notifier.Suscribir(empleados);
        string? ultimoEnviado = null;

        while (!ct.IsCancellationRequested)
        {
            var avisos = await consulta.RecientesAsync(empleados, ct);
            var json = System.Text.Json.JsonSerializer.Serialize(avisos);

            if (json != ultimoEnviado)
            {
                ultimoEnviado = json;
                yield return new SseItem<IReadOnlyList<AvisoColaboradorDto>?>(avisos, "avisos");
            }

            var huboCambio = await EsperarAsync(suscripcion.Lector, RevisionMisAvisos, ct);
            if (ct.IsCancellationRequested)
                yield break;

            if (huboCambio)
                while (suscripcion.Lector.TryRead(out _)) { }
            else
                yield return new SseItem<IReadOnlyList<AvisoColaboradorDto>?>(null, "ping");
        }
    }

    // Mientras el corte se procesa se revisa la BD seguido; en reposo cada tanto (por si otra
    // instancia del backend lo procesó) y se manda un "ping" para que el proxy no corte la conexión.
    private static readonly TimeSpan RevisionCorteEnProceso = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan RevisionCorteEnReposo = TimeSpan.FromSeconds(30);
    // Varios envíos seguidos se juntan en una sola actualización
    private static readonly TimeSpan AgrupacionCambiosCorte = TimeSpan.FromSeconds(1);

    private static async IAsyncEnumerable<SseItem<ResumenCorteDto?>> StreamCortesAsync(
        IResumenCorteCache resumenes,
        ICorteNotifier notifier,
        [EnumeratorCancellation] CancellationToken ct)
    {
        using var suscripcion = notifier.Suscribir();
        string? ultimoEnviado = null;

        while (!ct.IsCancellationRequested)
        {
            // Compartido entre conexiones: como máximo una consulta por segundo en total
            var resumen = await resumenes.ObtenerAsync(ct);
            var json = System.Text.Json.JsonSerializer.Serialize(resumen);

            if (json != ultimoEnviado)
            {
                ultimoEnviado = json;
                yield return new SseItem<ResumenCorteDto?>(resumen, "corte");
            }

            var espera = resumen?.EnProceso == true ? RevisionCorteEnProceso : RevisionCorteEnReposo;
            var huboCambio = await EsperarAsync(suscripcion.Lector, espera, ct);
            if (ct.IsCancellationRequested)
                yield break;

            if (huboCambio)
            {
                await PausaAsync(AgrupacionCambiosCorte, ct);
                while (suscripcion.Lector.TryRead(out _)) { }
            }
            else if (resumen?.EnProceso != true)
            {
                yield return new SseItem<ResumenCorteDto?>(null, "ping");
            }
        }
    }

    private static async Task<bool> EsperarAsync<T>(System.Threading.Channels.ChannelReader<T> lector, TimeSpan espera, CancellationToken ct)
    {
        using var temporizador = CancellationTokenSource.CreateLinkedTokenSource(ct);
        temporizador.CancelAfter(espera);
        try
        {
            return await lector.WaitToReadAsync(temporizador.Token);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private static async Task PausaAsync(TimeSpan pausa, CancellationToken ct)
    {
        try
        {
            await Task.Delay(pausa, ct);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private const string Pendiente = "Pendiente";

    private static IResult? ValidarEmpleados(int[]? empleadoIds)
    {
        if (empleadoIds is null || empleadoIds.Length == 0)
            return Results.BadRequest(new { Mensaje = "Debe indicar al menos un empleado." });
        if (empleadoIds.Length > MaxEmpleadosEstado)
            return Results.BadRequest(new { Mensaje = $"Máximo {MaxEmpleadosEstado} empleados por consulta." });
        return null;
    }

    private static async Task<List<EstadoEmpleadoDto>> ConsultarEstadoAsync(
        ApplicationDbContext db,
        int usuarioId,
        IReadOnlyCollection<int> ids,
        DateTimeOffset desde,
        int? idProyecto,
        CancellationToken ct)
    {
        // Con idProyecto solo cuenta el aviso de ese proyecto (fila de Seguimiento); sin él, cualquiera
        var registros = await db.NotificacionesEnvio
            .AsNoTracking()
            .Where(n => n.Origen == OrigenNotificacion.Manual
                     && n.IdUsuarioEjecutor == usuarioId
                     && n.FechaEnvio >= desde
                     && ids.Contains(n.IdEmpleado)
                     && (idProyecto == null || n.IdProyecto == idProyecto))
            .OrderByDescending(n => n.FechaEnvio)
            .ToListAsync(ct);

        var ultimoPorEmpleado = registros
            .GroupBy(n => n.IdEmpleado)
            .ToDictionary(g => g.Key, g => g.First());

        return ids
            .Select(id => ultimoPorEmpleado.TryGetValue(id, out var r)
                ? new EstadoEmpleadoDto(id, r.Estado.ToString(), r.DetalleError)
                : new EstadoEmpleadoDto(id, Pendiente, null))
            .ToList();
    }

    private static async IAsyncEnumerable<SseItem<EstadoEmpleadoDto?>> StreamEstadoAsync(
        ApplicationDbContext db,
        INotificacionEstadoNotifier notifier,
        int usuarioId,
        List<int> ids,
        DateTimeOffset desde,
        int? idProyecto,
        [EnumeratorCancellation] CancellationToken ct)
    {
        // Suscribirse antes de mirar la BD: así no se pierde un resultado que llegue entre medio
        using var suscripcion = notifier.Suscribir(usuarioId);
        var pendientes = ids.ToHashSet();
        var limite = DateTimeOffset.UtcNow + DuracionMaximaStream;

        foreach (var estado in await ConsultarEstadoAsync(db, usuarioId, ids, desde, idProyecto, ct))
        {
            if (estado.Estado == Pendiente)
                continue;
            pendientes.Remove(estado.IdEmpleado);
            yield return new SseItem<EstadoEmpleadoDto?>(estado, "estado");
        }

        while (pendientes.Count > 0 && DateTimeOffset.UtcNow < limite && !ct.IsCancellationRequested)
        {
            var llegoAlgo = await EsperarCambioAsync(suscripcion, IntervaloRevisionStream, ct);
            if (ct.IsCancellationRequested)
                yield break;

            if (llegoAlgo)
            {
                while (suscripcion.Lector.TryRead(out var cambio))
                {
                    if (cambio.FechaEnvio < desde
                        || (idProyecto is not null && cambio.IdProyecto != idProyecto)
                        || !pendientes.Remove(cambio.IdEmpleado))
                        continue;
                    yield return new SseItem<EstadoEmpleadoDto?>(
                        new EstadoEmpleadoDto(cambio.IdEmpleado, cambio.Estado.ToString(), cambio.Detalle), "estado");
                }
                continue;
            }

            // Sin novedades en memoria: se revisa la BD (el evento pudo procesarse en otra instancia)
            foreach (var estado in await ConsultarEstadoAsync(db, usuarioId, pendientes.ToList(), desde, idProyecto, ct))
            {
                if (estado.Estado == Pendiente)
                    continue;
                pendientes.Remove(estado.IdEmpleado);
                yield return new SseItem<EstadoEmpleadoDto?>(estado, "estado");
            }

            if (pendientes.Count > 0)
                yield return new SseItem<EstadoEmpleadoDto?>(null, "ping");
        }

        if (!ct.IsCancellationRequested)
            yield return new SseItem<EstadoEmpleadoDto?>(null, "fin");
    }

    // true si llegó un cambio por el notificador; false si pasó el intervalo sin novedades
    private static async Task<bool> EsperarCambioAsync(SuscripcionEstado suscripcion, TimeSpan espera, CancellationToken ct)
    {
        using var temporizador = CancellationTokenSource.CreateLinkedTokenSource(ct);
        temporizador.CancelAfter(espera);
        try
        {
            return await suscripcion.Lector.WaitToReadAsync(temporizador.Token);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}

public record NotificarResponse(string Mensaje, int Solicitados, long SolicitadoEnMs);
public record EstadoEmpleadoDto(int IdEmpleado, string Estado, string? Detalle);
public record EstadoNotificacionResponse(bool Completo, IReadOnlyList<EstadoEmpleadoDto> Empleados);

public class NotificarRequest
{
    public List<int> EmpleadoIds { get; set; } = new();

    // Rango del filtro de Seguimiento (yyyy-MM-dd). Si no se envía, se usa el período en curso hasta ayer.
    public DateOnly? FechaDesde { get; set; }
    public DateOnly? FechaHasta { get; set; }

    // Proyecto de la fila de Seguimiento: solo sus horas, y el umbral entre avisos es por proyecto.
    // Null = todas las horas del colaborador (fila "Sin Proyecto").
    public int? IdProyecto { get; set; }
}
