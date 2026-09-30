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

    // sm - PROVISIONAL: meses con atraso dentro de la ventana para marcar a un colaborador como recurrente.
    public const int UmbralRecurrenciaPorDefecto = 3;

    // sm - PROVISIONAL: estados (catálogo EPR) que cuentan como proyecto activo / vigente.
    public static readonly string[] EstadosActivos = ["PRO"];

    // sm - Estados (catálogo EPR) que se consideran cerrados: Completado y Cancelado.
    public static readonly string[] EstadosCerrados = ["COM", "CAN"];

    // sm - Confirmado: las novedades (vacaciones y permisos) se toman de las actividades de tipo "Vacaciones" y
    // "Permiso", se restan de las horas esperadas y no cuentan como reportadas.
    public static readonly string[] TiposActividadNovedad = ["VACACIONES", "PERMISO"];

    public const string ReglaReparto =
        "PROVISIONAL: la jornada diaria se reparte en partes iguales entre los proyectos asignados vigentes ese día";

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
}

// sm - Datos mínimos para el cálculo (se arman desde las entidades en el endpoint).
public sealed record EmpleadoCalculo(int Id, DateOnly? Ingreso, DateOnly? Salida, decimal Jornada);

public sealed record AsignacionCalculo(int IdEmpleado, int IdProyecto, DateOnly? Desde, DateOnly? Hasta);

public sealed record ActividadCalculo(
    int IdEmpleado,
    int? IdProyecto,
    DateOnly Fecha,
    decimal Horas,
    bool EsNovedad,
    DateOnly FechaRegistro);

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
/// horas esperadas = jornada de cada día laborable vigente × parte de la asignación − horas de novedades.
/// - Día laborable = lunes a viernes sin feriados, dentro del periodo y de la vigencia del colaborador (ingreso/salida).
/// - Solo se esperan horas los días en que el colaborador tiene al menos una asignación vigente.
/// - Jornada 8 h / 6 h pasante (CalculoHorasPeriodo.HorasJornada).
/// - Novedades: horas de actividades tipo "Vacaciones" o "Permiso" de ese día; se restan de la jornada y no cuentan
///   como reportadas.
/// - Reparto entre proyectos: ver ParametrosDashboard.ReglaReparto (provisional).
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

        // sm - Para el histórico: solo lo registrado hasta la fecha de cierre (lo posterior es regularización).
        var actividadesPeriodo = actividades
            .Where(a => a.Fecha >= desde && a.Fecha <= hasta
                        && (!registradasHasta.HasValue || a.FechaRegistro <= registradasHasta.Value))
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

            var esperadas = asignacionesEmpleado.Select(a => a.IdProyecto).Distinct().ToDictionary(id => id, _ => 0m);

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
                var horasDia = Math.Max(0m, empleado.Jornada - horasNovedad);
                var parte = horasDia / proyectosVigentes.Count;
                foreach (var idProyecto in proyectosVigentes) esperadas[idProyecto] += parte;
            }

            // sm - Se redondea cada proyecto a 2 decimales y la diferencia de redondeo se asigna al proyecto con más horas,
            // para que la suma del colaborador sea exacta (8 h ÷ 3 proyectos no debe sumar 176,01 h en vez de 176 h).
            var totalRedondeado = Math.Round(esperadas.Values.Sum(), 2);
            foreach (var idProyecto in esperadas.Keys.ToList()) esperadas[idProyecto] = Math.Round(esperadas[idProyecto], 2);
            var diferencia = totalRedondeado - esperadas.Values.Sum();
            if (diferencia != 0m) esperadas[esperadas.MaxBy(kv => kv.Value).Key] += diferencia;

            var registradas = actividadesEmpleado
                .Where(a => !a.EsNovedad && a.IdProyecto.HasValue && esperadas.ContainsKey(a.IdProyecto.Value))
                .GroupBy(a => a.IdProyecto!.Value)
                .ToDictionary(g => g.Key, g => g.Sum(a => a.Horas));

            foreach (var (idProyecto, horasEsperadas) in esperadas)
            {
                registradas.TryGetValue(idProyecto, out var horasRegistradas);
                if (horasEsperadas == 0m && horasRegistradas == 0m) continue;
                filas.Add(new FilaCumplimiento(empleado.Id, idProyecto, horasEsperadas, horasRegistradas));
            }
        }

        return filas;
    }
}
