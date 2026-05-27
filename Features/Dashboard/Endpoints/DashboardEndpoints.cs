using Microsoft.EntityFrameworkCore;
using tmr_backend.Features.Dashboard.Domain;
using tmr_backend.Features.Dashboard.DTOs;
using tmr_backend.Infrastructure.Database;

namespace tmr_backend.Features.Dashboard;

public static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/dashboard").WithTags("Dashboard");

        group.MapGet("/", async (string? rango, ApplicationDbContext db) =>
        {
            Console.WriteLine($"[Dashboard] Rango recibido en backend: '{rango}'");
            var queryActividades = db.TimeReportActividadesDiarias.Where(a => a.Activo);

            if (!string.IsNullOrEmpty(rango))
            {
                var hoyUtc = DateTime.UtcNow.Date;
                if (rango == "mes")
                {
                    var inicioMes = new DateTime(hoyUtc.Year, hoyUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                    queryActividades = queryActividades.Where(a => a.FechaActividad >= inicioMes);
                }
                else if (rango == "trimestre")
                {
                    var inicioTrimestre = DateTime.SpecifyKind(hoyUtc.AddMonths(-3), DateTimeKind.Utc);
                    queryActividades = queryActividades.Where(a => a.FechaActividad >= inicioTrimestre);
                }
                else if (rango == "anio")
                {
                    var inicioAnio = new DateTime(hoyUtc.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                    queryActividades = queryActividades.Where(a => a.FechaActividad >= inicioAnio);
                }
            }

            var totalProyectos = await db.TimeReportProyectos.CountAsync(p => p.Activo);

            var horasReportadas = await queryActividades
                .SumAsync(a => (decimal?)a.CantidadHoras) ?? 0m;

            var colaboradoresActivos = await db.AdministracionEmpleados
                .CountAsync(e => e.Activo);

            var clientesActivos = await db.AdministracionClientes.CountAsync();

            var metricas = new DashboardMetricasResponse(
                totalProyectos, 
                horasReportadas, 
                colaboradoresActivos, 
                clientesActivos
            );

            var proyectos = await db.TimeReportProyectos
                .Include(p => p.Cliente)
                .Where(p => p.Activo)
                .ToListAsync();

            var horasProyectos = await queryActividades
                .Where(a => a.IdProyecto.HasValue)
                .GroupBy(a => a.IdProyecto!.Value)
                .Select(g => new { ProyectoId = g.Key, Horas = g.Sum(a => a.CantidadHoras) })
                .ToDictionaryAsync(x => x.ProyectoId, x => x.Horas);

            var proximosACerrar = proyectos
                .OrderBy(p => p.FechaFinPlaneada ?? DateTime.MaxValue)
                .Take(3)
                .Select(p => new DashboardProyectoResumenResponse(
                    p.Codigo ?? "",
                    p.Nombre,
                    p.Cliente?.NombreComercial ?? "Sin Cliente",
                    "En progreso",
                    horasProyectos.TryGetValue(p.Id, out var h) ? h : 0m,
                    p.Presupuesto ?? 0m
                ))
                .ToList();

            var horasPorProyecto = proyectos
                .Select(p => new DashboardHorasPorProyectoResponse(
                    p.Nombre,
                    horasProyectos.TryGetValue(p.Id, out var h) ? h : 0m,
                    p.Codigo ?? ""
                ))
                .ToList();

            var dashboardData = new DashboardDataResponse(metricas, proximosACerrar, horasPorProyecto);
            return Results.Ok(dashboardData);
        });

        group.MapGet("/{id:guid}", async (Guid id, ApplicationDbContext db) =>
        {
            var item = await db.DashboardItems.FindAsync(id);

            if (item is null) return Results.NotFound();

            return Results.Ok(new DashboardItemResponse(item.Id, item.Nombre, item.Descripcion, item.Activo, item.FechaCreacion));
        });

        group.MapPost("/", async (CrearDashboardItemRequest request, ApplicationDbContext db) =>
        {
            try
            {
                var nuevoItem = DashboardItem.Crear(request.Nombre, request.Descripcion);
                
                db.DashboardItems.Add(nuevoItem);
                await db.SaveChangesAsync();

                var response = new DashboardItemResponse(nuevoItem.Id, nuevoItem.Nombre, nuevoItem.Descripcion, nuevoItem.Activo, nuevoItem.FechaCreacion);
                return Results.Created($"/api/dashboard/{nuevoItem.Id}", response);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { Mensaje = ex.Message });
            }
        });

        group.MapPut("/{id:guid}", async (Guid id, ActualizarDashboardItemRequest request, ApplicationDbContext db) =>
        {
            var item = await db.DashboardItems.FindAsync(id);

            if (item is null) return Results.NotFound();

            try
            {
                item.ActualizarDetalles(request.Nombre, request.Descripcion);
                await db.SaveChangesAsync();

                return Results.NoContent();
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { Mensaje = ex.Message });
            }
        });

        group.MapDelete("/{id:guid}", async (Guid id, ApplicationDbContext db) =>
        {
            var item = await db.DashboardItems.FindAsync(id);

            if (item is null) return Results.NotFound();

            item.Desactivar();
            await db.SaveChangesAsync();

            return Results.NoContent();
        });
    }
}
