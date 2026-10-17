using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using tmr_backend.Features.Notificaciones.Application.Handlers;
using tmr_backend.Features.Notificaciones.Application.Queries;
using tmr_backend.Features.Notificaciones.Domain;
using tmr_backend.Features.Notificaciones.Domain.Events;
using tmr_backend.Features.TimeReport.Services;
using tmr_backend.Infrastructure.Messaging;

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
        // Quedan como omitidos del corte para el resumen que ven admin y líderes
        Assert.Equal(2, _bus.Omitidas.Count);
        Assert.All(_bus.Omitidas, o =>
        {
            Assert.Equal(OrigenNotificacion.Automatico, o.Origen);
            Assert.Equal(FechaCorte, o.FechaCorte);
            Assert.Contains("correo", o.Motivo);
        });
        Assert.Equal(16m, _bus.Omitidas[0].HorasFaltantes);
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
    public async Task Manual_registra_como_omitido_a_quien_esta_dentro_del_umbral()
    {
        _consulta.ConDeficit = [Empleado(1, 16m)];
        _consulta.UltimosAvisos[1] = DateTimeOffset.UtcNow.AddHours(-2);

        await CrearHandler(umbralHoras: 24).HandleAsync(new NotificacionManualSolicitada([1], Inicio, Fin, 7), default);

        var omitida = Assert.Single(_bus.Omitidas);
        Assert.Equal(1, omitida.IdEmpleado);
        Assert.Equal(7, omitida.IdUsuarioEjecutor);
        Assert.Contains("24", omitida.Motivo);
    }

    [Fact]
    public async Task Manual_registra_como_omitido_a_quien_no_debe_horas()
    {
        _consulta.ConDeficit = [Empleado(1, 16m)];

        await CrearHandler().HandleAsync(new NotificacionManualSolicitada([1, 2], Inicio, Fin, 7), default);

        var omitida = Assert.Single(_bus.Omitidas);
        Assert.Equal(2, omitida.IdEmpleado);
        Assert.Contains("horas pendientes", omitida.Motivo);
        Assert.Single(_bus.Publicados);
    }

    [Fact]
    public async Task Manual_registra_como_omitido_a_quien_no_tiene_correo()
    {
        _consulta.ConDeficit = [Empleado(1, 16m, email: null)];

        await CrearHandler().HandleAsync(new NotificacionManualSolicitada([1], Inicio, Fin, 7), default);

        var omitida = Assert.Single(_bus.Omitidas);
        Assert.Equal(1, omitida.IdEmpleado);
        Assert.Contains("correo", omitida.Motivo);
        Assert.Empty(_bus.Publicados);
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
    public async Task Manual_recorta_el_fin_del_periodo_a_hoy()
    {
        var hoy = CalculoHorasPeriodo.HoyEcuador();
        _consulta.ConDeficit = [Empleado(1, 16m)];

        await CrearHandler().HandleAsync(
            new NotificacionManualSolicitada([1], Inicio, hoy.AddDays(10), 7), default);

        Assert.Equal(hoy, _consulta.FinSolicitado);
        Assert.Equal(hoy, Assert.Single(_bus.Publicados).FinPeriodo);
    }

    [Fact]
    public async Task Manual_usa_el_rango_pedido_cuando_ya_paso()
    {
        _consulta.ConDeficit = [Empleado(1, 16m)];

        await CrearHandler().HandleAsync(new NotificacionManualSolicitada([1], Inicio, Fin, 7), default);

        Assert.Equal(Fin, _consulta.FinSolicitado);
        Assert.Equal(Fin, Assert.Single(_bus.Publicados).FinPeriodo);
    }

    [Theory]
    [InlineData("2026-10-15", "2026-10-11", "2026-10-11")]
    [InlineData("2026-10-11", "2026-10-11", "2026-10-11")]
    [InlineData("2026-10-05", "2026-10-11", "2026-10-05")]
    public void FinManualEfectivo_nunca_pasa_de_hoy(string fin, string hoy, string esperado) =>
        Assert.Equal(
            DateOnly.Parse(esperado),
            DetectarHorasFaltantesHandler.FinManualEfectivo(DateOnly.Parse(fin), DateOnly.Parse(hoy)));

    [Fact]
    public async Task Manual_sin_empleados_no_consulta_ni_publica()
    {
        await CrearHandler().HandleAsync(new NotificacionManualSolicitada([], Inicio, Fin, 7), default);

        Assert.Null(_consulta.IdsSolicitados);
        Assert.Empty(_bus.Publicados);
    }

    [Theory]
    [InlineData(null, 24, false)]
    [InlineData(2.0, 24, true)]
    [InlineData(23.9, 24, true)]
    [InlineData(24.0, 24, false)]
    [InlineData(30.0, 24, false)]
    [InlineData(1.0, 0, false)]
    public void DentroDelUmbral_compara_el_ultimo_aviso_con_el_umbral(double? horasDesdeUltimo, int umbral, bool esperado)
    {
        var ahora = new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);
        DateTimeOffset? ultimo = horasDesdeUltimo is null ? null : ahora.AddHours(-horasDesdeUltimo.Value);

        Assert.Equal(esperado, DetectarHorasFaltantesHandler.DentroDelUmbral(ultimo, ahora, umbral));
    }

    [Fact]
    public async Task Manual_por_proyecto_calcula_solo_ese_proyecto_e_incluye_a_quien_ya_salio()
    {
        _consulta.ConDeficit = [Empleado(1, 16m)];
        _consulta.NombresProyecto[40] = "Portal Web";

        await CrearHandler().HandleAsync(new NotificacionManualSolicitada([1], Inicio, Fin, 7, IdProyecto: 40), default);

        Assert.Equal(40, _consulta.ProyectoSolicitado);
        Assert.True(_consulta.IncluyoInactivos);
        var evento = Assert.Single(_bus.Publicados);
        Assert.Equal(40, evento.IdProyecto);
        Assert.Equal("Portal Web", evento.NombreProyecto);
    }

    [Fact]
    public async Task Manual_de_otro_proyecto_no_se_bloquea_por_el_umbral()
    {
        _consulta.ConDeficit = [Empleado(1, 16m)];
        _consulta.UltimosAvisos[1] = DateTimeOffset.UtcNow.AddHours(-1);
        _consulta.ProyectoDeLosAvisos = 40;   // el aviso de hace 1 h fue del proyecto 40

        await CrearHandler(umbralHoras: 24).HandleAsync(
            new NotificacionManualSolicitada([1], Inicio, Fin, 7, IdProyecto: 41), default);

        Assert.Single(_bus.Publicados);
        Assert.Empty(_bus.Omitidas);
    }

    [Fact]
    public async Task Manual_del_mismo_proyecto_respeta_el_umbral_y_lo_nombra_en_el_motivo()
    {
        _consulta.ConDeficit = [Empleado(1, 16m)];
        _consulta.UltimosAvisos[1] = DateTimeOffset.UtcNow.AddHours(-1);
        _consulta.ProyectoDeLosAvisos = 40;
        _consulta.NombresProyecto[40] = "Portal Web";

        await CrearHandler(umbralHoras: 24).HandleAsync(
            new NotificacionManualSolicitada([1], Inicio, Fin, 7, IdProyecto: 40), default);

        Assert.Empty(_bus.Publicados);
        var omitida = Assert.Single(_bus.Omitidas);
        Assert.Equal(40, omitida.IdProyecto);
        Assert.Contains("Portal Web", omitida.Motivo);
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
        public DateOnly? FinSolicitado { get; private set; }
        public int? ProyectoSolicitado { get; private set; }
        public bool? IncluyoInactivos { get; private set; }
        // Proyecto al que pertenecen los avisos de UltimosAvisos (el umbral es por proyecto)
        public int? ProyectoDeLosAvisos { get; set; }
        public Dictionary<int, string> NombresProyecto { get; } = new();

        public Task<IReadOnlyList<EmpleadoConHorasFaltantes>> CalcularAsync(
            DateOnly inicioPeriodo, DateOnly finPeriodo, IReadOnlyCollection<int>? idsEmpleados, CancellationToken ct,
            int? idProyecto = null, bool incluirInactivos = false)
        {
            FinSolicitado = finPeriodo;
            IdsSolicitados = idsEmpleados?.ToList();
            ProyectoSolicitado = idProyecto;
            IncluyoInactivos = incluirInactivos;
            IReadOnlyList<EmpleadoConHorasFaltantes> resultado = idsEmpleados is null
                ? ConDeficit
                : ConDeficit.Where(e => idsEmpleados.Contains(e.IdEmpleado)).ToList();
            return Task.FromResult(resultado);
        }

        // El corte usa el cálculo por proyecto; en estas pruebas devuelve la misma lista
        public Task<IReadOnlyList<EmpleadoConHorasFaltantes>> CalcularPorProyectoAsync(
            DateOnly inicioPeriodo, DateOnly finPeriodo, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<EmpleadoConHorasFaltantes>>(ConDeficit);

        public Task<PendientesNotificacionResponse> ObtenerPendientesAsync(CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<IReadOnlyDictionary<int, DateTimeOffset>> UltimosAvisosManualesAsync(
            IReadOnlyCollection<int> idsEmpleados, CancellationToken ct, int? idProyecto = null) =>
            Task.FromResult<IReadOnlyDictionary<int, DateTimeOffset>>(
                idProyecto == ProyectoDeLosAvisos ? UltimosAvisos : new Dictionary<int, DateTimeOffset>());

        public Task<string?> NombreProyectoAsync(int idProyecto, CancellationToken ct) =>
            Task.FromResult(NombresProyecto.TryGetValue(idProyecto, out var n) ? n : null);

        public Task<IReadOnlySet<int>> NotificadosEnCorteAsync(DateTimeOffset fechaCorte, CancellationToken ct) =>
            Task.FromResult<IReadOnlySet<int>>(NotificadosEnCorte);
    }

    private sealed class BusFalso : IEventBus
    {
        public List<HorasFaltantesDetectadas> Publicados { get; } = [];
        public List<NotificacionOmitida> Omitidas { get; } = [];

        public Task PublishAsync<T>(T evento, CancellationToken ct = default) where T : IEvento
        {
            if (evento is HorasFaltantesDetectadas detectado)
                Publicados.Add(detectado);
            if (evento is NotificacionOmitida omitida)
                Omitidas.Add(omitida);
            return Task.CompletedTask;
        }
    }
}
