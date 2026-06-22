using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
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
        // ACTIVIDADES
        // ─────────────────────────────────────────────
        var groupActividades = app.MapGroup("/api/time-report/actividades").WithTags("TimeReport - Actividades");

        groupActividades.MapGet("/tipos-actividad", async (ApplicationDbContext db) =>
        {
            var tipos = await db.TblTimeReportTipoActividads
                .Where(t => t.Activo)
                .Select(t => new { Id = t.Id, Nombre = t.Nombretipo })
                .ToListAsync();
            return Results.Ok(tipos);
        });

        groupActividades.MapGet("/proyectos-disponibles", async (ClaimsPrincipal user, ApplicationDbContext db) =>
        {
            // Si no está autenticado (desarrollo local / testing sin JWT), devolvemos todos los proyectos activos
            if (user.Identity?.IsAuthenticated != true)
            {
                var todosProyectos = await db.TblTimeReportProyectos
                    .AsNoTracking()
                    .Where(p => p.Activo)
                    .Select(p => new ProyectoLookupDto(p.Id, p.Nombre, p.Codigo))
                    .ToListAsync();
                return Results.Ok(todosProyectos);
            }

            var userIdClaim = user.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value 
                              ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var usuarioAutenticadoId))
            {
                return Results.Unauthorized();
            }

            var roles = user.FindAll(ClaimTypes.Role).Select(r => r.Value.ToUpper()).ToList();

            var usuarioDb = await db.TblAutenticacionUsuarios
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == usuarioAutenticadoId && u.Activo);

            if (usuarioDb == null) return Results.NotFound("Usuario no encontrado.");

            var empleado = await db.TblAdministracionEmpleados
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Idpersona == usuarioDb.Idpersona && e.Activo);

            if (empleado == null) return Results.NotFound("Empleado no encontrado.");

            if (roles.Contains("ADMINISTRADOR") || roles.Contains("RECURSOS HUMANOS") || roles.Contains("RECURSOS_HUMANOS"))
            {
                var proyectos = await db.TblTimeReportProyectos
                    .AsNoTracking()
                    .Where(p => p.Activo)
                    .Select(p => new ProyectoLookupDto(p.Id, p.Nombre, p.Codigo))
                    .ToListAsync();
                return Results.Ok(proyectos);
            }
            else if (roles.Contains("GERENTE"))
            {
                var proyectos = await db.TblTimeReportProyectos
                    .AsNoTracking()
                    .Where(p => p.Activo && p.TblTimeReportAsignacionProyectos.Any(ep => ep.Activo && ep.Idlider != null))
                    .Select(p => new ProyectoLookupDto(p.Id, p.Nombre, p.Codigo))
                    .ToListAsync();
                return Results.Ok(proyectos);
            }
            else if (roles.Contains("LIDER"))
            {
                var lider = await db.TblAdministracionLiders
                    .AsNoTracking()
                    .FirstOrDefaultAsync(l => l.Idpersona == empleado.Idpersona && l.Activo);

                if (lider == null) return Results.Ok(new List<ProyectoLookupDto>());

                var proyectos = await db.TblTimeReportProyectos
                    .AsNoTracking()
                    .Where(p => p.Activo && p.TblTimeReportAsignacionProyectos.Any(ep => ep.Activo && ep.Idlider == lider.Id))
                    .Select(p => new ProyectoLookupDto(p.Id, p.Nombre, p.Codigo))
                    .ToListAsync();
                return Results.Ok(proyectos);
            }
            else
            {
                var proyectos = await db.TblTimeReportAsignacionProyectos
                    .AsNoTracking()
                    .Where(ep => ep.Idempleado == empleado.Id && ep.Activo && ep.IdproyectoNavigation.Activo)
                    .Select(ep => new ProyectoLookupDto(ep.Idproyecto, ep.IdproyectoNavigation.Nombre, ep.IdproyectoNavigation.Codigo))
                    .Distinct()
                    .ToListAsync();
                return Results.Ok(proyectos);
            }
        });

        groupActividades.MapGet("/calendario", async (int idEmpleado, int anio, int mes, ApplicationDbContext db) =>
        {
            var fechaInicio = new DateOnly(anio, mes, 1);
            var fechaFin = fechaInicio.AddMonths(1).AddDays(-1);

            var actividades = await db.TblTimeReportActividadDiaria
                .Where(a => a.Activo && a.Idempleado == idEmpleado && a.Fechaactividad >= fechaInicio && a.Fechaactividad <= fechaFin)
                .Include(a => a.IdproyectoNavigation)
                .Include(a => a.IdtipoactividadNavigation)
                .Select(a => new CalendarioActividadDto(
                    a.Id,
                    a.Idempleado,
                    a.Idproyecto,
                    a.IdproyectoNavigation != null ? a.IdproyectoNavigation.Nombre : "Sin Proyecto",
                    a.Idtipoactividad,
                    a.IdtipoactividadNavigation != null ? a.IdtipoactividadNavigation.Nombretipo : "Otro",
                    a.Codigorequerimiento,
                    a.Cantidadhoras,
                    a.Fechaactividad,
                    a.Descripcionactividad,
                    a.Notas,
                    a.Esbillable
                ))
                .ToListAsync();

            return Results.Ok(actividades);
        });

        groupActividades.MapGet("/resumen", async (int idEmpleado, int? anio, int? mes, ApplicationDbContext db) =>
        {
            var hoy = DateOnly.FromDateTime(DateTime.Today);
            int year = anio ?? hoy.Year;
            int month = mes ?? hoy.Month;

            var inicioMes = new DateOnly(year, month, 1);
            var ultimoDiaMes = inicioMes.AddMonths(1).AddDays(-1);

            // Cargar las actividades del mes seleccionado
            var actividadesMes = await db.TblTimeReportActividadDiaria
                .Where(a => a.Activo && a.Idempleado == idEmpleado && a.Fechaactividad >= inicioMes && a.Fechaactividad <= ultimoDiaMes)
                .ToListAsync();

            var horasMes = actividadesMes.Sum(a => a.Cantidadhoras);

            // Horas registradas el día de hoy (solo si el mes seleccionado es el actual)
            var horasHoy = (year == hoy.Year && month == hoy.Month)
                ? actividadesMes.Where(a => a.Fechaactividad == hoy).Sum(a => a.Cantidadhoras)
                : 0m;

            // Calcular las horas registradas en la semana actual (siempre basada en la fecha de hoy)
            var inicioSemana = hoy.AddDays(-(int)hoy.DayOfWeek); // Definición de la semana actual
            var horasSemana = await db.TblTimeReportActividadDiaria
                .Where(a => a.Activo && a.Idempleado == idEmpleado && a.Fechaactividad >= inicioSemana && a.Fechaactividad <= hoy)
                .SumAsync(a => a.Cantidadhoras);

            // Obtener todos los feriados activos de todo el mes seleccionado
            var feriados = await db.TblTimeReportFeriados
                .Where(f => f.Activo && f.Fechaferiado >= inicioMes && f.Fechaferiado <= ultimoDiaMes)
                .Select(f => f.Fechaferiado)
                .ToListAsync();

            // Calcular los días laborables del mes completo (lunes a viernes, sin feriados)
            int diasLaborables = 0;
            for (var fecha = inicioMes; fecha <= ultimoDiaMes; fecha = fecha.AddDays(1))
            {
                var dayOfWeek = fecha.ToDateTime(TimeOnly.MinValue).DayOfWeek;
                var esFinDeSemana = dayOfWeek == DayOfWeek.Saturday || dayOfWeek == DayOfWeek.Sunday;
                var esFeriado = feriados.Contains(fecha);

                if (!esFinDeSemana && !esFeriado)
                {
                    diasLaborables++;
                }
            }

            var horasEsperadas = diasLaborables * 8m;
            var horasPorRegistrar = Math.Max(0m, horasEsperadas - horasMes);

            return Results.Ok(new ResumenHorasDto(horasPorRegistrar, horasHoy, horasSemana, horasMes));
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

        groupActividades.MapPut("/{id:int}", async (int id, ActualizarActividadDto request, ApplicationDbContext db) =>
        {
            var actividad = await db.TblTimeReportActividadDiaria.FindAsync(id);

            if (actividad is null) return Results.NotFound();

            // Validar horas registradas en el día si cambia la fecha o la cantidad de horas
            var totalHorasDia = await db.TblTimeReportActividadDiaria
                .Where(a => a.Activo && a.Id != id && a.Idempleado == actividad.Idempleado && a.Fechaactividad == request.FechaActividad)
                .SumAsync(a => a.Cantidadhoras);

            if (totalHorasDia + request.CantidadHoras > 24)
            {
                return Results.BadRequest(new { Mensaje = "No puede registrar más de 24 horas en un mismo día." });
            }

            actividad.Idproyecto = request.IdProyecto;
            actividad.Idtipoactividad = request.IdTipoActividad;
            actividad.Codigorequerimiento = request.CodigoRequerimiento;
            actividad.Cantidadhoras = request.CantidadHoras;
            actividad.Fechaactividad = request.FechaActividad;
            actividad.Descripcionactividad = request.DescripcionActividad;
            actividad.Notas = request.Notas;
            actividad.Esbillable = request.EsBillable ?? true;
            actividad.Fechamodificacion = DateTime.UtcNow;
            actividad.Usuariomodificacion = "Sistema";
            actividad.Ipmodificacion = "127.0.0.1";

            await db.SaveChangesAsync();

            return Results.NoContent();
        });

        groupActividades.MapDelete("/{id:int}", async (int id, ApplicationDbContext db) =>
        {
            var actividad = await db.TblTimeReportActividadDiaria.FindAsync(id);

            if (actividad is null) return Results.NotFound();

            // Eliminación lógica
            actividad.Activo = false;
            actividad.Fechamodificacion = DateTime.UtcNow;
            actividad.Usuariomodificacion = "Sistema";
            actividad.Ipmodificacion = "127.0.0.1";

            await db.SaveChangesAsync();

            return Results.NoContent();
        });

        // ─────────────────────────────────────────────
        // SEGUIMIENTO
        // ─────────────────────────────────────────────
        var groupSeguimiento = app.MapGroup("/api/time-report/seguimiento").WithTags("TimeReport - Seguimiento");

        groupSeguimiento.MapGet("/", async ([AsParameters] FiltroSeguimientoDto filtro, ApplicationDbContext db) =>
        {
            var query = db.TblAdministracionEmpleados
                .Where(e => e.Activo)
                .Include(e => e.IdpersonaNavigation)
                .Include(e => e.TblTimeReportAsignacionProyectos)
                    .ThenInclude(ep => ep.IdproyectoNavigation)
                        .ThenInclude(p => p.IdclienteNavigation)
                .Include(e => e.TblTimeReportAsignacionProyectos)
                    .ThenInclude(ep => ep.IdliderNavigation)
                        .ThenInclude(l => l.IdpersonaNavigation)
                .AsQueryable();

            if (!string.IsNullOrEmpty(filtro.Busqueda))
            {
                var term = filtro.Busqueda.ToLower();
                query = query.Where(e => 
                    e.IdpersonaNavigation.Nombres.ToLower().Contains(term) 
                    || e.IdpersonaNavigation.Apellidos.ToLower().Contains(term)
                    || e.TblTimeReportAsignacionProyectos.Any(ep => 
                        ep.Activo 
                        && ep.IdproyectoNavigation.Activo 
                        && ep.IdproyectoNavigation.Nombre.ToLower().Contains(term))
                    || e.TblTimeReportAsignacionProyectos.Any(ep => 
                        ep.Activo 
                        && ep.IdproyectoNavigation.Activo 
                        && ep.IdliderNavigation != null 
                        && (ep.IdliderNavigation.IdpersonaNavigation.Nombres.ToLower().Contains(term)
                            || ep.IdliderNavigation.IdpersonaNavigation.Apellidos.ToLower().Contains(term)))
                );
            }

            if (!string.IsNullOrEmpty(filtro.ClienteSeleccionado))
            {
                query = query.Where(e => e.TblTimeReportAsignacionProyectos.Any(ep => 
                    ep.Activo && ep.IdproyectoNavigation.Activo && ep.IdproyectoNavigation.IdclienteNavigation != null &&
                    (ep.IdproyectoNavigation.IdclienteNavigation.Nombrecomercial == filtro.ClienteSeleccionado || 
                     ep.IdproyectoNavigation.IdclienteNavigation.Razonsocial == filtro.ClienteSeleccionado)));
            }

            var employees = await query.ToListAsync();

            // Fetch feriados in the range
            var feriados = await db.TblTimeReportFeriados
                .Where(f => f.Activo && f.Fechaferiado >= filtro.FechaDesde && f.Fechaferiado <= filtro.FechaHasta)
                .Select(f => f.Fechaferiado)
                .ToListAsync();

            // Fetch activities for these employees in the range
            var empIds = employees.Select(e => e.Id).ToList();
            var actividades = await db.TblTimeReportActividadDiaria
                .Where(a => a.Activo && empIds.Contains(a.Idempleado) && a.Fechaactividad >= filtro.FechaDesde && a.Fechaactividad <= filtro.FechaHasta)
                .ToListAsync();

            var colaboradores = new List<SeguimientoColaboradorDto>();

            foreach (var e in employees)
            {
                var empActividades = actividades.Where(a => a.Idempleado == e.Id).ToList();
                var nroHoras = empActividades.Sum(a => a.Cantidadhoras);
                var diasConReporte = empActividades.Select(a => a.Fechaactividad).Distinct().Count();

                // Calculate working days in the range
                var workingDays = 0;
                var current = filtro.FechaDesde;
                while (current <= filtro.FechaHasta)
                {
                    var dayOfWeek = current.ToDateTime(TimeOnly.MinValue).DayOfWeek;
                    var isWeekend = dayOfWeek == DayOfWeek.Saturday || dayOfWeek == DayOfWeek.Sunday;
                    var isFeriado = feriados.Contains(current);
                    if (!isWeekend && !isFeriado)
                    {
                        workingDays++;
                    }
                    current = current.AddDays(1);
                }

                var diasACompletar = Math.Max(0, workingDays - diasConReporte);

                // Determine Estado
                var estado = "Pendiente";
                if (empActividades.Any())
                {
                    var allApproved = empActividades.All(a => a.Fechaaprobacion != null);
                    estado = allApproved ? "Completo" : "En progreso";
                }

                // Project details
                var empProys = e.TblTimeReportAsignacionProyectos.Where(ep => ep.Activo).ToList();
                var proyectosStr = empProys.Any() 
                    ? string.Join(", ", empProys.Select(ep => ep.IdproyectoNavigation.Nombre).Distinct()) 
                    : "Sin Proyecto";

                var clientesStr = empProys.Any()
                    ? string.Join(", ", empProys.Where(ep => ep.IdproyectoNavigation.IdclienteNavigation != null).Select(ep => ep.IdproyectoNavigation.IdclienteNavigation.Nombrecomercial ?? ep.IdproyectoNavigation.IdclienteNavigation.Razonsocial).Distinct())
                    : "Sin Cliente";
                if (string.IsNullOrWhiteSpace(clientesStr)) clientesStr = "Sin Cliente";

                var lideresStr = empProys.Any()
                    ? string.Join(", ", empProys.Where(ep => ep.IdliderNavigation != null).Select(ep => ep.IdliderNavigation.IdpersonaNavigation.Nombres + " " + ep.IdliderNavigation.IdpersonaNavigation.Apellidos).Distinct())
                    : "Sin Líder";
                if (string.IsNullOrWhiteSpace(lideresStr)) lideresStr = "Sin Líder";

                colaboradores.Add(new SeguimientoColaboradorDto(
                    e.Id,
                    e.IdpersonaNavigation.Nombres + " " + e.IdpersonaNavigation.Apellidos,
                    proyectosStr,
                    clientesStr,
                    lideresStr,
                    nroHoras,
                    estado,
                    diasConReporte,
                    diasACompletar
                ));
            }

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

        groupSeguimiento.MapGet("/colaborador/{id:int}/actividades", async (int id, DateOnly fechaDesde, DateOnly fechaHasta, ApplicationDbContext db) =>
        {
            var actividades = await db.TblTimeReportActividadDiaria
                .Where(a => a.Activo && a.Idempleado == id && a.Fechaactividad >= fechaDesde && a.Fechaactividad <= fechaHasta)
                .Select(a => new {
                    Fecha = a.Fechaactividad.ToString("yyyy-MM-dd"),
                    Proyecto = a.IdproyectoNavigation != null ? a.IdproyectoNavigation.Nombre : "Sin Proyecto",
                    TipoActividad = a.IdtipoactividadNavigation != null ? a.IdtipoactividadNavigation.Nombretipo : "Otro",
                    CodigoRequerimiento = a.Codigorequerimiento ?? "",
                    Horas = a.Cantidadhoras,
                    Descripcion = a.Descripcionactividad ?? "",
                    Notas = a.Notas ?? "",
                    EsBillable = a.Esbillable == true ? "Sí" : "No",
                    LiderProyecto = a.IdproyectoNavigation != null 
                        ? (a.IdproyectoNavigation.TblTimeReportAsignacionProyectos
                            .Where(ep => ep.Activo && ep.Idlider != null && ep.IdliderNavigation != null && ep.IdliderNavigation.IdpersonaNavigation != null)
                            .Select(ep => ep.IdliderNavigation.IdpersonaNavigation.Nombres + " " + ep.IdliderNavigation.IdpersonaNavigation.Apellidos)
                            .FirstOrDefault() ?? "Sin Líder")
                        : "Sin Líder",
                    ClienteProyecto = a.IdproyectoNavigation != null && a.IdproyectoNavigation.IdclienteNavigation != null 
                        ? (a.IdproyectoNavigation.IdclienteNavigation.Nombrecomercial ?? a.IdproyectoNavigation.IdclienteNavigation.Razonsocial ?? "Sin Cliente")
                        : "Sin Cliente"
                })
                .ToListAsync();

            var feriados = await db.TblTimeReportFeriados
                .Where(f => f.Activo && f.Fechaferiado >= fechaDesde && f.Fechaferiado <= fechaHasta)
                .Select(f => f.Fechaferiado)
                .ToListAsync();

            var feriadosList = feriados.Select(f => f.ToString("yyyy-MM-dd")).ToList();

            return Results.Ok(new { Actividades = actividades, Feriados = feriadosList });
        });
    }
}