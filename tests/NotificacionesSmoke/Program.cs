using System.Reflection;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using tmr_backend.Features.Permisos.Endpoints;
using tmr_backend.Features.Permisos.Services;
using tmr_backend.Features.TimeReport.Services;
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

// Verificar vigencia cuando una solicitud nace directamente en APROBADA (informativa) o PENDIENTE_TH (sin líder)
var solInformativa = new TblTimeReportSolicitud
{
    Activo = true,
    FechaCreacion = version,
    Estado = new TblTimeReportEstadoSolicitud { Codigo = "APROBADA", CodigoCorto = "CON", Nombre = "Confirmado" },
    Empleado = new TblAdministracionEmpleado { Codigoempleado = "EMP-01", IdpersonaNavigation = new TblAdministracionPersona { Nombres = "Carlos", Apellidos = "Colaborador" } },
    TipoPermiso = new TblTimeReportTipoPermiso { Codigo = "CITAS_MEDICAS", CodigoCorto = "CIT", Nombre = "Citas médicas" },
    FechaSolicitud = new DateOnly(2026, 11, 11),
    FechaInicio = new DateOnly(2026, 11, 11),
    FechaFin = new DateOnly(2026, 11, 11),
    FechaRegreso = new DateOnly(2026, 11, 12),
    DiasSolicitados = 1m,
    HorasSolicitadas = 8m,
    Horario = "COMPLETA",
    DetalleHorario = "08:30 - 17:30",
    Motivo = "Control médico general",
    Codigo = "PER-2026-001"
};
var avisoInformativo = new CorreoPermisoEncolado(
    1, "PER-2026-001", "colab@tmr.com", "Carlos",
    RolDestinatarioCorreoPermiso.Colaborador, EventoNotificacionPermiso.SolicitudCreada,
    VersionSolicitud: version, EstadoSolicitud: "APROBADA");
Check(PermisoAvisoVigencia.EsVigente(avisoInformativo, solInformativa), "aviso informativo naciente en APROBADA es vigente");

// Una corrección vuelve al mismo estado, pero invalida avisos de la versión anterior.
var corregida = new TblTimeReportSolicitud { Activo = true, FechaCreacion = version,
    FechaModificacion = version.AddMinutes(1), Estado = new TblTimeReportEstadoSolicitud { Codigo = "PENDIENTE_JEFE" },
    TokenExterno = "nuevo-token", TokenExternoExpira = DateTime.UtcNow.AddHours(72) };
Check(!PermisoAvisoVigencia.EsVigente(aviso, corregida), "correo del ciclo anterior invalidado aunque el estado coincida");
var reenvio = aviso with { Evento = EventoNotificacionPermiso.SolicitudCorregidaReenviada,
    VersionSolicitud = corregida.FechaModificacion, EstadoSolicitud = "PENDIENTE_JEFE" };
Check(PermisoAvisoVigencia.EsVigente(reenvio, corregida), "correo de corrección vigente");
var snapshot = PermisosEndpoints.MapearSolicitudDto(solInformativa, null) with { TokenExterno = "token-anterior" };
Check(!PermisoAvisoVigencia.EsVigente(reenvio with { IncluirTokenExterno = true, Instantanea = snapshot }, corregida), "token anterior rechazado");
Check(PermisoAvisoVigencia.EsVigente(reenvio with { IncluirTokenExterno = true, Instantanea = snapshot with { TokenExterno = "nuevo-token" } }, corregida), "token nuevo aceptado");
corregida.Estado.Codigo = "PENDIENTE_TH";
Check(PermisoAvisoVigencia.EsVigente(reenvio with { EstadoSolicitud = "PENDIENTE_TH" }, corregida), "reenvío directo a TH vigente");

// Dos ciclos de observación no deben recuperar aprobaciones ni comentarios antiguos.
solInformativa.Estado.Codigo = "PENDIENTE_JEFE";
solInformativa.Revisiones.Add(new TblTimeReportSolicitudRevision { EtapaRevision = "JEFE_INMEDIATO", Decision = "APROBADO", FechaRevision = version });
solInformativa.Revisiones.Add(new TblTimeReportSolicitudRevision { EtapaRevision = "TALENTO_HUMANO", Decision = "OBSERVADO", Comentario = "Observación anterior", FechaRevision = version.AddSeconds(1) });
var pendienteCorregida = PermisosEndpoints.MapearSolicitudDto(solInformativa, null);
Check(!pendienteCorregida.AprobadoLider && !pendienteCorregida.ConfirmadoRrhh, "corrección reinicia aprobaciones");
solInformativa.Revisiones.Add(new TblTimeReportSolicitudRevision { EtapaRevision = "JEFE_INMEDIATO", Decision = "OBSERVADO", Comentario = "Observación actual", FechaRevision = version.AddSeconds(2) });
solInformativa.Estado.Codigo = "OBSERVADA";
Check(PermisosEndpoints.MapearSolicitudDto(solInformativa, null).Comentarios == "Observación actual", "segunda observación muestra comentario del ciclo actual");
solInformativa.Revisiones.Clear();
solInformativa.Estado.Codigo = "APROBADA";

// Verificar formato de Período y Duración sin paréntesis anidados
var dtoDiaCompleto = PermisosEndpoints.MapearSolicitudDto(solInformativa, null);
Check(dtoDiaCompleto.Fechas == "11/11/2026 · 08:30 - 17:30", $"formato fecha limpia: {dtoDiaCompleto.Fechas}");
Check(dtoDiaCompleto.Duracion == "1 día · 8 horas", $"formato duración día completo sin paréntesis: {dtoDiaCompleto.Duracion}");
Check(!dtoDiaCompleto.Fechas.Contains('(') && !dtoDiaCompleto.Duracion.Contains('('), "sin paréntesis en período ni duración");

solInformativa.DiasSolicitados = 0.25m;
solInformativa.HorasSolicitadas = 2m;
solInformativa.Horario = "OTROS";
solInformativa.DetalleHorario = "09:00 - 11:00";
var dtoParcial = PermisosEndpoints.MapearSolicitudDto(solInformativa, null);
Check(dtoParcial.Fechas == "11/11/2026 · 09:00 - 11:00", $"formato fecha parcial: {dtoParcial.Fechas}");
Check(dtoParcial.Duracion == "2 horas", $"formato duración parcial sin redundancia: {dtoParcial.Duracion}");

// Verificar renderizado de plantilla HTML para todos los eventos y roles sin placeholders pendientes
var rutaPlantilla = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Features", "Permisos", "Templates", "notificacion-permiso.html"));
var htmlPlantilla = await File.ReadAllTextAsync(rutaPlantilla);
foreach (var ev in Enum.GetValues<EventoNotificacionPermiso>())
{
    foreach (var rol in Enum.GetValues<RolDestinatarioCorreoPermiso>())
    {
        var (asuntoRender, htmlRender) = PermisosNotificacionService.ConstruirMensajeCorreo(
            htmlPlantilla,
            dtoDiaCompleto with { TokenExterno = "0123456789abcdef0123456789abcdef", LiderTipo = "EXT", ObservacionRrhh = "Revisado OK" },
            rol,
            "Usuario Prueba",
            ev,
            "http://localhost:4200",
            "http://localhost:4200/assets/img/logo-isc.png");
        Check(!string.IsNullOrWhiteSpace(asuntoRender), $"asunto generado para {ev}/{rol}");
        Check(!htmlRender.Contains("{{"), $"sin placeholders pendientes en {ev}/{rol}");
    }
}

// Verificar sincronización de cálculo de horas con RESTA_META y SUMA_REPORTADAS
var diaPrueba = new DateOnly(2026, 11, 11); // Miércoles laborable
var empPrueba = new TblAdministracionEmpleado { Id = 10, Activo = true, Fechaingreso = new DateOnly(2022, 1, 1) };
var calcSinPermiso = CalculoHorasPeriodo.Calcular(
    empPrueba, diaPrueba, diaPrueba, [], new HashSet<DateOnly>(), diaPrueba);
Check(calcSinPermiso.HorasEsperadas == 8m && calcSinPermiso.HorasPorRegistrar == 8m, "sin permiso exige 8h");

var calcRestaMeta = CalculoHorasPeriodo.Calcular(
    empPrueba, diaPrueba, diaPrueba, [], new HashSet<DateOnly>(), diaPrueba,
    solicitudes: [new SolicitudAprobadaPeriodo(10, diaPrueba, diaPrueba, "COMPLETA", 8m, "RESTA_META")]);
Check(calcRestaMeta.HorasEsperadas == 0m && calcRestaMeta.HorasPorRegistrar == 0m && calcRestaMeta.DiasACompletar == 0 && calcRestaMeta.Estado == "Completo", "RESTA_META reduce meta a 0h y cubre el día");

var calcSumaReportadas = CalculoHorasPeriodo.Calcular(
    empPrueba, diaPrueba, diaPrueba, [], new HashSet<DateOnly>(), diaPrueba,
    solicitudes: [new SolicitudAprobadaPeriodo(10, diaPrueba, diaPrueba, "MANANA", 4m, "SUMA_REPORTADAS")]);
Check(calcSumaReportadas.HorasEsperadas == 8m && calcSumaReportadas.HorasRegistradas == 4m && calcSumaReportadas.HorasPorRegistrar == 4m, "SUMA_REPORTADAS acredita 4h registradas");

var cs = Environment.GetEnvironmentVariable("TMR_TEST_POSTGRES");
if (string.IsNullOrEmpty(cs))
{
    Console.WriteLine("Comprobaciones unitarias de notificaciones, plantillas y Time Report finalizaron correctamente.");
    return;
}
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
