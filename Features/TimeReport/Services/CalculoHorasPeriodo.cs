using tmr_backend.Infrastructure.Database.Entities;

namespace tmr_backend.Features.TimeReport.Services;

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
    string Estado)
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
    // sm - "Hoy" según la hora de Ecuador (UTC−5, sin horario de verano), sin importar la zona horaria del servidor.
    // Se usa para no contar los días futuros del rango.
    public static DateOnly HoyEcuador() => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-5));

    // sm - Jornada mínima diaria: 8 h; 6 h si el tipo de contrato es Pasantía (código PAS).
    // Se expone para que el Dashboard use exactamente la misma regla.
    public static decimal HorasJornada(TblAdministracionEmpleado empleado) =>
        empleado.IdtipocontratoNavigation?.Codigovalor?.Trim().ToUpper() == "PAS" ? 6m : 8m;

    // sm - Regla de negocio por colaborador:
    // - Periodo = rango pedido, desde su fecha de ingreso y hasta su fecha de salida si caen dentro del rango,
    //   y nunca después de hoy (fecha de Ecuador): los días futuros del rango no se cuentan.
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
        DateOnly hoy)
    {
        var horasJornada = HorasJornada(empleado);

        var inicioPeriodo = empleado.Fechaingreso.HasValue && empleado.Fechaingreso.Value > desde
            ? empleado.Fechaingreso.Value
            : desde;
        var finPeriodo = empleado.Fechaterminacion.HasValue && empleado.Fechaterminacion.Value < hasta
            ? empleado.Fechaterminacion.Value
            : hasta;
        if (finPeriodo > hoy) finPeriodo = hoy;

        // sm - Horas registradas por día dentro del periodo (para comparar cada día contra la jornada).
        var horasPorDia = actividades
            .Where(a => a.Idempleado == empleado.Id && a.Fechaactividad >= inicioPeriodo && a.Fechaactividad <= finPeriodo)
            .GroupBy(a => a.Fechaactividad)
            .ToDictionary(g => g.Key, g => g.Sum(a => a.Cantidadhoras));

        var diasLaborables = 0;
        var diasConReporte = 0;
        var horasRegistradas = 0m;
        for (var dia = inicioPeriodo; dia <= finPeriodo; dia = dia.AddDays(1))
        {
            var diaSemana = dia.ToDateTime(TimeOnly.MinValue).DayOfWeek;
            if (diaSemana == DayOfWeek.Saturday || diaSemana == DayOfWeek.Sunday || feriados.Contains(dia))
                continue;

            diasLaborables++;
            horasPorDia.TryGetValue(dia, out var horasDia);
            horasRegistradas += horasDia;
            if (horasDia >= horasJornada) diasConReporte++;
        }

        var diasACompletar = diasLaborables - diasConReporte;
        var horasEsperadas = diasLaborables * horasJornada;
        var horasPorRegistrar = Math.Max(0m, horasEsperadas - horasRegistradas);

        var estado = diasLaborables == 0 ? "-"
            : diasACompletar == 0 ? "Completo"
            : diasConReporte == 0 ? "Pendiente"
            : "En progreso";

        return new HorasPeriodo(horasJornada, diasLaborables, diasConReporte, diasACompletar,
            horasRegistradas, horasEsperadas, horasPorRegistrar, estado);
    }
}
