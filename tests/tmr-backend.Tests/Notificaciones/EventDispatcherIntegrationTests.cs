using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using tmr_backend.Features.Notificaciones;
using tmr_backend.Features.Notificaciones.Application.Handlers;
using tmr_backend.Features.Notificaciones.Application.Queries;
using tmr_backend.Features.Notificaciones.Domain;
using tmr_backend.Features.Notificaciones.Domain.Events;
using tmr_backend.Infrastructure.Database;
using tmr_backend.Infrastructure.Extensions;
using tmr_backend.Infrastructure.Messaging;
using tmr_backend.Infrastructure.Shared;

namespace tmr_backend.Tests.Notificaciones;

public class EventDispatcherIntegrationTests
{
    private static readonly DateOnly Inicio = new(2026, 10, 1);
    private static readonly DateOnly Fin = new(2026, 10, 15);
    private static readonly DateTimeOffset FechaCorte = new(2026, 10, 15, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task FlujoCompleto_Desde_HorasFaltantesDetectadas_Hasta_NotificacionEnvio_Guardada()
    {
        var services = new ServiceCollection();

        // Base de datos InMemory
        var dbName = Guid.NewGuid().ToString();
        services.AddDbContext<ApplicationDbContext>(opt => opt.UseInMemoryDatabase(dbName));

        // Configuración
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["NotificacionHoras:Habilitado"] = "true",
            ["NotificacionHoras:UmbralNotificacionManual"] = "24",
            ["EmailSettings:FrontendUrl"] = "http://localhost:3000"
        }).Build();
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging();

        // Módulo de mensajería y notificaciones
        services.AddMessaging();
        services.AddNotificacionesModule(config);

        // Email service simulado
        var emailFalso = new EmailServiceSimulado();
        services.AddScoped<IEmailService>(_ => emailFalso);

        var sp = services.BuildServiceProvider();

        // 1. Obtener EventBus y publicar HorasFaltantesDetectadas
        using (var scope = sp.CreateScope())
        {
            var bus = scope.ServiceProvider.GetRequiredService<IEventBus>();
            await bus.PublishAsync(new HorasFaltantesDetectadas(
                IdEmpleado: 42,
                Nombre: "Lucia Morales",
                Email: "lucia@isc.com",
                HorasFaltantes: 10m,
                InicioPeriodo: Inicio,
                FinPeriodo: Fin,
                Origen: OrigenNotificacion.Automatico,
                FechaCorte: FechaCorte,
                IdUsuarioEjecutor: null));
        }

        // 2. Verificar que el evento está en EventoOutbox como Pendiente
        using (var scope = sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var outboxEvento = await db.EventosOutbox.FirstOrDefaultAsync(e => e.TipoEvento == nameof(HorasFaltantesDetectadas));
            Assert.NotNull(outboxEvento);
            Assert.Equal(EstadoEvento.Pendiente, outboxEvento.Estado);
        }

        // 3. Ejecutar el EventDispatcher para procesar los eventos en el canal
        var canal = sp.GetRequiredService<Channel<Guid>>();
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
        var tiposDeEvento = sp.GetRequiredService<IReadOnlyDictionary<string, Type>>();
        var dispatcher = new EventDispatcher(canal, scopeFactory, tiposDeEvento, NullLogger<EventDispatcher>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var executeTask = dispatcher.StartAsync(cts.Token);

        // Esperar a que se procese el evento y el subsiguiente (NotificacionEnviada)
        await Task.Delay(500);
        await dispatcher.StopAsync(CancellationToken.None);

        // 4. Verificar que se envió el correo
        Assert.Single(emailFalso.Enviados);
        Assert.Equal("lucia@isc.com", emailFalso.Enviados[0].ToEmail);

        // 5. Verificar que se registró en NotificacionEnvio
        using (var scope = sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var envio = await db.NotificacionesEnvio.FirstOrDefaultAsync(n => n.IdEmpleado == 42);
            Assert.NotNull(envio);
            Assert.Equal(42, envio.IdEmpleado);
            Assert.Equal(10m, envio.HorasFaltantes);
            Assert.Equal(EstadoEnvio.Enviado, envio.Estado);
            Assert.Equal(OrigenNotificacion.Automatico, envio.Origen);
            Assert.Equal(FechaCorte.ToUniversalTime(), envio.FechaCorte);
        }
    }

    [Fact]
    public async Task Flujo2_Completo_NotificacionManual_EjecutaCadenaCompleta_DeEventos()
    {
        var services = new ServiceCollection();
        var dbName = Guid.NewGuid().ToString();
        services.AddDbContext<ApplicationDbContext>(opt => opt.UseInMemoryDatabase(dbName));

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["NotificacionHoras:Habilitado"] = "true",
            ["NotificacionHoras:UmbralNotificacionManual"] = "24",
            ["EmailSettings:FrontendUrl"] = "http://localhost:3000"
        }).Build();
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging();

        services.AddMessaging();
        services.AddNotificacionesModule(config);

        var emailFalso = new EmailServiceSimulado();
        services.AddScoped<IEmailService>(_ => emailFalso);

        var consultaMock = new ConsultaHorasFaltantesMock();
        consultaMock.EmpleadosConDeficit.Add(new EmpleadoConHorasFaltantes(
            IdEmpleado: 99,
            Nombre: "Esteban Paz",
            Email: "esteban@isc.com",
            Calculo: new ResultadoHorasFaltantes(99, Inicio, Fin, 10, 80m, 64m, 16m)
        ));
        services.AddScoped<IConsultaHorasFaltantes>(_ => consultaMock);

        var sp = services.BuildServiceProvider();

        // 1. Líder solicita notificación manual para empleado 99
        using (var scope = sp.CreateScope())
        {
            var bus = scope.ServiceProvider.GetRequiredService<IEventBus>();
            await bus.PublishAsync(new NotificacionManualSolicitada(
                IdsEmpleados: [99],
                InicioPeriodo: Inicio,
                FinPeriodo: Fin,
                IdUsuarioEjecutor: 7));
        }

        // 2. Iniciar EventDispatcher para que procese toda la cadena:
        // NotificacionManualSolicitada -> DetectarHorasFaltantesHandler
        // -> HorasFaltantesDetectadas -> EnviarCorreoHandler
        // -> NotificacionEnviada -> RegistrarEnvioHandler
        var canal = sp.GetRequiredService<Channel<Guid>>();
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
        var tiposDeEvento = sp.GetRequiredService<IReadOnlyDictionary<string, Type>>();
        var dispatcher = new EventDispatcher(canal, scopeFactory, tiposDeEvento, NullLogger<EventDispatcher>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        _ = dispatcher.StartAsync(cts.Token);

        // Esperar a que los 3 eventos en cadena se procesen
        await Task.Delay(800);
        await dispatcher.StopAsync(CancellationToken.None);

        // 3. Verificar correo enviado
        var emailEnviado = Assert.Single(emailFalso.Enviados);
        Assert.Equal("esteban@isc.com", emailEnviado.ToEmail);
        Assert.Contains("16", emailEnviado.Subject);

        // 4. Verificar registro guardado en NotificacionEnvio
        using (var scope = sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var envio = await db.NotificacionesEnvio.FirstOrDefaultAsync(n => n.IdEmpleado == 99);
            Assert.NotNull(envio);
            Assert.Equal(99, envio.IdEmpleado);
            Assert.Equal(16m, envio.HorasFaltantes);
            Assert.Equal(OrigenNotificacion.Manual, envio.Origen);
            Assert.Equal(EstadoEnvio.Enviado, envio.Estado);
            Assert.Equal(7, envio.IdUsuarioEjecutor);
            Assert.Null(envio.FechaCorte);
        }
    }

    [Fact]
    public async Task Doble_solicitud_manual_seguida_envia_un_solo_correo()
    {
        var (sp, emailFalso) = CrearServicios(consulta =>
            consulta.EmpleadosConDeficit.Add(new EmpleadoConHorasFaltantes(
                99, "Esteban Paz", "esteban@isc.com", new ResultadoHorasFaltantes(99, Inicio, Fin, 10, 80m, 64m, 16m))));

        // Doble clic: las dos solicitudes quedan en cola antes de que se registre la primera
        using (var scope = sp.CreateScope())
        {
            var bus = scope.ServiceProvider.GetRequiredService<IEventBus>();
            await bus.PublishAsync(new NotificacionManualSolicitada([99], Inicio, Fin, 7));
            await bus.PublishAsync(new NotificacionManualSolicitada([99], Inicio, Fin, 7));
        }

        await ProcesarColaAsync(sp, 1000);

        Assert.Single(emailFalso.Enviados);
        using var verificacion = sp.CreateScope();
        var db = verificacion.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(1, await db.NotificacionesEnvio.CountAsync(n => n.Estado == EstadoEnvio.Enviado));
        Assert.Equal(1, await db.NotificacionesEnvio.CountAsync(n => n.Estado == EstadoEnvio.Omitido));
    }

    [Fact]
    public async Task Avisos_manuales_de_dos_proyectos_distintos_envian_dos_correos_y_guardan_el_proyecto()
    {
        var (sp, emailFalso) = CrearServicios(consulta =>
            consulta.EmpleadosConDeficit.Add(new EmpleadoConHorasFaltantes(
                99, "Esteban Paz", "esteban@isc.com", new ResultadoHorasFaltantes(99, Inicio, Fin, 10, 80m, 64m, 16m))));

        using (var scope = sp.CreateScope())
        {
            var bus = scope.ServiceProvider.GetRequiredService<IEventBus>();
            await bus.PublishAsync(new NotificacionManualSolicitada([99], Inicio, Fin, 7, IdProyecto: 40));
            await bus.PublishAsync(new NotificacionManualSolicitada([99], Inicio, Fin, 7, IdProyecto: 41));
        }

        await ProcesarColaAsync(sp, 1000);

        Assert.Equal(2, emailFalso.Enviados.Count);
        Assert.Contains(emailFalso.Enviados, e => e.Subject.Contains("Proyecto 40"));
        using var verificacion = sp.CreateScope();
        var db = verificacion.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var proyectos = await db.NotificacionesEnvio
            .Where(n => n.Estado == EstadoEnvio.Enviado)
            .Select(n => n.IdProyecto)
            .ToListAsync();
        Assert.Equal(new int?[] { 40, 41 }, proyectos.OrderBy(p => p));
    }

    [Fact]
    public async Task Aviso_que_agota_sus_intentos_queda_Fallido_en_el_outbox_y_registrado_como_Fallido()
    {
        // El envío falla siempre (p. ej. un error inesperado del handler, no del SMTP)
        var (sp, _) = CrearServicios(_ => { }, services =>
        {
            services.RemoveAll<IEventHandler<HorasFaltantesDetectadas>>();
            services.AddScoped<IEventHandler<HorasFaltantesDetectadas>, HandlerQueSiempreFalla>();
        });

        using (var scope = sp.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IEventBus>().PublishAsync(new HorasFaltantesDetectadas(
                42, "Lucia Morales", "lucia@isc.com", 10m, Inicio, Fin, OrigenNotificacion.Automatico, FechaCorte, null));
        }

        var dispatcher = new EventDispatcher(
            sp.GetRequiredService<Channel<Guid>>(),
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetRequiredService<IReadOnlyDictionary<string, Type>>(),
            NullLogger<EventDispatcher>.Instance,
            esperaEntreReintentos: TimeSpan.FromMilliseconds(20));
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        _ = dispatcher.StartAsync(cts.Token);
        await Task.Delay(1500);
        await dispatcher.StopAsync(CancellationToken.None);

        using var verificacion = sp.CreateScope();
        var db = verificacion.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var fila = await db.EventosOutbox.SingleAsync(e => e.TipoEvento == nameof(HorasFaltantesDetectadas));
        Assert.Equal(EstadoEvento.Fallido, fila.Estado);
        var envio = await db.NotificacionesEnvio.SingleAsync(n => n.IdEmpleado == 42);
        Assert.Equal(EstadoEnvio.Fallido, envio.Estado);
        Assert.Contains("intentos", envio.DetalleError);
    }

    private sealed class HandlerQueSiempreFalla : IEventHandler<HorasFaltantesDetectadas>
    {
        public Task HandleAsync(HorasFaltantesDetectadas evento, CancellationToken ct) =>
            throw new InvalidOperationException("Error inesperado");
    }

    [Fact]
    public async Task Handler_que_falla_al_guardar_incrementa_Intentos_en_vez_de_quedar_en_bucle()
    {
        Guid idEvento;
        var (sp, _) = CrearServicios(_ => { }, services =>
            services.AddScoped<IEventHandler<NotificacionOmitida>, HandlerQueDejaBasuraYFalla>());

        using (var scope = sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.NotificacionesFechaCorte.Add(new NotificacionFechaCorte { Id = 1, Tipo = "Quincena" });
            await db.SaveChangesAsync();

            var bus = scope.ServiceProvider.GetRequiredService<IEventBus>();
            await bus.PublishAsync(new NotificacionOmitida(1, Inicio, Fin, 7, DateTimeOffset.UtcNow, "prueba"));
            idEvento = (await db.EventosOutbox.SingleAsync()).Id;
        }

        await ProcesarColaAsync(sp, 500);

        using var verificacion = sp.CreateScope();
        var fila = await verificacion.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .EventosOutbox.SingleAsync(e => e.Id == idEvento);
        // Antes quedaba en 0 para siempre (el SaveChanges del catch volvía a fallar). Puede ser 2 porque al
        // arrancar el dispatcher reencola los pendientes y el evento ya estaba en el canal.
        Assert.InRange(fila.Intentos, 1, 2);
        Assert.Equal(EstadoEvento.Pendiente, fila.Estado);
        Assert.Equal("Fallo simulado del handler", fila.UltimoError);
    }

    private static (ServiceProvider Sp, EmailServiceSimulado Email) CrearServicios(
        Action<ConsultaHorasFaltantesMock> configurarConsulta,
        Action<IServiceCollection>? extra = null)
    {
        var services = new ServiceCollection();
        var dbName = Guid.NewGuid().ToString();
        services.AddDbContext<ApplicationDbContext>(opt => opt.UseInMemoryDatabase(dbName));

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["NotificacionHoras:Habilitado"] = "true",
            ["NotificacionHoras:UmbralNotificacionManual"] = "24",
            ["EmailSettings:FrontendUrl"] = "http://localhost:3000"
        }).Build();
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging();
        services.AddMessaging();
        services.AddNotificacionesModule(config);

        var emailFalso = new EmailServiceSimulado();
        services.AddScoped<IEmailService>(_ => emailFalso);

        var consulta = new ConsultaHorasFaltantesMock();
        configurarConsulta(consulta);
        services.AddScoped<IConsultaHorasFaltantes>(_ => consulta);
        extra?.Invoke(services);

        return (services.BuildServiceProvider(), emailFalso);
    }

    private static async Task ProcesarColaAsync(ServiceProvider sp, int esperaMs)
    {
        var dispatcher = new EventDispatcher(
            sp.GetRequiredService<Channel<Guid>>(),
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetRequiredService<IReadOnlyDictionary<string, Type>>(),
            NullLogger<EventDispatcher>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        _ = dispatcher.StartAsync(cts.Token);
        await Task.Delay(esperaMs);
        await dispatcher.StopAsync(CancellationToken.None);
    }

    // Deja en el DbContext una fila que no se puede guardar (Id que ya existe en la BD) y luego falla,
    // como cuando RegistrarEnvioHandler choca con un índice único.
    private sealed class HandlerQueDejaBasuraYFalla(ApplicationDbContext db) : IEventHandler<NotificacionOmitida>
    {
        public Task HandleAsync(NotificacionOmitida evento, CancellationToken ct)
        {
            db.NotificacionesFechaCorte.Add(new NotificacionFechaCorte { Id = 1, Tipo = "Duplicado" });
            throw new InvalidOperationException("Fallo simulado del handler");
        }
    }

    private sealed class ConsultaHorasFaltantesMock : IConsultaHorasFaltantes
    {
        public List<EmpleadoConHorasFaltantes> EmpleadosConDeficit { get; } = [];

        public Task<IReadOnlyList<EmpleadoConHorasFaltantes>> CalcularAsync(
            DateOnly inicioPeriodo, DateOnly finPeriodo, IReadOnlyCollection<int>? idsEmpleados, CancellationToken ct,
            int? idProyecto = null, bool incluirInactivos = false)
        {
            IReadOnlyList<EmpleadoConHorasFaltantes> list = EmpleadosConDeficit;
            return Task.FromResult(list);
        }

        public Task<IReadOnlyList<EmpleadoConHorasFaltantes>> CalcularPorProyectoAsync(
            DateOnly inicioPeriodo, DateOnly finPeriodo, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<EmpleadoConHorasFaltantes>>(EmpleadosConDeficit);

        public Task<PendientesNotificacionResponse> ObtenerPendientesAsync(CancellationToken ct) =>
            Task.FromResult(new PendientesNotificacionResponse(Inicio, Fin, []));

        public Task<IReadOnlyDictionary<int, DateTimeOffset>> UltimosAvisosManualesAsync(
            IReadOnlyCollection<int> idsEmpleados, CancellationToken ct, int? idProyecto = null) =>
            Task.FromResult<IReadOnlyDictionary<int, DateTimeOffset>>(new Dictionary<int, DateTimeOffset>());

        public Task<string?> NombreProyectoAsync(int idProyecto, CancellationToken ct) =>
            Task.FromResult<string?>($"Proyecto {idProyecto}");

        public Task<IReadOnlySet<int>> NotificadosEnCorteAsync(DateTimeOffset fechaCorte, CancellationToken ct) =>
            Task.FromResult<IReadOnlySet<int>>(new HashSet<int>());
    }

    private sealed class EmailServiceSimulado : IEmailService
    {
        public List<(string ToEmail, string Subject, string Body)> Enviados { get; } = [];

        public Task SendEmailAsync(string toEmail, string subject, string htmlBody)
        {
            Enviados.Add((toEmail, subject, htmlBody));
            return Task.CompletedTask;
        }
    }
}
