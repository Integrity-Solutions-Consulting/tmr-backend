using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using tmr_backend.Features.Notificaciones.Application.Queries;
using tmr_backend.Features.Notificaciones.Domain;
using tmr_backend.Features.Notificaciones.Domain.Events;
using tmr_backend.Infrastructure.Database;
using tmr_backend.Infrastructure.Database.Entities;
using tmr_backend.Infrastructure.Messaging;

namespace tmr_backend.Tests.Notificaciones;

public class ConsultaResumenCorteTests
{
    // Corte reciente (el resumen solo muestra cortes de los últimos días)
    private static readonly DateTimeOffset Corte = DateTimeOffset.UtcNow.AddHours(-2);
    private static readonly DateOnly Inicio = new(2026, 10, 1);
    private static readonly DateOnly Fin = new(2026, 10, 15);

    private readonly ApplicationDbContext _db = new(new DbContextOptionsBuilder<ApplicationDbContext>()
        // Sin chequeo de obligatorios: los empleados de prueba solo necesitan Id y nombre
        .UseInMemoryDatabase(Guid.NewGuid().ToString(), o => o.EnableNullChecks(false))
        .Options);

    private ConsultaResumenCorte Consulta() => new(_db);

    [Fact]
    public async Task Sin_cortes_recientes_devuelve_null()
    {
        Registrar(1, EstadoEnvio.Enviado, DateTimeOffset.UtcNow.AddDays(-40));
        await _db.SaveChangesAsync();

        Assert.Null(await Consulta().UltimoCorteAsync(default));
    }

    [Fact]
    public async Task Cuenta_enviados_fallidos_y_omitidos_del_ultimo_corte()
    {
        AgregarEmpleado(1, "Ana", "Torres");
        AgregarEmpleado(2, "Luis", "Paz");
        AgregarEmpleado(3, "Eva", "Ruiz");
        Registrar(1, EstadoEnvio.Enviado, Corte);
        Registrar(2, EstadoEnvio.Fallido, Corte, "Apprise caído");
        Registrar(3, EstadoEnvio.Omitido, Corte, "No tiene correo registrado");
        Registrar(1, EstadoEnvio.Enviado, Corte.AddDays(-15));    // corte anterior: no cuenta
        await _db.SaveChangesAsync();

        var resumen = await Consulta().UltimoCorteAsync(default);

        Assert.NotNull(resumen);
        Assert.Equal(Corte, resumen.FechaCorte);
        Assert.False(resumen.EnProceso);
        Assert.Equal((1, 1, 1, 0), (resumen.Enviados, resumen.Fallidos, resumen.Omitidos, resumen.Pendientes));
        Assert.Equal("Luis Paz", resumen.Empleados[0].Nombre);   // los fallidos primero
        Assert.Equal("Apprise caído", resumen.Empleados[0].Detalle);
    }

    [Fact]
    public async Task Mientras_se_procesa_muestra_pendientes_y_resultados_aun_en_cola()
    {
        AgregarEmpleado(1, "Ana", "Torres");
        Registrar(1, EstadoEnvio.Enviado, Corte);
        EnCola(new HorasFaltantesDetectadas(2, "Luis Paz", "luis@isc.com", 8m, Inicio, Fin,
            OrigenNotificacion.Automatico, Corte, null));
        EnCola(new NotificacionFallida(3, 4m, Inicio, Fin, OrigenNotificacion.Automatico, Corte, null,
            DateTimeOffset.UtcNow, "SMTP"));
        // Un aviso manual en cola no es parte del corte
        EnCola(new HorasFaltantesDetectadas(9, "Manual", "m@isc.com", 8m, Inicio, Fin,
            OrigenNotificacion.Manual, null, 7));
        await _db.SaveChangesAsync();

        var resumen = await Consulta().UltimoCorteAsync(default);

        Assert.NotNull(resumen);
        Assert.True(resumen.EnProceso);
        Assert.Equal((1, 1, 0, 1), (resumen.Enviados, resumen.Fallidos, resumen.Omitidos, resumen.Pendientes));
        Assert.Equal("Luis Paz", resumen.Empleados.Single(e => e.IdEmpleado == 2).Nombre);
        Assert.DoesNotContain(resumen.Empleados, e => e.IdEmpleado == 9);
    }

    [Fact]
    public async Task Un_corte_recien_disparado_aparece_en_proceso_aunque_no_tenga_registros()
    {
        EnCola(new CorteDeHorasAlcanzado(Inicio, Fin, Corte));
        await _db.SaveChangesAsync();

        var resumen = await Consulta().UltimoCorteAsync(default);

        Assert.NotNull(resumen);
        Assert.True(resumen.EnProceso);
        Assert.Empty(resumen.Empleados);
    }

    private void Registrar(int idEmpleado, EstadoEnvio estado, DateTimeOffset corte, string? detalle = null) =>
        _db.NotificacionesEnvio.Add(new NotificacionEnvio
        {
            IdEmpleado = idEmpleado,
            InicioPeriodo = Inicio,
            FinPeriodo = Fin,
            FechaCorte = corte.ToUniversalTime(),
            Origen = OrigenNotificacion.Automatico,
            Estado = estado,
            HorasFaltantes = 8m,
            FechaEnvio = corte.ToUniversalTime(),
            DetalleError = detalle
        });

    private void EnCola<T>(T evento) where T : IEvento =>
        _db.EventosOutbox.Add(new EventoOutbox
        {
            Id = Guid.NewGuid(),
            TipoEvento = typeof(T).Name,
            Payload = JsonSerializer.Serialize(evento),
            Estado = EstadoEvento.Pendiente,
            FechaCreacion = DateTimeOffset.UtcNow
        });

    private void AgregarEmpleado(int id, string nombres, string apellidos) =>
        _db.TblAdministracionEmpleados.Add(new TblAdministracionEmpleado
        {
            Id = id,
            IdpersonaNavigation = new TblAdministracionPersona { Id = 100 + id, Nombres = nombres, Apellidos = apellidos }
        });
}
