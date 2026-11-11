using System.Reflection;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using tmr_backend.Features.Permisos.Services;
using tmr_backend.Infrastructure.Database;
using tmr_backend.Infrastructure.Database.Entities;
using tmr_backend.Infrastructure.Messaging;

var version = DateTime.UtcNow;
var solicitud = new TblTimeReportSolicitud { Activo = true, FechaCreacion = version,
    Estado = new TblTimeReportEstadoSolicitud { Codigo = "PENDIENTE_JEFE" } };
var aviso = new CorreoPermisoEncolado(1, "TEST", "test@example.invalid", "Test",
    RolDestinatarioCorreoPermiso.JefeInmediato, EventoNotificacionPermiso.SolicitudCreada,
    VersionSolicitud: version);
Check(PermisoAvisoVigencia.EsVigente(aviso, solicitud), "aviso vigente");
solicitud.FechaCreacion = new DateTime(version.Ticks / 10 * 10, DateTimeKind.Utc);
Check(PermisoAvisoVigencia.EsVigente(aviso, solicitud), "precisión PostgreSQL de microsegundos");
solicitud.Estado.Codigo = "APROBADA";
Check(!PermisoAvisoVigencia.EsVigente(aviso, solicitud), "aviso atrasado omitido");
solicitud.Estado.Codigo = "PENDIENTE_JEFE";
solicitud.TokenExterno = "test-token"; solicitud.TokenExternoExpira = DateTime.UtcNow.AddMinutes(-1);
Check(!PermisoAvisoVigencia.EsVigente(aviso with { IncluirTokenExterno = true }, solicitud), "enlace vencido");
solicitud.TokenExternoExpira = DateTime.UtcNow.AddHours(1);
Check(PermisoAvisoVigencia.EsVigente(aviso with { IncluirTokenExterno = true }, solicitud), "enlace vigente");
solicitud.TokenExternoUsadoEn = DateTime.UtcNow;
Check(!PermisoAvisoVigencia.EsVigente(aviso with { IncluirTokenExterno = true }, solicitud), "enlace usado");
solicitud.Activo = false;
Check(!PermisoAvisoVigencia.EsVigente(aviso, solicitud), "solicitud inactiva");

var cs = Environment.GetEnvironmentVariable("TMR_TEST_POSTGRES");
if (string.IsNullOrEmpty(cs)) throw new Exception("TMR_TEST_POSTGRES debe apuntar a PostgreSQL aislado.");
var connection = new NpgsqlConnectionStringBuilder(cs);
if (connection.Database != "tmr_notifications_tests" || connection.Host != "127.0.0.1")
    throw new Exception("Solo se admite la base local aislada tmr_notifications_tests.");
var services = new ServiceCollection().AddLogging();
services.AddDbContext<ApplicationDbContext>(o => o.UseNpgsql(cs));
services.AddSingleton(Channel.CreateUnbounded<Guid>());
services.AddScoped<IEventBus, EventBus>();
services.AddScoped<IEventHandler<PruebaEvento>, PruebaHandler>();
await using var provider = services.BuildServiceProvider();
using (var scope = provider.CreateScope()) {
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.ExecuteSqlRawAsync("""
      CREATE SCHEMA IF NOT EXISTS notificacion;
      CREATE SCHEMA IF NOT EXISTS time_report;
      CREATE TABLE IF NOT EXISTS notificacion.evento_outbox (
        id uuid PRIMARY KEY, tipo_evento varchar(100) NOT NULL, payload jsonb NOT NULL,
        estado varchar(20) NOT NULL, intentos integer NOT NULL DEFAULT 0, ultimo_error text,
        fecha_creacion timestamptz NOT NULL, fecha_procesado timestamptz);
      ALTER TABLE notificacion.evento_outbox ALTER COLUMN intentos SET DEFAULT 0;
      CREATE TABLE IF NOT EXISTS time_report.tbl_time_report_solicitud (id int PRIMARY KEY, estado text NOT NULL);
      CREATE TABLE IF NOT EXISTS public.prueba_envios (id uuid PRIMARY KEY);
      TRUNCATE notificacion.evento_outbox, time_report.tbl_time_report_solicitud, public.prueba_envios;
      INSERT INTO time_report.tbl_time_report_solicitud VALUES (1, 'PENDIENTE_JEFE');
      """);
}
var filtro = new PermisoNotificacionTransactionFilter();
async Task<object?> Decision(bool fallar = false) {
    using var scope = provider.CreateScope();
    var http = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
    http.Request.Method = "POST"; http.Request.Path = "/api/permisos/1/decision-lider"; http.Request.RouteValues["id"] = "1";
    return await filtro.InvokeAsync(new DefaultEndpointFilterInvocationContext(http), async ctx => {
        var db = ctx.HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
        var updated = await db.Database.ExecuteSqlRawAsync("UPDATE time_report.tbl_time_report_solicitud SET estado = 'PENDIENTE_TH' WHERE id = 1 AND estado = 'PENDIENTE_JEFE'");
        if (updated == 0) return Results.Conflict();
        await scope.ServiceProvider.GetRequiredService<IEventBus>().PublishAsync(new PruebaEvento(Guid.NewGuid()));
        if (fallar) throw new InvalidOperationException("Fallo simulado antes del commit");
        return Results.Ok();
    });
}
try { await Decision(true); throw new Exception("No se simuló fallo"); } catch (InvalidOperationException) { }
using (var scope = provider.CreateScope()) {
    Check(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().EventosOutbox.CountAsync() == 0, "rollback elimina evento junto con decisión");
}
var results = await Task.WhenAll(Decision(), Decision());
Check(results.Count(r => r is IStatusCodeHttpResult x && x.StatusCode == 200) == 1, "dos decisiones concurrentes: un ganador");
Guid eventId;
using (var scope = provider.CreateScope()) {
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    eventId = await db.EventosOutbox.Select(e => e.Id).SingleAsync();
    Check(await db.EventosOutbox.CountAsync() == 1, "un único evento persistido");
}
var channel = provider.GetRequiredService<Channel<Guid>>();
Check(!channel.Reader.TryRead(out _), "evento transaccional no despierta worker antes del commit");
var dispatcher = new EventDispatcher(channel, provider.GetRequiredService<IServiceScopeFactory>(),
    new Dictionary<string, Type> { [nameof(PruebaEvento)] = typeof(PruebaEvento) },
    provider.GetRequiredService<ILogger<EventDispatcher>>());
var process = typeof(EventDispatcher).GetMethod("ProcesarAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
Task Process() => (Task)process.Invoke(dispatcher, [eventId, CancellationToken.None])!;
await Task.WhenAll(Process(), Process());
using (var scope = provider.CreateScope()) {
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    Check(await db.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM public.prueba_envios").SingleAsync() == 1, "dos workers/reintentos: un solo procesamiento");
    Check((await db.EventosOutbox.SingleAsync()).Estado == EstadoEvento.Procesado, "resultado persistido");
}
// Un fallo revierte los efectos parciales del handler y persiste el contador.
async Task<Guid> Encolar(PruebaEvento e) {
    using var scope = provider.CreateScope();
    await scope.ServiceProvider.GetRequiredService<IEventBus>().PublishAsync(e);
    return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().EventosOutbox
        .OrderByDescending(x => x.FechaCreacion).Select(x => x.Id).FirstAsync();
}
var fallido = await Encolar(new PruebaEvento(Guid.NewGuid(), Fallar: true));
for (var i = 0; i < EventDispatcher.MaxIntentos; i++)
    await (Task)process.Invoke(dispatcher, [fallido, CancellationToken.None])!;
using (var scope = provider.CreateScope()) {
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var row = await db.EventosOutbox.SingleAsync(x => x.Id == fallido);
    Check(row.Estado == EstadoEvento.Fallido && row.Intentos == 5, "fallo definitivo tras cinco intentos");
    Check(await db.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM public.prueba_envios").SingleAsync() == 1, "rollback de efectos parciales del handler");
}
var obsoleto = await Encolar(new PruebaEvento(Guid.NewGuid(), Obsoleto: true));
await (Task)process.Invoke(dispatcher, [obsoleto, CancellationToken.None])!;
using (var scope = provider.CreateScope()) {
    var row = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().EventosOutbox.SingleAsync(x => x.Id == obsoleto);
    Check(row.Estado == EstadoEvento.Procesado && row.UltimoError == "AVISO_NO_VIGENTE", "aviso obsoleto se omite sin envío");
}
// Recuperación periódica descubre eventos confirmados sin señal en memoria.
while (channel.Reader.TryRead(out _)) { }
Guid recuperado;
using (var scope = provider.CreateScope()) {
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await using var tx = await db.Database.BeginTransactionAsync();
    await scope.ServiceProvider.GetRequiredService<IEventBus>().PublishAsync(new PruebaEvento(Guid.NewGuid()));
    recuperado = await db.EventosOutbox.OrderByDescending(e => e.FechaCreacion).Select(e => e.Id).FirstAsync();
    await tx.CommitAsync();
}
using var recoveryDispatcher = new EventDispatcher(channel, provider.GetRequiredService<IServiceScopeFactory>(),
    new Dictionary<string, Type> { [nameof(PruebaEvento)] = typeof(PruebaEvento) },
    provider.GetRequiredService<ILogger<EventDispatcher>>(), TimeSpan.FromMilliseconds(100));
await recoveryDispatcher.StartAsync(CancellationToken.None);
var recuperadoOk = false;
for (var i = 0; i < 50; i++) {
    await Task.Delay(100);
    using var scope = provider.CreateScope();
    recuperadoOk = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().EventosOutbox
        .AnyAsync(x => x.Id == recuperado && x.Estado == EstadoEvento.Procesado);
    if (recuperadoOk) break;
}
await recoveryDispatcher.StopAsync(CancellationToken.None);
Check(recuperadoOk, "recuperación persistida sin señal en memoria");
Console.WriteLine("Todas las comprobaciones finalizaron correctamente.");
static void Check(bool ok, string label) { if (!ok) throw new Exception(label); Console.WriteLine("PASS " + label); }
public sealed record PruebaEvento(Guid Id, bool Fallar = false, bool Obsoleto = false) : IEvento;
public sealed class PruebaHandler(ApplicationDbContext db) : IEventHandler<PruebaEvento> {
    public async Task HandleAsync(PruebaEvento evento, CancellationToken ct) {
        if (evento.Obsoleto) throw new EventoObsoletoException();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO public.prueba_envios VALUES ({evento.Id})", ct);
        if (evento.Fallar) throw new InvalidOperationException("Error simulado del proveedor");
    }
}
