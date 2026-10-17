using Microsoft.EntityFrameworkCore;
using tmr_backend.Features.Notificaciones.Application.Queries;
using tmr_backend.Features.Notificaciones.Domain;
using tmr_backend.Infrastructure.Database;
using tmr_backend.Infrastructure.Database.Entities;

namespace tmr_backend.Tests.Notificaciones;

public class ConsultaHorasFaltantesTests
{
    private static readonly DateOnly Inicio = new(2026, 10, 1);
    private static readonly DateOnly Fin = new(2026, 10, 15);
    private static readonly DateTimeOffset FechaCorte = new(2026, 10, 15, 18, 0, 0, TimeSpan.Zero);

    private readonly ApplicationDbContext _db;
    private readonly ConsultaHorasFaltantes _consulta;

    public ConsultaHorasFaltantesTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new ApplicationDbContext(options);
        _consulta = new ConsultaHorasFaltantes(_db, new CalculadorHorasFaltantes());
    }

    [Fact]
    public async Task NotificadosEnCorteAsync_devuelve_solo_los_notificados_automaticamente_en_ese_corte()
    {
        _db.NotificacionesEnvio.AddRange(
            new NotificacionEnvio
            {
                IdEmpleado = 1,
                InicioPeriodo = Inicio,
                FinPeriodo = Fin,
                FechaCorte = FechaCorte.ToUniversalTime(),
                Origen = OrigenNotificacion.Automatico,
                Estado = EstadoEnvio.Enviado,
                HorasFaltantes = 10m,
                FechaEnvio = FechaCorte
            },
            new NotificacionEnvio
            {
                IdEmpleado = 2,
                InicioPeriodo = Inicio,
                FinPeriodo = Fin,
                FechaCorte = FechaCorte.ToUniversalTime(),
                Origen = OrigenNotificacion.Automatico,
                Estado = EstadoEnvio.Fallido, // Fallido no cuenta como notificado
                HorasFaltantes = 5m,
                FechaEnvio = FechaCorte
            },
            new NotificacionEnvio
            {
                IdEmpleado = 3,
                InicioPeriodo = Inicio,
                FinPeriodo = Fin,
                FechaCorte = null,
                Origen = OrigenNotificacion.Manual, // Manual no cuenta como corte
                Estado = EstadoEnvio.Enviado,
                HorasFaltantes = 8m,
                FechaEnvio = FechaCorte
            }
        );
        await _db.SaveChangesAsync();

        var notificados = await _consulta.NotificadosEnCorteAsync(FechaCorte, default);

        Assert.Contains(1, notificados);
        Assert.DoesNotContain(2, notificados);
        Assert.DoesNotContain(3, notificados);
    }

    [Fact]
    public async Task UltimosAvisosManualesAsync_devuelve_la_fecha_mas_reciente_por_empleado()
    {
        var fecha1 = DateTimeOffset.UtcNow.AddHours(-10);
        var fecha2 = DateTimeOffset.UtcNow.AddHours(-2); // mas reciente

        _db.NotificacionesEnvio.AddRange(
            new NotificacionEnvio
            {
                IdEmpleado = 5,
                InicioPeriodo = Inicio,
                FinPeriodo = Fin,
                Origen = OrigenNotificacion.Manual,
                Estado = EstadoEnvio.Enviado,
                HorasFaltantes = 6m,
                FechaEnvio = fecha1
            },
            new NotificacionEnvio
            {
                IdEmpleado = 5,
                InicioPeriodo = Inicio,
                FinPeriodo = Fin,
                Origen = OrigenNotificacion.Manual,
                Estado = EstadoEnvio.Enviado,
                HorasFaltantes = 4m,
                FechaEnvio = fecha2
            }
        );
        await _db.SaveChangesAsync();

        var ultimos = await _consulta.UltimosAvisosManualesAsync([5], default);

        Assert.True(ultimos.ContainsKey(5));
        Assert.Equal(fecha2, ultimos[5]);
    }

    [Fact]
    public async Task UltimosAvisosManualesAsync_por_proyecto_ignora_los_avisos_de_otros_proyectos()
    {
        _db.NotificacionesEnvio.AddRange(
            new NotificacionEnvio
            {
                IdEmpleado = 5, InicioPeriodo = Inicio, FinPeriodo = Fin, Origen = OrigenNotificacion.Manual,
                Estado = EstadoEnvio.Enviado, HorasFaltantes = 6m, FechaEnvio = DateTimeOffset.UtcNow, IdProyecto = 40
            },
            new NotificacionEnvio
            {
                IdEmpleado = 6, InicioPeriodo = Inicio, FinPeriodo = Fin, Origen = OrigenNotificacion.Manual,
                Estado = EstadoEnvio.Enviado, HorasFaltantes = 6m, FechaEnvio = DateTimeOffset.UtcNow, IdProyecto = 41
            });
        await _db.SaveChangesAsync();

        var ultimos = await _consulta.UltimosAvisosManualesAsync([5, 6], default, idProyecto: 40);

        Assert.Equal(new[] { 5 }, ultimos.Keys);
    }

    [Fact]
    public async Task CalcularAsync_con_fin_anterior_al_inicio_devuelve_lista_vacia()
    {
        var resultado = await _consulta.CalcularAsync(Fin, Inicio, null, default);
        Assert.Empty(resultado);
    }
}
