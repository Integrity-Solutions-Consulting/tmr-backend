using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using tmr_backend.Features.Notificaciones.Application.Estado;
using tmr_backend.Features.Notificaciones.Application.Handlers;
using tmr_backend.Features.Notificaciones.Domain;
using tmr_backend.Features.Notificaciones.Domain.Events;
using tmr_backend.Infrastructure.Database;

namespace tmr_backend.Tests.Notificaciones;

public class RegistrarEnvioHandlerTests
{
    private static readonly DateOnly Inicio = new(2026, 10, 1);
    private static readonly DateOnly Fin = new(2026, 10, 15);
    private static readonly DateTimeOffset FechaCorte = new(2026, 10, 15, 18, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Ahora = DateTimeOffset.UtcNow;

    private readonly ApplicationDbContext _db;
    private readonly RegistrarEnvioHandler _handler;

    public RegistrarEnvioHandlerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new ApplicationDbContext(options);
        _handler = new RegistrarEnvioHandler(_db, NullLogger<RegistrarEnvioHandler>.Instance);
    }

    [Fact]
    public async Task Guarda_NotificacionEnvio_con_estado_Enviado_para_NotificacionEnviada()
    {
        var evento = new NotificacionEnviada(
            IdEmpleado: 10,
            HorasFaltantes: 12.5m,
            InicioPeriodo: Inicio,
            FinPeriodo: Fin,
            Origen: OrigenNotificacion.Automatico,
            FechaCorte: FechaCorte,
            IdUsuarioEjecutor: null,
            FechaEnvio: Ahora);

        await _handler.HandleAsync(evento, default);

        var guardado = await _db.NotificacionesEnvio.FirstOrDefaultAsync(n => n.IdEmpleado == 10);
        Assert.NotNull(guardado);
        Assert.Equal(10, guardado.IdEmpleado);
        Assert.Equal(12.5m, guardado.HorasFaltantes);
        Assert.Equal(Inicio, guardado.InicioPeriodo);
        Assert.Equal(Fin, guardado.FinPeriodo);
        Assert.Equal(OrigenNotificacion.Automatico, guardado.Origen);
        Assert.Equal(EstadoEnvio.Enviado, guardado.Estado);
        Assert.Equal(FechaCorte.ToUniversalTime(), guardado.FechaCorte);
        Assert.Null(guardado.IdUsuarioEjecutor);
        Assert.Null(guardado.DetalleError);
    }

    [Fact]
    public async Task Guarda_NotificacionEnvio_con_estado_Fallido_y_DetalleError_para_NotificacionFallida()
    {
        var evento = new NotificacionFallida(
            IdEmpleado: 20,
            HorasFaltantes: 8m,
            InicioPeriodo: Inicio,
            FinPeriodo: Fin,
            Origen: OrigenNotificacion.Manual,
            FechaCorte: null,
            IdUsuarioEjecutor: 3,
            FechaEnvio: Ahora,
            Error: "Servidor SMTP no respondió");

        await _handler.HandleAsync(evento, default);

        var guardado = await _db.NotificacionesEnvio.FirstOrDefaultAsync(n => n.IdEmpleado == 20);
        Assert.NotNull(guardado);
        Assert.Equal(20, guardado.IdEmpleado);
        Assert.Equal(8m, guardado.HorasFaltantes);
        Assert.Equal(OrigenNotificacion.Manual, guardado.Origen);
        Assert.Equal(EstadoEnvio.Fallido, guardado.Estado);
        Assert.Null(guardado.FechaCorte);
        Assert.Equal(3, guardado.IdUsuarioEjecutor);
        Assert.Equal("Servidor SMTP no respondió", guardado.DetalleError);
    }

    [Fact]
    public async Task Guarda_NotificacionEnvio_con_estado_Omitido_y_motivo_para_NotificacionOmitida()
    {
        var evento = new NotificacionOmitida(
            IdEmpleado: 11,
            InicioPeriodo: Inicio,
            FinPeriodo: Fin,
            IdUsuarioEjecutor: 7,
            FechaEnvio: Ahora,
            Motivo: "No tiene correo registrado");

        await _handler.HandleAsync(evento, default);

        var guardado = await _db.NotificacionesEnvio.SingleAsync(n => n.IdEmpleado == 11);
        Assert.Equal(EstadoEnvio.Omitido, guardado.Estado);
        Assert.Equal(OrigenNotificacion.Manual, guardado.Origen);
        Assert.Equal(7, guardado.IdUsuarioEjecutor);
        Assert.Equal(0m, guardado.HorasFaltantes);
        Assert.Null(guardado.FechaCorte);
        Assert.Equal("No tiene correo registrado", guardado.DetalleError);
    }

    [Fact]
    public async Task Reintento_exitoso_del_corte_actualiza_la_fila_fallida_en_vez_de_duplicarla()
    {
        await _handler.HandleAsync(Fallida(OrigenNotificacion.Automatico, FechaCorte), default);
        await _handler.HandleAsync(Enviada(OrigenNotificacion.Automatico, FechaCorte), default);

        var guardado = await _db.NotificacionesEnvio.SingleAsync(n => n.IdEmpleado == 30);
        Assert.Equal(EstadoEnvio.Enviado, guardado.Estado);
        Assert.Null(guardado.DetalleError);
    }

    [Fact]
    public async Task Dos_fallos_del_mismo_corte_dejan_una_sola_fila()
    {
        await _handler.HandleAsync(Fallida(OrigenNotificacion.Automatico, FechaCorte), default);
        await _handler.HandleAsync(Fallida(OrigenNotificacion.Automatico, FechaCorte), default);

        Assert.Equal(1, await _db.NotificacionesEnvio.CountAsync(n => n.IdEmpleado == 30));
    }

    [Fact]
    public async Task Un_fallo_posterior_no_pisa_el_envio_exitoso_del_corte()
    {
        await _handler.HandleAsync(Enviada(OrigenNotificacion.Automatico, FechaCorte), default);
        await _handler.HandleAsync(Fallida(OrigenNotificacion.Automatico, FechaCorte), default);

        var guardado = await _db.NotificacionesEnvio.SingleAsync(n => n.IdEmpleado == 30);
        Assert.Equal(EstadoEnvio.Enviado, guardado.Estado);
    }

    [Fact]
    public async Task Los_manuales_se_guardan_siempre_como_filas_nuevas()
    {
        await _handler.HandleAsync(Fallida(OrigenNotificacion.Manual, null), default);
        await _handler.HandleAsync(Enviada(OrigenNotificacion.Manual, null), default);

        Assert.Equal(2, await _db.NotificacionesEnvio.CountAsync(n => n.IdEmpleado == 30));
    }

    [Fact]
    public async Task Avisa_al_lider_por_el_notificador_cuando_se_registra_un_aviso_manual()
    {
        var notifier = new NotificacionEstadoNotifier();
        using var suscripcion = notifier.Suscribir(idUsuario: 4);
        using var otroLider = notifier.Suscribir(idUsuario: 99);
        var handler = new RegistrarEnvioHandler(_db, NullLogger<RegistrarEnvioHandler>.Instance, notifier);

        await handler.HandleAsync(Enviada(OrigenNotificacion.Manual, null), default);
        await handler.HandleAsync(Enviada(OrigenNotificacion.Automatico, FechaCorte), default);

        Assert.True(suscripcion.Lector.TryRead(out var cambio));
        Assert.Equal(30, cambio!.IdEmpleado);
        Assert.Equal(EstadoEnvio.Enviado, cambio.Estado);
        Assert.False(suscripcion.Lector.TryRead(out _));
        Assert.False(otroLider.Lector.TryRead(out _));
    }

    [Fact]
    public async Task Omitido_automatico_se_guarda_con_su_corte_y_avisa_al_resumen()
    {
        var corteNotifier = new CorteNotifier();
        using var suscripcion = corteNotifier.Suscribir();
        var handler = new RegistrarEnvioHandler(_db, NullLogger<RegistrarEnvioHandler>.Instance, corteNotifier: corteNotifier);

        await handler.HandleAsync(new NotificacionOmitida(30, Inicio, Fin, null, DateTimeOffset.UtcNow,
            "No tiene correo registrado", OrigenNotificacion.Automatico, FechaCorte, 12m), default);

        var guardado = await _db.NotificacionesEnvio.SingleAsync(n => n.IdEmpleado == 30);
        Assert.Equal(OrigenNotificacion.Automatico, guardado.Origen);
        Assert.Equal(EstadoEnvio.Omitido, guardado.Estado);
        Assert.Equal(FechaCorte.ToUniversalTime(), guardado.FechaCorte);
        Assert.Equal(12m, guardado.HorasFaltantes);
        Assert.True(suscripcion.Lector.TryRead(out _));
    }

    private static NotificacionEnviada Enviada(OrigenNotificacion origen, DateTimeOffset? corte) =>
        new(30, 8m, Inicio, Fin, origen, corte, origen == OrigenNotificacion.Manual ? 4 : null, DateTimeOffset.UtcNow);

    private static NotificacionFallida Fallida(OrigenNotificacion origen, DateTimeOffset? corte) =>
        new(30, 8m, Inicio, Fin, origen, corte, origen == OrigenNotificacion.Manual ? 4 : null, DateTimeOffset.UtcNow, "Apprise caído");
}
