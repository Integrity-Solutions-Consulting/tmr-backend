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
    IReadOnlyList<HorasProyecto>? Proyectos = null);   // ya no se llena: la jornada se exige por persona, no por proyecto

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
    /// <summary>
    /// Horas faltantes con jornada total por persona: 8 h por día laborable (6 h si es pasante),
    /// contra la suma de horas de todos sus proyectos.
    /// </summary>
    /// <param name="idProyecto">
    /// Fila de Seguimiento desde la que se avisa. No recorta el cálculo: cuentan las horas de todos los proyectos.
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
    /// Corte automático: mismos colaboradores que Seguimiento, con jornada total por persona (8 h, o 6 h si es
    /// pasante, por día laborable dentro de sus asignaciones) contra la suma de horas de todos sus proyectos.
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

        // idProyecto ya no recorta el cálculo: la jornada se exige una sola vez por persona y cuentan
        // las horas de todos sus proyectos. Solo identifica la fila de Seguimiento desde la que se avisa.
        var empleados = _db.TblAdministracionEmpleados
            .Where(e => (e.Activo || incluirInactivos) && (idsEmpleados == null || idsEmpleados.Contains(e.Id)));

        return await CalcularJornadaTotalAsync(empleados, inicioPeriodo, finPeriodo, ct);
    }

    public async Task<IReadOnlyList<EmpleadoConHorasFaltantes>> CalcularPorProyectoAsync(
        DateOnly inicioPeriodo,
        DateOnly finPeriodo,
        CancellationToken ct)
    {
        if (finPeriodo < inicioPeriodo)
            return [];

        // Mismos colaboradores que Seguimiento: activos + los que salieron dentro del período
        var empleados = _db.TblAdministracionEmpleados
            .Where(e => e.Activo
                || (e.Fechaterminacion.HasValue
                    && e.Fechaterminacion.Value >= inicioPeriodo
                    && e.Fechaterminacion.Value <= finPeriodo));

        return await CalcularJornadaTotalAsync(empleados, inicioPeriodo, finPeriodo, ct);
    }

    /// <summary>
    /// Regla de horas faltantes de las notificaciones: una sola jornada por día laborable y por persona,
    /// sin importar en cuántos proyectos esté. Antes cada proyecto exigía su propia jornada completa, así que
    /// quien tenía dos proyectos debía registrar 16 h diarias y recibía el correo aunque hubiera cumplido.
    /// <list type="bullet">
    /// <item>Jornada diaria: 6 h si es pasante (tipo de contrato PAS) y 8 h para el resto, según el contrato
    /// vigente cada día (historial de contratos; sin historial, el tipo de contrato actual).</item>
    /// <item>Días exigidos: lunes a viernes sin feriados (los recurrentes se repiten cada año), entre ingreso y salida, dentro de alguna de sus
    /// asignaciones del período (si no tiene ninguna, todo el período) y nunca después de hoy.</item>
    /// <item>Horas registradas: la suma de todos sus proyectos en esos días.</item>
    /// <item>Horas faltantes: max(0, horas esperadas − horas registradas).</item>
    /// </list>
    /// </summary>
    private async Task<IReadOnlyList<EmpleadoConHorasFaltantes>> CalcularJornadaTotalAsync(
        IQueryable<TblAdministracionEmpleado> filtroEmpleados,
        DateOnly inicioPeriodo,
        DateOnly finPeriodo,
        CancellationToken ct)
    {
        var empleados = await filtroEmpleados
            .AsNoTracking()
            .Include(e => e.IdpersonaNavigation)
            .Include(e => e.IdtipocontratoNavigation)   // necesario para saber si es pasante
            .Include(e => e.TblTimeReportAsignacionProyectos.Where(ep => ep.Activo
                    && (ep.Fechaasignacion == null || ep.Fechaasignacion <= finPeriodo)
                    && (ep.Fechafinasignacion == null || ep.Fechafinasignacion >= inicioPeriodo)))
            .ToListAsync(ct);

        if (empleados.Count == 0)
            return [];

        var empIds = empleados.Select(e => e.Id).ToList();

        // Los del período y todos los recurrentes (se proyectan al año del período más abajo)
        var feriadosDb = await _db.TblTimeReportFeriados
            .AsNoTracking()
            .Where(f => f.Activo
                && ((f.Fechaferiado >= inicioPeriodo && f.Fechaferiado <= finPeriodo) || f.Esrecurrente == true))
            .Select(f => new FeriadoRegistrado(f.Fechaferiado, f.Esrecurrente == true))
            .ToListAsync(ct);
        var feriados = FeriadosDelPeriodo(feriadosDb, inicioPeriodo, finPeriodo);

        // Todas las actividades del período, de cualquier proyecto
        var actividadesPorEmpleado = (await _db.TblTimeReportActividadDiaria
                .AsNoTracking()
                .Where(a => a.Activo
                    && empIds.Contains(a.Idempleado)
                    && a.Fechaactividad >= inicioPeriodo
                    && a.Fechaactividad <= finPeriodo)
                .ToListAsync(ct))
            .ToLookup(a => a.Idempleado);

        // Jornada por contrato vigente (8 h / 6 h pasante), por si cambió de tipo de contrato en el período
        var jornadas = await CalculoHorasPeriodo.CargarJornadasAsync(_db, empIds);
        var hoy = CalculoHorasPeriodo.HoyEcuador();

        var resultado = new List<EmpleadoConHorasFaltantes>();
        foreach (var empleado in empleados)
        {
            var calculo = CalcularJornadaTotal(
                empleado, inicioPeriodo, finPeriodo, actividadesPorEmpleado[empleado.Id], feriados, hoy, jornadas[empleado.Id]);

            if (!calculo.DebeHoras)
                continue;

            var persona = empleado.IdpersonaNavigation;
            var nombre = $"{persona?.Nombres} {persona?.Apellidos}".Trim();
            var email = string.IsNullOrWhiteSpace(empleado.Emailcorporativo) ? persona?.Email : empleado.Emailcorporativo;

            // Sin desglose por proyecto: los proyectos ya no tienen una cuota propia que pueda "faltar"
            resultado.Add(new EmpleadoConHorasFaltantes(empleado.Id, nombre, email, calculo));
        }

        return resultado;
    }

    /// <summary>
    /// Horas faltantes de una persona con jornada total (ver <see cref="CalcularJornadaTotalAsync"/>).
    /// Requiere el empleado con IdtipocontratoNavigation y sus asignaciones del período cargadas.
    /// </summary>
    internal static ResultadoHorasFaltantes CalcularJornadaTotal(
        TblAdministracionEmpleado empleado,
        DateOnly inicioPeriodo,
        DateOnly finPeriodo,
        IEnumerable<TblTimeReportActividadDiarium> actividades,
        ICollection<DateOnly> feriados,
        DateOnly hoy,
        IEnumerable<PeriodoJornada>? jornadas)
    {
        // Unión de las vigencias de todas sus asignaciones: un día cuenta una sola vez aunque
        // esté en varios proyectos. Sin asignaciones (lista vacía) se evalúa todo el período.
        var vigencias = empleado.TblTimeReportAsignacionProyectos
            .Select(ep => new VigenciaAsignacion(ep.Fechaasignacion, ep.Fechafinasignacion))
            .ToList();

        // Calcular aplica 6 h por día si el contrato vigente es pasantía (PAS) y 8 h en cualquier otro caso
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
