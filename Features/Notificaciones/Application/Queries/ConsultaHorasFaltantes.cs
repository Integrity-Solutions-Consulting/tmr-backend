using Microsoft.EntityFrameworkCore;
using TmrBackend.Features.Notificaciones.Domain;
using tmr_backend.Features.TimeReport.Services;
using tmr_backend.Infrastructure.Database;

namespace TmrBackend.Features.Notificaciones.Application.Queries;

/// <summary>
/// Empleado con déficit de horas en un período, con los datos necesarios para avisarle por correo.
/// </summary>
public sealed record EmpleadoConHorasFaltantes(
    int IdEmpleado,
    string Nombre,
    string? Email,
    ResultadoHorasFaltantes Calculo);

/// <summary>
/// Fila de la pantalla de notificaciones (GET /pendientes).
/// </summary>
public sealed record EmpleadoPendienteDto(
    int IdEmpleado,
    string Nombre,
    string? Email,
    decimal HorasEsperadas,
    decimal HorasRegistradas,
    decimal HorasFaltantes,
    DateTimeOffset? UltimoAvisoManual);

public sealed record PendientesNotificacionResponse(
    DateOnly InicioPeriodo,
    DateOnly FinPeriodo,
    IReadOnlyList<EmpleadoPendienteDto> Empleados);

/// <summary>
/// Consultas de horas faltantes que comparten el detector (flujos 1, 2 y 3) y el endpoint GET /pendientes (flujo 2).
/// </summary>
public interface IConsultaHorasFaltantes
{
    /// <summary>
    /// Calcula el déficit del período y devuelve solo los empleados activos que deben horas.
    /// Si se pasan ids, solo evalúa a esos empleados.
    /// </summary>
    Task<IReadOnlyList<EmpleadoConHorasFaltantes>> CalcularAsync(
        DateOnly inicioPeriodo,
        DateOnly finPeriodo,
        IReadOnlyCollection<int>? idsEmpleados,
        CancellationToken ct);

    /// <summary>
    /// Empleados que deben horas desde el inicio del período en curso hasta el día anterior,
    /// junto con la fecha de su último aviso manual. Se calcula en el momento, sin eventos.
    /// </summary>
    Task<PendientesNotificacionResponse> ObtenerPendientesAsync(CancellationToken ct);

    /// <summary>
    /// Fecha del último aviso manual enviado con éxito a cada empleado (solo los que tienen alguno).
    /// </summary>
    Task<IReadOnlyDictionary<int, DateTimeOffset>> UltimosAvisosManualesAsync(
        IReadOnlyCollection<int> idsEmpleados,
        CancellationToken ct);

    /// <summary>
    /// Ids de los empleados que ya recibieron el aviso automático de ese corte.
    /// </summary>
    Task<IReadOnlySet<int>> NotificadosEnCorteAsync(DateTimeOffset fechaCorte, CancellationToken ct);
}

public sealed class ConsultaHorasFaltantes : IConsultaHorasFaltantes
{
    private readonly ApplicationDbContext _db;
    private readonly CalculadorHorasFaltantes _calculador;

    public ConsultaHorasFaltantes(ApplicationDbContext db, CalculadorHorasFaltantes calculador)
    {
        _db = db;
        _calculador = calculador;
    }

    public async Task<IReadOnlyList<EmpleadoConHorasFaltantes>> CalcularAsync(
        DateOnly inicioPeriodo,
        DateOnly finPeriodo,
        IReadOnlyCollection<int>? idsEmpleados,
        CancellationToken ct)
    {
        if (finPeriodo < inicioPeriodo || idsEmpleados is { Count: 0 })
            return [];

        // Solo empleados activos: quien ya salió de la empresa no puede registrar sus horas.
        var empleados = await _db.TblAdministracionEmpleados
            .AsNoTracking()
            .Where(e => e.Activo && (idsEmpleados == null || idsEmpleados.Contains(e.Id)))
            .Include(e => e.IdpersonaNavigation)
            // Tipo de contrato para saber si es pasante (jornada de 6 h).
            .Include(e => e.IdtipocontratoNavigation)
            .ToListAsync(ct);

        if (empleados.Count == 0)
            return [];

        var empIds = empleados.Select(e => e.Id).ToList();

        var feriados = await _db.TblTimeReportFeriados
            .AsNoTracking()
            .Where(f => f.Activo && f.Fechaferiado >= inicioPeriodo && f.Fechaferiado <= finPeriodo)
            .Select(f => f.Fechaferiado)
            .ToListAsync(ct);

        var actividadesPorEmpleado = (await _db.TblTimeReportActividadDiaria
                .AsNoTracking()
                .Where(a => a.Activo
                    && empIds.Contains(a.Idempleado)
                    && a.Fechaactividad >= inicioPeriodo
                    && a.Fechaactividad <= finPeriodo)
                .ToListAsync(ct))
            .ToLookup(a => a.Idempleado);

        var jornadas = await CalculoHorasPeriodo.CargarJornadasAsync(_db, empIds);
        var hoy = CalculoHorasPeriodo.HoyEcuador();

        var resultado = new List<EmpleadoConHorasFaltantes>();
        foreach (var empleado in empleados)
        {
            var calculo = _calculador.Calcular(
                empleado, inicioPeriodo, finPeriodo, actividadesPorEmpleado[empleado.Id], feriados, hoy, jornadas[empleado.Id]);

            if (!calculo.DebeHoras)
                continue;

            var persona = empleado.IdpersonaNavigation;
            var nombre = $"{persona?.Nombres} {persona?.Apellidos}".Trim();
            var email = string.IsNullOrWhiteSpace(empleado.Emailcorporativo) ? persona?.Email : empleado.Emailcorporativo;

            resultado.Add(new EmpleadoConHorasFaltantes(empleado.Id, nombre, email, calculo));
        }

        return resultado;
    }

    public async Task<PendientesNotificacionResponse> ObtenerPendientesAsync(CancellationToken ct)
    {
        var hoy = CalculoHorasPeriodo.HoyEcuador();
        var (inicio, _) = CalculadorHorasFaltantes.PeriodoEnCurso(hoy);
        var fin = hoy.AddDays(-1);

        // El día 1 y el día 16 el período en curso todavía no tiene días cumplidos.
        if (fin < inicio)
            return new PendientesNotificacionResponse(inicio, fin, []);

        var conDeficit = await CalcularAsync(inicio, fin, null, ct);
        var ultimosAvisos = await UltimosAvisosManualesAsync(conDeficit.Select(e => e.IdEmpleado).ToList(), ct);

        var empleados = conDeficit
            .Select(e => new EmpleadoPendienteDto(
                e.IdEmpleado,
                e.Nombre,
                e.Email,
                e.Calculo.HorasEsperadas,
                e.Calculo.HorasRegistradas,
                e.Calculo.HorasFaltantes,
                ultimosAvisos.TryGetValue(e.IdEmpleado, out var ultimo) ? ultimo : null))
            .OrderByDescending(e => e.HorasFaltantes)
            .ThenBy(e => e.Nombre)
            .ToList();

        return new PendientesNotificacionResponse(inicio, fin, empleados);
    }

    public async Task<IReadOnlyDictionary<int, DateTimeOffset>> UltimosAvisosManualesAsync(
        IReadOnlyCollection<int> idsEmpleados,
        CancellationToken ct)
    {
        if (idsEmpleados.Count == 0)
            return new Dictionary<int, DateTimeOffset>();

        return await _db.Set<NotificacionEnvio>()
            .AsNoTracking()
            .Where(n => n.Origen == OrigenNotificacion.Manual
                && n.Estado == EstadoEnvio.Enviado
                && idsEmpleados.Contains(n.IdEmpleado))
            .GroupBy(n => n.IdEmpleado)
            .Select(g => new { IdEmpleado = g.Key, Ultimo = g.Max(n => n.FechaEnvio) })
            .ToDictionaryAsync(x => x.IdEmpleado, x => x.Ultimo, ct);
    }

    public async Task<IReadOnlySet<int>> NotificadosEnCorteAsync(DateTimeOffset fechaCorte, CancellationToken ct)
    {
        // Npgsql solo acepta DateTimeOffset en UTC para columnas timestamptz.
        var corteUtc = fechaCorte.ToUniversalTime();

        // Solo cuentan los envíos exitosos: si el correo falló, el empleado puede volver a recibir el aviso del corte.
        var ids = await _db.Set<NotificacionEnvio>()
            .AsNoTracking()
            .Where(n => n.Origen == OrigenNotificacion.Automatico
                && n.Estado == EstadoEnvio.Enviado
                && n.FechaCorte == corteUtc)
            .Select(n => n.IdEmpleado)
            .Distinct()
            .ToListAsync(ct);

        return ids.ToHashSet();
    }
}
