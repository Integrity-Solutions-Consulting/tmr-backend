using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TmrBackend.Features.Notificaciones.Application.Queries;
using TmrBackend.Features.Notificaciones.Domain;
using TmrBackend.Features.Notificaciones.Domain.Events;
using TmrBackend.Infrastructure.Messaging;

namespace TmrBackend.Features.Notificaciones.Application.Handlers;

/// <summary>
/// Detecta a los empleados con horas faltantes y publica un HorasFaltantesDetectadas por cada uno.
/// - CorteDeHorasAlcanzado (flujos 1 y 3): evalúa a todos los empleados activos y descarta a quien ya
///   recibió el aviso automático de ese corte.
/// - NotificacionManualSolicitada (flujo 2): evalúa solo a los empleados elegidos por el líder y descarta a quien
///   ya no debe horas o recibió un aviso manual hace menos del umbral. No mira los avisos del corte, porque el
///   aviso manual es independiente.
/// </summary>
public sealed class DetectarHorasFaltantesHandler :
    IEventHandler<CorteDeHorasAlcanzado>,
    IEventHandler<NotificacionManualSolicitada>
{
    private readonly IConsultaHorasFaltantes _consulta;
    private readonly IEventBus _eventBus;
    private readonly NotificacionHorasOptions _opciones;
    private readonly ILogger<DetectarHorasFaltantesHandler> _logger;

    public DetectarHorasFaltantesHandler(
        IConsultaHorasFaltantes consulta,
        IEventBus eventBus,
        IOptions<NotificacionHorasOptions> opciones,
        ILogger<DetectarHorasFaltantesHandler> logger)
    {
        _consulta = consulta;
        _eventBus = eventBus;
        _opciones = opciones.Value;
        _logger = logger;
    }

    public async Task HandleAsync(CorteDeHorasAlcanzado evento, CancellationToken ct)
    {
        var conDeficit = await _consulta.CalcularAsync(evento.InicioPeriodo, evento.FinPeriodo, null, ct);
        var yaNotificados = await _consulta.NotificadosEnCorteAsync(evento.FechaCorte, ct);

        var publicados = 0;
        foreach (var empleado in conDeficit)
        {
            if (yaNotificados.Contains(empleado.IdEmpleado) || !TieneEmail(empleado))
                continue;

            await _eventBus.PublishAsync(new HorasFaltantesDetectadas(
                empleado.IdEmpleado,
                empleado.Nombre,
                empleado.Email!,
                empleado.Calculo.HorasFaltantes,
                evento.InicioPeriodo,
                evento.FinPeriodo,
                OrigenNotificacion.Automatico,
                evento.FechaCorte,
                IdUsuarioEjecutor: null), ct);
            publicados++;
        }

        _logger.LogInformation(
            "Corte {FechaCorte} ({Inicio} a {Fin}): {ConDeficit} empleados con horas faltantes, {YaNotificados} ya notificados, {Publicados} avisos publicados.",
            evento.FechaCorte, evento.InicioPeriodo, evento.FinPeriodo, conDeficit.Count, yaNotificados.Count, publicados);
    }

    public async Task HandleAsync(NotificacionManualSolicitada evento, CancellationToken ct)
    {
        var ids = evento.IdsEmpleados.Distinct().ToList();
        if (ids.Count == 0)
            return;

        // Se recalcula en este momento: si el empleado registró sus horas desde que el líder abrió la pantalla, ya no se le avisa.
        var conDeficit = await _consulta.CalcularAsync(evento.InicioPeriodo, evento.FinPeriodo, ids, ct);
        var ultimosAvisos = await _consulta.UltimosAvisosManualesAsync(conDeficit.Select(e => e.IdEmpleado).ToList(), ct);
        var ahora = DateTimeOffset.UtcNow;

        var publicados = 0;
        foreach (var empleado in conDeficit)
        {
            DateTimeOffset? ultimo = ultimosAvisos.TryGetValue(empleado.IdEmpleado, out var fecha) ? fecha : null;
            if (DentroDelUmbral(ultimo, ahora, _opciones.UmbralNotificacionManual))
            {
                _logger.LogInformation(
                    "Empleado {IdEmpleado}: se omite el aviso manual porque recibió otro el {UltimoAviso} (umbral {Umbral} h).",
                    empleado.IdEmpleado, ultimo, _opciones.UmbralNotificacionManual);
                continue;
            }

            if (!TieneEmail(empleado))
                continue;

            await _eventBus.PublishAsync(new HorasFaltantesDetectadas(
                empleado.IdEmpleado,
                empleado.Nombre,
                empleado.Email!,
                empleado.Calculo.HorasFaltantes,
                evento.InicioPeriodo,
                evento.FinPeriodo,
                OrigenNotificacion.Manual,
                FechaCorte: null,
                evento.IdUsuarioEjecutor), ct);
            publicados++;
        }

        _logger.LogInformation(
            "Aviso manual del usuario {IdUsuario} ({Inicio} a {Fin}): {Solicitados} solicitados, {ConDeficit} con horas faltantes, {Publicados} avisos publicados.",
            evento.IdUsuarioEjecutor, evento.InicioPeriodo, evento.FinPeriodo, ids.Count, conDeficit.Count, publicados);
    }

    /// <summary>
    /// Indica si el último aviso manual es tan reciente que todavía no se puede enviar otro.
    /// Un umbral de 0 o menos desactiva la restricción.
    /// </summary>
    public static bool DentroDelUmbral(DateTimeOffset? ultimoAvisoManual, DateTimeOffset ahora, int umbralHoras) =>
        ultimoAvisoManual.HasValue
        && umbralHoras > 0
        && ahora - ultimoAvisoManual.Value < TimeSpan.FromHours(umbralHoras);

    private bool TieneEmail(EmpleadoConHorasFaltantes empleado)
    {
        if (!string.IsNullOrWhiteSpace(empleado.Email))
            return true;

        _logger.LogWarning(
            "Empleado {IdEmpleado} ({Nombre}) debe {Horas} h pero no tiene correo configurado. Se omite el aviso.",
            empleado.IdEmpleado, empleado.Nombre, empleado.Calculo.HorasFaltantes);
        return false;
    }
}
