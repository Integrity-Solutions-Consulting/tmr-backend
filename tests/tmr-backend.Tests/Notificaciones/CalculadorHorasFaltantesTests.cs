using TmrBackend.Features.Notificaciones.Domain;
using tmr_backend.Features.TimeReport.Services;
using tmr_backend.Infrastructure.Database.Entities;

namespace tmr_backend.Tests.Notificaciones;

public class CalculadorHorasFaltantesTests
{
    // Semana de referencia: lunes 5 a domingo 11 de octubre de 2026 (5 días laborables).
    private static readonly DateOnly Lunes = new(2026, 10, 5);
    private static readonly DateOnly Viernes = new(2026, 10, 9);
    private static readonly DateOnly Domingo = new(2026, 10, 11);
    private static readonly DateOnly HoyPosterior = new(2026, 10, 20);

    private readonly CalculadorHorasFaltantes _calculador = new();

    [Fact]
    public void Sin_actividades_debe_toda_la_jornada_de_los_dias_laborables()
    {
        var resultado = _calculador.Calcular(Empleado(), Lunes, Domingo, [], [], HoyPosterior);

        Assert.Equal(5, resultado.DiasLaborables);
        Assert.Equal(40m, resultado.HorasEsperadas);
        Assert.Equal(0m, resultado.HorasRegistradas);
        Assert.Equal(40m, resultado.HorasFaltantes);
        Assert.True(resultado.DebeHoras);
    }

    [Fact]
    public void Con_la_jornada_completa_no_debe_horas()
    {
        var actividades = DiasLaborables(Lunes, Viernes).Select(d => Actividad(d, 8m));

        var resultado = _calculador.Calcular(Empleado(), Lunes, Domingo, actividades, [], HoyPosterior);

        Assert.Equal(0m, resultado.HorasFaltantes);
        Assert.False(resultado.DebeHoras);
    }

    [Fact]
    public void Calcula_la_diferencia_cuando_registra_horas_parciales()
    {
        var actividades = new[]
        {
            Actividad(Lunes, 8m),
            Actividad(Lunes.AddDays(1), 4m),
            Actividad(Lunes.AddDays(1), 2m), // dos actividades el mismo día se suman
            Actividad(Lunes.AddDays(2), 8m),
        };

        var resultado = _calculador.Calcular(Empleado(), Lunes, Domingo, actividades, [], HoyPosterior);

        Assert.Equal(22m, resultado.HorasRegistradas);
        Assert.Equal(18m, resultado.HorasFaltantes);
    }

    [Fact]
    public void Los_fines_de_semana_no_cuentan_como_esperados_ni_como_registrados()
    {
        var actividades = new[]
        {
            Actividad(new DateOnly(2026, 10, 10), 8m), // sábado
            Actividad(Domingo, 8m),
        };

        var resultado = _calculador.Calcular(Empleado(), Lunes, Domingo, actividades, [], HoyPosterior);

        Assert.Equal(40m, resultado.HorasEsperadas);
        Assert.Equal(0m, resultado.HorasRegistradas);
        Assert.Equal(40m, resultado.HorasFaltantes);
    }

    [Fact]
    public void Los_feriados_se_excluyen_del_calculo()
    {
        var feriado = Lunes.AddDays(2); // miércoles
        var actividades = DiasLaborables(Lunes, Viernes).Where(d => d != feriado).Select(d => Actividad(d, 8m));

        var resultado = _calculador.Calcular(Empleado(), Lunes, Domingo, actividades, [feriado], HoyPosterior);

        Assert.Equal(4, resultado.DiasLaborables);
        Assert.Equal(32m, resultado.HorasEsperadas);
        Assert.Equal(0m, resultado.HorasFaltantes);
    }

    [Fact]
    public void Un_pasante_tiene_jornada_de_seis_horas()
    {
        var actividades = DiasLaborables(Lunes, Viernes).Select(d => Actividad(d, 6m));

        var resultado = _calculador.Calcular(Empleado(tipoContrato: "PAS"), Lunes, Domingo, actividades, [], HoyPosterior);

        Assert.Equal(30m, resultado.HorasEsperadas);
        Assert.Equal(0m, resultado.HorasFaltantes);
    }

    [Fact]
    public void Usa_la_jornada_del_contrato_vigente_cada_dia()
    {
        // Pasante de lunes a miércoles (6 h) y contrato completo desde el jueves (8 h).
        var jornadas = new[]
        {
            new PeriodoJornada(new DateOnly(2026, 1, 1), Lunes.AddDays(2), 6m),
            new PeriodoJornada(Lunes.AddDays(3), null, 8m),
        };

        var resultado = _calculador.Calcular(Empleado(), Lunes, Domingo, [], [], HoyPosterior, jornadas);

        Assert.Equal(3 * 6m + 2 * 8m, resultado.HorasEsperadas);
    }

    [Fact]
    public void Solo_cuenta_desde_la_fecha_de_ingreso()
    {
        var empleado = Empleado(fechaIngreso: Lunes.AddDays(3)); // ingresa el jueves

        var resultado = _calculador.Calcular(empleado, Lunes, Domingo, [], [], HoyPosterior);

        Assert.Equal(2, resultado.DiasLaborables);
        Assert.Equal(16m, resultado.HorasFaltantes);
    }

    [Fact]
    public void Solo_cuenta_hasta_la_fecha_de_salida()
    {
        var empleado = Empleado(fechaTerminacion: Lunes.AddDays(1)); // sale el martes

        var resultado = _calculador.Calcular(empleado, Lunes, Domingo, [], [], HoyPosterior);

        Assert.Equal(2, resultado.DiasLaborables);
        Assert.Equal(16m, resultado.HorasFaltantes);
    }

    [Fact]
    public void No_cuenta_los_dias_posteriores_a_hoy()
    {
        var hoy = Lunes.AddDays(2); // miércoles

        var resultado = _calculador.Calcular(Empleado(), Lunes, Domingo, [], [], hoy);

        Assert.Equal(3, resultado.DiasLaborables);
        Assert.Equal(24m, resultado.HorasFaltantes);
    }

    [Fact]
    public void Ignora_actividades_de_otros_empleados_y_fuera_del_periodo()
    {
        var actividades = new[]
        {
            Actividad(Lunes, 8m, idEmpleado: 99),
            Actividad(Lunes.AddDays(-3), 8m), // viernes de la semana anterior
            Actividad(Lunes.AddDays(14), 8m),
        };

        var resultado = _calculador.Calcular(Empleado(), Lunes, Domingo, actividades, [], HoyPosterior);

        Assert.Equal(0m, resultado.HorasRegistradas);
        Assert.Equal(40m, resultado.HorasFaltantes);
    }

    [Fact]
    public void Las_horas_extra_de_un_dia_compensan_las_de_otro()
    {
        // Misma regla que Time Report: el faltante es el total esperado menos el total registrado.
        var actividades = new[]
        {
            Actividad(Lunes, 12m),
            Actividad(Lunes.AddDays(1), 4m),
            Actividad(Lunes.AddDays(2), 8m),
            Actividad(Lunes.AddDays(3), 8m),
            Actividad(Viernes, 8m),
        };

        var resultado = _calculador.Calcular(Empleado(), Lunes, Domingo, actividades, [], HoyPosterior);

        Assert.Equal(0m, resultado.HorasFaltantes);
    }

    [Fact]
    public void Un_periodo_sin_dias_laborables_no_debe_horas()
    {
        var sabado = new DateOnly(2026, 10, 10);

        var resultado = _calculador.Calcular(Empleado(), sabado, Domingo, [], [], HoyPosterior);

        Assert.Equal(0, resultado.DiasLaborables);
        Assert.False(resultado.DebeHoras);
    }

    [Fact]
    public void Rechaza_un_periodo_con_fin_anterior_al_inicio()
    {
        Assert.Throws<ArgumentException>(() =>
            _calculador.Calcular(Empleado(), Domingo, Lunes, [], [], HoyPosterior));
    }

    [Theory]
    [InlineData("2026-10-01", "2026-10-01", "2026-10-15")]
    [InlineData("2026-10-15", "2026-10-01", "2026-10-15")]
    [InlineData("2026-10-16", "2026-10-16", "2026-10-31")]
    [InlineData("2026-11-30", "2026-11-16", "2026-11-30")]
    [InlineData("2026-02-20", "2026-02-16", "2026-02-28")]
    [InlineData("2028-02-29", "2028-02-16", "2028-02-29")]
    public void PeriodoEnCurso_devuelve_la_quincena_o_el_fin_de_mes(string fecha, string inicio, string fin)
    {
        var (inicioPeriodo, finPeriodo) = CalculadorHorasFaltantes.PeriodoEnCurso(DateOnly.Parse(fecha));

        Assert.Equal(DateOnly.Parse(inicio), inicioPeriodo);
        Assert.Equal(DateOnly.Parse(fin), finPeriodo);
    }

    private static IEnumerable<DateOnly> DiasLaborables(DateOnly desde, DateOnly hasta)
    {
        for (var dia = desde; dia <= hasta; dia = dia.AddDays(1))
            if (dia.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
                yield return dia;
    }

    private static TblAdministracionEmpleado Empleado(
        string tipoContrato = "IND",
        DateOnly? fechaIngreso = null,
        DateOnly? fechaTerminacion = null) => new()
    {
        Id = 1,
        Codigoempleado = "EMP001",
        Activo = true,
        Fechaingreso = fechaIngreso,
        Fechaterminacion = fechaTerminacion,
        IdtipocontratoNavigation = new TblAdministracionCatalogoDetalle { Codigovalor = tipoContrato, Valor = tipoContrato },
    };

    private static TblTimeReportActividadDiarium Actividad(DateOnly fecha, decimal horas, int idEmpleado = 1) => new()
    {
        Idempleado = idEmpleado,
        Fechaactividad = fecha,
        Cantidadhoras = horas,
        Descripcionactividad = "Prueba",
        Activo = true,
    };
}
