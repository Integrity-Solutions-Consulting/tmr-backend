using tmr_backend.Features.TimeReport.Services;
using tmr_backend.Infrastructure.Database.Entities;

namespace TmrBackend.Features.Notificaciones.Domain;

/// <summary>
/// Resultado del cálculo de horas faltantes de un empleado en un período.
/// </summary>
public sealed record ResultadoHorasFaltantes(
    int IdEmpleado,
    DateOnly InicioPeriodo,
    DateOnly FinPeriodo,
    int DiasLaborables,
    decimal HorasEsperadas,
    decimal HorasRegistradas,
    decimal HorasFaltantes)
{
    public bool DebeHoras => HorasFaltantes > 0;
}

/// <summary>
/// Función pura que compara las horas esperadas contra las registradas de un empleado en un período.
/// Usa la misma regla que Time Report (CalculoHorasPeriodo) para que el correo muestre los mismos números
/// que el colaborador ve en la plataforma:
/// - Solo cuentan los días de lunes a viernes que no son feriado.
/// - El período se recorta a la fecha de ingreso y de salida del empleado, y nunca pasa de "hoy".
/// - Jornada de 8 h por día, o 6 h si el contrato vigente ese día es Pasantía.
/// No consulta la base de datos: recibe todo lo que necesita, por eso se puede probar con datos en memoria.
/// </summary>
public sealed class CalculadorHorasFaltantes
{
    /// <param name="empleado">Empleado con IdtipocontratoNavigation cargado (para saber si es pasante).</param>
    /// <param name="actividades">Actividades activas del empleado; las de otros empleados o fuera del período se ignoran.</param>
    /// <param name="feriados">Fechas de los feriados activos del período.</param>
    /// <param name="hoy">Fecha actual en Ecuador; los días posteriores no se cuentan.</param>
    /// <param name="jornadas">Historial de contratos del empleado; si es null se usa el contrato actual.</param>
    public ResultadoHorasFaltantes Calcular(
        TblAdministracionEmpleado empleado,
        DateOnly inicioPeriodo,
        DateOnly finPeriodo,
        IEnumerable<TblTimeReportActividadDiarium> actividades,
        ICollection<DateOnly> feriados,
        DateOnly hoy,
        IEnumerable<PeriodoJornada>? jornadas = null)
    {
        if (finPeriodo < inicioPeriodo)
            throw new ArgumentException("El fin del período no puede ser anterior a su inicio.", nameof(finPeriodo));

        var calculo = CalculoHorasPeriodo.Calcular(
            empleado, inicioPeriodo, finPeriodo, actividades, feriados, hoy, jornadas);

        return new ResultadoHorasFaltantes(
            empleado.Id,
            inicioPeriodo,
            finPeriodo,
            calculo.DiasLaborables,
            calculo.HorasEsperadas,
            calculo.HorasRegistradas,
            calculo.HorasPorRegistrar);
    }

    /// <summary>
    /// Período en curso según los dos cortes del mes: del 1 al 15 (quincena) o del 16 al último día (fin de mes).
    /// </summary>
    public static (DateOnly Inicio, DateOnly Fin) PeriodoEnCurso(DateOnly fecha)
    {
        if (fecha.Day <= 15)
            return (new DateOnly(fecha.Year, fecha.Month, 1), new DateOnly(fecha.Year, fecha.Month, 15));

        var ultimoDia = DateTime.DaysInMonth(fecha.Year, fecha.Month);
        return (new DateOnly(fecha.Year, fecha.Month, 16), new DateOnly(fecha.Year, fecha.Month, ultimoDia));
    }
}
