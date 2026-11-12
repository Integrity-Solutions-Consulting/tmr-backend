using tmr_backend.Infrastructure.Database.Entities;

namespace tmr_backend.Features.TimeReport.Services;

/// <summary>
/// sm - Qué colaborador corresponde al usuario de la sesión. Un colaborador con salida registrada queda Inactivo pero
/// sigue usando el TMR para completar sus actividades hasta su fecha de salida, así que también cuenta. Los inactivos
/// sin fecha de salida (desactivados sin registrar salida) no. Si una persona tiene varios registros, se prefiere el activo.
/// </summary>
public static class EmpleadoSesion
{
    public static IQueryable<TblAdministracionEmpleado> DeLaPersona(
        this IQueryable<TblAdministracionEmpleado> empleados, int? idPersona) =>
        empleados
            .Where(e => e.Idpersona == idPersona && (e.Activo || e.Fechaterminacion != null))
            .OrderByDescending(e => e.Activo)
            .ThenByDescending(e => e.Fechaterminacion);

    // sm - Mensaje si la fecha es posterior a la salida de un colaborador inactivo; null si puede registrar.
    public static string? ErrorFechaPosteriorSalida(TblAdministracionEmpleado empleado, DateOnly fecha) =>
        !empleado.Activo && empleado.Fechaterminacion.HasValue && fecha > empleado.Fechaterminacion.Value
            ? $"Su fecha de salida es el {empleado.Fechaterminacion.Value:dd/MM/yyyy}: no puede registrar actividades posteriores a esa fecha."
            : null;
}
