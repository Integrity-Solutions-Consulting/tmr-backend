using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using tmr_backend.Features.Reportes.DTOs;
using tmr_backend.Features.TimeReport.Services;
using tmr_backend.Infrastructure.Database;

namespace tmr_backend.Features.Reportes;

/// <summary>
/// Reportes "Por Colaborador" y "Por Cliente": historial de asignaciones a proyectos a lo largo del tiempo.
/// Devuelven todo el historial (colaboradores que ya salieron y proyectos inactivos incluidos); los filtros
/// por texto y por estado se aplican en el frontend.
/// </summary>
public static class ReportesHistorialEndpoints
{
    private const string SinCliente = "Sin cliente";

    // Roles con acceso a todos los módulos (mismo criterio que isAdmin() del frontend / roleGuard).
    private static readonly string[] RolesConAccesoTotal = ["ADMINISTRADOR", "ADMIN", "RECURSOS HUMANOS", "RECURSOS_HUMANOS"];

    /// <summary>
    /// Estos reportes exponen el historial de todos los colaboradores: se exige el permiso REPORTES_VIEW (o un rol
    /// con acceso total), igual que la pantalla. Un colaborador sin ese permiso no puede consultarlos por la API.
    /// </summary>
    private static bool PuedeVerReportes(System.Security.Claims.ClaimsPrincipal user) =>
        user.HasClaim("permission", "REPORTES_VIEW")
        || user.FindAll(System.Security.Claims.ClaimTypes.Role)
            .Any(r => RolesConAccesoTotal.Contains(r.Value.Trim().ToUpperInvariant()));

    public static void MapReportesHistorial(RouteGroupBuilder group)
    {
        var historial = group.MapGroup("")
            .RequireAuthorization(p => p.RequireAuthenticatedUser().RequireAssertion(ctx => PuedeVerReportes(ctx.User)));

        historial.MapGet("/colaboradores", async (ApplicationDbContext db) =>
        {
            var hoy = CalculoHorasPeriodo.HoyEcuador();
            var asignaciones = await CargarAsignacionesAsync(db, hoy);
            var horasPorProyecto = await CargarHorasPorEmpleadoProyectoAsync(db);

            var resumen = asignaciones
                .GroupBy(a => new { a.IdEmpleado, a.IdCliente })
                .Select(g =>
                {
                    var primera = g.First();
                    var enCurso = g.Any(a => a.Vigente && a.FechaFin is null);
                    var proyectos = g.Select(a => a.IdProyecto).Distinct().ToList();
                    return new ColaboradorClienteResumenDto(
                        primera.IdEmpleado,
                        primera.Colaborador,
                        primera.Identificacion,
                        primera.Cargo,
                        primera.ColaboradorActivo,
                        primera.IdCliente,
                        primera.Cliente,
                        proyectos.Count,
                        string.Join(", ", g.Select(a => a.Proyecto).Distinct().OrderBy(n => n)),
                        g.Min(a => a.FechaInicio),
                        enCurso ? null : g.Max(a => a.FechaFin),
                        enCurso,
                        DiasSinSuperposicion(g.Select(a => Intervalo(a.FechaInicio, a.FechaFin, a.Vigente, hoy))),
                        // Todas las horas del colaborador en los proyectos de este cliente.
                        proyectos.Sum(p => horasPorProyecto.GetValueOrDefault((primera.IdEmpleado, p)))
                    );
                })
                .OrderBy(r => r.Colaborador).ThenBy(r => r.FechaInicio)
                .ToList();

            return Results.Ok(new ReporteColaboradoresResponse(resumen, asignaciones));
        });

        historial.MapGet("/clientes", async (ApplicationDbContext db) =>
        {
            var hoy = CalculoHorasPeriodo.HoyEcuador();
            var asignaciones = await CargarAsignacionesAsync(db, hoy);
            var porProyecto = asignaciones.ToLookup(a => a.IdProyecto);

            var proyectos = await db.TblTimeReportProyectos
                .AsNoTracking()
                .Select(p => new
                {
                    p.Id,
                    p.Idcliente,
                    p.Codigo,
                    p.Nombre,
                    Tipo = p.IdtipoproyectoNavigation != null ? p.IdtipoproyectoNavigation.Nombretipo : null,
                    Estado = p.IdestadoproyectoNavigation.Valor,
                    p.Activo,
                    Inicio = p.Fechainicioreal ?? p.Fechainicioplaneada,
                    Fin = p.Fechafinreal ?? p.Fechafinplaneada
                })
                .ToListAsync();

            var clientes = await db.TblAdministracionClientes
                .AsNoTracking()
                .Select(c => new
                {
                    c.Id,
                    Nombre = c.Nombrecomercial ?? c.Razonsocial ?? (c.Nombres + " " + c.Apellidos),
                    c.Numeroidentificacion,
                    c.Activo
                })
                .ToListAsync();
            var nombreCliente = clientes.ToDictionary(c => c.Id, c => c.Nombre);

            var proyectosDto = proyectos
                .Select(p =>
                {
                    var equipo = porProyecto[p.Id].ToList();
                    return new ClienteProyectoDto(
                        p.Id,
                        p.Idcliente,
                        p.Idcliente is int idc && nombreCliente.TryGetValue(idc, out var nc) ? nc : SinCliente,
                        p.Codigo,
                        p.Nombre,
                        p.Tipo,
                        p.Estado,
                        p.Activo,
                        p.Inicio,
                        p.Fin,
                        equipo.Select(a => a.IdEmpleado).Distinct().Count(),
                        equipo.Where(a => a.Vigente).Select(a => a.IdEmpleado).Distinct().Count(),
                        string.Join(", ", equipo.Select(a => a.Colaborador).Distinct().OrderBy(n => n)));
                })
                .OrderBy(p => p.Cliente).ThenBy(p => p.Codigo)
                .ToList();

            var resumenClientes = clientes
                .Select(c =>
                {
                    var suyos = proyectosDto.Where(p => p.IdCliente == c.Id).ToList();
                    var equipo = asignaciones.Where(a => a.IdCliente == c.Id).ToList();
                    return new ClienteResumenDto(
                        c.Id,
                        c.Nombre,
                        c.Numeroidentificacion,
                        c.Activo,
                        suyos.Count,
                        suyos.Count(p => p.ProyectoActivo),
                        string.Join(", ", suyos.Select(p => p.Proyecto).OrderBy(n => n)),
                        equipo.Select(a => a.IdEmpleado).Distinct().Count(),
                        equipo.Where(a => a.Vigente).Select(a => a.IdEmpleado).Distinct().Count());
                })
                .ToList();

            // Proyectos sin cliente asignado: se agrupan en una fila aparte para no perderlos del conteo.
            var sinCliente = proyectosDto.Where(p => p.IdCliente is null).ToList();
            if (sinCliente.Count > 0)
            {
                var equipo = asignaciones.Where(a => a.IdCliente is null).ToList();
                resumenClientes.Add(new ClienteResumenDto(
                    null, SinCliente, null, true,
                    sinCliente.Count,
                    sinCliente.Count(p => p.ProyectoActivo),
                    string.Join(", ", sinCliente.Select(p => p.Proyecto).OrderBy(n => n)),
                    equipo.Select(a => a.IdEmpleado).Distinct().Count(),
                    equipo.Where(a => a.Vigente).Select(a => a.IdEmpleado).Distinct().Count()));
            }

            return Results.Ok(new ReporteClientesResponse(
                resumenClientes.OrderBy(c => c.Cliente).ToList(),
                proyectosDto,
                asignaciones));
        });
    }

    /// <summary>
    /// Todas las asignaciones de colaboradores a proyectos. Solo se toman las filas con Activo = true: al editar un
    /// proyecto se desactivan las filas anteriores y se vuelven a crear, así que las inactivas son copias reemplazadas
    /// (el historial real queda en las fechas de entrada y salida de las filas activas).
    /// </summary>
    private static async Task<List<AsignacionHistorialDto>> CargarAsignacionesAsync(ApplicationDbContext db, DateOnly hoy)
    {
        var filas = await db.TblTimeReportAsignacionProyectos
            .AsNoTracking()
            .Where(a => a.Activo && a.Idempleado != null)
            .Select(a => new
            {
                a.Id,
                IdEmpleado = a.Idempleado!.Value,
                Nombres = a.IdempleadoNavigation!.IdpersonaNavigation.Nombres,
                Apellidos = a.IdempleadoNavigation.IdpersonaNavigation.Apellidos,
                Identificacion = a.IdempleadoNavigation.IdpersonaNavigation.Numeroidentificacion,
                Cargo = a.IdempleadoNavigation.IdcargoNavigation != null ? a.IdempleadoNavigation.IdcargoNavigation.Nombrecargo : null,
                EmpleadoActivo = a.IdempleadoNavigation.Activo,
                a.IdempleadoNavigation.Fechaterminacion,
                a.Idproyecto,
                Codigo = a.IdproyectoNavigation.Codigo,
                Proyecto = a.IdproyectoNavigation.Nombre,
                EstadoProyecto = a.IdproyectoNavigation.IdestadoproyectoNavigation.Valor,
                ProyectoActivo = a.IdproyectoNavigation.Activo,
                InicioProyecto = a.IdproyectoNavigation.Fechainicioreal ?? a.IdproyectoNavigation.Fechainicioplaneada,
                FinRealProyecto = a.IdproyectoNavigation.Fechafinreal,
                IdCliente = a.IdproyectoNavigation.Idcliente,
                Cliente = a.IdproyectoNavigation.IdclienteNavigation != null
                    ? (a.IdproyectoNavigation.IdclienteNavigation.Nombrecomercial
                        ?? a.IdproyectoNavigation.IdclienteNavigation.Razonsocial
                        ?? (a.IdproyectoNavigation.IdclienteNavigation.Nombres + " " + a.IdproyectoNavigation.IdclienteNavigation.Apellidos))
                    : null,
                a.Rolasignado,
                Lider = a.IdliderNavigation != null
                    ? a.IdliderNavigation.IdpersonaNavigation.Nombres + " " + a.IdliderNavigation.IdpersonaNavigation.Apellidos
                    : null,
                a.Fechaasignacion,
                a.Fechafinasignacion,
                a.Estadoasignacion
            })
            .ToListAsync();

        // Horas por (empleado, proyecto, fecha) para sumar solo las que caen dentro de cada asignación.
        var actividades = await db.TblTimeReportActividadDiaria
            .AsNoTracking()
            .Where(a => a.Activo && a.Idproyecto != null)
            .GroupBy(a => new { a.Idempleado, Idproyecto = a.Idproyecto!.Value, a.Fechaactividad })
            .Select(g => new { g.Key.Idempleado, g.Key.Idproyecto, g.Key.Fechaactividad, Horas = g.Sum(a => a.Cantidadhoras) })
            .ToListAsync();
        var horasPorEmpleadoProyecto = actividades.ToLookup(a => (a.Idempleado, a.Idproyecto));

        return filas
            .Select(f =>
            {
                var inicio = f.Fechaasignacion ?? f.InicioProyecto;
                var salidaEmpresa = f.EmpleadoActivo ? null : f.Fechaterminacion;
                var vigente = f.Estadoasignacion && f.EmpleadoActivo
                    && (f.Fechafinasignacion is null || f.Fechafinasignacion >= hoy);

                // Si la asignación no tiene salida, termina hoy, o antes si el colaborador salió de la empresa o el
                // proyecto ya tiene fecha fin real.
                var finCalculo = f.Fechafinasignacion
                    ?? new[] { (DateOnly?)hoy, salidaEmpresa, f.FinRealProyecto }.Where(d => d.HasValue).Min();
                var finMostrado = f.Fechafinasignacion ?? (vigente ? null : finCalculo);

                var horas = horasPorEmpleadoProyecto[(f.IdEmpleado, f.Idproyecto)]
                    .Where(a => (inicio is null || a.Fechaactividad >= inicio) && (finCalculo is null || a.Fechaactividad <= finCalculo))
                    .Sum(a => a.Horas);

                return new AsignacionHistorialDto(
                    f.Id,
                    f.IdEmpleado,
                    $"{f.Nombres} {f.Apellidos}".Trim(),
                    f.Identificacion,
                    f.Cargo,
                    f.EmpleadoActivo,
                    f.IdCliente,
                    f.Cliente ?? SinCliente,
                    f.Idproyecto,
                    f.Codigo,
                    f.Proyecto,
                    f.EstadoProyecto,
                    f.ProyectoActivo,
                    f.Rolasignado,
                    f.Lider,
                    inicio,
                    finMostrado,
                    vigente,
                    DiasSinSuperposicion([(inicio, finCalculo)]),
                    horas);
            })
            .OrderBy(a => a.Colaborador).ThenBy(a => a.FechaInicio)
            .ToList();
    }

    private static async Task<Dictionary<(int, int), decimal>> CargarHorasPorEmpleadoProyectoAsync(ApplicationDbContext db) =>
        (await db.TblTimeReportActividadDiaria
            .AsNoTracking()
            .Where(a => a.Activo && a.Idproyecto != null)
            .GroupBy(a => new { a.Idempleado, Idproyecto = a.Idproyecto!.Value })
            .Select(g => new { g.Key.Idempleado, g.Key.Idproyecto, Horas = g.Sum(a => a.Cantidadhoras) })
            .ToListAsync())
        .ToDictionary(x => (x.Idempleado, x.Idproyecto), x => x.Horas);

    /// <summary>Intervalo para el cálculo de días: una asignación abierta (sin fecha fin) cuenta hasta hoy.</summary>
    private static (DateOnly? Inicio, DateOnly? Fin) Intervalo(DateOnly? inicio, DateOnly? fin, bool vigente, DateOnly hoy) =>
        (inicio, fin ?? (vigente ? hoy : null));

    /// <summary>
    /// Días calendario cubiertos por los intervalos, sin contar dos veces los días en que se superponen
    /// (p. ej. dos proyectos del mismo cliente a la vez). Los intervalos sin inicio o sin fin no se cuentan.
    /// </summary>
    private static int DiasSinSuperposicion(IEnumerable<(DateOnly? Inicio, DateOnly? Fin)> intervalos)
    {
        var validos = intervalos
            .Where(i => i.Inicio.HasValue && i.Fin.HasValue && i.Fin >= i.Inicio)
            .Select(i => (Inicio: i.Inicio!.Value, Fin: i.Fin!.Value))
            .OrderBy(i => i.Inicio)
            .ToList();

        var total = 0;
        DateOnly? actualInicio = null, actualFin = null;
        foreach (var (inicio, fin) in validos)
        {
            if (actualFin is null || inicio > actualFin.Value.AddDays(1))
            {
                if (actualInicio.HasValue) total += actualFin!.Value.DayNumber - actualInicio.Value.DayNumber + 1;
                actualInicio = inicio;
                actualFin = fin;
            }
            else if (fin > actualFin)
            {
                actualFin = fin;
            }
        }
        if (actualInicio.HasValue) total += actualFin!.Value.DayNumber - actualInicio.Value.DayNumber + 1;
        return total;
    }
}
