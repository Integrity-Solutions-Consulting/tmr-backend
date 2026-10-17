using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using tmr_backend.Features.Notificaciones.Application.Estado;
using tmr_backend.Features.Notificaciones.Domain;
using tmr_backend.Features.Notificaciones.Domain.Events;
using tmr_backend.Infrastructure.Database;
using tmr_backend.Infrastructure.Messaging;

namespace tmr_backend.Features.Notificaciones.Application.Handlers;

/// <summary>
/// Registra cada intento de envío de correo en NotificacionEnvio.
/// RF-006: Guarda origen, estado, fecha, horas notificadas y detalle de error si aplica.
/// Los automáticos tienen un índice único por (empleado, corte) que también cubre los fallidos,
/// así que un reintento del mismo corte actualiza la fila existente en vez de insertar otra.
/// </summary>
public sealed class RegistrarEnvioHandler :
    IEventHandler<NotificacionEnviada>,
    IEventHandler<NotificacionFallida>,
    IEventHandler<NotificacionOmitida>
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<RegistrarEnvioHandler> _logger;
    private readonly INotificacionEstadoNotifier? _notifier;
    private readonly ICorteNotifier? _corteNotifier;
    private readonly IAvisoColaboradorNotifier? _avisoColaborador;

    public RegistrarEnvioHandler(
        ApplicationDbContext db,
        ILogger<RegistrarEnvioHandler> logger,
        INotificacionEstadoNotifier? notifier = null,
        ICorteNotifier? corteNotifier = null,
        IAvisoColaboradorNotifier? avisoColaborador = null)
    {
        _db = db;
        _logger = logger;
        _notifier = notifier;
        _corteNotifier = corteNotifier;
        _avisoColaborador = avisoColaborador;
    }

    public async Task HandleAsync(NotificacionEnviada evento, CancellationToken ct)
    {
        var envio = new NotificacionEnvio
        {
            IdEmpleado = evento.IdEmpleado,
            InicioPeriodo = evento.InicioPeriodo,
            FinPeriodo = evento.FinPeriodo,
            FechaCorte = evento.FechaCorte?.ToUniversalTime(),
            Origen = evento.Origen,
            Estado = EstadoEnvio.Enviado,
            HorasFaltantes = evento.HorasFaltantes,
            IdUsuarioEjecutor = evento.IdUsuarioEjecutor,
            IdProyecto = evento.IdProyecto,
            FechaEnvio = evento.FechaEnvio.ToUniversalTime(),
            // Corte: detalle por proyecto de lo que se informó (se muestra en "Ver más" del popup)
            DetalleError = evento.Detalle
        };

        await GuardarAsync(envio, ct);

        _logger.LogInformation(
            "Registrado envío exitoso para empleado {IdEmpleado} ({Origen}, {HorasFaltantes}h)",
            evento.IdEmpleado, evento.Origen, evento.HorasFaltantes);
    }

    public async Task HandleAsync(NotificacionFallida evento, CancellationToken ct)
    {
        var envio = new NotificacionEnvio
        {
            IdEmpleado = evento.IdEmpleado,
            InicioPeriodo = evento.InicioPeriodo,
            FinPeriodo = evento.FinPeriodo,
            FechaCorte = evento.FechaCorte?.ToUniversalTime(),
            Origen = evento.Origen,
            Estado = EstadoEnvio.Fallido,
            HorasFaltantes = evento.HorasFaltantes,
            IdUsuarioEjecutor = evento.IdUsuarioEjecutor,
            IdProyecto = evento.IdProyecto,
            FechaEnvio = evento.FechaEnvio.ToUniversalTime(),
            DetalleError = evento.Error
        };

        await GuardarAsync(envio, ct);

        _logger.LogWarning(
            "Registrado fallo de envío para empleado {IdEmpleado} ({Origen}, Error: {Error})",
            evento.IdEmpleado, evento.Origen, evento.Error);
    }

    public async Task HandleAsync(NotificacionOmitida evento, CancellationToken ct)
    {
        var envio = new NotificacionEnvio
        {
            IdEmpleado = evento.IdEmpleado,
            InicioPeriodo = evento.InicioPeriodo,
            FinPeriodo = evento.FinPeriodo,
            FechaCorte = evento.FechaCorte?.ToUniversalTime(),
            Origen = evento.Origen,
            Estado = EstadoEnvio.Omitido,
            HorasFaltantes = evento.HorasFaltantes,
            IdUsuarioEjecutor = evento.IdUsuarioEjecutor,
            IdProyecto = evento.IdProyecto,
            FechaEnvio = evento.FechaEnvio.ToUniversalTime(),
            DetalleError = evento.Motivo
        };

        await GuardarAsync(envio, ct);

        _logger.LogInformation(
            "Registrado aviso omitido para empleado {IdEmpleado} ({Origen}): {Motivo}",
            evento.IdEmpleado, evento.Origen, evento.Motivo);
    }

    private async Task GuardarAsync(NotificacionEnvio envio, CancellationToken ct)
    {
        if (envio.Origen == OrigenNotificacion.Automatico && envio.FechaCorte is { } corte)
        {
            var existente = await _db.NotificacionesEnvio.FirstOrDefaultAsync(
                n => n.IdEmpleado == envio.IdEmpleado
                  && n.Origen == OrigenNotificacion.Automatico
                  && n.FechaCorte == corte, ct);

            if (existente is not null)
            {
                // Un envío exitoso del corte no se pisa con un fallo posterior
                if (existente.Estado == EstadoEnvio.Enviado && envio.Estado != EstadoEnvio.Enviado)
                {
                    _logger.LogInformation(
                        "Empleado {IdEmpleado} ya tiene registrado el envío del corte {FechaCorte}; se ignora el resultado {Estado}",
                        envio.IdEmpleado, corte, envio.Estado);
                    return;
                }

                existente.InicioPeriodo = envio.InicioPeriodo;
                existente.FinPeriodo = envio.FinPeriodo;
                existente.Estado = envio.Estado;
                existente.HorasFaltantes = envio.HorasFaltantes;
                existente.FechaEnvio = envio.FechaEnvio;
                existente.DetalleError = envio.DetalleError;
                await _db.SaveChangesAsync(ct);
                _corteNotifier?.NotificarCambio();
                AvisarAlColaborador(envio);
                return;
            }
        }

        _db.NotificacionesEnvio.Add(envio);
        await _db.SaveChangesAsync(ct);

        // El resumen del corte que ven admin y líderes por /cortes/stream
        if (envio.Origen == OrigenNotificacion.Automatico)
            _corteNotifier?.NotificarCambio();

        AvisarAlColaborador(envio);

        // Solo los avisos manuales tienen a alguien esperando el resultado en pantalla
        if (envio.Origen == OrigenNotificacion.Manual && envio.IdUsuarioEjecutor is { } idUsuario)
        {
            _notifier?.Publicar(new CambioEstadoNotificacion(
                idUsuario, envio.IdEmpleado, envio.Estado, envio.DetalleError, envio.FechaEnvio, envio.IdProyecto));
        }
    }

    // El correo le llegó: también aparece en la campana del colaborador (/mias/stream)
    private void AvisarAlColaborador(NotificacionEnvio envio)
    {
        if (envio.Estado == EstadoEnvio.Enviado)
            _avisoColaborador?.Notificar(envio.IdEmpleado);
    }
}
