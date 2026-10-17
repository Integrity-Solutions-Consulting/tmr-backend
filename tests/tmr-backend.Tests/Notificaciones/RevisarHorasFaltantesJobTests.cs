using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Quartz;
using tmr_backend.Features.Notificaciones.Domain;
using tmr_backend.Features.Notificaciones.Domain.Events;
using tmr_backend.Features.Notificaciones.Infrastructure.Jobs;
using tmr_backend.Infrastructure.Messaging;

namespace tmr_backend.Tests.Notificaciones;

public class RevisarHorasFaltantesJobTests
{
    private static readonly TimeSpan Ecuador = TimeSpan.FromHours(-5);

    [Fact]
    public async Task Cuando_deshabilitado_no_publica_eventos()
    {
        var bus = new BusFalso();
        var job = CrearJob(bus, habilitado: false);

        await job.Execute(null!, default);

        Assert.Empty(bus.Publicados);
    }

    [Fact]
    public async Task Corte_de_quincena_publica_el_periodo_del_1_al_15()
    {
        var bus = new BusFalso();
        var job = CrearJob(bus, habilitado: true);
        var fechaCorte = new DateTimeOffset(2026, 10, 15, 18, 0, 0, Ecuador);

        await job.PublicarCorteAsync(fechaCorte, default);

        var evento = Assert.Single(bus.Publicados.OfType<CorteDeHorasAlcanzado>());
        Assert.Equal(new DateOnly(2026, 10, 1), evento.InicioPeriodo);
        Assert.Equal(new DateOnly(2026, 10, 15), evento.FinPeriodo);
        Assert.Equal(fechaCorte, evento.FechaCorte);
    }

    [Fact]
    public async Task Corte_de_fin_de_mes_publica_el_periodo_del_16_al_ultimo_dia()
    {
        var bus = new BusFalso();
        var job = CrearJob(bus, habilitado: true);
        // Así llega del cron: 23:00 UTC = 18:00 Ecuador del mismo día
        var fechaCorte = new DateTimeOffset(2026, 10, 31, 23, 0, 0, TimeSpan.Zero);

        await job.PublicarCorteAsync(fechaCorte, default);

        var evento = Assert.Single(bus.Publicados.OfType<CorteDeHorasAlcanzado>());
        Assert.Equal(new DateOnly(2026, 10, 16), evento.InicioPeriodo);
        Assert.Equal(new DateOnly(2026, 10, 31), evento.FinPeriodo);
        Assert.Equal(fechaCorte, evento.FechaCorte);
    }

    [Theory]
    [InlineData(2026, 2, 28)]
    [InlineData(2028, 2, 29)]
    [InlineData(2026, 4, 30)]
    public void PeriodoDelCorte_usa_el_ultimo_dia_real_del_mes(int anio, int mes, int ultimoDia)
    {
        var (inicio, fin) = RevisarHorasFaltantesJob.PeriodoDelCorte(
            new DateTimeOffset(anio, mes, ultimoDia, 18, 0, 0, Ecuador));

        Assert.Equal(new DateOnly(anio, mes, 16), inicio);
        Assert.Equal(new DateOnly(anio, mes, ultimoDia), fin);
    }

    [Theory]
    // ahora (hora Ecuador)       → último corte esperado
    [InlineData("2026-10-15T17:59:00-05:00", "2026-09-30T18:00:00-05:00")]
    [InlineData("2026-10-15T18:00:00-05:00", "2026-10-15T18:00:00-05:00")]
    [InlineData("2026-10-20T09:00:00-05:00", "2026-10-15T18:00:00-05:00")]
    [InlineData("2026-11-01T08:00:00-05:00", "2026-10-31T18:00:00-05:00")]
    [InlineData("2027-01-03T10:00:00-05:00", "2026-12-31T18:00:00-05:00")]
    public void UltimoCorte_devuelve_el_corte_mas_reciente(string ahora, string esperado)
    {
        Assert.Equal(DateTimeOffset.Parse(esperado), RevisarHorasFaltantesJob.UltimoCorte(DateTimeOffset.Parse(ahora)));
    }

    [Theory]
    [InlineData("2026-10-15T17:59:00-05:00", "2026-10-15T18:00:00-05:00")]
    [InlineData("2026-10-15T18:00:00-05:00", "2026-10-31T18:00:00-05:00")]
    [InlineData("2026-12-31T19:00:00-05:00", "2027-01-15T18:00:00-05:00")]
    public void ProximoCorte_devuelve_el_siguiente_corte(string ahora, string esperado)
    {
        Assert.Equal(DateTimeOffset.Parse(esperado), RevisarHorasFaltantesJob.ProximoCorte(DateTimeOffset.Parse(ahora)));
    }

    [Theory]
    [InlineData(RevisarHorasFaltantesJob.CronQuincena, "2026-10-01T00:00:00Z", "2026-10-15T23:00:00Z")]
    [InlineData(RevisarHorasFaltantesJob.CronFinDeMes, "2026-10-16T00:00:00Z", "2026-10-31T23:00:00Z")]
    [InlineData(RevisarHorasFaltantesJob.CronFinDeMes, "2026-11-01T00:00:00Z", "2026-11-30T23:00:00Z")]
    [InlineData(RevisarHorasFaltantesJob.CronFinDeMes, "2027-02-01T00:00:00Z", "2027-02-28T23:00:00Z")]
    public void Los_cron_se_disparan_el_15_y_el_ultimo_dia_a_las_18_de_Ecuador(string cron, string desde, string esperado)
    {
        var expresion = new CronExpression(cron, TimeZoneInfo.Utc);

        var siguiente = expresion.GetNextValidTimeAfter(DateTimeOffset.Parse(desde));

        Assert.Equal(DateTimeOffset.Parse(esperado), siguiente);
    }

    private static RevisarHorasFaltantesJob CrearJob(IEventBus bus, bool habilitado) =>
        new(bus,
            Options.Create(new NotificacionHorasOptions { Habilitado = habilitado }),
            NullLogger<RevisarHorasFaltantesJob>.Instance);

    private sealed class BusFalso : IEventBus
    {
        public List<IEvento> Publicados { get; } = [];

        public Task PublishAsync<T>(T evento, CancellationToken ct = default) where T : IEvento
        {
            Publicados.Add(evento);
            return Task.CompletedTask;
        }
    }
}
