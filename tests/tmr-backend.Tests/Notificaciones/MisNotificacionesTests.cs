using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using tmr_backend.Features.Notificaciones.Application.Estado;
using tmr_backend.Features.Notificaciones.Application.Handlers;
using tmr_backend.Features.Notificaciones.Application.Queries;
using tmr_backend.Features.Notificaciones.Domain;
using tmr_backend.Features.Notificaciones.Domain.Events;
using tmr_backend.Infrastructure.Database;
using tmr_backend.Infrastructure.Database.Entities;

namespace tmr_backend.Tests.Notificaciones;

/// <summary>
/// Campana del colaborador: los correos de horas faltantes que se le enviaron (flujos 1 y 2).
/// </summary>
public class MisNotificacionesTests
{
    private static readonly DateOnly Inicio = new(2026, 10, 1);
    private static readonly DateOnly Fin = new(2026, 10, 15);

    private readonly ApplicationDbContext _db = new(new DbContextOptionsBuilder<ApplicationDbContext>()
        // Sin chequeo de obligatorios: las entidades de prueba solo llevan lo que usa la consulta
        .UseInMemoryDatabase(Guid.NewGuid().ToString(), o => o.EnableNullChecks(false))
        .Options);

    private ConsultaMisNotificaciones Consulta() => new(_db);

    [Fact]
    public async Task El_usuario_ve_los_empleados_de_su_persona()
    {
        _db.TblAutenticacionUsuarios.Add(new TblAutenticacionUsuario { Id = 7, Idpersona = 70, Activo = true });
        _db.TblAdministracionEmpleados.AddRange(
            new TblAdministracionEmpleado { Id = 1, Idpersona = 70 },
            new TblAdministracionEmpleado { Id = 2, Idpersona = 80 });
        await _db.SaveChangesAsync();

        Assert.Equal(new[] { 1 }, await Consulta().EmpleadosDelUsuarioAsync(7, default));
        Assert.Empty(await Consulta().EmpleadosDelUsuarioAsync(99, default));
    }

    [Fact]
    public async Task Solo_muestra_lo_que_se_le_envio_reciente_y_lo_mas_nuevo_primero()
    {
        _db.TblTimeReportProyectos.Add(new TblTimeReportProyecto { Id = 10, Nombre = "Portal Web" });
        var ahora = DateTimeOffset.UtcNow;
        Envio(1, EstadoEnvio.Enviado, OrigenNotificacion.Manual, ahora.AddHours(-1), proyecto: 10);
        Envio(1, EstadoEnvio.Enviado, OrigenNotificacion.Automatico, ahora.AddDays(-1), detalle: "Portal Web: 16 h");
        Envio(1, EstadoEnvio.Fallido, OrigenNotificacion.Manual, ahora);          // no le llegó
        Envio(1, EstadoEnvio.Omitido, OrigenNotificacion.Manual, ahora);          // no le llegó
        Envio(1, EstadoEnvio.Enviado, OrigenNotificacion.Manual, ahora.AddDays(-40));   // muy viejo
        Envio(2, EstadoEnvio.Enviado, OrigenNotificacion.Manual, ahora);          // de otro colaborador
        await _db.SaveChangesAsync();

        var avisos = await Consulta().RecientesAsync([1], default);

        Assert.Equal(2, avisos.Count);
        Assert.Equal(("Manual", "Portal Web"), (avisos[0].Origen, avisos[0].Proyecto));
        Assert.Equal(("Automatico", "Portal Web: 16 h"), (avisos[1].Origen, avisos[1].Detalle));
    }

    [Fact]
    public async Task Al_registrar_un_envio_exitoso_se_avisa_a_la_campana_del_colaborador()
    {
        var notifier = new AvisoColaboradorNotifier();
        using var suya = notifier.Suscribir([30]);
        using var deOtro = notifier.Suscribir([31]);
        var handler = new RegistrarEnvioHandler(_db, NullLogger<RegistrarEnvioHandler>.Instance, avisoColaborador: notifier);

        await handler.HandleAsync(new NotificacionFallida(30, 8m, Inicio, Fin, OrigenNotificacion.Manual, null, 4,
            DateTimeOffset.UtcNow, "SMTP"), default);
        Assert.False(suya.Lector.TryRead(out _));   // un fallo no le llegó

        await handler.HandleAsync(new NotificacionEnviada(30, 8m, Inicio, Fin, OrigenNotificacion.Manual, null, 4,
            DateTimeOffset.UtcNow), default);
        Assert.True(suya.Lector.TryRead(out _));
        Assert.False(deOtro.Lector.TryRead(out _));
    }

    private void Envio(int idEmpleado, EstadoEnvio estado, OrigenNotificacion origen, DateTimeOffset fecha,
        int? proyecto = null, string? detalle = null) =>
        _db.NotificacionesEnvio.Add(new NotificacionEnvio
        {
            IdEmpleado = idEmpleado,
            InicioPeriodo = Inicio,
            FinPeriodo = Fin,
            Origen = origen,
            Estado = estado,
            HorasFaltantes = 16m,
            FechaEnvio = fecha,
            IdProyecto = proyecto,
            DetalleError = detalle,
            FechaCorte = origen == OrigenNotificacion.Automatico ? fecha : null
        });
}
