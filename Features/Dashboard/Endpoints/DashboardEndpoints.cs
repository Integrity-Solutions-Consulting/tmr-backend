using Microsoft.EntityFrameworkCore;
using tmr_backend.Features.Dashboard.Domain;
using tmr_backend.Features.Dashboard.DTOs;
using tmr_backend.Infrastructure.Database;
using tmr_backend.Infrastructure.Database.Entities;

namespace tmr_backend.Features.Dashboard;

public static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/dashboard").WithTags("Dashboard");

        group.MapGet("/", async (string? rango, ApplicationDbContext db) =>
        {
            Console.WriteLine($"[Dashboard] Rango recibido en backend: '{rango}'");
            var queryActividades = db.TblTimeReportActividadDiaria.Where(a => a.Activo);

            if (!string.IsNullOrEmpty(rango))
            {
                var hoyUtc = DateTime.UtcNow.Date;
                if (rango == "mes")
                {
                    var inicioMes = new DateOnly(hoyUtc.Year, hoyUtc.Month, 1);
                    queryActividades = queryActividades.Where(a => a.Fechaactividad >= inicioMes);
                }
                else if (rango == "trimestre")
                {
                    var inicioTrimestre = DateOnly.FromDateTime(hoyUtc.AddMonths(-3));
                    queryActividades = queryActividades.Where(a => a.Fechaactividad >= inicioTrimestre);
                }
                else if (rango == "anio")
                {
                    var inicioAnio = new DateOnly(hoyUtc.Year, 1, 1);
                    queryActividades = queryActividades.Where(a => a.Fechaactividad >= inicioAnio);
                }
            }

            var totalProyectos = await db.TblTimeReportProyectos.CountAsync(p => p.Activo);

            var horasReportadas = await queryActividades
                .SumAsync(a => (decimal?)a.Cantidadhoras) ?? 0m;

            var colaboradoresActivos = await db.TblAdministracionEmpleados
                .CountAsync(e => e.Activo);

            var clientesActivos = await db.TblAdministracionClientes.CountAsync(c => c.Activo);

            var metricas = new DashboardMetricasResponse(
                totalProyectos, 
                horasReportadas, 
                colaboradoresActivos, 
                clientesActivos
            );

            var proyectos = await db.TblTimeReportProyectos
                .Include(p => p.IdclienteNavigation)
                .Include(p => p.IdestadoproyectoNavigation)
                .Where(p => p.Activo)
                .ToListAsync();

            var horasProyectos = await queryActividades
                .Where(a => a.Idproyecto.HasValue)
                .GroupBy(a => a.Idproyecto!.Value)
                .Select(g => new { ProyectoId = g.Key, Horas = g.Sum(a => a.Cantidadhoras) })
                .ToDictionaryAsync(x => x.ProyectoId, x => x.Horas);

            var proximosACerrar = proyectos
                .OrderBy(p => p.Fechafinplaneada ?? DateOnly.MaxValue)
                .Take(3)
                .Select(p => new DashboardProyectoResumenResponse(
                    p.Codigo ?? "",
                    p.Nombre,
                    p.IdclienteNavigation?.Nombrecomercial ?? "Sin Cliente",
                    p.IdestadoproyectoNavigation?.Idcatalogo == 11 ? p.IdestadoproyectoNavigation.Valor : "En progreso",
                    horasProyectos.TryGetValue(p.Id, out var h) ? h : 0m,
                    p.Presupuesto ?? 0m,
                    p.Fechafinplaneada
                ))
                .ToList();

            var horasPorProyecto = proyectos
                .Select(p => new DashboardHorasPorProyectoResponse(
                    p.Nombre,
                    horasProyectos.TryGetValue(p.Id, out var h) ? h : 0m,
                    p.Codigo ?? "",
                    p.Horasasignadas ?? 0m
                ))
                .Where(hp => hp.Horas > 0)
                .OrderByDescending(hp => hp.Horas)
                .Take(15)
                .ToList();

            var totalProyectosActivos = proyectos.Count;
            var proyectosPorCliente = proyectos
                .GroupBy(p => p.IdclienteNavigation?.Nombrecomercial ?? p.IdclienteNavigation?.Razonsocial ?? "Sin Cliente")
                .Select(g => new DashboardProyectosPorClienteResponse(
                    g.Key,
                    g.Count(),
                    totalProyectosActivos > 0 ? Math.Round((decimal)g.Count() / totalProyectosActivos * 100, 1) : 0m
                ))
                .OrderByDescending(x => x.ProyectosAsignados)
                .ToList();

            var dashboardData = new DashboardDataResponse(metricas, proximosACerrar, horasPorProyecto, proyectosPorCliente);
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
