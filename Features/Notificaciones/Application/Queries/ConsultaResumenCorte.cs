using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using tmr_backend.Features.Notificaciones.Domain;
using tmr_backend.Features.Notificaciones.Domain.Events;
using tmr_backend.Features.Notificaciones.Application.Cortes;
using tmr_backend.Infrastructure.Database;
using tmr_backend.Infrastructure.Messaging;

namespace tmr_backend.Features.Notificaciones.Application.Queries;

public sealed record EmpleadoCorteDto(
    int IdEmpleado,
    string Nombre,
    string Estado,              // Enviado, Fallido, Omitido o Pendiente
    decimal HorasFaltantes,
    string? Detalle,
    DateTimeOffset? Fecha);

public sealed record ResumenCorteDto(
    DateTimeOffset FechaCorte,
    DateOnly InicioPeriodo,
    DateOnly FinPeriodo,
    bool EnProceso,
    int Enviados,
    int Fallidos,
    int Omitidos,
    int Pendientes,
    IReadOnlyList<EmpleadoCorteDto> Empleados,
    // Cuándo se disparó la ejecución más reciente del corte: si el mismo corte se vuelve a ejecutar
    // (reinicio, recuperación), el frontend vuelve a mostrar el popup
    DateTimeOffset? Ejecucion = null);

public interface IConsultaResumenCorte
{
    /// <summary>
    /// Resumen del corte automático más reciente (de los últimos días), o null si no hubo ninguno.
    /// Junta lo ya registrado en NotificacionEnvio con lo que sigue en cola en el Outbox,
    /// así se ve el avance mientras el corte se procesa.
    /// </summary>
    Task<ResumenCorteDto?> UltimoCorteAsync(CancellationToken ct);
}

public sealed class ConsultaResumenCorte : IConsultaResumenCorte
{
    // Un corte más viejo que esto ya no se muestra: el siguiente corte llega a los ~15 días
    private static readonly TimeSpan VentanaCortes = TimeSpan.FromDays(16);

    private const string Enviado = nameof(EstadoEnvio.Enviado);
    private const string Fallido = nameof(EstadoEnvio.Fallido);
    private const string Omitido = nameof(EstadoEnvio.Omitido);
    private const string Pendiente = "Pendiente";

    private static readonly string[] TiposEnCola =
    [
        nameof(CorteDeHorasAlcanzado),
        nameof(HorasFaltantesDetectadas),
        nameof(NotificacionEnviada),
        nameof(NotificacionFallida),
        nameof(NotificacionOmitida)
    ];

    private readonly ApplicationDbContext _db;
    private readonly ICalendarioCortesService _calendario;

    public ConsultaResumenCorte(ApplicationDbContext db, ICalendarioCortesService calendario)
    {
        _db = db;
        _calendario = calendario;
    }

    public async Task<ResumenCorteDto?> UltimoCorteAsync(CancellationToken ct)
    {
        var desde = DateTimeOffset.UtcNow - VentanaCortes;

        var enCola = (await _db.EventosOutbox.AsNoTracking()
                .Where(e => e.Estado == EstadoEvento.Pendiente && TiposEnCola.Contains(e.TipoEvento))
                .OrderBy(e => e.FechaCreacion)
                .Select(e => new { e.TipoEvento, e.Payload })
                .ToListAsync(ct))
            .Select(e => LeerEventoEnCola(e.TipoEvento, e.Payload))
            .Where(e => e is not null && e.FechaCorte >= desde)
            .Select(e => e!)
            .ToList();

        var ultimoRegistrado = await _db.NotificacionesEnvio.AsNoTracking()
            .Where(n => n.Origen == OrigenNotificacion.Automatico && n.FechaCorte != null && n.FechaCorte >= desde)
            .OrderByDescending(n => n.FechaCorte)
            .Select(n => n.FechaCorte)
            .FirstOrDefaultAsync(ct);

        DateTimeOffset? corte = ultimoRegistrado;
        foreach (var e in enCola)
        {
            if (corte is null || e.FechaCorte > corte)
                corte = e.FechaCorte;
        }

        if (corte is not { } fechaCorte)
            return null;

        var corteUtc = fechaCorte.ToUniversalTime();
        var registros = await _db.NotificacionesEnvio.AsNoTracking()
            .Where(n => n.Origen == OrigenNotificacion.Automatico && n.FechaCorte == corteUtc)
            .ToListAsync(ct);

        // 1. Lo ya registrado; 2. lo que sigue en cola encima (un reintento vuelve a "Pendiente";
        // un resultado en cola ya es definitivo aunque RegistrarEnvioHandler no lo haya guardado)
        var porEmpleado = registros.ToDictionary(
            r => r.IdEmpleado,
            r => new Fila(r.Estado.ToString(), r.HorasFaltantes, r.DetalleError, r.FechaEnvio, null));

        var delCorte = enCola.Where(e => e.FechaCorte == fechaCorte).ToList();
        foreach (var e in delCorte.Where(e => e.Estado == Pendiente && e.IdEmpleado is not null))
            porEmpleado[e.IdEmpleado!.Value] = new Fila(Pendiente, e.Horas, null, null, e.Nombre);
        foreach (var e in delCorte.Where(e => e.Estado != Pendiente && e.IdEmpleado is not null))
        {
            var nombre = porEmpleado.TryGetValue(e.IdEmpleado!.Value, out var previa) ? previa.Nombre : null;
            porEmpleado[e.IdEmpleado.Value] = new Fila(e.Estado!, e.Horas, e.Detalle, e.Fecha, nombre);
        }

        var nombres = await NombresAsync(porEmpleado.Keys.ToList(), ct);

        var empleados = porEmpleado
            .Select(kv => new EmpleadoCorteDto(
                kv.Key,
                nombres.TryGetValue(kv.Key, out var n) && n.Length > 0 ? n : kv.Value.Nombre ?? $"Empleado {kv.Key}",
                kv.Value.Estado,
                kv.Value.Horas,
                kv.Value.Detalle,
                kv.Value.Fecha))
            .OrderBy(e => OrdenEstado(e.Estado))
            .ThenBy(e => e.Nombre)
            .ToList();

        // El período se guarda con el corte (puede no ser 1–15 / 16–fin si alguien movió la fecha)
        var (inicio, fin) = await _calendario.PeriodoDelCorteAsync(fechaCorte, ct)
            ?? CalendarioCortes.PeriodoDelCorte(fechaCorte);
        var ejecucion = await UltimaEjecucionAsync(fechaCorte, desde, ct);

        return new ResumenCorteDto(
            fechaCorte,
            inicio,
            fin,
            EnProceso: delCorte.Count > 0,
            Enviados: empleados.Count(e => e.Estado == Enviado),
            Fallidos: empleados.Count(e => e.Estado == Fallido),
            Omitidos: empleados.Count(e => e.Estado == Omitido),
            Pendientes: empleados.Count(e => e.Estado == Pendiente),
            empleados,
            ejecucion);
    }

    // El CorteDeHorasAlcanzado más reciente de ese corte (procesado o no) en el Outbox
    private async Task<DateTimeOffset?> UltimaEjecucionAsync(DateTimeOffset fechaCorte, DateTimeOffset desde, CancellationToken ct)
    {
        var disparos = await _db.EventosOutbox.AsNoTracking()
            .Where(e => e.TipoEvento == nameof(CorteDeHorasAlcanzado) && e.FechaCreacion >= desde)
            .Select(e => new { e.FechaCreacion, e.Payload })
            .ToListAsync(ct);

        DateTimeOffset? ultima = null;
        foreach (var d in disparos)
        {
            if (LeerEventoEnCola(nameof(CorteDeHorasAlcanzado), d.Payload)?.FechaCorte == fechaCorte
                && (ultima is null || d.FechaCreacion > ultima))
                ultima = d.FechaCreacion;
        }
        return ultima;
    }

    private async Task<Dictionary<int, string>> NombresAsync(List<int> ids, CancellationToken ct)
    {
        if (ids.Count == 0)
            return new();

        var personas = await _db.TblAdministracionEmpleados.AsNoTracking()
            .Where(e => ids.Contains(e.Id))
            .Select(e => new { e.Id, e.IdpersonaNavigation!.Nombres, e.IdpersonaNavigation.Apellidos })
            .ToListAsync(ct);

        return personas.ToDictionary(p => p.Id, p => $"{p.Nombres} {p.Apellidos}".Trim());
    }

    // Primero lo que requiere atención
    private static int OrdenEstado(string estado) => estado switch
    {
        Fallido => 0,
        Omitido => 1,
        Pendiente => 2,
        _ => 3
    };

    private sealed record Fila(string Estado, decimal Horas, string? Detalle, DateTimeOffset? Fecha, string? Nombre);

    private sealed record EventoEnCola(
        DateTimeOffset FechaCorte,
        int? IdEmpleado,
        string? Estado,
        decimal Horas,
        string? Detalle,
        DateTimeOffset? Fecha,
        string? Nombre);

    // Solo interesan los eventos automáticos (los que tienen FechaCorte)
    private static EventoEnCola? LeerEventoEnCola(string tipo, string payload)
    {
        try
        {
            return tipo switch
            {
                nameof(CorteDeHorasAlcanzado) when JsonSerializer.Deserialize<CorteDeHorasAlcanzado>(payload) is { } c =>
                    new EventoEnCola(c.FechaCorte, null, null, 0m, null, null, null),
                nameof(HorasFaltantesDetectadas) when JsonSerializer.Deserialize<HorasFaltantesDetectadas>(payload) is
                    { Origen: OrigenNotificacion.Automatico, FechaCorte: { } corte } d =>
                    new EventoEnCola(corte, d.IdEmpleado, Pendiente, d.HorasFaltantes, HorasProyecto.Describir(d.Proyectos), null, d.Nombre),
                nameof(NotificacionEnviada) when JsonSerializer.Deserialize<NotificacionEnviada>(payload) is
                    { Origen: OrigenNotificacion.Automatico, FechaCorte: { } corte } e =>
                    new EventoEnCola(corte, e.IdEmpleado, Enviado, e.HorasFaltantes, null, e.FechaEnvio, null),
                nameof(NotificacionFallida) when JsonSerializer.Deserialize<NotificacionFallida>(payload) is
                    { Origen: OrigenNotificacion.Automatico, FechaCorte: { } corte } f =>
                    new EventoEnCola(corte, f.IdEmpleado, Fallido, f.HorasFaltantes, f.Error, f.FechaEnvio, null),
                nameof(NotificacionOmitida) when JsonSerializer.Deserialize<NotificacionOmitida>(payload) is
                    { Origen: OrigenNotificacion.Automatico, FechaCorte: { } corte } o =>
                    new EventoEnCola(corte, o.IdEmpleado, Omitido, o.HorasFaltantes, o.Motivo, o.FechaEnvio, null),
                _ => null
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
