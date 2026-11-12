using tmr_backend.Features.TimeReport.Services;
using tmr_backend.Infrastructure.Database.Entities;

namespace tmr_backend.Features.Notificaciones.Domain;

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

public sealed class CalculadorHorasFaltantes
{
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

    public static (DateOnly Inicio, DateOnly Fin) PeriodoEnCurso(DateOnly fecha)
    {
        if (fecha.Day <= 15)
            return (new DateOnly(fecha.Year, fecha.Month, 1), new DateOnly(fecha.Year, fecha.Month, 15));

        var ultimoDia = DateTime.DaysInMonth(fecha.Year, fecha.Month);
        return (new DateOnly(fecha.Year, fecha.Month, 16), new DateOnly(fecha.Year, fecha.Month, ultimoDia));
    }
}
