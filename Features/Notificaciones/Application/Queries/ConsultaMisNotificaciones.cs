using Microsoft.EntityFrameworkCore;
using tmr_backend.Features.Notificaciones.Domain;
using tmr_backend.Infrastructure.Database;

namespace tmr_backend.Features.Notificaciones.Application.Queries;

/// <summary>
/// Un correo de horas faltantes que se le envió al colaborador, para su campana de notificaciones.
/// Origen: "Manual" (su líder desde Seguimiento) o "Automatico" (corte de quincena o fin de mes).
/// </summary>
public sealed record AvisoColaboradorDto(
    int Id,
    string Origen,
    decimal HorasFaltantes,
    DateOnly InicioPeriodo,
    DateOnly FinPeriodo,
    string? Proyecto,           // aviso manual de una fila de Seguimiento
    string? Detalle,            // corte: "Portal Web: 16 h · ERP: 8 h"
    DateTimeOffset FechaEnvio);

public interface IConsultaMisNotificaciones
{
    /// <summary>Colaboradores (empleados) de la persona del usuario; vacío si el usuario no es colaborador.</summary>
    Task<IReadOnlyList<int>> EmpleadosDelUsuarioAsync(int idUsuario, CancellationToken ct);

    /// <summary>Correos enviados a esos colaboradores en los últimos días, del más reciente al más antiguo.</summary>
    Task<IReadOnlyList<AvisoColaboradorDto>> RecientesAsync(IReadOnlyCollection<int> idsEmpleado, CancellationToken ct);
}

public sealed class ConsultaMisNotificaciones : IConsultaMisNotificaciones
{
    private static readonly TimeSpan Ventana = TimeSpan.FromDays(15);
    private const int Maximo = 10;

    private readonly ApplicationDbContext _db;

    public ConsultaMisNotificaciones(ApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<int>> EmpleadosDelUsuarioAsync(int idUsuario, CancellationToken ct)
    {
        if (idUsuario == 0)
            return [];

        // Mismo vínculo que "mis horas incompletas" del Dashboard: usuario → persona → empleado
        var idPersona = await _db.TblAutenticacionUsuarios
            .AsNoTracking()
            .Where(u => u.Id == idUsuario && u.Activo)
            .Select(u => u.Idpersona)
            .FirstOrDefaultAsync(ct);

        if (idPersona is null)
            return [];

        return await _db.TblAdministracionEmpleados
            .AsNoTracking()
            .Where(e => e.Idpersona == idPersona)
            .Select(e => e.Id)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AvisoColaboradorDto>> RecientesAsync(IReadOnlyCollection<int> idsEmpleado, CancellationToken ct)
    {
        if (idsEmpleado.Count == 0)
            return [];

        var desde = DateTimeOffset.UtcNow - Ventana;

        // Solo lo que de verdad le llegó: los fallidos y omitidos no se le enviaron
        var avisos = await _db.NotificacionesEnvio
            .AsNoTracking()
            .Where(n => idsEmpleado.Contains(n.IdEmpleado)
                     && n.Estado == EstadoEnvio.Enviado
                     && n.FechaEnvio >= desde)
            .OrderByDescending(n => n.FechaEnvio)
            .Take(Maximo)
            .Select(n => new
            {
                n.Id,
                n.Origen,
                n.HorasFaltantes,
                n.InicioPeriodo,
                n.FinPeriodo,
                n.DetalleError,
                n.FechaEnvio,
                Proyecto = _db.TblTimeReportProyectos.Where(p => p.Id == n.IdProyecto).Select(p => p.Nombre).FirstOrDefault()
            })
            .ToListAsync(ct);

        return avisos
            .Select(a => new AvisoColaboradorDto(
                a.Id,
                a.Origen.ToString(),
                a.HorasFaltantes,
                a.InicioPeriodo,
                a.FinPeriodo,
                a.Proyecto,
                a.Origen == OrigenNotificacion.Automatico ? a.DetalleError : null,
                a.FechaEnvio))
            .ToList();
    }
}
