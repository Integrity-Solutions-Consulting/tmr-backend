using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using tmr_backend.Features.Notificaciones.Application.Handlers;
using tmr_backend.Features.Notificaciones.Application.Queries;
using tmr_backend.Features.Notificaciones.Domain;
using tmr_backend.Features.Notificaciones.Domain.Events;
using tmr_backend.Infrastructure.Database;
using tmr_backend.Infrastructure.Database.Entities;
using tmr_backend.Infrastructure.Messaging;

namespace tmr_backend.Tests.Notificaciones;

/// <summary>
/// Corte automático con la regla de Seguimiento: 8 h por proyecto, y ex empleados solo si salieron en el período.
/// </summary>
public class CorteProyectosTests
{
    // Lunes 7 a viernes 11 de septiembre de 2026: 5 días laborables, ya pasados
    private static readonly DateOnly Inicio = new(2026, 9, 7);
    private static readonly DateOnly Fin = new(2026, 9, 11);

    private readonly ApplicationDbContext _db = new(new DbContextOptionsBuilder<ApplicationDbContext>()
        // Sin chequeo de obligatorios: las entidades de prueba solo llevan lo que usa el cálculo
        .UseInMemoryDatabase(Guid.NewGuid().ToString(), o => o.EnableNullChecks(false))
        .Options);

    private ConsultaHorasFaltantes Consulta() => new(_db, new CalculadorHorasFaltantes());

    [Fact]
    public async Task Cada_proyecto_exige_su_jornada_aunque_el_total_del_dia_llegue_a_8_horas()
    {
        AgregarProyecto(10, "Portal Web");
        AgregarProyecto(11, "ERP");
        AgregarEmpleado(1, activo: true);
        Asignar(1, 10);
        Asignar(1, 11);
        RegistrarCadaDia(1, proyecto: 10, horas: 4m);   // 20 h de 40 en Portal Web
        RegistrarCadaDia(1, proyecto: 11, horas: 8m);   // completo en ERP
        await _db.SaveChangesAsync();

        var empleado = Assert.Single(await Consulta().CalcularPorProyectoAsync(Inicio, Fin, default));

        Assert.Equal(20m, empleado.Calculo.HorasFaltantes);
        var proyecto = Assert.Single(empleado.Proyectos!);
        Assert.Equal((10, "Portal Web", 20m), (proyecto.IdProyecto!.Value, proyecto.Nombre, proyecto.HorasFaltantes));
    }

    [Fact]
    public async Task Sin_proyecto_asignado_se_evalua_con_todas_sus_horas()
    {
        AgregarEmpleado(1, activo: true);
        RegistrarCadaDia(1, proyecto: null, horas: 6m);
        await _db.SaveChangesAsync();

        var empleado = Assert.Single(await Consulta().CalcularPorProyectoAsync(Inicio, Fin, default));

        Assert.Equal(10m, empleado.Calculo.HorasFaltantes);
        Assert.Empty(empleado.Proyectos!);
    }

    [Fact]
    public async Task Incluye_a_quien_salio_en_el_periodo_pero_no_a_quien_salio_antes()
    {
        AgregarEmpleado(1, activo: false, salida: new DateOnly(2026, 9, 9));   // salió el miércoles
        AgregarEmpleado(2, activo: false, salida: new DateOnly(2026, 8, 31));  // salió antes del corte
        await _db.SaveChangesAsync();

        var empleado = Assert.Single(await Consulta().CalcularPorProyectoAsync(Inicio, Fin, default));

        Assert.Equal(1, empleado.IdEmpleado);
        Assert.Equal(24m, empleado.Calculo.HorasFaltantes);   // lunes a miércoles
    }

    [Fact]
    public async Task El_resumen_identifica_la_ultima_ejecucion_del_corte()
    {
        var corte = DateTimeOffset.UtcNow.AddHours(-2);
        var primera = DateTimeOffset.UtcNow.AddHours(-2);
        var segunda = DateTimeOffset.UtcNow.AddMinutes(-5);   // el mismo corte, re-ejecutado
        DisparoProcesado(corte, primera);
        DisparoProcesado(corte, segunda);
        DisparoProcesado(corte.AddDays(-15), DateTimeOffset.UtcNow.AddMinutes(-1));   // otro corte
        _db.NotificacionesEnvio.Add(new NotificacionEnvio
        {
            IdEmpleado = 1, InicioPeriodo = Inicio, FinPeriodo = Fin, FechaCorte = corte.ToUniversalTime(),
            Origen = OrigenNotificacion.Automatico, Estado = EstadoEnvio.Enviado, FechaEnvio = corte
        });
        await _db.SaveChangesAsync();

        var resumen = await new ConsultaResumenCorte(_db).UltimoCorteAsync(default);

        Assert.NotNull(resumen);
        Assert.Equal(segunda, resumen.Ejecucion);
    }

    [Fact]
    public async Task Un_aviso_manual_que_agoto_sus_intentos_queda_fallido_para_cada_colaborador()
    {
        var bus = new BusFalso();

        await new FalloDefinitivoHandler(bus).HandleFalloDefinitivoAsync(
            new NotificacionManualSolicitada([3, 4, 3], Inicio, Fin, 7, IdProyecto: 10), "BD caída", default);

        Assert.Equal(new[] { 3, 4 }, bus.Fallidas.Select(f => f.IdEmpleado));
        Assert.All(bus.Fallidas, f =>
        {
            Assert.Equal(10, f.IdProyecto);
            Assert.Equal(7, f.IdUsuarioEjecutor);
            Assert.Contains("BD caída", f.Error);
        });
    }

    private void AgregarProyecto(int id, string nombre) =>
        _db.TblTimeReportProyectos.Add(new TblTimeReportProyecto { Id = id, Nombre = nombre });

    private void AgregarEmpleado(int id, bool activo, DateOnly? salida = null) =>
        _db.TblAdministracionEmpleados.Add(new TblAdministracionEmpleado
        {
            Id = id,
            Activo = activo,
            Fechaterminacion = salida,
            IdpersonaNavigation = new TblAdministracionPersona { Id = 100 + id, Nombres = $"Empleado {id}", Apellidos = "Prueba" }
        });

    private void Asignar(int idEmpleado, int idProyecto) =>
        _db.TblTimeReportAsignacionProyectos.Add(new TblTimeReportAsignacionProyecto
        {
            Idempleado = idEmpleado, Idproyecto = idProyecto, Activo = true
        });

    private void RegistrarCadaDia(int idEmpleado, int? proyecto, decimal horas)
    {
        for (var dia = Inicio; dia <= Fin; dia = dia.AddDays(1))
        {
            _db.TblTimeReportActividadDiaria.Add(new TblTimeReportActividadDiarium
            {
                Idempleado = idEmpleado, Idproyecto = proyecto, Fechaactividad = dia, Cantidadhoras = horas, Activo = true
            });
        }
    }

    private void DisparoProcesado(DateTimeOffset corte, DateTimeOffset creado) =>
        _db.EventosOutbox.Add(new EventoOutbox
        {
            Id = Guid.NewGuid(),
            TipoEvento = nameof(CorteDeHorasAlcanzado),
            Payload = JsonSerializer.Serialize(new CorteDeHorasAlcanzado(Inicio, Fin, corte)),
            Estado = EstadoEvento.Procesado,
            FechaCreacion = creado
        });

    private sealed class BusFalso : IEventBus
    {
        public List<NotificacionFallida> Fallidas { get; } = [];

        public Task PublishAsync<T>(T evento, CancellationToken ct = default) where T : IEvento
        {
            if (evento is NotificacionFallida f)
                Fallidas.Add(f);
            return Task.CompletedTask;
        }
    }
}
