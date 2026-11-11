using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using tmr_backend.Features.Permisos.Services;
using tmr_backend.Infrastructure.Database;
using tmr_backend.Infrastructure.Messaging;
using tmr_backend.Infrastructure.Shared;
using tmr_backend.Shared.Wrappers;

namespace tmr_backend.Features.Permisos.Endpoints;

public static partial class PermisosEndpoints
{
    private static void MapEnviosPermisoEndpoints(RouteGroupBuilder group)
    {
        group.MapGet("/{id:int}", async (int id, ApplicationDbContext db, ICurrentUserService user, CancellationToken ct) =>
        {
            var actor = await ResolverActorActualAsync(db, user, ct);
            var solicitud = await ConsultarSolicitudesBase(db).AsNoTracking().FirstOrDefaultAsync(s => s.Id == id && s.Activo, ct);
            if (solicitud is null) return Results.NotFound();
            if (actor is null || !PuedeConsultarSolicitud(actor, solicitud)) return Results.Forbid();
            return Results.Ok(ApiResponse<DTOs.PermisoItemDto>.Ok(MapearSolicitudDto(solicitud, null)));
        }).RequireAuthorization("PERMISOS_VIEW");

        group.MapGet("/{id:int}/envios", async (int id, ApplicationDbContext db,
            ICurrentUserService user, CancellationToken ct) =>
        {
            var actor = await ResolverActorActualAsync(db, user, ct);
            if (actor is null || (!actor.EsAdmin && !actor.EsRrhh)) return Results.Forbid();
            var solicitud = await ConsultarSolicitudesBase(db).AsNoTracking().FirstOrDefaultAsync(s => s.Id == id && s.Activo, ct);
            if (solicitud is null) return Results.NotFound();
            var rows = await db.EventosOutbox.FromSqlInterpolated($"SELECT * FROM notificacion.evento_outbox WHERE tipo_evento = 'CorreoPermisoEncolado' AND payload->>'IdSolicitud' = {id.ToString()}")
                .AsNoTracking().OrderByDescending(e => e.FechaCreacion).Take(100).ToListAsync(ct);
            var reintentados = await db.EventosOutbox
                .FromSqlInterpolated($"SELECT * FROM notificacion.evento_outbox WHERE tipo_evento = 'CorreoPermisoEncolado' AND payload->>'IdSolicitud' = {id.ToString()} AND payload->>'EnvioAnterior' IS NOT NULL")
                .AsNoTracking().Select(e => e.Payload).ToListAsync(ct);
            var padres = reintentados.Select(p => JsonSerializer.Deserialize<CorreoPermisoEncolado>(p)!.EnvioAnterior).ToHashSet();
            var envios = rows.Select(e => {
                var aviso = JsonSerializer.Deserialize<CorreoPermisoEncolado>(e.Payload)!;
                return new {
                    e.Id, Canal = "Correo", Destinatario = aviso.DestinatarioOriginal,
                    Fecha = e.FechaCreacion, e.FechaProcesado, e.Intentos,
                    Estado = e.UltimoError == "AVISO_NO_VIGENTE" ? "No vigente" :
                        e.Estado == EstadoEvento.Procesado ? "Enviado al proveedor" :
                        e.Estado == EstadoEvento.Fallido ? "Fallido" : "Pendiente",
                    PuedeReintentar = e.Estado == EstadoEvento.Fallido && !padres.Contains(e.Id) && PermisoAvisoVigencia.EsVigente(aviso, solicitud),
                    aviso.ReintentadoPor, aviso.FechaReintento
                };
            }).ToList();
            var campana = await db.NotificacionesUsuario.AsNoTracking().Where(n => n.IdSolicitud == id)
                .OrderByDescending(n => n.FechaCreacion).Take(100)
                .Select(n => new { n.Id, n.IdUsuarioDestino, n.Titulo, n.Leida, n.FechaCreacion }).ToListAsync(ct);
            return Results.Ok(ApiResponse<object>.Ok(new { Envios = envios, Campana = campana }));
        }).RequireAuthorization("PERMISOS_RRHH_READ");

        group.MapPost("/{id:int}/envios/{envioId:guid}/reintentar", async (int id, Guid envioId,
            ApplicationDbContext db, ICurrentUserService user, CancellationToken ct) =>
        {
            var actor = await ResolverActorActualAsync(db, user, ct);
            if (actor is null || (!actor.EsAdmin && !actor.EsRrhh)) return Results.Forbid();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var envio = await db.EventosOutbox.FromSqlInterpolated($"SELECT * FROM notificacion.evento_outbox WHERE id = {envioId} FOR UPDATE")
                .FirstOrDefaultAsync(ct);
            if (envio is null || envio.TipoEvento != nameof(CorreoPermisoEncolado)) return Results.NotFound();
            var aviso = JsonSerializer.Deserialize<CorreoPermisoEncolado>(envio.Payload)!;
            if (aviso.IdSolicitud != id) return Results.NotFound();
            if (envio.Estado != EstadoEvento.Fallido)
                return Results.Conflict(ApiResponse<object>.Fail(409, "Este envío ya fue atendido o está pendiente.", []));
            var solicitud = await ConsultarSolicitudesBase(db).AsNoTracking().FirstOrDefaultAsync(s => s.Id == id && s.Activo, ct);
            if (!PermisoAvisoVigencia.EsVigente(aviso, solicitud))
                return Results.Conflict(ApiResponse<object>.Fail(409, "El aviso ya no corresponde al estado actual. Si venció el enlace externo, usa Reenviar enlace.", []));
            // Cada reintento queda como evento nuevo; el fallido conserva su resultado e historial.
            var hijos = await db.EventosOutbox.FromSqlInterpolated($"SELECT * FROM notificacion.evento_outbox WHERE tipo_evento = 'CorreoPermisoEncolado' AND payload->>'EnvioAnterior' = {envioId.ToString()}").AnyAsync(ct);
            if (hijos) return Results.Conflict(ApiResponse<object>.Fail(409, "Ya existe un reintento para este envío. Actualiza la lista.", []));
            db.EventosOutbox.Add(new EventoOutbox {
                Id = Guid.NewGuid(), TipoEvento = nameof(CorreoPermisoEncolado), Estado = EstadoEvento.Pendiente,
                FechaCreacion = DateTimeOffset.UtcNow,
                Payload = JsonSerializer.Serialize(aviso with { ReintentadoPor = actor.Email, FechaReintento = DateTimeOffset.UtcNow, EnvioAnterior = envioId })
            });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Results.Ok(ApiResponse<object>.Ok(new { }, "Reintento registrado. El correo está pendiente de envío."));
        }).RequireAuthorization("PERMISOS_RRHH_CONFIRM");
    }
}
