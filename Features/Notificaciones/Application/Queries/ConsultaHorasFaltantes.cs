using Microsoft.EntityFrameworkCore;
using tmr_backend.Features.Notificaciones.Domain;
using tmr_backend.Features.TimeReport.Services;
using tmr_backend.Infrastructure.Database;
using tmr_backend.Infrastructure.Database.Entities;

namespace tmr_backend.Features.Notificaciones.Application.Queries;

public sealed record EmpleadoConHorasFaltantes(
    int IdEmpleado,
    string Nombre,
    string? Email,
    ResultadoHorasFaltantes Calculo,
    IReadOnlyList<HorasProyecto>? Proyectos = null);   // proyectos en los que le faltan horas (detalle del correo)

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

    /// <summary>False si el proyecto está Inactivo, Completado o Cancelado: ya no se avisa por sus horas.</summary>
    Task<bool> ProyectoVigenteAsync(int idProyecto, CancellationToken ct);

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

        // Fila de un proyecto inactivo o cerrado: ya no se avisa por sus horas
        if (idProyecto is { } proyectoFila && !await ProyectoVigenteAsync(proyectoFila, ct))
            return [];

        var datos = await CargarDatosAsync(
            _db.TblAdministracionEmpleados
                .Where(e => (e.Activo || incluirInactivos) && (idsEmpleados == null || idsEmpleados.Contains(e.Id))),
            inicioPeriodo, finPeriodo, ct);

        var resultado = new List<EmpleadoConHorasFaltantes>();
        foreach (var empleado in datos.Empleados)
        {
            var actividades = datos.Actividades[empleado.Id].ToList();
            var jornadas = datos.Jornadas[empleado.Id];

            // Fila "Sin Proyecto" de quien solo tuvo proyectos inactivos o cerrados en el período: no se le exige nada
            var asignaciones = empleado.TblTimeReportAsignacionProyectos;
            if (idProyecto is null && asignaciones.Count > 0 && !asignaciones.Any(ep => ProyectoVigente(ep.IdproyectoNavigation)))
                continue;

            // Lo que decide si se avisa: la fila de Seguimiento desde la que se pidió (ese proyecto, o "Sin Proyecto")
            var calculo = CalcularFila(
                empleado, inicioPeriodo, finPeriodo, actividades, datos.Feriados, datos.Hoy, jornadas, idProyecto);
            if (!calculo.DebeHoras)
                continue;

            // El correo detalla lo que le falta en cada uno de sus proyectos, no solo en el de la fila
            var desglose = DesglosePorProyecto(
                empleado, inicioPeriodo, finPeriodo, actividades, datos.Feriados, datos.Hoy, jornadas);

            resultado.Add(Crear(empleado, calculo, ConFaltantes(desglose)));
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

        // Mismo filtro que Seguimiento (TimeReportEndpoints): activos + los que salieron dentro del período
        var datos = await CargarDatosAsync(
            _db.TblAdministracionEmpleados
                .Where(e => e.Activo
                    || (e.Fechaterminacion.HasValue
                        && e.Fechaterminacion.Value >= inicioPeriodo
                        && e.Fechaterminacion.Value <= finPeriodo)),
            inicioPeriodo, finPeriodo, ct);

        var resultado = new List<EmpleadoConHorasFaltantes>();
        foreach (var empleado in datos.Empleados)
        {
            var desglose = DesglosePorProyecto(
                empleado, inicioPeriodo, finPeriodo, datos.Actividades[empleado.Id].ToList(),
                datos.Feriados, datos.Hoy, datos.Jornadas[empleado.Id]);

            var faltantes = desglose.Sum(d => d.Calculo.HorasFaltantes);
            if (faltantes <= 0)
                continue;

            // Total del corte: la suma de lo que falta en cada proyecto
            var total = new ResultadoHorasFaltantes(
                empleado.Id,
                inicioPeriodo,
                finPeriodo,
                desglose.Max(d => d.Calculo.DiasLaborables),
                desglose.Sum(d => d.Calculo.HorasEsperadas),
                desglose.Sum(d => d.Calculo.HorasRegistradas),
                faltantes);

            resultado.Add(Crear(empleado, total, ConFaltantes(desglose)));
        }

        return resultado;
    }

    // ---------------------------------------------------------------------
    // Regla por proyecto (igual que Seguimiento)
    // ---------------------------------------------------------------------

    /// <summary>Lo que le falta al colaborador en un proyecto (o en "Sin proyecto" si no tiene asignaciones).</summary>
    internal sealed record DesgloseProyecto(int? IdProyecto, string Nombre, ResultadoHorasFaltantes Calculo);

    private sealed record DatosCalculo(
        List<TblAdministracionEmpleado> Empleados,
        ILookup<int, TblTimeReportActividadDiarium> Actividades,
        HashSet<DateOnly> Feriados,
        ILookup<int, PeriodoJornada> Jornadas,
        DateOnly Hoy);

    private async Task<DatosCalculo> CargarDatosAsync(
        IQueryable<TblAdministracionEmpleado> filtroEmpleados,
        DateOnly inicioPeriodo,
        DateOnly finPeriodo,
        CancellationToken ct)
    {
        // Asignaciones activas que se cruzan con el período, con el nombre del proyecto para el correo
        var empleados = await filtroEmpleados
            .AsNoTracking()
            .Include(e => e.IdpersonaNavigation)
            .Include(e => e.IdtipocontratoNavigation)   // jornada: 6 h si es pasante (PAS), 8 h el resto
            .Include(e => e.TblTimeReportAsignacionProyectos.Where(ep => ep.Activo
                    && (ep.Fechaasignacion == null || ep.Fechaasignacion <= finPeriodo)
                    && (ep.Fechafinasignacion == null || ep.Fechafinasignacion >= inicioPeriodo)))
                .ThenInclude(ep => ep.IdproyectoNavigation)
                    .ThenInclude(p => p.IdestadoproyectoNavigation)   // para descartar proyectos cerrados
            .ToListAsync(ct);

        var empIds = empleados.Select(e => e.Id).ToList();

        // Los del período y todos los recurrentes (se proyectan al año del período)
        var feriadosDb = await _db.TblTimeReportFeriados
            .AsNoTracking()
            .Where(f => f.Activo
                && ((f.Fechaferiado >= inicioPeriodo && f.Fechaferiado <= finPeriodo) || f.Esrecurrente == true))
            .Select(f => new FeriadoRegistrado(f.Fechaferiado, f.Esrecurrente == true))
            .ToListAsync(ct);

        var actividades = (await _db.TblTimeReportActividadDiaria
                .AsNoTracking()
                .Where(a => a.Activo
                    && empIds.Contains(a.Idempleado)
                    && a.Fechaactividad >= inicioPeriodo
                    && a.Fechaactividad <= finPeriodo)
                .ToListAsync(ct))
            .ToLookup(a => a.Idempleado);

        // Jornada por contrato vigente cada día (8 h / 6 h pasante), por si cambió de contrato en el período
        var jornadas = empIds.Count == 0
            ? Array.Empty<PeriodoJornada>().ToLookup(_ => 0)
            : await CalculoHorasPeriodo.CargarJornadasAsync(_db, empIds);

        return new DatosCalculo(
            empleados,
            actividades,
            FeriadosDelPeriodo(feriadosDb, inicioPeriodo, finPeriodo),
            jornadas,
            CalculoHorasPeriodo.HoyEcuador());
    }

    /// <summary>
    /// Cada proyecto asignado en el período exige su propia jornada (8 h, o 6 h si es pasante) contra las horas de
    /// ese proyecto, solo entre la entrada y la salida del colaborador en él, como las filas de Seguimiento.
    /// Sin proyectos asignados: una fila "Sin proyecto" con todas sus horas contra una jornada.
    /// Los proyectos que ya no están activos (ver <see cref="ProyectoVigente"/>) no exigen horas ni se avisan; si todos
    /// sus proyectos del período están en ese caso, no se le exige nada (no pasa a "Sin proyecto").
    /// </summary>
    internal static IReadOnlyList<DesgloseProyecto> DesglosePorProyecto(
        TblAdministracionEmpleado empleado,
        DateOnly inicioPeriodo,
        DateOnly finPeriodo,
        IReadOnlyCollection<TblTimeReportActividadDiarium> actividades,
        ICollection<DateOnly> feriados,
        DateOnly hoy,
        IEnumerable<PeriodoJornada>? jornadas)
    {
        var todas = empleado.TblTimeReportAsignacionProyectos;
        var asignaciones = todas
            .Where(ep => ProyectoVigente(ep.IdproyectoNavigation))
            .GroupBy(ep => ep.Idproyecto)
            .ToList();

        // Solo tenía proyectos inactivos o cerrados en el período: no hay nada que avisarle
        if (asignaciones.Count == 0 && todas.Count > 0)
            return [];

        if (asignaciones.Count == 0)
            return [new DesgloseProyecto(null, "Sin proyecto",
                CalcularFila(empleado, inicioPeriodo, finPeriodo, actividades, feriados, hoy, jornadas, idProyecto: null))];

        return asignaciones
            .Select(g => new DesgloseProyecto(
                g.Key,
                g.First().IdproyectoNavigation?.Nombre ?? $"Proyecto {g.Key}",
                Calcular(empleado, inicioPeriodo, finPeriodo,
                    actividades.Where(a => a.Idproyecto == g.Key), feriados, hoy, jornadas,
                    g.Select(ep => new VigenciaAsignacion(ep.Fechaasignacion, ep.Fechafinasignacion)).ToList())))
            .ToList();
    }

    /// <summary>
    /// Fila de Seguimiento: con idProyecto, solo las horas de ese proyecto y los días de su asignación (si la tiene
    /// en el período); sin idProyecto, todas sus horas contra una jornada (fila "Sin Proyecto").
    /// </summary>
    internal static ResultadoHorasFaltantes CalcularFila(
        TblAdministracionEmpleado empleado,
        DateOnly inicioPeriodo,
        DateOnly finPeriodo,
        IEnumerable<TblTimeReportActividadDiarium> actividades,
        ICollection<DateOnly> feriados,
        DateOnly hoy,
        IEnumerable<PeriodoJornada>? jornadas,
        int? idProyecto)
    {
        if (idProyecto is null)
            return Calcular(empleado, inicioPeriodo, finPeriodo, actividades, feriados, hoy, jornadas, vigencias: null);

        var vigencias = empleado.TblTimeReportAsignacionProyectos
            .Where(ep => ep.Idproyecto == idProyecto)
            .Select(ep => new VigenciaAsignacion(ep.Fechaasignacion, ep.Fechafinasignacion))
            .ToList();

        return Calcular(empleado, inicioPeriodo, finPeriodo,
            actividades.Where(a => a.Idproyecto == idProyecto), feriados, hoy, jornadas, vigencias);
    }

    private static ResultadoHorasFaltantes Calcular(
        TblAdministracionEmpleado empleado,
        DateOnly inicioPeriodo,
        DateOnly finPeriodo,
        IEnumerable<TblTimeReportActividadDiarium> actividades,
        ICollection<DateOnly> feriados,
        DateOnly hoy,
        IEnumerable<PeriodoJornada>? jornadas,
        IReadOnlyCollection<VigenciaAsignacion>? vigencias)
    {
        // CalculoHorasPeriodo aplica 6 h por día si el contrato vigente es pasantía (PAS) y 8 h en otro caso
        var horas = CalculoHorasPeriodo.Calcular(
            empleado, inicioPeriodo, finPeriodo, actividades, feriados, hoy, jornadas, vigencias);

        return new ResultadoHorasFaltantes(
            empleado.Id,
            inicioPeriodo,
            finPeriodo,
            horas.DiasLaborables,
            horas.HorasEsperadas,
            horas.HorasRegistradas,
            horas.HorasPorRegistrar);
    }

    // Proyectos en los que le faltan horas, del que más debe al que menos (para el correo y el resumen del corte)
    private static IReadOnlyList<HorasProyecto> ConFaltantes(IReadOnlyList<DesgloseProyecto> desglose) =>
        desglose
            .Where(d => d.Calculo.DebeHoras)
            .OrderByDescending(d => d.Calculo.HorasFaltantes)
            .ThenBy(d => d.Nombre)
            .Select(d => new HorasProyecto(d.IdProyecto, d.Nombre, d.Calculo.HorasFaltantes))
            .ToList();

    private static EmpleadoConHorasFaltantes Crear(
        TblAdministracionEmpleado empleado, ResultadoHorasFaltantes calculo, IReadOnlyList<HorasProyecto> proyectos)
    {
        var persona = empleado.IdpersonaNavigation;
        var nombre = $"{persona?.Nombres} {persona?.Apellidos}".Trim();
        var email = string.IsNullOrWhiteSpace(empleado.Emailcorporativo) ? persona?.Email : empleado.Emailcorporativo;
        return new EmpleadoConHorasFaltantes(empleado.Id, nombre, email, calculo, proyectos);
    }

    internal sealed record FeriadoRegistrado(DateOnly Fecha, bool EsRecurrente);

    /// <summary>
    /// Días feriados que caen en el período. Un feriado recurrente se repite el mismo día y mes de cada año del
    /// período, aunque solo esté registrado en otro año (misma regla que el módulo de Permisos). Si el día no existe
    /// ese año (29 de febrero), se usa el último día del mes.
    /// </summary>
    internal static HashSet<DateOnly> FeriadosDelPeriodo(
        IEnumerable<FeriadoRegistrado> feriados,
        DateOnly inicioPeriodo,
        DateOnly finPeriodo)
    {
        var resultado = new HashSet<DateOnly>();
        foreach (var feriado in feriados)
        {
            if (feriado.Fecha >= inicioPeriodo && feriado.Fecha <= finPeriodo)
                resultado.Add(feriado.Fecha);

            if (!feriado.EsRecurrente)
                continue;

            for (var anio = inicioPeriodo.Year; anio <= finPeriodo.Year; anio++)
            {
                var dia = Math.Min(feriado.Fecha.Day, DateTime.DaysInMonth(anio, feriado.Fecha.Month));
                var proyectado = new DateOnly(anio, feriado.Fecha.Month, dia);
                if (proyectado >= inicioPeriodo && proyectado <= finPeriodo)
                    resultado.Add(proyectado);
            }
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

    public async Task<bool> ProyectoVigenteAsync(int idProyecto, CancellationToken ct)
    {
        var proyecto = await _db.TblTimeReportProyectos
            .AsNoTracking()
            .Include(p => p.IdestadoproyectoNavigation)
            .FirstOrDefaultAsync(p => p.Id == idProyecto, ct);
        return ProyectoVigente(proyecto);
    }

    /// <summary>
    /// Proyecto que todavía exige horas en las notificaciones: no está marcado Inactivo y su estado no es Completado
    /// ni Cancelado (los estados cerrados del Dashboard). Registrar horas atrasadas en un proyecto cerrado sigue
    /// permitido (TR-004); solo deja de avisarse.
    /// </summary>
    internal static bool ProyectoVigente(TblTimeReportProyecto? proyecto) =>
        proyecto is not null
        && proyecto.Activo
        && !tmr_backend.Features.Dashboard.Services.ParametrosDashboard.EstadosCerrados.Contains(
            proyecto.IdestadoproyectoNavigation?.Codigovalor?.Trim().ToUpperInvariant() ?? "");

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
