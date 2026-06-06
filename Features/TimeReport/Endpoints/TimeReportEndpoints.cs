using Microsoft.EntityFrameworkCore;
using tmr_backend.Features.TimeReport.Domain;
using tmr_backend.Features.TimeReport.DTOs;
using tmr_backend.Infrastructure.Database;

namespace tmr_backend.Features.TimeReport;

public static class TimeReportEndpoints
{
    public static void MapTimeReportEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/time-report").WithTags("TimeReport");

        group.MapGet("/", async (ApplicationDbContext db) =>
        {
            var registros = await db.RegistrosTiempo
                .Where(c => c.Activo)
                .Select(c => new RegistroTiempoResponse(c.Id, c.Nombre, c.Descripcion, c.Activo, c.FechaCreacion))
                .ToListAsync();

            return Results.Ok(registros);
        });

        group.MapGet("/{id:guid}", async (Guid id, ApplicationDbContext db) =>
        {
            var registro = await db.RegistrosTiempo.FindAsync(id);

            if (registro is null) return Results.NotFound();

            return Results.Ok(new RegistroTiempoResponse(registro.Id, registro.Nombre, registro.Descripcion, registro.Activo, registro.FechaCreacion));
        });

        group.MapPost("/", async (CrearRegistroTiempoRequest request, ApplicationDbContext db) =>
        {
            try
            {
                var nuevoRegistro = RegistroTiempo.Crear(request.Nombre, request.Descripcion);
                
                db.RegistrosTiempo.Add(nuevoRegistro);
                await db.SaveChangesAsync();

                var response = new RegistroTiempoResponse(nuevoRegistro.Id, nuevoRegistro.Nombre, nuevoRegistro.Descripcion, nuevoRegistro.Activo, nuevoRegistro.FechaCreacion);
                return Results.Created($"/api/time-report/{nuevoRegistro.Id}", response);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { Mensaje = ex.Message });
            }
        });

        group.MapPut("/{id:guid}", async (Guid id, ActualizarRegistroTiempoRequest request, ApplicationDbContext db) =>
        {
            var registro = await db.RegistrosTiempo.FindAsync(id);

            if (registro is null) return Results.NotFound();

            try
            {
                registro.ActualizarDetalles(request.Nombre, request.Descripcion);
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
            var registro = await db.RegistrosTiempo.FindAsync(id);

            if (registro is null) return Results.NotFound();

            registro.Desactivar();
            await db.SaveChangesAsync();

            return Results.NoContent();
        });
        // ─────────────────────────────────────────────
        // TIPOS DE ACTIVIDAD
        // ─────────────────────────────────────────────
        group.MapGet("/tipos-actividad", async (ApplicationDbContext db) =>
        {
            var tiposActividad = await db.TblTimeReportTipoActividads
                .Where(t => t.Activo)
                .Select(t => new TipoActividadDto(
                    t.Id,
                    t.Nombretipo,
                    t.Descripcion
                ))
                .OrderBy(t => t.Nombre)
                .ToListAsync();

            return Results.Ok(tiposActividad);
        });

        // ─────────────────────────────────────────────
        // ACTIVIDADES
        // ─────────────────────────────────────────────
        var groupActividades = app.MapGroup("/api/time-report/actividades").WithTags("TimeReport - Actividades");

        groupActividades.MapGet("/calendario", async (int idEmpleado, int anio, int mes, ApplicationDbContext db) =>
        {
            var fechaInicio = new DateOnly(anio, mes, 1);
            var fechaFin = fechaInicio.AddMonths(1).AddDays(-1);

            var actividades = await db.TblTimeReportActividadDiaria
                .Where(a => a.Activo && a.Idempleado == idEmpleado && a.Fechaactividad >= fechaInicio && a.Fechaactividad <= fechaFin)
                .GroupBy(a => a.Fechaactividad)
                .Select(g => new ActividadDiaDto(g.Key, g.Sum(x => x.Cantidadhoras)))
                .ToListAsync();

            return Results.Ok(actividades);
        });

        groupActividades.MapGet("/resumen", async (int idEmpleado, ApplicationDbContext db) =>
        {
            var hoy = DateOnly.FromDateTime(DateTime.Today);
            var inicioMes = new DateOnly(hoy.Year, hoy.Month, 1);
            var inicioSemana = hoy.AddDays(-(int)hoy.DayOfWeek); // simplificado

            var actividadesMes = await db.TblTimeReportActividadDiaria
                .Where(a => a.Activo && a.Idempleado == idEmpleado && a.Fechaactividad >= inicioMes)
                .ToListAsync();

            var horasMes = actividadesMes.Sum(a => a.Cantidadhoras);
            var horasSemana = actividadesMes.Where(a => a.Fechaactividad >= inicioSemana).Sum(a => a.Cantidadhoras);
            
            // Asumiendo 8 horas laborables por día (hasta hoy)
            var diasLaborables = hoy.DayNumber - inicioMes.DayNumber + 1;
            var horasEsperadas = diasLaborables * 8m;
            var horasPorRegistrar = Math.Max(0, horasEsperadas - horasMes);

            return Results.Ok(new ResumenHorasDto(horasPorRegistrar, horasMes, horasSemana, horasMes));
        });

        groupActividades.MapPost("/", async (CrearActividadDto request, ApplicationDbContext db) =>
        {
            var horasRegistradasHoy = await db.TblTimeReportActividadDiaria
                .Where(a => a.Activo && a.Idempleado == request.IdEmpleado && a.Fechaactividad == request.FechaActividad)
                .SumAsync(a => a.Cantidadhoras);

            if (horasRegistradasHoy + request.CantidadHoras > 24)
            {
                return Results.BadRequest(new { Mensaje = "No puede registrar más de 24 horas en un mismo día." });
            }

            var nuevaActividad = new tmr_backend.Infrastructure.Database.Entities.TblTimeReportActividadDiarium
            {
                Idempleado = request.IdEmpleado,
                Idproyecto = request.IdProyecto,
                Idtipoactividad = request.IdTipoActividad,
                Codigorequerimiento = request.CodigoRequerimiento,
                Cantidadhoras = request.CantidadHoras,
                Fechaactividad = request.FechaActividad,
                Descripcionactividad = request.DescripcionActividad,
                Notas = request.Notas,
                Esbillable = request.EsBillable ?? true,
                Activo = true,
                Fechacreacion = DateTime.UtcNow,
                Usuariocreacion = "Sistema", // Debería tomarse del token
                Ipcreacion = "127.0.0.1"
            };

            db.TblTimeReportActividadDiaria.Add(nuevaActividad);
            await db.SaveChangesAsync();

            return Results.Created($"/api/time-report/actividades/{nuevaActividad.Id}", nuevaActividad);
        });

        // ─────────────────────────────────────────────
        // SEGUIMIENTO
        // ─────────────────────────────────────────────
        var groupSeguimiento = app.MapGroup("/api/time-report/seguimiento").WithTags("TimeReport - Seguimiento");

        groupSeguimiento.MapGet("/", async ([AsParameters] FiltroSeguimientoDto filtro, ApplicationDbContext db) =>
        {
            var query = db.TblAdministracionEmpleados
                .Where(e => e.Activo)
                .AsQueryable();

            if (!string.IsNullOrEmpty(filtro.Busqueda))
            {
                query = query.Where(e => e.IdpersonaNavigation.Nombres.Contains(filtro.Busqueda) || e.IdpersonaNavigation.Apellidos.Contains(filtro.Busqueda));
            }

            // Simplificación del JOIN para Seguimiento
            var colaboradores = await query.Select(e => new SeguimientoColaboradorDto(
                e.Id,
                e.IdpersonaNavigation.Nombres + " " + e.IdpersonaNavigation.Apellidos,
                "Proyecto X", // Debería venir de un JOIN con TblTimeReportEmpleadoProyecto
                "Cliente Y",
                "Líder Z",
                db.TblTimeReportActividadDiaria.Where(a => a.Idempleado == e.Id && a.Fechaactividad >= filtro.FechaDesde && a.Fechaactividad <= filtro.FechaHasta).Sum(a => (decimal?)a.Cantidadhoras) ?? 0,
                "Completado",
                db.TblTimeReportActividadDiaria.Where(a => a.Idempleado == e.Id && a.Fechaactividad >= filtro.FechaDesde && a.Fechaactividad <= filtro.FechaHasta).Select(a => a.Fechaactividad).Distinct().Count(),
                0
            )).ToListAsync();

            return Results.Ok(colaboradores);
        });

        groupSeguimiento.MapPost("/aprobar", async (AprobarHorasRequest request, ApplicationDbContext db) =>
        {
            var actividades = await db.TblTimeReportActividadDiaria
                .Where(a => request.Ids.Contains(a.Idempleado) && a.Fechaaprobacion == null)
                .ToListAsync();

            foreach(var act in actividades)
            {
                act.Fechaaprobacion = DateTime.UtcNow;
                act.Aprobadopor = 1; // Id del usuario logueado
            }

            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }
}
