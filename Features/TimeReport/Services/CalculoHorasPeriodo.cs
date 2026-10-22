using Microsoft.EntityFrameworkCore;
using tmr_backend.Infrastructure.Database;
using tmr_backend.Infrastructure.Database.Entities;

namespace tmr_backend.Features.TimeReport.Services;

// sm - Jornada vigente en un rango de fechas, según el historial de tipo de contrato (script 11).
public sealed record PeriodoJornada(DateOnly Desde, DateOnly? Hasta, decimal Jornada);

// sm - Vigencia de una asignación a proyecto (Entrada/Salida). Null = sin límite por ese lado.
public sealed record VigenciaAsignacion(DateOnly? Desde, DateOnly? Hasta)
{
    public bool Incluye(DateOnly dia) =>
        (Desde is null || Desde.Value <= dia) && (Hasta is null || Hasta.Value >= dia);
}

/// <summary>
/// sm - Resultado del cálculo de horas de un colaborador en un periodo (misma regla para Seguimiento y Actividades).
/// </summary>
public sealed record HorasPeriodo(
    decimal HorasJornada,
    int DiasLaborables,
    int DiasConReporte,
    int DiasACompletar,
    decimal HorasRegistradas,
    decimal HorasEsperadas,
    decimal HorasPorRegistrar,
    string Estado,
    // sm - Días laborables con la jornada completa (8 h/6 h) pero con menos de MinimoActividadesPorDia actividades
    // registradas ese día: cumplen horas, pero no el detalle mínimo de actividades. Se cuentan aparte de
    // DiasACompletar para que Seguimiento pueda distinguir el motivo ("faltan horas" vs "faltan actividades").
    int DiasActividadesInsuficientes = 0,
    // sm - Las fechas puntuales de esos mismos días (no solo el conteo), para que el calendario de "Ver detalle"
    // en Seguimiento pueda resaltar exactamente esos días, además del resumen numérico de arriba.
    IReadOnlyList<DateOnly>? FechasActividadesInsuficientes = null)
{
    // sm - Promedio por día = horas registradas en días laborables ÷ días laborables del periodo.
    public decimal PromedioPorDia => DiasLaborables > 0 ? HorasRegistradas / DiasLaborables : 0m;
}

/// <summary>
/// sm - Regla de negocio de horas, compartida por la tabla de Seguimiento y las métricas de Actividades
/// para que ambos muestren exactamente los mismos números.
/// </summary>
public static class CalculoHorasPeriodo
{
    // sm - Mínimo de actividades distintas que debe tener un colaborador en UN proyecto, en UN día, para que ese
    // día cuente como reportado: antes bastaba una sola fila con todas las horas del día y se perdía el detalle
    // de qué se hizo. Aplica por igual a trabajadores y pasantes (lo que cambia por pasantía es la jornada en horas).
    public const int MinimoActividadesPorDia = 3;

    // sm - "Hoy" según la hora de Ecuador (UTC−5, sin horario de verano), sin importar la zona horaria del servidor.
    // Se usa para no contar los días futuros del rango.
    public static DateOnly HoyEcuador() => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-5));

    // sm - Jornada mínima diaria: 8 h; 6 h si el tipo de contrato es Pasantía (código PAS).
    // Se expone para que el Dashboard use exactamente la misma regla.
    public static decimal HorasJornada(TblAdministracionEmpleado empleado) =>
        HorasJornadaPorCodigo(empleado.IdtipocontratoNavigation?.Codigovalor);

    // sm - Misma regla a partir del código del tipo de contrato (lo usa el historial de contratos del dashboard).
    public static decimal HorasJornadaPorCodigo(string? codigoTipoContrato) =>
        codigoTipoContrato?.Trim().ToUpper() == "PAS" ? 6m : 8m;

    // sm - Historial de jornadas por empleado (script 11). Sin historial, se usa el tipo de contrato actual.
    public static async Task<ILookup<int, PeriodoJornada>> CargarJornadasAsync(ApplicationDbContext db, ICollection<int>? idsEmpleado = null)
    {
        var contratos = await db.TblAdministracionEmpleadoContratos
            .AsNoTracking()
            .Where(c => c.Activo && (idsEmpleado == null || idsEmpleado.Contains(c.Idempleado)))
            .Join(db.TblAdministracionCatalogoDetalles, c => c.Idtipocontrato, d => d.Id,
                (c, d) => new { c.Idempleado, c.Fechadesde, c.Fechahasta, d.Codigovalor })
            .ToListAsync();
        return contratos.ToLookup(
            c => c.Idempleado,
            c => new PeriodoJornada(c.Fechadesde, c.Fechahasta, HorasJornadaPorCodigo(c.Codigovalor)));
    }

    // sm - Jornada de un día: la del contrato vigente ese día; si no hay historial, la del tipo de contrato actual.
    public static decimal JornadaEn(DateOnly dia, IEnumerable<PeriodoJornada>? jornadas, decimal jornadaActual) =>
        jornadas?.FirstOrDefault(j => j.Desde <= dia && (j.Hasta is null || j.Hasta >= dia))?.Jornada ?? jornadaActual;

    // sm - Un día cuenta si cae dentro de alguna vigencia de asignación. Sin vigencias (null o vacía) no se recorta:
    // es el caso "Sin Proyecto" de Seguimiento, que se mide contra todo el periodo.
    public static bool EstaVigente(DateOnly dia, IReadOnlyCollection<VigenciaAsignacion>? vigencias) =>
        vigencias is null || vigencias.Count == 0 || vigencias.Any(v => v.Incluye(dia));

    // sm - Vigencias (Entrada/Salida) de las asignaciones actuales de un empleado, opcionalmente de un solo proyecto.
    public static async Task<List<VigenciaAsignacion>> CargarVigenciasAsync(ApplicationDbContext db, int idEmpleado, int? idProyecto = null) =>
        await db.TblTimeReportAsignacionProyectos
            .AsNoTracking()
            .Where(ep => ep.Activo && ep.Idempleado == idEmpleado && (idProyecto == null || ep.Idproyecto == idProyecto))
            .Select(ep => new VigenciaAsignacion(ep.Fechaasignacion, ep.Fechafinasignacion))
            .ToListAsync();

    // sm - Regla de negocio por colaborador:
    // - Periodo = rango pedido, desde su fecha de ingreso y hasta su fecha de salida si caen dentro del rango,
    //   y nunca después de hoy (fecha de Ecuador): los días futuros del rango no se cuentan.
    // - Vigencias de asignación (opcional): solo cuentan los días entre la Entrada y la Salida del recurso en el
    //   proyecto. Si salió (Inactivo) o entró dentro del rango, solo se le exigen horas de los días que estuvo.
    // - Días laborables = lunes a viernes del periodo, sin feriados. Sábados, domingos y feriados no cuentan.
    // - Jornada mínima = 8 h por día; 6 h si el tipo de contrato es Pasantía (código PAS).
    // - Días con reporte = días laborables con horas registradas >= jornada.
    // - Días a completar = días laborables con horas < jornada (incluye días sin registro).
    // - Horas registradas = horas de los días laborables del periodo.
    // - Horas por registrar = max(0, días laborables × jornada − horas registradas en esos días).
    // - Estado: "-" sin días laborables, "Completo" sin días por completar, "Pendiente" sin ningún día completo,
    //   "En progreso" si tiene días completos y días por completar.
    // Requiere que el empleado tenga cargado IdtipocontratoNavigation.
    public static HorasPeriodo Calcular(
        TblAdministracionEmpleado empleado,
        DateOnly desde,
        DateOnly hasta,
        IEnumerable<TblTimeReportActividadDiarium> actividades,
        ICollection<DateOnly> feriados,
        DateOnly hoy,
        IEnumerable<PeriodoJornada>? jornadas = null,
        IReadOnlyCollection<VigenciaAsignacion>? vigencias = null)
    {
        var horasJornada = HorasJornada(empleado);
        var historial = jornadas?.ToList();
        var horasEsperadas = 0m;

        var inicioPeriodo = empleado.Fechaingreso.HasValue && empleado.Fechaingreso.Value > desde
            ? empleado.Fechaingreso.Value
            : desde;
        // sm - La fecha de salida solo recorta el periodo si el colaborador está inactivo: un activo con una salida
        // vieja guardada (lo reactivaron sin limpiarla) quedaba con 0 días laborables en todo rango posterior a ella.
        var finPeriodo = !empleado.Activo && empleado.Fechaterminacion.HasValue && empleado.Fechaterminacion.Value < hasta
            ? empleado.Fechaterminacion.Value
            : hasta;
        if (finPeriodo > hoy) finPeriodo = hoy;

        // sm - Horas y cantidad de actividades registradas por día dentro del periodo (para comparar cada día
        // contra la jornada y contra el mínimo de actividades).
        var actividadesPorDia = actividades
            .Where(a => a.Idempleado == empleado.Id && a.Fechaactividad >= inicioPeriodo && a.Fechaactividad <= finPeriodo)
            .GroupBy(a => a.Fechaactividad)
            .ToDictionary(g => g.Key, g => (Horas: g.Sum(a => a.Cantidadhoras), Cantidad: g.Count()));

        var diasLaborables = 0;
        var diasConReporte = 0;
        var diasActividadesInsuficientes = 0;
        var fechasActividadesInsuficientes = new List<DateOnly>();
        var horasRegistradas = 0m;
        for (var dia = inicioPeriodo; dia <= finPeriodo; dia = dia.AddDays(1))
        {
            var diaSemana = dia.ToDateTime(TimeOnly.MinValue).DayOfWeek;
            if (diaSemana == DayOfWeek.Saturday || diaSemana == DayOfWeek.Sunday || feriados.Contains(dia))
                continue;
            // sm - Días antes de la Entrada o después de la Salida del proyecto no se exigen.
            if (!EstaVigente(dia, vigencias)) continue;

            diasLaborables++;
            actividadesPorDia.TryGetValue(dia, out var datosDia);
            var horasDia = datosDia.Horas;
            var cantidadActividadesDia = datosDia.Cantidad;
            horasRegistradas += horasDia;
            // sm - Jornada del contrato vigente ese día (historial); antes era siempre la del contrato actual.
            var jornadaDia = JornadaEn(dia, historial, horasJornada);
            horasEsperadas += jornadaDia;

            var horasCompletas = horasDia >= jornadaDia;
            var actividadesCompletas = cantidadActividadesDia >= MinimoActividadesPorDia;
            if (horasCompletas && actividadesCompletas) diasConReporte++;
            else if (horasCompletas && !actividadesCompletas)
            {
                diasActividadesInsuficientes++;
                fechasActividadesInsuficientes.Add(dia);
            }
        }

        var diasACompletar = diasLaborables - diasConReporte;
        // var horasEsperadas = diasLaborables * horasJornada; // sm - ahora se suma la jornada de cada día
        var horasPorRegistrar = Math.Max(0m, horasEsperadas - horasRegistradas);

        var estado = diasLaborables == 0 ? "-"
            : diasACompletar == 0 ? "Completo"
            : diasConReporte == 0 ? "Pendiente"
            : "En progreso";

        return new HorasPeriodo(horasJornada, diasLaborables, diasConReporte, diasACompletar,
            horasRegistradas, horasEsperadas, horasPorRegistrar, estado, diasActividadesInsuficientes,
            fechasActividadesInsuficientes);
    }
}
