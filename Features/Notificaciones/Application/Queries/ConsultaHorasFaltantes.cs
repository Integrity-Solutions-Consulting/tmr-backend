using Microsoft.EntityFrameworkCore;
using tmr_backend.Features.Notificaciones.Domain;
using tmr_backend.Features.TimeReport.Services;
using tmr_backend.Infrastructure.Database;

namespace tmr_backend.Features.Notificaciones.Application.Queries;

public sealed record EmpleadoConHorasFaltantes(
    int IdEmpleado,
    string Nombre,
    string? Email,
    ResultadoHorasFaltantes Calculo,
    IReadOnlyList<HorasProyecto>? Proyectos = null);   // corte: proyectos con horas pendientes

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

public interface IConsultaHorasFaltantes
{
    /// <param name="idProyecto">
    /// Solo las horas de ese proyecto, igual que la fila de Seguimiento (cada proyecto exige su propia jornada).
    /// Null = todas las horas del colaborador.
    /// </param>
    /// <param name="incluirInactivos">
    /// Aviso manual: también a quien ya salió, para que complete hasta su fecha de salida (Seguimiento lo muestra).
    /// </param>
    Task<IReadOnlyList<EmpleadoConHorasFaltantes>> CalcularAsync(
        DateOnly inicioPeriodo,
        DateOnly finPeriodo,
        IReadOnlyCollection<int>? idsEmpleados,
        CancellationToken ct,
        int? idProyecto = null,
        bool incluirInactivos = false);

    /// <summary>
    /// Corte automático: mismos colaboradores y proyectos que Seguimiento. Cada proyecto asignado en el período
    /// exige su propia jornada; el colaborador sin proyecto se evalúa con todas sus horas.
    /// Incluye a quien salió dentro del período (debe completar hasta su fecha de salida), no a quien salió antes.
    /// </summary>
    Task<IReadOnlyList<EmpleadoConHorasFaltantes>> CalcularPorProyectoAsync(
        DateOnly inicioPeriodo,
        DateOnly finPeriodo,
        CancellationToken ct);

    Task<PendientesNotificacionResponse> ObtenerPendientesAsync(CancellationToken ct);

    /// <summary>Último aviso manual enviado a cada empleado para ese proyecto (null = avisos sin proyecto).</summary>
    Task<IReadOnlyDictionary<int, DateTimeOffset>> UltimosAvisosManualesAsync(
        IReadOnlyCollection<int> idsEmpleados,
        CancellationToken ct,
        int? idProyecto = null);

    Task<string?> NombreProyectoAsync(int idProyecto, CancellationToken ct);

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
        CancellationToken ct,
        int? idProyecto = null,
        bool incluirInactivos = false)
    {
        if (finPeriodo < inicioPeriodo || idsEmpleados is { Count: 0 })
            return [];

        var empleados = await _db.TblAdministracionEmpleados
            .AsNoTracking()
            .Where(e => (e.Activo || incluirInactivos) && (idsEmpleados == null || idsEmpleados.Contains(e.Id)))
            .Include(e => e.IdpersonaNavigation)
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
                    && a.Fechaactividad <= finPeriodo
                    && (idProyecto == null || a.Idproyecto == idProyecto))
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

    public async Task<IReadOnlyList<EmpleadoConHorasFaltantes>> CalcularPorProyectoAsync(
        DateOnly inicioPeriodo,
        DateOnly finPeriodo,
        CancellationToken ct)
    {
        if (finPeriodo < inicioPeriodo)
            return [];

        // Mismo filtro que Seguimiento (TimeReportEndpoints): activos + los que salieron dentro del período,
        // con las asignaciones activas que se cruzan con el período
        var empleados = await _db.TblAdministracionEmpleados
            .AsNoTracking()
            .Where(e => e.Activo
                || (e.Fechaterminacion.HasValue
                    && e.Fechaterminacion.Value >= inicioPeriodo
                    && e.Fechaterminacion.Value <= finPeriodo))
            .Include(e => e.IdpersonaNavigation)
            .Include(e => e.IdtipocontratoNavigation)
            .Include(e => e.TblTimeReportAsignacionProyectos.Where(ep => ep.Activo
                    && (ep.Fechaasignacion == null || ep.Fechaasignacion <= finPeriodo)
                    && (ep.Fechafinasignacion == null || ep.Fechafinasignacion >= inicioPeriodo)))
                .ThenInclude(ep => ep.IdproyectoNavigation)
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
            var actividades = actividadesPorEmpleado[empleado.Id].ToList();
            var asignaciones = empleado.TblTimeReportAsignacionProyectos.GroupBy(ep => ep.Idproyecto).ToList();

            var calculos = new List<ResultadoHorasFaltantes>();
            var proyectos = new List<HorasProyecto>();

            if (asignaciones.Count == 0)
            {
                // Fila "Sin Proyecto" de Seguimiento: todas sus actividades contra una jornada
                calculos.Add(_calculador.Calcular(
                    empleado, inicioPeriodo, finPeriodo, actividades, feriados, hoy, jornadas[empleado.Id]));
            }
            else
            {
                foreach (var g in asignaciones)
                {
                    var calculo = _calculador.Calcular(
                        empleado, inicioPeriodo, finPeriodo, actividades.Where(a => a.Idproyecto == g.Key),
                        feriados, hoy, jornadas[empleado.Id]);
                    calculos.Add(calculo);
                    if (calculo.DebeHoras)
                        proyectos.Add(new HorasProyecto(g.Key, g.First().IdproyectoNavigation.Nombre, calculo.HorasFaltantes));
                }
            }

            var faltantes = calculos.Sum(c => c.HorasFaltantes);
            if (faltantes <= 0)
                continue;

            var total = new ResultadoHorasFaltantes(
                empleado.Id,
                inicioPeriodo,
                finPeriodo,
                calculos.Max(c => c.DiasLaborables),
                calculos.Sum(c => c.HorasEsperadas),
                calculos.Sum(c => c.HorasRegistradas),
                faltantes);

            var persona = empleado.IdpersonaNavigation;
            var nombre = $"{persona?.Nombres} {persona?.Apellidos}".Trim();
            var email = string.IsNullOrWhiteSpace(empleado.Emailcorporativo) ? persona?.Email : empleado.Emailcorporativo;

            resultado.Add(new EmpleadoConHorasFaltantes(
                empleado.Id, nombre, email, total, proyectos.OrderByDescending(p => p.HorasFaltantes).ToList()));
        }

        return resultado;
    }

    public async Task<PendientesNotificacionResponse> ObtenerPendientesAsync(CancellationToken ct)
    {
        var hoy = CalculoHorasPeriodo.HoyEcuador();
        var (inicio, _) = CalculadorHorasFaltantes.PeriodoEnCurso(hoy);
        var fin = hoy.AddDays(-1);

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
        CancellationToken ct,
        int? idProyecto = null)
    {
        if (idsEmpleados.Count == 0)
            return new Dictionary<int, DateTimeOffset>();

        return await _db.Set<NotificacionEnvio>()
            .AsNoTracking()
            .Where(n => n.Origen == OrigenNotificacion.Manual
                && n.Estado == EstadoEnvio.Enviado
                && n.IdProyecto == idProyecto
                && idsEmpleados.Contains(n.IdEmpleado))
            .GroupBy(n => n.IdEmpleado)
            .Select(g => new { IdEmpleado = g.Key, Ultimo = g.Max(n => n.FechaEnvio) })
            .ToDictionaryAsync(x => x.IdEmpleado, x => x.Ultimo, ct);
    }

    public Task<string?> NombreProyectoAsync(int idProyecto, CancellationToken ct) =>
        _db.TblTimeReportProyectos
            .AsNoTracking()
            .Where(p => p.Id == idProyecto)
            .Select(p => (string?)p.Nombre)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlySet<int>> NotificadosEnCorteAsync(DateTimeOffset fechaCorte, CancellationToken ct)
    {
        var corteUtc = fechaCorte.ToUniversalTime();

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
