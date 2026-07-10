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
                .Include(p => p.TblTimeReportAsignacionProyectos)
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
                .Select(p => {
                    var totalAsignadas = p.TblTimeReportAsignacionProyectos
                        .Where(ap => ap.Activo)
                        .Sum(ap => ap.Horasasignadas ?? 0m);

                    if (totalAsignadas == 0m)
                    {
                        totalAsignadas = p.Horasasignadas ?? 0m;
                    }

                    return new DashboardHorasPorProyectoResponse(
                        p.Id,
                        p.Nombre,
                        horasProyectos.TryGetValue(p.Id, out var h) ? h : 0m,
                        p.Codigo ?? "",
                        totalAsignadas
                    );
                })
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

        group.MapGet("/mis-horas-incompletas", async (
            string? rango,
            tmr_backend.Infrastructure.Shared.ICurrentUserService currentUserService,
            ApplicationDbContext db) =>
        {
            var userId = currentUserService.UserId;
            if (userId == 0)
            {
                return Results.Unauthorized();
            }

            var idPersona = await db.TblAutenticacionUsuarios
                .Where(u => u.Id == userId && u.Activo)
                .Select(u => u.Idpersona)
                .FirstOrDefaultAsync();

            if (idPersona == null)
            {
                return Results.Ok(new { TieneFaltantes = false, HorasFaltantes = 0m, DiasIncompletos = Array.Empty<object>() });
            }

            var empleado = await db.TblAdministracionEmpleados
                .FirstOrDefaultAsync(e => e.Idpersona == idPersona && e.Activo);

            if (empleado == null)
            {
                return Results.Ok(new { TieneFaltantes = false, HorasFaltantes = 0m, DiasIncompletos = Array.Empty<object>() });
            }

            var empId = empleado.Id;

            // Determinar rango de fechas
            var hoyUtc = DateTime.UtcNow.Date;
            var endDate = DateOnly.FromDateTime(hoyUtc);
            var startDate = new DateOnly(hoyUtc.Year, hoyUtc.Month, 1); // por defecto: este mes

            if (!string.IsNullOrEmpty(rango))
            {
                if (rango == "trimestre")
                {
                    startDate = DateOnly.FromDateTime(hoyUtc.AddMonths(-3));
                }
                else if (rango == "anio")
                {
                    startDate = new DateOnly(hoyUtc.Year, 1, 1);
                }
            }

            // 1. Obtener feriados en el rango
            var feriados = await db.TblTimeReportFeriados
                .Where(f => f.Activo && f.Fechaferiado >= startDate && f.Fechaferiado <= endDate)
                .Select(f => f.Fechaferiado)
                .ToListAsync();

            // 2. Obtener total registrado por día por este empleado
            var actividades = await db.TblTimeReportActividadDiaria
                .Where(a => a.Idempleado == empId && a.Activo && a.Fechaactividad >= startDate && a.Fechaactividad <= endDate)
                .Select(a => new { a.Fechaactividad, a.Cantidadhoras })
                .ToListAsync();

            var horasPorDia = actividades
                .GroupBy(a => a.Fechaactividad)
                .ToDictionary(g => g.Key, g => g.Sum(a => a.Cantidadhoras));

            // Determinar rango de chequeo de este empleado
            var startCheck = startDate;
            if (empleado.Fechaingreso.HasValue && empleado.Fechaingreso.Value > startDate)
            {
                startCheck = empleado.Fechaingreso.Value;
            }

            var endCheck = endDate;
            if (empleado.Fechaterminacion.HasValue && empleado.Fechaterminacion.Value < endDate)
            {
                endCheck = empleado.Fechaterminacion.Value;
            }

            var diasIncompletos = new List<object>();
            decimal totalExpected = 0m;
            decimal totalRegistered = 0m;

            if (startCheck <= endCheck)
            {
                for (var date = startCheck; date <= endCheck; date = date.AddDays(1))
                {
                    var dayOfWeek = new DateTime(date.Year, date.Month, date.Day).DayOfWeek;
                    var isWeekend = dayOfWeek == DayOfWeek.Saturday || dayOfWeek == DayOfWeek.Sunday;
                    var isFeriado = feriados.Contains(date);

                    if (!isWeekend && !isFeriado)
                    {
                        totalExpected += 8m;

                        horasPorDia.TryGetValue(date, out var logged);
                        totalRegistered += logged;

                        if (logged < 8m)
                        {
                            diasIncompletos.Add(new
                            {
                                Fecha = date,
                                HorasRegistradas = logged,
                                HorasFaltantes = 8m - logged
                            });
                        }
                    }
                }
            }

            var horasFaltantes = Math.Max(0m, totalExpected - totalRegistered);

            return Results.Ok(new
            {
                TieneFaltantes = horasFaltantes > 0m,
                HorasFaltantes = horasFaltantes,
                DiasIncompletos = diasIncompletos
            });
        }).RequireAuthorization();

        group.MapGet("/proyectos/{idProyecto:int}/horas-incompletas", async (int idProyecto, string? rango, ApplicationDbContext db) =>
        {
            var hoyUtc = DateTime.UtcNow.Date;
            var endDate = DateOnly.FromDateTime(hoyUtc);
            var startDate = new DateOnly(hoyUtc.Year, hoyUtc.Month, 1); // por defecto: este mes

            if (!string.IsNullOrEmpty(rango))
            {
                if (rango == "trimestre")
                {
                    startDate = DateOnly.FromDateTime(hoyUtc.AddMonths(-3));
                }
                else if (rango == "anio")
                {
                    startDate = new DateOnly(hoyUtc.Year, 1, 1);
                }
            }

            // 1. Obtener feriados en el rango
            var feriados = await db.TblTimeReportFeriados
                .Where(f => f.Activo && f.Fechaferiado >= startDate && f.Fechaferiado <= endDate)
                .Select(f => f.Fechaferiado)
                .ToListAsync();

            // 2. Obtener colaboradores que han registrado actividades en este proyecto
            var cutoffDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-60));
            var searchStartDate = startDate < cutoffDate ? startDate : cutoffDate;

            var collaboratorIds = await db.TblTimeReportActividadDiaria
                .Where(a => a.Idproyecto == idProyecto && a.Activo && a.Fechaactividad >= searchStartDate)
                .Select(a => a.Idempleado)
                .Distinct()
                .ToListAsync();

            var empleados = await db.TblAdministracionEmpleados
                .Include(e => e.IdpersonaNavigation)
                .Where(e => e.Activo && collaboratorIds.Contains(e.Id))
                .ToListAsync();

            var resultado = new List<CollaboratorMissingHoursResponse>();

            if (empleados.Any())
            {
                var empIds = empleados.Select(e => e.Id).ToList();

                // 3. Obtener actividades de estos colaboradores en el rango
                var actividades = await db.TblTimeReportActividadDiaria
                    .Where(a => a.Activo && empIds.Contains(a.Idempleado) && a.Fechaactividad >= startDate && a.Fechaactividad <= endDate)
                    .Select(a => new { IdEmpleado = a.Idempleado, a.Fechaactividad, a.Cantidadhoras })
                    .ToListAsync();

                var horasPorDia = actividades
                    .GroupBy(a => new { a.IdEmpleado, a.Fechaactividad })
                    .ToDictionary(
                        g => (g.Key.IdEmpleado, g.Key.Fechaactividad),
                        g => g.Sum(a => a.Cantidadhoras)
                    );

                foreach (var empleado in empleados)
                {
                    var persona = empleado.IdpersonaNavigation;
                    var empId = empleado.Id;

                    // Determinar rango de chequeo de este empleado
                    var startCheck = startDate;
                    if (empleado.Fechaingreso.HasValue && empleado.Fechaingreso.Value > startDate)
                    {
                        startCheck = empleado.Fechaingreso.Value;
                    }

                    var endCheck = endDate;
                    if (empleado.Fechaterminacion.HasValue && empleado.Fechaterminacion.Value < endDate)
                    {
                        endCheck = empleado.Fechaterminacion.Value;
                    }

                    if (startCheck > endCheck) continue;

                    var diasIncompletos = new List<DiaIncompletoDto>();
                    decimal totalExpected = 0m;
                    decimal totalRegistered = 0m;

                    for (var date = startCheck; date <= endCheck; date = date.AddDays(1))
                    {
                        var dayOfWeek = new DateTime(date.Year, date.Month, date.Day).DayOfWeek;
                        var isWeekend = dayOfWeek == DayOfWeek.Saturday || dayOfWeek == DayOfWeek.Sunday;
                        var isFeriado = feriados.Contains(date);

                        if (!isWeekend && !isFeriado)
                        {
                            totalExpected += 8m;

                            horasPorDia.TryGetValue((empId, date), out var logged);
                            totalRegistered += logged;

                            if (logged < 8m)
                            {
                                diasIncompletos.Add(new DiaIncompletoDto(
                                    date,
                                    logged,
                                    8m - logged
                                ));
                            }
                        }
                    }

                    if (diasIncompletos.Any())
                    {
                        resultado.Add(new CollaboratorMissingHoursResponse(
                            empId,
                            $"{persona.Nombres} {persona.Apellidos}".Trim(),
                            empleado.Emailcorporativo ?? persona.Email ?? string.Empty,
                            totalExpected,
                            totalRegistered,
                            totalExpected - totalRegistered,
                            diasIncompletos.OrderByDescending(d => d.Fecha).ToList()
                        ));
                    }
                }
            }

            return Results.Ok(resultado.OrderByDescending(r => r.HorasFaltantes).ToList());
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
