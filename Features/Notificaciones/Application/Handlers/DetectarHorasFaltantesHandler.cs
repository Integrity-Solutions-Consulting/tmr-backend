using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using tmr_backend.Features.Notificaciones.Application.Estado;
using tmr_backend.Features.Notificaciones.Application.Queries;
using tmr_backend.Features.Notificaciones.Domain;
using tmr_backend.Features.Notificaciones.Domain.Events;
using tmr_backend.Features.TimeReport.Services;
using tmr_backend.Infrastructure.Messaging;

namespace tmr_backend.Features.Notificaciones.Application.Handlers;

public sealed class DetectarHorasFaltantesHandler :
    IEventHandler<CorteDeHorasAlcanzado>,
    IEventHandler<NotificacionManualSolicitada>
{
    private readonly IConsultaHorasFaltantes _consulta;
    private readonly IEventBus _eventBus;
    private readonly NotificacionHorasOptions _opciones;
    private readonly ILogger<DetectarHorasFaltantesHandler> _logger;
    private readonly ICorteNotifier? _corteNotifier;

    public DetectarHorasFaltantesHandler(
        IConsultaHorasFaltantes consulta,
        IEventBus eventBus,
        IOptions<NotificacionHorasOptions> opciones,
        ILogger<DetectarHorasFaltantesHandler> logger,
        ICorteNotifier? corteNotifier = null)
    {
        _consulta = consulta;
        _eventBus = eventBus;
        _opciones = opciones.Value;
        _logger = logger;
        _corteNotifier = corteNotifier;
    }

    public async Task HandleAsync(CorteDeHorasAlcanzado evento, CancellationToken ct)
    {
        // 8 h por proyecto, como Seguimiento; incluye a quien salió dentro del período
        var conDeficit = await _consulta.CalcularPorProyectoAsync(evento.InicioPeriodo, evento.FinPeriodo, ct);
        var yaNotificados = await _consulta.NotificadosEnCorteAsync(evento.FechaCorte, ct);

        var publicados = 0;
        var ahora = DateTimeOffset.UtcNow;
        foreach (var empleado in conDeficit)
        {
            if (yaNotificados.Contains(empleado.IdEmpleado))
                continue;

            if (!TieneEmail(empleado))
            {
                // Queda en el resumen del corte como omitido, para que admin y líderes lo vean
                await _eventBus.PublishAsync(new NotificacionOmitida(
                    empleado.IdEmpleado,
                    evento.InicioPeriodo,
                    evento.FinPeriodo,
                    IdUsuarioEjecutor: null,
                    ahora,
                    "No tiene correo registrado",
                    OrigenNotificacion.Automatico,
                    evento.FechaCorte,
                    empleado.Calculo.HorasFaltantes), ct);
                continue;
            }

            await _eventBus.PublishAsync(new HorasFaltantesDetectadas(
                empleado.IdEmpleado,
                empleado.Nombre,
                empleado.Email!,
                empleado.Calculo.HorasFaltantes,
                evento.InicioPeriodo,
                evento.FinPeriodo,
                OrigenNotificacion.Automatico,
                evento.FechaCorte,
                IdUsuarioEjecutor: null,
                Proyectos: empleado.Proyectos), ct);
            publicados++;
        }

        _logger.LogInformation(
            "Corte {FechaCorte} ({Inicio} a {Fin}): {ConDeficit} empleados con horas faltantes, {YaNotificados} ya notificados, {Publicados} avisos publicados.",
            evento.FechaCorte, evento.InicioPeriodo, evento.FinPeriodo, conDeficit.Count, yaNotificados.Count, publicados);

        // Ya se sabe a quiénes se va a avisar: el resumen del corte puede mostrar el total
        _corteNotifier?.NotificarCambio();
    }

    public async Task HandleAsync(NotificacionManualSolicitada evento, CancellationToken ct)
    {
        var ids = evento.IdsEmpleados.Distinct().ToList();
        if (ids.Count == 0)
            return;

        var finPeriodo = FinManualEfectivo(evento.FinPeriodo, CalculoHorasPeriodo.HoyEcuador());

        // Como la fila de Seguimiento: solo las horas de ese proyecto, y también a quien ya salió
        // (debe completar hasta su fecha de salida)
        var conDeficit = await _consulta.CalcularAsync(
            evento.InicioPeriodo, finPeriodo, ids, ct, evento.IdProyecto, incluirInactivos: true);
        // El umbral entre avisos es por proyecto: el aviso de otro proyecto no bloquea este
        var ultimosAvisos = await _consulta.UltimosAvisosManualesAsync(
            conDeficit.Select(e => e.IdEmpleado).ToList(), ct, evento.IdProyecto);
        var nombreProyecto = evento.IdProyecto is { } idProyecto
            ? await _consulta.NombreProyectoAsync(idProyecto, ct)
            : null;
        var ahora = DateTimeOffset.UtcNow;

        // Los que se pidieron pero no deben horas: se registra el motivo para que el líder lo vea
        var idsConDeficit = conDeficit.Select(e => e.IdEmpleado).ToHashSet();
        var sinPendientes = nombreProyecto is null
            ? "No tiene horas pendientes en el período"
            : $"No tiene horas pendientes en el proyecto {nombreProyecto} en el período";
        foreach (var idSinDeficit in ids.Where(id => !idsConDeficit.Contains(id)))
        {
            await PublicarOmitidaAsync(evento, finPeriodo, idSinDeficit, sinPendientes, ahora, ct);
        }

        var publicados = 0;
        foreach (var empleado in conDeficit)
        {
            DateTimeOffset? ultimo = ultimosAvisos.TryGetValue(empleado.IdEmpleado, out var fecha) ? fecha : null;
            if (DentroDelUmbral(ultimo, ahora, _opciones.UmbralNotificacionManual))
            {
                _logger.LogInformation(
                    "Empleado {IdEmpleado}: se omite el aviso manual porque recibió otro el {UltimoAviso} (umbral {Umbral} h).",
                    empleado.IdEmpleado, ultimo, _opciones.UmbralNotificacionManual);
                await PublicarOmitidaAsync(
                    evento, finPeriodo, empleado.IdEmpleado,
                    MotivoUmbral(nombreProyecto, _opciones.UmbralNotificacionManual), ahora, ct);
                continue;
            }

            if (!TieneEmail(empleado))
            {
                await PublicarOmitidaAsync(evento, finPeriodo, empleado.IdEmpleado, "No tiene correo registrado", ahora, ct);
                continue;
            }

            await _eventBus.PublishAsync(new HorasFaltantesDetectadas(
                empleado.IdEmpleado,
                empleado.Nombre,
                empleado.Email!,
                empleado.Calculo.HorasFaltantes,
                evento.InicioPeriodo,
                finPeriodo,
                OrigenNotificacion.Manual,
                FechaCorte: null,
                evento.IdUsuarioEjecutor,
                evento.IdProyecto,
                nombreProyecto), ct);
            publicados++;
        }

        _logger.LogInformation(
            "Aviso manual del usuario {IdUsuario} ({Inicio} a {Fin}): {Solicitados} solicitados, {ConDeficit} con horas faltantes, {Publicados} avisos publicados.",
            evento.IdUsuarioEjecutor, evento.InicioPeriodo, finPeriodo, ids.Count, conDeficit.Count, publicados);
    }

    private Task PublicarOmitidaAsync(
        NotificacionManualSolicitada evento,
        DateOnly finPeriodo,
        int idEmpleado,
        string motivo,
        DateTimeOffset ahora,
        CancellationToken ct) =>
        _eventBus.PublishAsync(new NotificacionOmitida(
            idEmpleado,
            evento.InicioPeriodo,
            finPeriodo,
            evento.IdUsuarioEjecutor,
            ahora,
            motivo,
            IdProyecto: evento.IdProyecto), ct);

    public static string MotivoUmbral(string? nombreProyecto, int umbralHoras) =>
        nombreProyecto is null
            ? $"Ya recibió un aviso manual hace menos de {umbralHoras} h"
            : $"Ya recibió un aviso manual del proyecto {nombreProyecto} hace menos de {umbralHoras} h";

    /// <summary>
    /// El aviso manual usa el rango que el líder ve en Seguimiento; igual que esa pantalla, nunca cuenta días futuros.
    /// </summary>
    public static DateOnly FinManualEfectivo(DateOnly finSolicitado, DateOnly hoy) =>
        finSolicitado < hoy ? finSolicitado : hoy;

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
