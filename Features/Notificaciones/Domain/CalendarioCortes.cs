namespace tmr_backend.Features.Notificaciones.Domain;

/// <summary>
/// Calendario de los cortes de horas (flujos 1 y 3), sin BD ni Quartz.
/// Cada mes tiene dos cortes: "Quincena" (período 1–15) y "FinDeMes" (16–último día).
/// Por defecto se ejecutan el día 15 y el último día del mes a las 18:00 de Ecuador; quien tenga
/// el permiso NOTIFICACIONES_CONFIGURAR puede moverlos desde la pantalla Fechas de Corte.
/// </summary>
public static class CalendarioCortes
{
    public const string Quincena = "Quincena";
    public const string FinDeMes = "FinDeMes";

    // Ecuador: UTC−5, sin horario de verano
    public static readonly TimeSpan OffsetEcuador = TimeSpan.FromHours(-5);
    public const int HoraPorDefecto = 18;

    /// <summary>Hasta cuántos días después del fin natural de su período se puede retrasar un corte.</summary>
    public const int DiasMaximosDeRetraso = 10;

    /// <summary>
    /// Un corte vencido se ejecuta solo si pasó hace menos de esto (p. ej. el backend estaba apagado a esa hora).
    /// Uno más viejo queda como "No ejecutado" para no mandar correos de un período ya cerrado.
    /// </summary>
    public static readonly TimeSpan VentanaEjecucion = TimeSpan.FromHours(24);

    public readonly record struct ClaveCorte(int Anio, int Mes, string Tipo)
    {
        public static ClaveCorte De(NotificacionFechaCorte corte) => new(corte.Anio, corte.Mes, corte.Tipo);

        public ClaveCorte Siguiente()
        {
            if (Tipo == Quincena)
                return this with { Tipo = FinDeMes };
            var mes = new DateOnly(Anio, Mes, 1).AddMonths(1);
            return new ClaveCorte(mes.Year, mes.Month, Quincena);
        }

        public ClaveCorte Anterior()
        {
            if (Tipo == FinDeMes)
                return this with { Tipo = Quincena };
            var mes = new DateOnly(Anio, Mes, 1).AddMonths(-1);
            return new ClaveCorte(mes.Year, mes.Month, FinDeMes);
        }

        /// <summary>1–15 para la quincena; 16–último día para fin de mes.</summary>
        public (DateOnly Inicio, DateOnly Fin) PeriodoNatural() => Tipo == Quincena
            ? (new DateOnly(Anio, Mes, 1), new DateOnly(Anio, Mes, 15))
            : (new DateOnly(Anio, Mes, 16), new DateOnly(Anio, Mes, DateTime.DaysInMonth(Anio, Mes)));

        /// <summary>Día 15 o último día del mes a las 18:00 de Ecuador.</summary>
        public DateTimeOffset FechaPorDefecto()
        {
            var dia = PeriodoNatural().Fin;
            return new DateTimeOffset(dia.Year, dia.Month, dia.Day, HoraPorDefecto, 0, 0, OffsetEcuador);
        }

        /// <summary>Último día al que se puede retrasar el corte.</summary>
        public DateOnly DiaMaximo() => PeriodoNatural().Fin.AddDays(DiasMaximosDeRetraso);

        /// <summary>Para ordenar: quincena antes que fin de mes.</summary>
        public int Orden => (Anio * 12 + Mes) * 2 + (Tipo == Quincena ? 0 : 1);
    }

    /// <summary>Los cortes de <paramref name="meses"/> meses seguidos, en orden, desde el mes indicado.</summary>
    public static IEnumerable<ClaveCorte> Secuencia(DateOnly primerMes, int meses)
    {
        for (var i = 0; i < meses; i++)
        {
            var mes = primerMes.AddMonths(i);
            yield return new ClaveCorte(mes.Year, mes.Month, Quincena);
            yield return new ClaveCorte(mes.Year, mes.Month, FinDeMes);
        }
    }

    public static DateOnly DiaEcuador(DateTimeOffset fecha) =>
        DateOnly.FromDateTime(fecha.ToOffset(OffsetEcuador).DateTime);

    /// <summary>
    /// Fin del período que revisa el corte: el natural, salvo que el corte se adelante; entonces termina
    /// el día del corte, para no pedir horas de días que todavía no pasan. El período siguiente empieza
    /// al día siguiente, así ningún día queda sin revisar.
    /// </summary>
    public static DateOnly FinEfectivo(ClaveCorte clave, DateTimeOffset fechaCorte)
    {
        var finNatural = clave.PeriodoNatural().Fin;
        var dia = DiaEcuador(fechaCorte);
        return dia < finNatural ? dia : finNatural;
    }

    /// <summary>
    /// Período natural según el día del corte (1–15 o 16–fin). Solo es un respaldo para cortes
    /// que no están en notificacion_fecha_corte; el período real se guarda en cada fila.
    /// </summary>
    public static (DateOnly Inicio, DateOnly Fin) PeriodoDelCorte(DateTimeOffset fechaCorte) =>
        CalculadorHorasFaltantes.PeriodoEnCurso(DiaEcuador(fechaCorte));
}
