using tmr_backend.Features.TimeReport.Services;

namespace tmr_backend.Features.Dashboard.Services;

/// <summary>
/// sm - Parámetros del Dashboard Time Report. Los marcados como PROVISIONAL están pendientes de confirmar con negocio
/// y se muestran en pantalla y en la descarga (RF 16: el usuario debe conocer los parámetros aplicados).
/// </summary>
public static class ParametrosDashboard
{
    // sm - Semáforo confirmado: verde = 100 %, amarillo = 80 % a menos de 100 %, rojo = menos de 80 %.
    public const decimal UmbralVerde = 100m;
    public const decimal UmbralAmarillo = 80m;

    // sm - RF 05: horizonte de "próximos a terminar" (configurable desde la pantalla).
    public const int HorizontePorDefecto = 30;

    // sm - Confirmado (2026-10-02): la recurrencia ya no se mide en meses sino en ocasiones (cortes) con atraso.
    // Cortes: día 15 y fin de mes (último día hábil ≤ 15 y último día hábil del mes). En cada corte se revisa su
    // quincena; hay atraso si al corte tiene MÁS de MaxDiasIncompletosPorCorte días hábiles incompletos.
    // Con UmbralRecurrenciaPorDefecto ocasiones o más dentro de la ventana, el colaborador es recurrente.
    public const int UmbralRecurrenciaPorDefecto = 3;
    public const int MaxDiasIncompletosPorCorte = 2;

    // sm - Confirmado (2026-10-02): gracia en cada corte. Los últimos días hábiles de la quincena (hasta el corte) no se
    // evalúan: a esa fecha se notifica al colaborador y se le da tiempo para subirlos, así no sale atrasado siempre.
    public const int DiasGraciaCorte = 1;

    public static readonly string ReglaRecurrencia =
        $"Cortes el 15 y a fin de mes (último día hábil). Hay atraso en un corte si a esa fecha la quincena tiene más de " +
        $"{MaxDiasIncompletosPorCorte} días hábiles con horas incompletas. No se evalúa el último día hábil antes del corte " +
        "(gracia). Las horas agregadas o aumentadas después del corte (aunque sea editando una actividad) no lo cubren";

    // sm - Confirmado (2026-10-02): estados (catálogo EPR) que cuentan como proyecto activo / vigente:
    // En Progreso, Aprobado, Planificación y En Espera.
    public static readonly string[] EstadosActivos = ["PRO", "APR", "PLN", "ESP"];

    // sm - Estados (catálogo EPR) que se consideran cerrados: Completado y Cancelado.
    public static readonly string[] EstadosCerrados = ["COM", "CAN"];

    // sm - Confirmado: las novedades (vacaciones y permisos) se toman de las actividades de tipo "Vacaciones" y
    // "Permiso", se restan de las horas esperadas y no cuentan como reportadas.
    public static readonly string[] TiposActividadNovedad = ["VACACIONES", "PERMISO"];

    // sm - Confirmado (2026-10-02): cada proyecto exige su propia jornada completa (8 h, o 6 h si es pasante) por
    // cada día hábil en que estuvo vigente; no se reparte la jornada de la persona entre sus proyectos. Un
    // colaborador con 2 proyectos activos el mismo día debe completar la jornada en CADA uno (misma regla que
    // Seguimiento: ver CalculoHorasPeriodo). Las novedades (vacaciones/permiso) reducen la jornada del día: si están
    // registradas contra un proyecto puntual, solo reducen la de ese proyecto; sin proyecto, se consideran día libre
    // general y reducen la jornada de todos los proyectos vigentes ese día.
    public const string ReglaReparto =
        "Cada proyecto exige su propia jornada completa (8 h, o 6 h si es pasante) por cada día hábil vigente; no se " +
        "reparte la jornada de la persona entre proyectos. Las novedades (vacaciones/permiso) reducen la jornada del " +
        "día: si tienen un proyecto asociado, solo la de ese proyecto; si no, la de todos los proyectos vigentes ese día";

    // sm - Confirmado: el cierre de cada mes es el último día hábil del mes (lunes a viernes, sin feriados).
    // Lo registrado después del cierre se considera regularización.
    public const string ReglaCierre =
        "El cierre de cada mes es su último día hábil (lunes a viernes, sin feriados)";

    public static string Semaforo(decimal porcentaje) =>
        porcentaje >= UmbralVerde ? "Verde" : porcentaje >= UmbralAmarillo ? "Amarillo" : "Rojo";

    public static bool EsDiaLaborable(DateOnly dia, ICollection<DateOnly> feriados)
    {
        var diaSemana = dia.DayOfWeek;
        return diaSemana != DayOfWeek.Saturday && diaSemana != DayOfWeek.Sunday && !feriados.Contains(dia);
    }

    // sm - Fecha de cierre del mes: último día hábil (lunes a viernes, sin feriados).
    public static DateOnly UltimoDiaHabil(int anio, int mes, ICollection<DateOnly> feriados)
    {
        var dia = new DateOnly(anio, mes, 1).AddMonths(1).AddDays(-1);
        while (!EsDiaLaborable(dia, feriados) && dia.Day > 1) dia = dia.AddDays(-1);
        return dia;
    }

    // sm - Fecha del corte de quincena: último día hábil hasta el 15 (si el 15 cae fin de semana o feriado, se adelanta).
    public static DateOnly CorteQuincena(int anio, int mes, ICollection<DateOnly> feriados)
    {
        var dia = new DateOnly(anio, mes, 15);
        while (!EsDiaLaborable(dia, feriados) && dia.Day > 1) dia = dia.AddDays(-1);
        return dia;
    }
}

// sm - Datos mínimos para el cálculo (se arman desde las entidades en el endpoint).
// sm - Jornada = la del tipo de contrato actual; Jornadas = historial por vigencia (script 11). Cada día usa la jornada
// del contrato vigente ese día (un ex pasante tiene 6 h en sus meses de pasantía y 8 h después).
public sealed record EmpleadoCalculo(
    int Id, DateOnly? Ingreso, DateOnly? Salida, decimal Jornada, IReadOnlyList<PeriodoJornada>? Jornadas = null)
{
    public decimal JornadaEn(DateOnly dia) => CalculoHorasPeriodo.JornadaEn(dia, Jornadas, Jornada);
}


public sealed record AsignacionCalculo(int IdEmpleado, int IdProyecto, DateOnly? Desde, DateOnly? Hasta);

public sealed record ActividadCalculo(
    int IdEmpleado,
    int? IdProyecto,
    DateOnly Fecha,
    decimal Horas,
    bool EsNovedad,
    DateOnly FechaRegistro,
    IReadOnlyList<CambioHoras>? CambiosHoras = null)
{
    // sm - Horas que tenía la actividad al terminar el día indicado (fecha de corte o de cierre):
    // 0 si se creó después; si se editaron sus horas después, las que tenía antes del primer cambio posterior
    // (sale de la auditoría). Así, completar un día editando una actividad después del corte también es atraso.
    public decimal HorasAl(DateOnly fecha)
    {
        if (FechaRegistro > fecha) return 0m;
        var cambioPosterior = CambiosHoras?.Where(c => c.Fecha > fecha).MinBy(c => c.Fecha);
        return cambioPosterior?.HorasAnteriores ?? Horas;
    }
}

// sm - Cambio de horas de una actividad registrado en la auditoría (fecha en hora de Ecuador).
public sealed record CambioHoras(DateOnly Fecha, decimal HorasAnteriores);

/// <summary>
/// sm - Cumplimiento de un colaborador en un proyecto dentro del periodo.
/// </summary>
public sealed record FilaCumplimiento(int IdEmpleado, int IdProyecto, decimal Esperadas, decimal Registradas)
{
    // sm - Horas reportadas válidas: lo registrado hasta lo esperado. El excedente no compensa a otros colaboradores
    // (CA 04: con cumplimiento total se muestra 100 % aunque existan registros excedentes).
    public decimal Validas => Math.Min(Registradas, Esperadas);
    public decimal Pendientes => Math.Max(0m, Esperadas - Registradas);
}

/// <summary>
/// sm - Regla de horas esperadas del Dashboard (sección 5 del requerimiento), igual que Seguimiento
/// (CalculoHorasPeriodo): cada proyecto exige su propia jornada completa.
/// - Día laborable = lunes a viernes sin feriados, dentro del periodo y de la vigencia del colaborador (ingreso/salida).
/// - Esperadas del proyecto = jornada del día (8 h / 6 h pasante) por cada día laborable en que el proyecto estuvo
///   vigente para ese colaborador; no se reparte entre proyectos (ver ParametrosDashboard.ReglaReparto).
/// - Novedades: horas de actividades tipo "Vacaciones" o "Permiso" de ese día; se restan de la jornada esperada
///   (del proyecto al que están asociadas, o de todos los vigentes ese día si no tienen proyecto) y no cuentan
///   como reportadas.
/// - Reportadas = horas registradas en el proyecto dentro del periodo (sin novedades).
/// </summary>
public static class CalculoCumplimiento
{
    public static List<FilaCumplimiento> Calcular(
        IEnumerable<EmpleadoCalculo> empleados,
        IEnumerable<AsignacionCalculo> asignaciones,
        IEnumerable<ActividadCalculo> actividades,
        ICollection<DateOnly> feriados,
        DateOnly desde,
        DateOnly hasta,
        DateOnly? registradasHasta = null)
    {
        var filas = new List<FilaCumplimiento>();
        if (hasta < desde) return filas;

        // sm - Para el histórico: solo lo registrado hasta la fecha de cierre (lo posterior es regularización),
        // incluidas las horas aumentadas después editando la actividad.
        var actividadesPeriodo = actividades
            .Where(a => a.Fecha >= desde && a.Fecha <= hasta)
            .Select(a => registradasHasta.HasValue ? a with { Horas = a.HorasAl(registradasHasta.Value) } : a)
            .Where(a => a.Horas > 0m)
            .ToList();
        var actividadesPorEmpleado = actividadesPeriodo.ToLookup(a => a.IdEmpleado);
        var asignacionesPorEmpleado = asignaciones.ToLookup(a => a.IdEmpleado);

        foreach (var empleado in empleados)
        {
            var asignacionesEmpleado = asignacionesPorEmpleado[empleado.Id]
                .Where(a => (a.Desde ?? DateOnly.MinValue) <= hasta && (a.Hasta ?? DateOnly.MaxValue) >= desde)
                .ToList();
            if (asignacionesEmpleado.Count == 0) continue;

            var actividadesEmpleado = actividadesPorEmpleado[empleado.Id].ToList();

            // sm - Novedades con proyecto asociado solo reducen la jornada de ESE proyecto; sin proyecto, se tratan
            // como día libre general y reducen la jornada de todos los proyectos vigentes ese día.
            var novedadesConProyectoPorDia = actividadesEmpleado
                .Where(a => a.EsNovedad && a.IdProyecto.HasValue)
                .GroupBy(a => (a.Fecha, Proyecto: a.IdProyecto!.Value))
                .ToDictionary(g => g.Key, g => g.Sum(a => a.Horas));
            var novedadesSinProyectoPorDia = actividadesEmpleado
                .Where(a => a.EsNovedad && !a.IdProyecto.HasValue)
                .GroupBy(a => a.Fecha)
                .ToDictionary(g => g.Key, g => g.Sum(a => a.Horas));

            var registradas = actividadesEmpleado
                .Where(a => !a.EsNovedad && a.IdProyecto.HasValue)
                .GroupBy(a => a.IdProyecto!.Value)
                .ToDictionary(g => g.Key, g => g.Sum(a => a.Horas));

            var proyectos = asignacionesEmpleado.Select(a => a.IdProyecto).Distinct().ToList();
            var esperadas = proyectos.ToDictionary(id => id, _ => 0m);

            var inicio = empleado.Ingreso.HasValue && empleado.Ingreso.Value > desde ? empleado.Ingreso.Value : desde;
            var fin = empleado.Salida.HasValue && empleado.Salida.Value < hasta ? empleado.Salida.Value : hasta;

            for (var dia = inicio; dia <= fin; dia = dia.AddDays(1))
            {
                if (!ParametrosDashboard.EsDiaLaborable(dia, feriados)) continue;

                var proyectosVigentes = asignacionesEmpleado
                    .Where(a => (a.Desde ?? DateOnly.MinValue) <= dia && (a.Hasta ?? DateOnly.MaxValue) >= dia)
                    .Select(a => a.IdProyecto)
                    .Distinct()
                    .ToList();
                if (proyectosVigentes.Count == 0) continue;

                novedadesSinProyectoPorDia.TryGetValue(dia, out var novedadGeneral);
                var jornadaDia = empleado.JornadaEn(dia);

                foreach (var idProyecto in proyectosVigentes)
                {
                    novedadesConProyectoPorDia.TryGetValue((dia, idProyecto), out var novedadProyecto);
                    esperadas[idProyecto] += Math.Max(0m, jornadaDia - novedadProyecto - novedadGeneral);
                }
            }

            foreach (var idProyecto in proyectos)
            {
                var horasEsperadas = Math.Round(esperadas[idProyecto], 2);
                registradas.TryGetValue(idProyecto, out var horasRegistradas);
                if (horasEsperadas == 0m && horasRegistradas == 0m) continue;
                filas.Add(new FilaCumplimiento(empleado.Id, idProyecto, horasEsperadas, horasRegistradas));
            }
        }

        return filas;
    }

    /// <summary>
    /// sm - Días hábiles incompletos de un colaborador entre desde y hasta, con lo registrado hasta registradasHasta
    /// (fecha de corte). Un día cuenta si es laborable, está dentro de su vigencia (ingreso/salida), tiene al menos una
    /// asignación vigente y las horas registradas ese día (cualquier proyecto, sin novedades) son menores que la
    /// jornada menos las novedades del día. Un día cubierto por vacaciones o permiso completos no cuenta.
    /// Gracia: los últimos diasGracia días hábiles hasta "hasta" no se evalúan.
    /// </summary>
    public static int DiasIncompletos(
        EmpleadoCalculo empleado,
        IEnumerable<AsignacionCalculo> asignacionesEmpleado,
        IEnumerable<ActividadCalculo> actividadesEmpleado,
        ICollection<DateOnly> feriados,
        DateOnly desde,
        DateOnly hasta,
        DateOnly registradasHasta,
        int diasGracia = ParametrosDashboard.DiasGraciaCorte)
    {
        var asignaciones = asignacionesEmpleado
            .Where(a => (a.Desde ?? DateOnly.MinValue) <= hasta && (a.Hasta ?? DateOnly.MaxValue) >= desde)
            .ToList();
        if (asignaciones.Count == 0) return 0;

        var horasPorDia = actividadesEmpleado
            .Where(a => a.Fecha >= desde && a.Fecha <= hasta)
            .GroupBy(a => a.Fecha)
            .ToDictionary(
                g => g.Key,
                g => (Novedad: g.Where(a => a.EsNovedad).Sum(a => a.HorasAl(registradasHasta)),
                      Registradas: g.Where(a => !a.EsNovedad).Sum(a => a.HorasAl(registradasHasta))));

        // sm - Gracia: se descartan los últimos días hábiles del tramo (hasta el corte).
        var finEvaluado = hasta;
        for (var gracia = diasGracia; gracia > 0 && finEvaluado >= desde; finEvaluado = finEvaluado.AddDays(-1))
            if (ParametrosDashboard.EsDiaLaborable(finEvaluado, feriados)) gracia--;

        var inicio = empleado.Ingreso.HasValue && empleado.Ingreso.Value > desde ? empleado.Ingreso.Value : desde;
        var fin = empleado.Salida.HasValue && empleado.Salida.Value < finEvaluado ? empleado.Salida.Value : finEvaluado;

        var incompletos = 0;
        for (var dia = inicio; dia <= fin; dia = dia.AddDays(1))
        {
            if (!ParametrosDashboard.EsDiaLaborable(dia, feriados)) continue;
            if (!asignaciones.Any(a => (a.Desde ?? DateOnly.MinValue) <= dia && (a.Hasta ?? DateOnly.MaxValue) >= dia)) continue;

            horasPorDia.TryGetValue(dia, out var horas);
            var horasDia = Math.Max(0m, empleado.JornadaEn(dia) - horas.Novedad);
            if (horasDia > 0m && horas.Registradas < horasDia) incompletos++;
        }

        return incompletos;
    }
}
