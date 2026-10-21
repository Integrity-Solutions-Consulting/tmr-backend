using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using tmr_backend.Features.Notificaciones.Domain;
using tmr_backend.Infrastructure.Database;
using static tmr_backend.Features.Notificaciones.Domain.CalendarioCortes;

namespace tmr_backend.Features.Notificaciones.Application.Cortes;

public static class EstadoCorte
{
    public const string Programado = "Programado";
    public const string EnProceso = "EnProceso";
    public const string Ejecutado = "Ejecutado";
    public const string NoEjecutado = "NoEjecutado";
}

public sealed record CorteProgramadoDto(
    int Id,
    int Anio,
    int Mes,
    string Tipo,
    DateOnly InicioPeriodo,
    DateOnly FinPeriodo,
    DateTimeOffset FechaCorte,          // hora de Ecuador (−05:00)
    DateTimeOffset FechaPorDefecto,
    bool Modificada,
    string Estado,                      // EstadoCorte
    bool Editable,                      // false solo mientras se ejecuta o si no queda ningún día disponible
    bool PuedeRestablecer,              // la fecha por defecto es futura y está dentro del rango permitido
    DateOnly FechaMinima,               // rango de días que acepta el selector de la pantalla
    DateOnly FechaMaxima,
    DateTimeOffset? FechaEjecucion,
    DateTimeOffset? FechaModificacion,
    string? ModificadoPor);

public sealed record CortesProgramadosResponse(
    bool Habilitado,                    // NotificacionHoras:Habilitado; si es false no se ejecuta ningún corte
    IReadOnlyList<CorteProgramadoDto> Cortes);

public sealed record ResultadoCambioCorte(CortesProgramadosResponse? Cortes, string? Error, bool NoEncontrado = false)
{
    public static ResultadoCambioCorte Falla(string error) => new(null, error);
    public static readonly ResultadoCambioCorte SinCorte = new(null, "No existe el corte indicado.", true);
}

public interface ICalendarioCortesService
{
    /// <summary>Crea con la fecha por defecto los cortes que falten (mes anterior, actual y los dos siguientes).</summary>
    Task AsegurarCortesAsync(CancellationToken ct);

    Task<CortesProgramadosResponse> ListarAsync(CancellationToken ct);

    Task<ResultadoCambioCorte> CambiarFechaAsync(int id, DateTimeOffset nuevaFecha, int idUsuario, CancellationToken ct);

    Task<ResultadoCambioCorte> RestablecerAsync(int id, int idUsuario, CancellationToken ct);

    /// <summary>Cortes sin ejecutar cuya fecha ya llegó (dentro de VentanaEjecucion), del más viejo al más nuevo.</summary>
    Task<IReadOnlyList<NotificacionFechaCorte>> PendientesAsync(DateTimeOffset ahora, CancellationToken ct);

    /// <summary>Marca el corte como ejecutado solo si nadie lo hizo antes. false = otra instancia ya lo tomó.</summary>
    Task<bool> TomarParaEjecutarAsync(int id, DateTimeOffset ahora, CancellationToken ct);

    /// <summary>Deshace TomarParaEjecutarAsync si no se pudo publicar el corte.</summary>
    Task LiberarAsync(int id, CancellationToken ct);

    /// <summary>Período guardado del corte con esa fecha, o null si no está en la tabla.</summary>
    Task<(DateOnly Inicio, DateOnly Fin)?> PeriodoDelCorteAsync(DateTimeOffset fechaCorte, CancellationToken ct);
}

public sealed class CalendarioCortesService : ICalendarioCortesService
{
    // Mes anterior (historial) + actual + dos siguientes
    private const int MesesAtras = 1;
    private const int MesesTotales = 4;
    private const int IdUsuarioSistema = 0;

    private readonly ApplicationDbContext _db;
    private readonly NotificacionHorasOptions _opciones;
    private readonly TimeProvider _reloj;
    private readonly ILogger<CalendarioCortesService> _logger;

    // logger al final y opcional: las pruebas que crean el servicio con (db, opciones, reloj) siguen compilando
    public CalendarioCortesService(
        ApplicationDbContext db,
        IOptions<NotificacionHorasOptions> opciones,
        TimeProvider? reloj = null,
        ILogger<CalendarioCortesService>? logger = null)
    {
        _db = db;
        _opciones = opciones.Value;
        _reloj = reloj ?? TimeProvider.System;
        _logger = logger ?? NullLogger<CalendarioCortesService>.Instance;
    }

    public async Task AsegurarCortesAsync(CancellationToken ct)
    {
        var claves = ClavesVisibles(_reloj.GetUtcNow());

        // También el corte anterior al primero: de él sale el inicio del período del primero
        var existentes = (await CargarAsync(claves[0].Anterior(), claves[^1], ct))
            .ToDictionary(ClaveCorte.De);

        if (claves.All(existentes.ContainsKey))
            return;

        existentes.TryGetValue(claves[0].Anterior(), out var anterior);
        DateOnly? finAnterior = anterior?.FinPeriodo;

        foreach (var clave in claves)
        {
            if (existentes.TryGetValue(clave, out var fila))
            {
                finAnterior = fila.FinPeriodo;
                continue;
            }

            var fecha = clave.FechaPorDefecto();
            var inicio = finAnterior?.AddDays(1) ?? clave.PeriodoNatural().Inicio;
            var fin = FinEfectivo(clave, fecha);
            var fechaUtc = fecha.ToUniversalTime();

            // ON CONFLICT: si otra instancia del backend la creó primero, se deja la suya
            int insertadas = await _db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO notificacion.notificacion_fecha_corte
                    (anio, mes, tipo, inicio_periodo, fin_periodo, fecha_corte, ejecutada, id_usuario_creador)
                VALUES ({clave.Anio}, {clave.Mes}, {clave.Tipo}, {inicio}, {fin}, {fechaUtc}, FALSE, {IdUsuarioSistema})
                ON CONFLICT (anio, mes, tipo) DO NOTHING
                """, ct);

            if (insertadas == 1)
                _logger.LogInformation(
                    "Corte {Tipo} {Mes}/{Anio} creado con la fecha por defecto {FechaCorte} (período {InicioPeriodo} - {FinPeriodo})",
                    clave.Tipo, clave.Mes, clave.Anio, fecha, inicio, fin);
            else
                _logger.LogDebug("El corte {Tipo} {Mes}/{Anio} ya lo creó otra instancia del backend", clave.Tipo, clave.Mes, clave.Anio);

            finAnterior = fin;
        }
    }

    public async Task<CortesProgramadosResponse> ListarAsync(CancellationToken ct)
    {
        await AsegurarCortesAsync(ct);

        var ahora = _reloj.GetUtcNow();
        var claves = ClavesVisibles(ahora);
        var filas = (await CargarAsync(claves[0], claves[^1], ct))
            .OrderBy(f => ClaveCorte.De(f).Orden)
            .ToList();

        var conEnvios = await CortesConEnviosAsync(filas, ct);
        var nombres = await NombresUsuariosAsync(filas, ct);

        var cortes = filas
            .Select((fila, i) => ADto(
                fila,
                i > 0 ? filas[i - 1] : null,
                i < filas.Count - 1 ? filas[i + 1] : null,
                ahora,
                conEnvios,
                nombres))
            .ToList();

        return new CortesProgramadosResponse(_opciones.Habilitado, cortes);
    }

    public Task<ResultadoCambioCorte> CambiarFechaAsync(int id, DateTimeOffset nuevaFecha, int idUsuario, CancellationToken ct) =>
        GuardarFechaAsync(id, _ => nuevaFecha, idUsuario, ct);

    public Task<ResultadoCambioCorte> RestablecerAsync(int id, int idUsuario, CancellationToken ct) =>
        GuardarFechaAsync(id, clave => clave.FechaPorDefecto(), idUsuario, ct);

    public async Task<IReadOnlyList<NotificacionFechaCorte>> PendientesAsync(DateTimeOffset ahora, CancellationToken ct)
    {
        var limite = ahora.ToUniversalTime();
        var desde = limite - VentanaEjecucion;

        return await _db.NotificacionesFechaCorte
            .AsNoTracking()
            .Where(c => !c.Ejecutada && c.FechaCorte <= limite && c.FechaCorte > desde)
            .OrderBy(c => c.FechaCorte)
            .ToListAsync(ct);
    }

    public async Task<bool> TomarParaEjecutarAsync(int id, DateTimeOffset ahora, CancellationToken ct)
    {
        var ahoraUtc = ahora.ToUniversalTime();
        var filas = await _db.NotificacionesFechaCorte
            .Where(c => c.Id == id && !c.Ejecutada)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.Ejecutada, true)
                .SetProperty(c => c.FechaEjecucion, ahoraUtc), ct);
        return filas == 1;
    }

    public Task LiberarAsync(int id, CancellationToken ct) =>
        _db.NotificacionesFechaCorte
            .Where(c => c.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.Ejecutada, false)
                .SetProperty(c => c.FechaEjecucion, (DateTimeOffset?)null), ct);

    public async Task<(DateOnly Inicio, DateOnly Fin)?> PeriodoDelCorteAsync(DateTimeOffset fechaCorte, CancellationToken ct)
    {
        var corteUtc = fechaCorte.ToUniversalTime();
        var periodo = await _db.NotificacionesFechaCorte
            .AsNoTracking()
            .Where(c => c.FechaCorte == corteUtc)
            .Select(c => new { c.InicioPeriodo, c.FinPeriodo })
            .FirstOrDefaultAsync(ct);

        return periodo is null ? null : (periodo.InicioPeriodo, periodo.FinPeriodo);
    }

    // ---------------------------------------------------------------------

    private async Task<ResultadoCambioCorte> GuardarFechaAsync(
        int id, Func<ClaveCorte, DateTimeOffset> nuevaFechaDe, int idUsuario, CancellationToken ct)
    {
        var corte = await _db.NotificacionesFechaCorte.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (corte is null)
        {
            _logger.LogWarning("Se intentó cambiar la fecha del corte {IdCorte}, que no existe", id);
            return ResultadoCambioCorte.SinCorte;
        }

        var clave = ClaveCorte.De(corte);
        var anterior = await BuscarAsync(clave.Anterior(), ct);
        var siguiente = await BuscarAsync(clave.Siguiente(), ct);
        var ahora = _reloj.GetUtcNow();
        var nuevaFecha = nuevaFechaDe(clave);

        var error = await ValidarAsync(corte, clave, nuevaFecha, anterior, siguiente, ahora, ct);
        if (error is not null)
        {
            _logger.LogInformation("Cambio de fecha del corte {IdCorte} ({Tipo} {Mes}/{Anio}) rechazado: {Motivo}",
                corte.Id, corte.Tipo, corte.Mes, corte.Anio, error);
            return ResultadoCambioCorte.Falla(error);
        }

        DateTimeOffset fechaAnterior = corte.FechaCorte;
        corte.FechaCorte = nuevaFecha.ToUniversalTime();
        corte.FinPeriodo = FinEfectivo(clave, nuevaFecha);
        corte.IdUsuarioModificacion = idUsuario;
        corte.FechaModificacion = ahora;
        // Reprogramado: vuelve a quedar pendiente. Como la fecha de corte es otra, el control de duplicados
        // no bloquea a quienes ya recibieron el aviso en la ejecución anterior; se les avisa otra vez si siguen debiendo.
        corte.Ejecutada = false;
        corte.FechaEjecucion = null;

        // El período siguiente arranca justo después de este: si el corte se adelantó, cubre los días que quedaron fuera
        if (siguiente is { Ejecutada: false })
        {
            siguiente.InicioPeriodo = corte.FinPeriodo.AddDays(1);
            _logger.LogDebug("El período del corte siguiente ({IdCorte}) ahora empieza el {InicioPeriodo}",
                siguiente.Id, siguiente.InicioPeriodo);
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Corte {IdCorte} ({Tipo} {Mes}/{Anio}) reprogramado por el usuario {IdUsuario}: de {FechaAnterior} a {FechaNueva}, período {InicioPeriodo} - {FinPeriodo}",
            corte.Id, corte.Tipo, corte.Mes, corte.Anio, idUsuario,
            fechaAnterior.ToOffset(OffsetEcuador), nuevaFecha.ToOffset(OffsetEcuador), corte.InicioPeriodo, corte.FinPeriodo);
        return new ResultadoCambioCorte(await ListarAsync(ct), null);
    }

    private async Task<string?> ValidarAsync(
        NotificacionFechaCorte corte,
        ClaveCorte clave,
        DateTimeOffset nuevaFecha,
        NotificacionFechaCorte? anterior,
        NotificacionFechaCorte? siguiente,
        DateTimeOffset ahora,
        CancellationToken ct)
    {
        // Un corte ejecutado (o que quedó sin ejecutar) se puede reprogramar: vuelve a ejecutarse en la nueva fecha.
        // Solo se bloquea mientras el job lo está procesando.
        if (EstadoDe(corte, ahora, await TieneEnviosAsync(corte, ct)) == EstadoCorte.EnProceso)
            return "Este corte se está ejecutando en este momento; espera a que termine para reprogramarlo.";

        if (nuevaFecha <= ahora)
            return "La nueva fecha de corte debe ser futura.";

        var dia = DiaEcuador(nuevaFecha);
        if (dia < corte.InicioPeriodo)
            return $"La fecha de corte no puede ser anterior al inicio del período ({Formato(corte.InicioPeriodo)}).";

        if (dia > clave.DiaMaximo())
            return $"La fecha de corte no puede ser posterior al {Formato(clave.DiaMaximo())} " +
                   $"({DiasMaximosDeRetraso} días después del fin del período).";

        if (anterior is not null && dia <= DiaEcuador(anterior.FechaCorte))
            return $"La fecha debe ser posterior al corte anterior ({Formato(DiaEcuador(anterior.FechaCorte))}).";

        if (siguiente is not null && dia >= DiaEcuador(siguiente.FechaCorte))
            return $"La fecha debe ser anterior al siguiente corte ({Formato(DiaEcuador(siguiente.FechaCorte))}).";

        return null;
    }

    private CorteProgramadoDto ADto(
        NotificacionFechaCorte fila,
        NotificacionFechaCorte? anterior,
        NotificacionFechaCorte? siguiente,
        DateTimeOffset ahora,
        IReadOnlySet<DateTimeOffset> conEnvios,
        IReadOnlyDictionary<int, string> nombres)
    {
        var clave = ClaveCorte.De(fila);
        var porDefecto = clave.FechaPorDefecto();
        var estado = EstadoDe(fila, ahora, conEnvios.Contains(fila.FechaCorte.ToUniversalTime()));

        // Rango que acepta el selector de días; ValidarAsync vuelve a revisar todo al guardar
        var minimo = Max(fila.InicioPeriodo, DiaEcuador(ahora));
        if (anterior is not null)
            minimo = Max(minimo, DiaEcuador(anterior.FechaCorte).AddDays(1));
        var maximo = clave.DiaMaximo();
        if (siguiente is not null)
            maximo = Min(maximo, DiaEcuador(siguiente.FechaCorte).AddDays(-1));

        // Cortes viejos (p. ej. del mes anterior) quedan sin días disponibles y no se pueden reprogramar
        var editable = estado != EstadoCorte.EnProceso && minimo <= maximo;
        var diaPorDefecto = DiaEcuador(porDefecto);
        var puedeRestablecer = editable
            && fila.FechaCorte != porDefecto
            && porDefecto > ahora
            && diaPorDefecto >= minimo
            && diaPorDefecto <= maximo;

        return new CorteProgramadoDto(
            fila.Id,
            fila.Anio,
            fila.Mes,
            fila.Tipo,
            fila.InicioPeriodo,
            fila.FinPeriodo,
            fila.FechaCorte.ToOffset(OffsetEcuador),
            porDefecto,
            fila.FechaCorte != porDefecto,
            estado,
            editable,
            puedeRestablecer,
            minimo,
            maximo,
            fila.FechaEjecucion?.ToOffset(OffsetEcuador),
            fila.FechaModificacion?.ToOffset(OffsetEcuador),
            fila.IdUsuarioModificacion is { } idUsuario && nombres.TryGetValue(idUsuario, out var nombre) ? nombre : null);
    }

    private static string EstadoDe(NotificacionFechaCorte fila, DateTimeOffset ahora, bool tieneEnvios)
    {
        // Con envíos registrados también cuenta como ejecutado: así se ven los cortes que hizo el cron anterior
        if (fila.Ejecutada || tieneEnvios)
            return EstadoCorte.Ejecutado;
        if (fila.FechaCorte <= ahora - VentanaEjecucion)
            return EstadoCorte.NoEjecutado;
        if (fila.FechaCorte <= ahora)
            return EstadoCorte.EnProceso;
        return EstadoCorte.Programado;
    }

    private static List<ClaveCorte> ClavesVisibles(DateTimeOffset ahora)
    {
        var hoy = DiaEcuador(ahora);
        var primerMes = new DateOnly(hoy.Year, hoy.Month, 1).AddMonths(-MesesAtras);
        return Secuencia(primerMes, MesesTotales).ToList();
    }

    private async Task<List<NotificacionFechaCorte>> CargarAsync(ClaveCorte desde, ClaveCorte hasta, CancellationToken ct)
    {
        var mesDesde = desde.Anio * 12 + desde.Mes;
        var mesHasta = hasta.Anio * 12 + hasta.Mes;

        var filas = await _db.NotificacionesFechaCorte
            .AsNoTracking()
            .Where(c => c.Anio * 12 + c.Mes >= mesDesde && c.Anio * 12 + c.Mes <= mesHasta)
            .ToListAsync(ct);

        return filas
            .Where(f => ClaveCorte.De(f).Orden >= desde.Orden && ClaveCorte.De(f).Orden <= hasta.Orden)
            .ToList();
    }

    private Task<NotificacionFechaCorte?> BuscarAsync(ClaveCorte clave, CancellationToken ct) =>
        _db.NotificacionesFechaCorte.FirstOrDefaultAsync(
            c => c.Anio == clave.Anio && c.Mes == clave.Mes && c.Tipo == clave.Tipo, ct);

    private async Task<bool> TieneEnviosAsync(NotificacionFechaCorte corte, CancellationToken ct)
    {
        var corteUtc = corte.FechaCorte.ToUniversalTime();
        return await _db.NotificacionesEnvio
            .AsNoTracking()
            .AnyAsync(n => n.Origen == OrigenNotificacion.Automatico && n.FechaCorte == corteUtc, ct);
    }

    private async Task<IReadOnlySet<DateTimeOffset>> CortesConEnviosAsync(List<NotificacionFechaCorte> filas, CancellationToken ct)
    {
        var fechas = filas.Select(f => f.FechaCorte.ToUniversalTime()).ToList();
        if (fechas.Count == 0)
            return new HashSet<DateTimeOffset>();

        var conEnvios = await _db.NotificacionesEnvio
            .AsNoTracking()
            .Where(n => n.Origen == OrigenNotificacion.Automatico
                     && n.FechaCorte != null
                     && fechas.Contains(n.FechaCorte.Value))
            .Select(n => n.FechaCorte!.Value)
            .Distinct()
            .ToListAsync(ct);

        return conEnvios.Select(f => f.ToUniversalTime()).ToHashSet();
    }

    private async Task<IReadOnlyDictionary<int, string>> NombresUsuariosAsync(List<NotificacionFechaCorte> filas, CancellationToken ct)
    {
        var ids = filas
            .Where(f => f.IdUsuarioModificacion is not null)
            .Select(f => f.IdUsuarioModificacion!.Value)
            .Distinct()
            .ToList();
        if (ids.Count == 0)
            return new Dictionary<int, string>();

        var usuarios = await _db.TblAutenticacionUsuarios
            .AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new
            {
                u.Id,
                u.Email,
                Nombres = u.IdpersonaNavigation != null ? u.IdpersonaNavigation.Nombres : null,
                Apellidos = u.IdpersonaNavigation != null ? u.IdpersonaNavigation.Apellidos : null
            })
            .ToListAsync(ct);

        return usuarios.ToDictionary(
            u => u.Id,
            u => $"{u.Nombres} {u.Apellidos}".Trim() is { Length: > 0 } nombre ? nombre : u.Email);
    }

    private static string Formato(DateOnly dia) => dia.ToString("dd/MM/yyyy");
    private static DateOnly Max(DateOnly a, DateOnly b) => a > b ? a : b;
    private static DateOnly Min(DateOnly a, DateOnly b) => a < b ? a : b;
}
