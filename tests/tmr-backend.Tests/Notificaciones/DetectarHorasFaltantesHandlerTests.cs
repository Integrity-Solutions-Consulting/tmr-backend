using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TmrBackend.Features.Notificaciones.Application.Handlers;
using TmrBackend.Features.Notificaciones.Application.Queries;
using TmrBackend.Features.Notificaciones.Domain;
using TmrBackend.Features.Notificaciones.Domain.Events;
using TmrBackend.Infrastructure.Messaging;

namespace tmr_backend.Tests.Notificaciones;

public class DetectarHorasFaltantesHandlerTests
{
    private static readonly DateOnly Inicio = new(2026, 10, 1);
    private static readonly DateOnly Fin = new(2026, 10, 15);
    private static readonly DateTimeOffset FechaCorte = new(2026, 10, 15, 18, 0, 0, TimeSpan.FromHours(-5));

    private readonly ConsultaFalsa _consulta = new();
    private readonly BusFalso _bus = new();

    private DetectarHorasFaltantesHandler CrearHandler(int umbralHoras = 24) => new(
        _consulta,
        _bus,
        Options.Create(new NotificacionHorasOptions { UmbralNotificacionManual = umbralHoras }),
        NullLogger<DetectarHorasFaltantesHandler>.Instance);

    [Fact]
    public async Task Corte_publica_un_evento_automatico_por_cada_empleado_con_deficit()
    {
        _consulta.ConDeficit = [Empleado(1, 16m), Empleado(2, 8m)];

        await CrearHandler().HandleAsync(new CorteDeHorasAlcanzado(Inicio, Fin, FechaCorte), default);

        Assert.Equal(2, _bus.Publicados.Count);
        var primero = _bus.Publicados[0];
        Assert.Equal(1, primero.IdEmpleado);
        Assert.Equal(16m, primero.HorasFaltantes);
        Assert.Equal(OrigenNotificacion.Automatico, primero.Origen);
        Assert.Equal(FechaCorte, primero.FechaCorte);
        Assert.Null(primero.IdUsuarioEjecutor);
        Assert.Equal(Inicio, primero.InicioPeriodo);
        Assert.Equal(Fin, primero.FinPeriodo);
    }

    [Fact]
    public async Task Corte_descarta_a_quien_ya_fue_notificado_en_ese_corte()
    {
        _consulta.ConDeficit = [Empleado(1, 16m), Empleado(2, 8m)];
        _consulta.NotificadosEnCorte = [1];

        await CrearHandler().HandleAsync(new CorteDeHorasAlcanzado(Inicio, Fin, FechaCorte), default);

        Assert.Equal(new[] { 2 }, _bus.Publicados.Select(e => e.IdEmpleado));
    }

    [Fact]
    public async Task Omite_a_los_empleados_sin_correo()
    {
        _consulta.ConDeficit = [Empleado(1, 16m, email: null), Empleado(2, 8m, email: " ")];

        await CrearHandler().HandleAsync(new CorteDeHorasAlcanzado(Inicio, Fin, FechaCorte), default);

        Assert.Empty(_bus.Publicados);
    }

    [Fact]
    public async Task Manual_publica_con_origen_manual_y_el_lider_que_lo_pidio()
    {
        _consulta.ConDeficit = [Empleado(1, 16m)];

        await CrearHandler().HandleAsync(new NotificacionManualSolicitada([1], Inicio, Fin, IdUsuarioEjecutor: 7), default);

        var evento = Assert.Single(_bus.Publicados);
        Assert.Equal(OrigenNotificacion.Manual, evento.Origen);
        Assert.Null(evento.FechaCorte);
        Assert.Equal(7, evento.IdUsuarioEjecutor);
    }

    [Fact]
    public async Task Manual_solo_evalua_a_los_empleados_elegidos()
    {
        await CrearHandler().HandleAsync(new NotificacionManualSolicitada([3, 1, 3], Inicio, Fin, 7), default);

        Assert.Equal(new[] { 3, 1 }, _consulta.IdsSolicitados);
    }

    [Fact]
    public async Task Manual_omite_a_quien_recibio_un_aviso_manual_dentro_del_umbral()
    {
        _consulta.ConDeficit = [Empleado(1, 16m), Empleado(2, 8m)];
        _consulta.UltimosAvisos[1] = DateTimeOffset.UtcNow.AddHours(-2);
        _consulta.UltimosAvisos[2] = DateTimeOffset.UtcNow.AddHours(-30);

        await CrearHandler(umbralHoras: 24).HandleAsync(new NotificacionManualSolicitada([1, 2], Inicio, Fin, 7), default);

        Assert.Equal(new[] { 2 }, _bus.Publicados.Select(e => e.IdEmpleado));
    }

    [Fact]
    public async Task Manual_no_se_bloquea_por_el_aviso_automatico_del_corte()
    {
        _consulta.ConDeficit = [Empleado(1, 16m)];
        _consulta.NotificadosEnCorte = [1];

        await CrearHandler().HandleAsync(new NotificacionManualSolicitada([1], Inicio, Fin, 7), default);

        Assert.Single(_bus.Publicados);
    }

    [Fact]
    public async Task Manual_sin_empleados_no_consulta_ni_publica()
    {
        await CrearHandler().HandleAsync(new NotificacionManualSolicitada([], Inicio, Fin, 7), default);

        Assert.Null(_consulta.IdsSolicitados);
        Assert.Empty(_bus.Publicados);
    }

    [Theory]
    [InlineData(null, 24, false)]  // nunca recibió aviso manual
    [InlineData(2.0, 24, true)]    // hace 2 h
    [InlineData(23.9, 24, true)]
    [InlineData(24.0, 24, false)]  // justo en el umbral ya se puede volver a enviar
    [InlineData(30.0, 24, false)]
    [InlineData(1.0, 0, false)]    // umbral 0 desactiva la restricción
    public void DentroDelUmbral_compara_el_ultimo_aviso_con_el_umbral(double? horasDesdeUltimo, int umbral, bool esperado)
    {
        var ahora = new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);
        DateTimeOffset? ultimo = horasDesdeUltimo is null ? null : ahora.AddHours(-horasDesdeUltimo.Value);

        Assert.Equal(esperado, DetectarHorasFaltantesHandler.DentroDelUmbral(ultimo, ahora, umbral));
    }

    private static EmpleadoConHorasFaltantes Empleado(int id, decimal horasFaltantes, string? email = "empleado@isc.com") =>
        new(id, $"Empleado {id}", email,
            new ResultadoHorasFaltantes(id, Inicio, Fin, 11, 88m, 88m - horasFaltantes, horasFaltantes));

    private sealed class ConsultaFalsa : IConsultaHorasFaltantes
    {
        public List<EmpleadoConHorasFaltantes> ConDeficit { get; set; } = [];
        public HashSet<int> NotificadosEnCorte { get; set; } = [];
        public Dictionary<int, DateTimeOffset> UltimosAvisos { get; } = new();
        public List<int>? IdsSolicitados { get; private set; }

        public Task<IReadOnlyList<EmpleadoConHorasFaltantes>> CalcularAsync(
            DateOnly inicioPeriodo, DateOnly finPeriodo, IReadOnlyCollection<int>? idsEmpleados, CancellationToken ct)
        {
            IdsSolicitados = idsEmpleados?.ToList();
            IReadOnlyList<EmpleadoConHorasFaltantes> resultado = idsEmpleados is null
                ? ConDeficit
                : ConDeficit.Where(e => idsEmpleados.Contains(e.IdEmpleado)).ToList();
            return Task.FromResult(resultado);
        }

        public Task<PendientesNotificacionResponse> ObtenerPendientesAsync(CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<IReadOnlyDictionary<int, DateTimeOffset>> UltimosAvisosManualesAsync(
            IReadOnlyCollection<int> idsEmpleados, CancellationToken ct) =>
            Task.FromResult<IReadOnlyDictionary<int, DateTimeOffset>>(UltimosAvisos);

        public Task<IReadOnlySet<int>> NotificadosEnCorteAsync(DateTimeOffset fechaCorte, CancellationToken ct) =>
            Task.FromResult<IReadOnlySet<int>>(NotificadosEnCorte);
    }

    private sealed class BusFalso : IEventBus
    {
        public List<HorasFaltantesDetectadas> Publicados { get; } = [];

        public Task PublishAsync<T>(T evento, CancellationToken ct = default) where T : IEvento
        {
            if (evento is HorasFaltantesDetectadas detectado)
                Publicados.Add(detectado);
            return Task.CompletedTask;
        }
    }
}
