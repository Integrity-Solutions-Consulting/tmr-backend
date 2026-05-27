using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using tmr_backend.Features.Reportes.Domain;
using tmr_backend.Features.Reportes.DTOs;
using tmr_backend.Infrastructure.Database;

namespace tmr_backend.Features.Reportes;

public static class ReportesEndpoints
{
    public static void MapReportesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/reportes").WithTags("Reportes"); //.RequireAuthorization();

        // 0. Test DB Connection
        group.MapGet("/test-db", async (ApplicationDbContext db) =>
        {
            try
            {
                var count = await db.TimeReportActividadesDiarias.CountAsync();
                return Results.Ok(new { message = "Conexión a DB y tabla exitosa", cantidadRegistros = count });
            }
            catch (System.Exception ex)
            {
                return Results.Problem($"Error conectando a la BD: {ex.Message}");
            }
        });

        // 1. Reporte por Horas
        group.MapGet("/horas", async (
            [AsParameters] ReporteHorasFiltroRequest filtro,
            ApplicationDbContext db) =>
        {
            var query = db.TimeReportActividadesDiarias
                .Include(a => a.Empleado)
                .Include(a => a.Proyecto).ThenInclude(p => p.Cliente)
                .Where(a => a.Activo);

            if (!string.IsNullOrWhiteSpace(filtro.Cliente))
                query = query.Where(a => a.Proyecto != null && a.Proyecto.Cliente != null && a.Proyecto.Cliente.NombreComercial.ToLower().Contains(filtro.Cliente.ToLower()));

            if (filtro.Mes.HasValue)
                query = query.Where(a => a.FechaActividad.Month == filtro.Mes.Value);

            if (filtro.Anio.HasValue)
                query = query.Where(a => a.FechaActividad.Year == filtro.Anio.Value);

            // Realizamos la agrupación
            var groupedQuery = query
                .GroupBy(a => new {
                    Cliente = a.Proyecto != null && a.Proyecto.Cliente != null ? a.Proyecto.Cliente.NombreComercial : "Sin Cliente",
                    Mes = a.FechaActividad.Month,
                    Anio = a.FechaActividad.Year
                })
                .Select(g => new {
                    Cliente = g.Key.Cliente,
                    MesNum = g.Key.Mes,
                    Anio = g.Key.Anio.ToString(),
                    Recursos = g.Select(x => x.IdEmpleado).Distinct().Count(),
                    Horas = g.Sum(x => x.CantidadHoras)
                });

            var total = await groupedQuery.CountAsync();

            var minYear = await db.TimeReportActividadesDiarias.Where(a => a.Activo).MinAsync(a => (int?)a.FechaActividad.Year);
            var maxYear = await db.TimeReportActividadesDiarias.Where(a => a.Activo).MaxAsync(a => (int?)a.FechaActividad.Year);

            var dbResult = await groupedQuery
                .OrderBy(g => g.Anio).ThenBy(g => g.MesNum).ThenBy(g => g.Cliente)
                .Skip((filtro.Page - 1) * filtro.PageSize)
                .Take(filtro.PageSize)
                .ToListAsync();

            var meses = new string[] { "", "Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio", "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre" };
            
            var resultado = dbResult.Select(r => new ReporteHorasResponse(
                Guid.NewGuid().ToString(),
                r.Cliente ?? "Sin Cliente",
                r.Recursos,
                r.Horas,
                r.MesNum >= 1 && r.MesNum <= 12 ? meses[r.MesNum] : "Desconocido",
                r.Anio
            )).ToList();

            return Results.Ok(new PaginatedResponse<ReporteHorasResponse>(resultado, total, minYear, maxYear));
        });

        // 2. Reporte por Fechas
        group.MapGet("/fechas", async (
            [AsParameters] ReporteFechasFiltroRequest filtro,
            ApplicationDbContext db) =>
        {
            var query = db.TimeReportActividadesDiarias
                .Include(a => a.Empleado).ThenInclude(e => e.Persona)
                .Include(a => a.Empleado).ThenInclude(e => e.Cargo)
                .Include(a => a.Proyecto).ThenInclude(p => p.Cliente)
                .Include(a => a.Proyecto).ThenInclude(p => p.Lider).ThenInclude(l => l.Persona)
                .Where(a => a.Activo);

            if (filtro.FechaInicio.HasValue)
                query = query.Where(a => a.FechaActividad >= filtro.FechaInicio.Value);

            if (filtro.FechaFin.HasValue)
                query = query.Where(a => a.FechaActividad <= filtro.FechaFin.Value);

            if (!string.IsNullOrWhiteSpace(filtro.Cliente))
                query = query.Where(a => a.Proyecto != null && a.Proyecto.Cliente != null && a.Proyecto.Cliente.NombreComercial.ToLower().Contains(filtro.Cliente.ToLower()));

            if (!string.IsNullOrWhiteSpace(filtro.Lider))
                query = query.Where(a => a.Proyecto != null && a.Proyecto.Lider != null && 
                                        (a.Proyecto.Lider.Persona.PrimerNombre.ToLower().Contains(filtro.Lider.ToLower()) || 
                                         a.Proyecto.Lider.Persona.ApellidoPaterno.ToLower().Contains(filtro.Lider.ToLower())));

            var total = await query.CountAsync();

            var resultado = await query
                .OrderByDescending(a => a.FechaActividad)
                .Skip((filtro.Page - 1) * filtro.PageSize)
                .Take(filtro.PageSize)
                .Select(a => new ReporteFechasResponse(
                    a.Id.ToString(),
                    a.Proyecto != null && a.Proyecto.Cliente != null ? (a.Proyecto.Cliente.NombreComercial ?? "Sin Cliente") : "Sin Cliente",
                    a.Proyecto != null && a.Proyecto.Lider != null ? (a.Proyecto.Lider.Persona.PrimerNombre + " " + a.Proyecto.Lider.Persona.ApellidoPaterno) : "Sin Lider",
                    a.Empleado.Persona.PrimerNombre + " " + a.Empleado.Persona.ApellidoPaterno,
                    a.Empleado.Cargo != null ? a.Empleado.Cargo.NombreCargo : "Sin Cargo",
                    a.FechaActividad,
                    a.FechaActividad
                ))
                .ToListAsync();

            return Results.Ok(new PaginatedResponse<ReporteFechasResponse>(resultado, total));
        });
    }
}
