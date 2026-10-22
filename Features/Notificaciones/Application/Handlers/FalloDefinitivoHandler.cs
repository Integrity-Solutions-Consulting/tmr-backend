using tmr_backend.Features.Notificaciones.Domain;
using tmr_backend.Features.Notificaciones.Domain.Events;
using tmr_backend.Infrastructure.Messaging;

namespace tmr_backend.Features.Notificaciones.Application.Handlers;

/// <summary>
/// Cuando un aviso agota sus reintentos (EventDispatcher.MaxIntentos) queda registrado como Fallido en
/// NotificacionEnvio. Así se ve en el popup del corte o en el toast del aviso manual, en vez de desaparecer.
/// </summary>
public sealed class FalloDefinitivoHandler :
    IEventoFallidoHandler<HorasFaltantesDetectadas>,
    IEventoFallidoHandler<NotificacionManualSolicitada>
{
    private readonly IEventBus _bus;

    public FalloDefinitivoHandler(IEventBus bus) => _bus = bus;

    public Task HandleFalloDefinitivoAsync(HorasFaltantesDetectadas evento, string error, CancellationToken ct) =>
        _bus.PublishAsync(new NotificacionFallida(
            evento.IdEmpleado,
            evento.HorasFaltantes,
            evento.InicioPeriodo,
            evento.FinPeriodo,
            evento.Origen,
            evento.FechaCorte,
            evento.IdUsuarioEjecutor,
            DateTimeOffset.UtcNow,
            Motivo(error),
            evento.IdProyecto), ct);

    // No se llegó a calcular nada: cada colaborador pedido queda como fallido para que el líder lo sepa
    public async Task HandleFalloDefinitivoAsync(NotificacionManualSolicitada evento, string error, CancellationToken ct)
    {
        var ahora = DateTimeOffset.UtcNow;
        foreach (var idEmpleado in evento.IdsEmpleados.Distinct())
        {
            await _bus.PublishAsync(new NotificacionFallida(
                idEmpleado,
                0m,
                evento.InicioPeriodo,
                evento.FinPeriodo,
                OrigenNotificacion.Manual,
                FechaCorte: null,
                evento.IdUsuarioEjecutor,
                ahora,
                Motivo(error),
                evento.IdProyecto), ct);
        }
    }

    private static string Motivo(string error) =>
        $"No se pudo procesar después de {EventDispatcher.MaxIntentos} intentos: {error}";
}
