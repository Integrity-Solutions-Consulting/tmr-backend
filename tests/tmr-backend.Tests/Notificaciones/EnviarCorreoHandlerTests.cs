using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using tmr_backend.Features.Notificaciones.Application.Handlers;
using tmr_backend.Features.Notificaciones.Domain;
using tmr_backend.Features.Notificaciones.Domain.Events;
using tmr_backend.Infrastructure.Database;
using tmr_backend.Infrastructure.Messaging;
using tmr_backend.Infrastructure.Shared;

namespace tmr_backend.Tests.Notificaciones;

public class EnviarCorreoHandlerTests
{
    private static readonly DateOnly Inicio = new(2026, 10, 1);
    private static readonly DateOnly Fin = new(2026, 10, 15);
    private static readonly DateTimeOffset FechaCorte = new(2026, 10, 15, 18, 0, 0, TimeSpan.Zero);

    private readonly ApplicationDbContext _db;
    private readonly EmailServiceFalso _emailService = new();
    private readonly EventBusFalso _bus = new();
    private readonly IConfiguration _config;

    public EnviarCorreoHandlerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new ApplicationDbContext(options);

        var configValues = new Dictionary<string, string?>
        {
            ["EmailSettings:FrontendUrl"] = "http://localhost:3000"
        };
        _config = new ConfigurationBuilder().AddInMemoryCollection(configValues).Build();
    }

    private EnviarCorreoHandler CrearHandler(int umbralHoras = 24) => new(
        _db,
        _emailService,
        _bus,
        Options.Create(new NotificacionHorasOptions { UmbralNotificacionManual = umbralHoras }),
        _config,
        NullLogger<EnviarCorreoHandler>.Instance);

    [Fact]
    public async Task Envia_correo_y_publica_NotificacionEnviada_para_evento_automatico()
    {
        var handler = CrearHandler();
        var evento = new HorasFaltantesDetectadas(
            IdEmpleado: 1,
            Nombre: "Ana Torres",
            Email: "ana@isc.com",
            HorasFaltantes: 16m,
            InicioPeriodo: Inicio,
            FinPeriodo: Fin,
            Origen: OrigenNotificacion.Automatico,
            FechaCorte: FechaCorte,
            IdUsuarioEjecutor: null);

        await handler.HandleAsync(evento, default);

        Assert.Single(_emailService.Enviados);
        Assert.Equal("ana@isc.com", _emailService.Enviados[0].ToEmail);
        Assert.Contains("16", _emailService.Enviados[0].Subject);

        var publicada = Assert.Single(_bus.Publicados.OfType<NotificacionEnviada>());
        Assert.Equal(1, publicada.IdEmpleado);
        Assert.Equal(16m, publicada.HorasFaltantes);
        Assert.Equal(OrigenNotificacion.Automatico, publicada.Origen);
        Assert.Equal(FechaCorte, publicada.FechaCorte);
    }

    [Fact]
    public async Task Envia_correo_y_publica_NotificacionEnviada_para_evento_manual()
    {
        var handler = CrearHandler();
        var evento = new HorasFaltantesDetectadas(
            IdEmpleado: 2,
            Nombre: "Carlos Ruiz",
            Email: "carlos@isc.com",
            HorasFaltantes: 8m,
            InicioPeriodo: Inicio,
            FinPeriodo: Fin,
            Origen: OrigenNotificacion.Manual,
            FechaCorte: null,
            IdUsuarioEjecutor: 5);

        await handler.HandleAsync(evento, default);

        Assert.Single(_emailService.Enviados);
        var publicada = Assert.Single(_bus.Publicados.OfType<NotificacionEnviada>());
        Assert.Equal(2, publicada.IdEmpleado);
        Assert.Equal(8m, publicada.HorasFaltantes);
        Assert.Equal(OrigenNotificacion.Manual, publicada.Origen);
        Assert.Equal(5, publicada.IdUsuarioEjecutor);
    }

    [Fact]
    public async Task Empleado_sin_email_publica_NotificacionFallida()
    {
        var handler = CrearHandler();
        var evento = new HorasFaltantesDetectadas(
            IdEmpleado: 3,
            Nombre: "Sin Correo",
            Email: "",
            HorasFaltantes: 8m,
            InicioPeriodo: Inicio,
            FinPeriodo: Fin,
            Origen: OrigenNotificacion.Automatico,
            FechaCorte: FechaCorte,
            IdUsuarioEjecutor: null);

        await handler.HandleAsync(evento, default);

        Assert.Empty(_emailService.Enviados);
        var fallida = Assert.Single(_bus.Publicados.OfType<NotificacionFallida>());
        Assert.Equal(3, fallida.IdEmpleado);
        Assert.Contains("sin correo", fallida.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Automatico_sin_FechaCorte_publica_NotificacionFallida()
    {
        var handler = CrearHandler();
        var evento = new HorasFaltantesDetectadas(
            IdEmpleado: 4,
            Nombre: "Pedro Diaz",
            Email: "pedro@isc.com",
            HorasFaltantes: 4m,
            InicioPeriodo: Inicio,
            FinPeriodo: Fin,
            Origen: OrigenNotificacion.Automatico,
            FechaCorte: null,
            IdUsuarioEjecutor: null);

        await handler.HandleAsync(evento, default);

        Assert.Empty(_emailService.Enviados);
        var fallida = Assert.Single(_bus.Publicados.OfType<NotificacionFallida>());
        Assert.Contains("sin FechaCorte", fallida.Error);
    }

    [Fact]
    public async Task Automatico_ya_notificado_en_corte_no_se_vuelve_a_enviar()
    {
        _db.NotificacionesEnvio.Add(new NotificacionEnvio
        {
            IdEmpleado = 1,
            InicioPeriodo = Inicio,
            FinPeriodo = Fin,
            FechaCorte = FechaCorte.ToUniversalTime(),
            Origen = OrigenNotificacion.Automatico,
            Estado = EstadoEnvio.Enviado,
            HorasFaltantes = 16m,
            FechaEnvio = DateTimeOffset.UtcNow
        });
        await _db.SaveChangesAsync();

        var handler = CrearHandler();
        var evento = new HorasFaltantesDetectadas(
            IdEmpleado: 1,
            Nombre: "Ana Torres",
            Email: "ana@isc.com",
            HorasFaltantes: 16m,
            InicioPeriodo: Inicio,
            FinPeriodo: Fin,
            Origen: OrigenNotificacion.Automatico,
            FechaCorte: FechaCorte,
            IdUsuarioEjecutor: null);

        await handler.HandleAsync(evento, default);

        Assert.Empty(_emailService.Enviados);
        Assert.Empty(_bus.Publicados);
    }

    [Fact]
    public async Task Manual_dentro_del_umbral_no_se_vuelve_a_enviar()
    {
        _db.NotificacionesEnvio.Add(new NotificacionEnvio
        {
            IdEmpleado = 2,
            InicioPeriodo = Inicio,
            FinPeriodo = Fin,
            FechaCorte = null,
            Origen = OrigenNotificacion.Manual,
            Estado = EstadoEnvio.Enviado,
            HorasFaltantes = 8m,
            FechaEnvio = DateTimeOffset.UtcNow.AddHours(-2) // hace 2 horas (umbral es 24h)
        });
        await _db.SaveChangesAsync();

        var handler = CrearHandler(umbralHoras: 24);
        var evento = new HorasFaltantesDetectadas(
            IdEmpleado: 2,
            Nombre: "Carlos Ruiz",
            Email: "carlos@isc.com",
            HorasFaltantes: 8m,
            InicioPeriodo: Inicio,
            FinPeriodo: Fin,
            Origen: OrigenNotificacion.Manual,
            FechaCorte: null,
            IdUsuarioEjecutor: 5);

        await handler.HandleAsync(evento, default);

        Assert.Empty(_emailService.Enviados);
        var omitida = Assert.Single(_bus.Publicados.OfType<NotificacionOmitida>());
        Assert.Equal(2, omitida.IdEmpleado);
        Assert.Equal(5, omitida.IdUsuarioEjecutor);
        Assert.Single(_bus.Publicados);
    }

    [Fact]
    public async Task Manual_fuera_del_umbral_si_se_envia()
    {
        _db.NotificacionesEnvio.Add(new NotificacionEnvio
        {
            IdEmpleado = 2,
            InicioPeriodo = Inicio,
            FinPeriodo = Fin,
            FechaCorte = null,
            Origen = OrigenNotificacion.Manual,
            Estado = EstadoEnvio.Enviado,
            HorasFaltantes = 8m,
            FechaEnvio = DateTimeOffset.UtcNow.AddHours(-25) // hace 25 horas (> 24h)
        });
        await _db.SaveChangesAsync();

        var handler = CrearHandler(umbralHoras: 24);
        var evento = new HorasFaltantesDetectadas(
            IdEmpleado: 2,
            Nombre: "Carlos Ruiz",
            Email: "carlos@isc.com",
            HorasFaltantes: 8m,
            InicioPeriodo: Inicio,
            FinPeriodo: Fin,
            Origen: OrigenNotificacion.Manual,
            FechaCorte: null,
            IdUsuarioEjecutor: 5);

        await handler.HandleAsync(evento, default);

        Assert.Single(_emailService.Enviados);
        Assert.Single(_bus.Publicados.OfType<NotificacionEnviada>());
    }

    [Fact]
    public async Task Fallo_en_EmailService_publica_NotificacionFallida()
    {
        _emailService.LanzarExcepcion = true;

        var handler = CrearHandler();
        var evento = new HorasFaltantesDetectadas(
            IdEmpleado: 1,
            Nombre: "Ana Torres",
            Email: "ana@isc.com",
            HorasFaltantes: 16m,
            InicioPeriodo: Inicio,
            FinPeriodo: Fin,
            Origen: OrigenNotificacion.Automatico,
            FechaCorte: FechaCorte,
            IdUsuarioEjecutor: null);

        await handler.HandleAsync(evento, default);

        var fallida = Assert.Single(_bus.Publicados.OfType<NotificacionFallida>());
        Assert.Equal(1, fallida.IdEmpleado);
        Assert.Equal("Fallo simulado SMTP", fallida.Error);
    }

    [Fact]
    public async Task Automatico_no_reenvia_si_el_envio_del_corte_sigue_sin_registrar_en_el_outbox()
    {
        await AgregarEnviadaPendienteAsync(new NotificacionEnviada(
            1, 16m, Inicio, Fin, OrigenNotificacion.Automatico, FechaCorte, null, DateTimeOffset.UtcNow));

        await CrearHandler().HandleAsync(Detectada(OrigenNotificacion.Automatico), default);

        Assert.Empty(_emailService.Enviados);
        Assert.Empty(_bus.Publicados);
    }

    [Fact]
    public async Task Manual_no_reenvia_si_el_envio_anterior_sigue_sin_registrar_en_el_outbox()
    {
        await AgregarEnviadaPendienteAsync(new NotificacionEnviada(
            1, 16m, Inicio, Fin, OrigenNotificacion.Manual, null, 5, DateTimeOffset.UtcNow.AddMinutes(-1)));

        await CrearHandler(umbralHoras: 24).HandleAsync(Detectada(OrigenNotificacion.Manual), default);

        Assert.Empty(_emailService.Enviados);
        Assert.Single(_bus.Publicados.OfType<NotificacionOmitida>());
    }

    [Fact]
    public async Task Envio_sin_registrar_de_otro_empleado_no_bloquea()
    {
        await AgregarEnviadaPendienteAsync(new NotificacionEnviada(
            2, 16m, Inicio, Fin, OrigenNotificacion.Automatico, FechaCorte, null, DateTimeOffset.UtcNow));

        await CrearHandler().HandleAsync(Detectada(OrigenNotificacion.Automatico), default);

        Assert.Single(_emailService.Enviados);
    }

    [Fact]
    public async Task Corte_detalla_las_horas_de_cada_proyecto_en_el_correo_y_en_el_registro()
    {
        var evento = Detectada(OrigenNotificacion.Automatico) with
        {
            HorasFaltantes = 24m,
            Proyectos = [new HorasProyecto(10, "Portal Web", 16m), new HorasProyecto(11, "ERP", 8m)]
        };

        await CrearHandler().HandleAsync(evento, default);

        var correo = Assert.Single(_emailService.Enviados);
        Assert.Contains("Portal Web", correo.HtmlBody);
        Assert.Contains("ERP", correo.HtmlBody);
        var enviada = Assert.Single(_bus.Publicados.OfType<NotificacionEnviada>());
        Assert.Equal("Portal Web: 16 h · ERP: 8 h", enviada.Detalle);
    }

    private static HorasFaltantesDetectadas Detectada(OrigenNotificacion origen) => new(
        IdEmpleado: 1,
        Nombre: "Ana Torres",
        Email: "ana@isc.com",
        HorasFaltantes: 16m,
        InicioPeriodo: Inicio,
        FinPeriodo: Fin,
        Origen: origen,
        FechaCorte: origen == OrigenNotificacion.Automatico ? FechaCorte : null,
        IdUsuarioEjecutor: origen == OrigenNotificacion.Manual ? 5 : null);

    private async Task AgregarEnviadaPendienteAsync(NotificacionEnviada enviada)
    {
        _db.EventosOutbox.Add(new EventoOutbox
        {
            Id = Guid.NewGuid(),
            TipoEvento = nameof(NotificacionEnviada),
            Payload = System.Text.Json.JsonSerializer.Serialize(enviada),
            Estado = EstadoEvento.Pendiente,
            FechaCreacion = DateTimeOffset.UtcNow
        });
        await _db.SaveChangesAsync();
    }

    private sealed class EmailServiceFalso : IEmailService
    {
        public List<(string ToEmail, string Subject, string HtmlBody)> Enviados { get; } = [];
        public bool LanzarExcepcion { get; set; }

        public Task SendEmailAsync(string toEmail, string subject, string htmlBody)
        {
            if (LanzarExcepcion)
                throw new InvalidOperationException("Fallo simulado SMTP");

            Enviados.Add((toEmail, subject, htmlBody));
            return Task.CompletedTask;
        }
    }

    private sealed class EventBusFalso : IEventBus
    {
        public List<IEvento> Publicados { get; } = [];

        public Task PublishAsync<T>(T evento, CancellationToken ct = default) where T : IEvento
        {
            Publicados.Add(evento);
            return Task.CompletedTask;
        }
    }
}
