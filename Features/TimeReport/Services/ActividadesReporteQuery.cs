using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using tmr_backend.Infrastructure.Database;

namespace tmr_backend.Features.TimeReport.Services;

// Actividades de un colaborador en un rango, con el líder vigente de cada proyecto.
// Es la consulta que alimenta tanto GET /api/time-report/actividades/mi-reporte como la generación de
// reportes con plantillas: una sola fuente para que ambos muestren siempre los mismos datos.
// Los nombres de propiedad son los mismos que antes devolvía el endpoint (el frontend depende de ellos).
public sealed record ActividadReporteDto(
    int? IdProyecto,
    string Fecha,
    string Proyecto,
    string TipoActividad,
    string CodigoRequerimiento,
    decimal Horas,
    string Descripcion,
    string Notas,
    string EsBillable,
    string LiderProyecto,
    string ClienteProyecto,
    [property: JsonIgnore] int? IdCliente);   // solo para filtrar en servidor; no se serializa

public sealed record ActividadesReporteDto(List<ActividadReporteDto> Actividades, List<string> Feriados);

public static class ActividadesReporteQuery
{
    public static async Task<ActividadesReporteDto> ObtenerAsync(
        ApplicationDbContext db,
        int idEmpleado,
        DateOnly fechaDesde,
        DateOnly fechaHasta,
        CancellationToken ct = default)
    {
        var registros = await db.TblTimeReportActividadDiaria
            .Where(a => a.Activo && a.Idempleado == idEmpleado && a.Fechaactividad >= fechaDesde && a.Fechaactividad <= fechaHasta)
            .Select(a => new
            {
                a.Idproyecto,
                a.Fechaactividad,
                Proyecto = a.IdproyectoNavigation != null ? a.IdproyectoNavigation.Nombre : "Sin Proyecto",
                TipoActividad = a.IdtipoactividadNavigation != null ? a.IdtipoactividadNavigation.Nombretipo : "Otro",
                CodigoRequerimiento = a.Codigorequerimiento ?? "",
                Horas = a.Cantidadhoras,
                Descripcion = a.Descripcionactividad ?? "",
                Notas = a.Notas ?? "",
                EsBillable = a.Esbillable == true ? "Sí" : "No",
                IdCliente = a.IdproyectoNavigation != null ? a.IdproyectoNavigation.Idcliente : null,
                ClienteProyecto = a.IdproyectoNavigation != null && a.IdproyectoNavigation.IdclienteNavigation != null
                    ? (a.IdproyectoNavigation.IdclienteNavigation.Nombrecomercial ?? a.IdproyectoNavigation.IdclienteNavigation.Razonsocial ?? "Sin Cliente")
                    : "Sin Cliente"
            })
            .ToListAsync(ct);

        var asignaciones = await db.TblTimeReportAsignacionProyectos
            .Where(ep => ep.Activo && ep.Idempleado == idEmpleado
                && ep.IdliderNavigation != null && ep.IdliderNavigation.IdpersonaNavigation != null)
            .Select(ep => new
            {
                ep.Idproyecto,
                ep.Fechaasignacion,
                ep.Fechafinasignacion,
                Lider = ep.IdliderNavigation!.IdpersonaNavigation.Nombres + " " + ep.IdliderNavigation.IdpersonaNavigation.Apellidos
            })
            .ToListAsync(ct);

        string LiderDe(int? idProyecto, DateOnly fecha)
        {
            var delProyecto = asignaciones
                .Where(ep => ep.Idproyecto == idProyecto)
                .OrderByDescending(ep => ep.Fechaasignacion ?? DateOnly.MinValue)
                .ToList();
            var vigente = delProyecto.FirstOrDefault(ep =>
                (ep.Fechaasignacion == null || ep.Fechaasignacion <= fecha)
                && (ep.Fechafinasignacion == null || ep.Fechafinasignacion >= fecha));
            return (vigente ?? delProyecto.FirstOrDefault())?.Lider ?? "Sin Líder";
        }

        var actividades = registros.Select(a => new ActividadReporteDto(
            a.Idproyecto,
            a.Fechaactividad.ToString("yyyy-MM-dd"),
            a.Proyecto,
            a.TipoActividad,
            a.CodigoRequerimiento,
            a.Horas,
            a.Descripcion,
            a.Notas,
            a.EsBillable,
            LiderDe(a.Idproyecto, a.Fechaactividad),
            a.ClienteProyecto,
            a.IdCliente)).ToList();

        var feriados = await db.TblTimeReportFeriados
            .Where(f => f.Activo && f.Fechaferiado >= fechaDesde && f.Fechaferiado <= fechaHasta)
            .Select(f => f.Fechaferiado)
            .ToListAsync(ct);

        return new ActividadesReporteDto(actividades, feriados.Select(f => f.ToString("yyyy-MM-dd")).ToList());
    }
}
