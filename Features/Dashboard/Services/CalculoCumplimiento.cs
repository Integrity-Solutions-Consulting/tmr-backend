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

    // sm - Confirmado (2026-10-02): no se reparte en partes iguales (hay personas que registran más de 8 h al día).
    public const string ReglaReparto =
        "Primero se mide a la persona (todo lo registrado en sus proyectos vs. su jornada). Luego se reparte entre " +
        "proyectos según lo registrado en cada uno; lo pendiente, en proporción a lo registrado (o a los días " +
        "vigentes si no registró nada). Si registró más de lo esperado, todos sus proyectos quedan al 100 %";

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
/// sm - Regla de horas esperadas del Dashboard (sección 5 del requerimiento):
/// horas esperadas de la persona = jornada de cada día laborable vigente − horas de novedades.
/// - Día laborable = lunes a viernes sin feriados, dentro del periodo y de la vigencia del colaborador (ingreso/salida).
/// - Solo se esperan horas los días en que el colaborador tiene al menos una asignación vigente.
/// - Jornada 8 h / 6 h pasante (CalculoHorasPeriodo.HorasJornada).
/// - Novedades: horas de actividades tipo "Vacaciones" o "Permiso" de ese día; se restan de la jornada y no cuentan
///   como reportadas.
/// - Reparto entre proyectos: ver ParametrosDashboard.ReglaReparto (según lo registrado, no en partes iguales).
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
            var novedadesPorDia = actividadesEmpleado
                .Where(a => a.EsNovedad)
                .GroupBy(a => a.Fecha)
                .ToDictionary(g => g.Key, g => g.Sum(a => a.Horas));

            var proyectos = asignacionesEmpleado.Select(a => a.IdProyecto).Distinct().ToList();
            // sm - Días laborables en que cada proyecto estuvo vigente: solo se usa para repartir lo pendiente
            // cuando la persona no registró nada en sus proyectos.
            var diasVigentes = proyectos.ToDictionary(id => id, _ => 0);
            var esperadasPersona = 0m;

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

                novedadesPorDia.TryGetValue(dia, out var horasNovedad);
                esperadasPersona += Math.Max(0m, empleado.JornadaEn(dia) - horasNovedad);
                foreach (var idProyecto in proyectosVigentes) diasVigentes[idProyecto]++;
            }

            var registradas = actividadesEmpleado
                .Where(a => !a.EsNovedad && a.IdProyecto.HasValue && diasVigentes.ContainsKey(a.IdProyecto.Value))
                .GroupBy(a => a.IdProyecto!.Value)
                .ToDictionary(g => g.Key, g => g.Sum(a => a.Horas));
            var registradasPersona = registradas.Values.Sum();

            // sm - Confirmado (2026-10-02): ya NO se reparte la jornada en partes iguales entre proyectos (generaba
            // pendientes falsos: 6 h en A + 2 h en B = 8 h completas, pero B salía con 2 h pendientes).
            // Primero se mide a la persona (sus horas esperadas vs. todo lo registrado en sus proyectos) y luego se
            // reparte entre proyectos según lo registrado:
            // - Esperadas del proyecto = lo registrado en él (válido) + su parte de lo pendiente de la persona.
            // - Si registró más de lo esperado, lo válido se escala para que la suma sea lo esperado (todos al 100 %).
            // - Lo pendiente se reparte en proporción a lo registrado en cada proyecto; si no registró nada,
            //   en proporción a los días laborables en que cada proyecto estuvo vigente.
            var validasPersona = Math.Min(registradasPersona, esperadasPersona);
            var pendientesPersona = esperadasPersona - validasPersona;
            var factorValidas = registradasPersona > esperadasPersona && registradasPersona > 0m
                ? esperadasPersona / registradasPersona
                : 1m;
            var pesosPendiente = registradasPersona > 0m
                ? proyectos.ToDictionary(id => id, id => registradas.GetValueOrDefault(id))
                : proyectos.ToDictionary(id => id, id => (decimal)diasVigentes[id]);
            var totalPesos = pesosPendiente.Values.Sum();

            var esperadas = proyectos.ToDictionary(id => id, id =>
                registradas.GetValueOrDefault(id) * factorValidas
                + (totalPesos > 0m ? pendientesPersona * pesosPendiente[id] / totalPesos : 0m));

            // sm - Se redondea cada proyecto a 2 decimales y la diferencia de redondeo se asigna al proyecto con más horas,
            // para que la suma del colaborador sea exacta (no debe sumar 176,01 h en vez de 176 h).
            var totalRedondeado = Math.Round(esperadasPersona, 2);
            foreach (var idProyecto in esperadas.Keys.ToList()) esperadas[idProyecto] = Math.Round(esperadas[idProyecto], 2);
            var diferencia = totalRedondeado - esperadas.Values.Sum();
            if (diferencia != 0m && esperadas.Count > 0) esperadas[esperadas.MaxBy(kv => kv.Value).Key] += diferencia;

            foreach (var (idProyecto, horasEsperadas) in esperadas)
            {
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
