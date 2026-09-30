using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using tmr_backend.Features.TimeReport.Domain;
using tmr_backend.Features.TimeReport.DTOs;
using tmr_backend.Features.TimeReport.Services;
using tmr_backend.Infrastructure.Database;

namespace tmr_backend.Features.TimeReport;
//comentario de prueba
public static class TimeReportEndpoints
{
    public static void MapTimeReportEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/time-report").WithTags("TimeReport")
                       .RequireAuthorization();  // sm - JWT: protege TODOS los endpoints del grupo

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
        var groupActividades = app.MapGroup("/api/time-report/actividades").WithTags("TimeReport - Actividades")
                       .RequireAuthorization();  // sm - JWT: protege TODOS los endpoints del grupo

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

        // Reporte del colaborador autenticado. No usa SEGUIMIENTO_READ porque
        // corresponde al propio perfil y no a la vista administrativa de Seguimiento.
        groupActividades.MapGet("/mi-reporte", async (ClaimsPrincipal user, DateOnly fechaDesde, DateOnly fechaHasta, ApplicationDbContext db) =>
        {
            var idEmpleado = await ObtenerEmpleadoSesionAsync(user, db);
            if (idEmpleado is null) return Results.Unauthorized();

            return await ObtenerActividadesReporteAsync(idEmpleado.Value, fechaDesde, fechaHasta, db);
        });

        groupActividades.MapGet("/calendario", async (int idEmpleado, int anio, int mes, ClaimsPrincipal user, ApplicationDbContext db) =>
        {
            // sm - Solo el propio colaborador o quien tiene permiso de Seguimiento (modal "Ver detalle", solo lectura).
            if (!await PuedeVerEmpleadoAsync(user, idEmpleado, db)) return Results.Forbid();

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

        groupActividades.MapGet("/resumen", async (int idEmpleado, int? anio, int? mes, ClaimsPrincipal user, ApplicationDbContext db) =>
        {
            // sm - Solo el propio colaborador o quien tiene permiso de Seguimiento (modal "Ver detalle", solo lectura).
            if (!await PuedeVerEmpleadoAsync(user, idEmpleado, db)) return Results.Forbid();

            // sm - Métricas del mes que muestra el calendario con la MISMA regla de negocio que Seguimiento
            // (CalculoHorasPeriodo): jornada 8 h / 6 h pasante, lunes a viernes sin feriados, ingreso/salida y hasta hoy.
            var hoyEcuador = CalculoHorasPeriodo.HoyEcuador();
            var inicio = new DateOnly(anio ?? hoyEcuador.Year, mes ?? hoyEcuador.Month, 1);
            var fin = inicio.AddMonths(1).AddDays(-1);

            var empleado = await db.TblAdministracionEmpleados
                .AsNoTracking()
                .Include(e => e.IdtipocontratoNavigation)
                .FirstOrDefaultAsync(e => e.Id == idEmpleado);
            // sm - Sin empleado no hay horas: las métricas quedan en 0 (no en guion).
            if (empleado is null) return Results.Ok(new ResumenHorasDto(0m, 0m, 0m));

            var actividadesPeriodo = await db.TblTimeReportActividadDiaria
                .AsNoTracking()
                .Where(a => a.Activo && a.Idempleado == idEmpleado && a.Fechaactividad >= inicio && a.Fechaactividad <= fin)
                .ToListAsync();
            var feriadosPeriodo = await db.TblTimeReportFeriados
                .Where(f => f.Activo && f.Fechaferiado >= inicio && f.Fechaferiado <= fin)
                .Select(f => f.Fechaferiado)
                .ToListAsync();

            var calculo = CalculoHorasPeriodo.Calcular(empleado, inicio, fin, actividadesPeriodo, feriadosPeriodo, hoyEcuador);
            return Results.Ok(new ResumenHorasDto(calculo.HorasPorRegistrar, calculo.HorasRegistradas, calculo.PromedioPorDia));

            // sm - Se comenta el resumen anterior: usaba 8 h para todos (también pasantes), contaba el mes completo
            // (incluidos días futuros) y devolvía horas de hoy/semana/mes, que ya no se muestran en Actividades.
            /*
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
            */
        });

        groupActividades.MapPost("/", async (CrearActividadDto request, ClaimsPrincipal user, ApplicationDbContext db) =>
        {
            // sm - Solo se pueden registrar actividades propias (antes se aceptaba cualquier IdEmpleado del cuerpo).
            if (await ObtenerEmpleadoSesionAsync(user, db) != request.IdEmpleado) return Results.Forbid();

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

        groupActividades.MapPut("/{id:int}", async (int id, ActualizarActividadDto request, ClaimsPrincipal user, ApplicationDbContext db) =>
        {
            var actividad = await db.TblTimeReportActividadDiaria.FindAsync(id);

            if (actividad is null) return Results.NotFound();
            // sm - Solo se pueden editar actividades propias.
            if (await ObtenerEmpleadoSesionAsync(user, db) != actividad.Idempleado) return Results.Forbid();

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

        groupActividades.MapDelete("/{id:int}", async (int id, ClaimsPrincipal user, ApplicationDbContext db) =>
        {
            var actividad = await db.TblTimeReportActividadDiaria.FindAsync(id);

            if (actividad is null) return Results.NotFound();
            // sm - Solo se pueden eliminar actividades propias.
            if (await ObtenerEmpleadoSesionAsync(user, db) != actividad.Idempleado) return Results.Forbid();

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
        // sm - Seguimiento solo para quien tiene el módulo "Seguimiento" en su rol (permiso SEGUIMIENTO_READ),
        // no para cualquier usuario con sesión: aquí se ven las horas y reportes de todos los colaboradores.
        var groupSeguimiento = app.MapGroup("/api/time-report/seguimiento").WithTags("TimeReport - Seguimiento")
                       .RequireAuthorization("SEGUIMIENTO_READ");

        groupSeguimiento.MapGet("/", async ([AsParameters] FiltroSeguimientoDto filtro, ApplicationDbContext db) =>
        {
            // sm - filtro.Periodo (quincena/mes-completo) no se usa aquí: el frontend ya lo resuelve
            // a FechaDesde/FechaHasta antes de llamar a este endpoint. Se recibe pero no se aplica.
            // sm - Asignaciones que cuentan para el rango: activas (las inactivas son versiones anteriores que se
            // reemplazan al guardar el proyecto) y cuyo periodo de entrada/salida se cruza con el rango consultado.
            // Así Proyecto/Cliente/Líder muestran lo que el colaborador tenía en ese rango y no solo lo actual.
            var employees = await db.TblAdministracionEmpleados
                // sm - Se comenta el filtro anterior porque solo mostraba activos y ocultaba a quien salió dentro del rango.
                // .Where(e => e.Activo)
                // sm - Nuevo filtro: todos los activos + los inactivos cuya fecha de terminación está dentro del rango filtrado
                // (fechas exactas del rango). Inactivos sin fecha de terminación o que salieron fuera del rango no aparecen.
                .Where(e => e.Activo
                    || (e.Fechaterminacion.HasValue
                        && e.Fechaterminacion.Value >= filtro.FechaDesde
                        && e.Fechaterminacion.Value <= filtro.FechaHasta))
                .Include(e => e.IdpersonaNavigation)
                // sm - Se incluye el tipo de contrato para saber si es pasante (jornada de 6 h).
                .Include(e => e.IdtipocontratoNavigation)
                .Include(e => e.TblTimeReportAsignacionProyectos.Where(ep => ep.Activo
                        && (ep.Fechaasignacion == null || ep.Fechaasignacion <= filtro.FechaHasta)
                        && (ep.Fechafinasignacion == null || ep.Fechafinasignacion >= filtro.FechaDesde)))
                    .ThenInclude(ep => ep.IdproyectoNavigation)
                        .ThenInclude(p => p.IdclienteNavigation)
                .Include(e => e.TblTimeReportAsignacionProyectos)
                    .ThenInclude(ep => ep.IdliderNavigation)
                        .ThenInclude(l => l.IdpersonaNavigation)
                .ToListAsync();

            // sm - Se quita el filtro de búsqueda del backend: la búsqueda (solo colaborador y proyecto) se hace en el
            // frontend sobre los datos ya cargados, sin volver a llamar al servidor en cada tecla.
            // sm - El filtro de cliente usa las mismas asignaciones del rango que se muestran en la tabla.
            if (!string.IsNullOrEmpty(filtro.ClienteSeleccionado))
            {
                employees = employees.Where(e => e.TblTimeReportAsignacionProyectos.Any(ep =>
                    ep.IdproyectoNavigation.IdclienteNavigation != null &&
                    (ep.IdproyectoNavigation.IdclienteNavigation.Nombrecomercial == filtro.ClienteSeleccionado ||
                     ep.IdproyectoNavigation.IdclienteNavigation.Razonsocial == filtro.ClienteSeleccionado))).ToList();
            }

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

            // sm - "Hoy" según la hora de Ecuador (ver CalculoHorasPeriodo): no se cuentan los días futuros del rango.
            var hoyEcuador = CalculoHorasPeriodo.HoyEcuador();

            foreach (var e in employees)
            {
                var empActividades = actividades.Where(a => a.Idempleado == e.Id).ToList();
                // sm - Se comenta el cálculo anterior de horas y días porque:
                // - nroHoras incluía días futuros del rango (se limita hasta hoy).
                // - diasConReporte contaba cualquier día con al menos una actividad (aunque fuera 1 h o fin de semana).
                // - diasACompletar usaba todos los días laborables del rango, sin considerar hoy, jornada ni fecha de ingreso/salida.
                // var nroHoras = empActividades.Sum(a => a.Cantidadhoras);
                // var diasConReporte = empActividades.Select(a => a.Fechaactividad).Distinct().Count();
                //
                // // Calculate working days in the range
                // var workingDays = 0;
                // var current = filtro.FechaDesde;
                // while (current <= filtro.FechaHasta)
                // {
                //     var dayOfWeek = current.ToDateTime(TimeOnly.MinValue).DayOfWeek;
                //     var isWeekend = dayOfWeek == DayOfWeek.Saturday || dayOfWeek == DayOfWeek.Sunday;
                //     var isFeriado = feriados.Contains(current);
                //     if (!isWeekend && !isFeriado)
                //     {
                //         workingDays++;
                //     }
                //     current = current.AddDays(1);
                // }
                //
                // var diasACompletar = Math.Max(0, workingDays - diasConReporte);

                // sm - El cálculo de horas, días y estado se movió a CalculoHorasPeriodo (Services) para que las métricas
                // de Actividades usen exactamente la misma regla de negocio que esta tabla.
                // sm - Se comenta el estado anterior porque dependía de la aprobación de horas (funcionalidad retirada).
                // var estado = "Pendiente";
                // if (empActividades.Any())
                // {
                //     var allApproved = empActividades.All(a => a.Fechaaprobacion != null);
                //     estado = allApproved ? "Completo" : "En progreso";
                // }
                var calculo = CalculoHorasPeriodo.Calcular(e, filtro.FechaDesde, filtro.FechaHasta, empActividades, feriados, hoyEcuador);

                // Project details (sm - asignaciones ya filtradas a las activas del rango en el Include)
                var empProys = e.TblTimeReportAsignacionProyectos.ToList();
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

                // sm - Desglose por proyecto (id, nombre, cliente, líder y SUS horas registradas en el rango). No se
                // recalcula jornada/estado por proyecto (no aplica: la jornada es del día completo del colaborador,
                // no por proyecto). Se usa para el modal "Ver detalle" y para generar un archivo por proyecto al descargar.
                var proyectosDto = empProys
                    .GroupBy(ep => ep.Idproyecto)
                    .Select(g =>
                    {
                        var clienteNombre = g.First().IdproyectoNavigation.IdclienteNavigation?.Nombrecomercial
                            ?? g.First().IdproyectoNavigation.IdclienteNavigation?.Razonsocial;
                        if (string.IsNullOrWhiteSpace(clienteNombre)) clienteNombre = "Sin Cliente";

                        var liderNombre = string.Join(", ", g
                            .Where(ep => ep.IdliderNavigation != null)
                            .Select(ep => ep.IdliderNavigation.IdpersonaNavigation.Nombres + " " + ep.IdliderNavigation.IdpersonaNavigation.Apellidos)
                            .Distinct());
                        if (string.IsNullOrWhiteSpace(liderNombre)) liderNombre = "Sin Líder";

                        // sm - Se limita a "hasta hoy" (igual que calculo.HorasRegistradas) para que la suma de horas
                        // por proyecto no supere el total del colaborador cuando el rango consultado llega al futuro.
                        var horasProyecto = empActividades
                            .Where(a => a.Idproyecto == g.Key && a.Fechaactividad <= hoyEcuador)
                            .Sum(a => a.Cantidadhoras);

                        return new SeguimientoProyectoDto(g.Key, g.First().IdproyectoNavigation.Nombre, clienteNombre, liderNombre, horasProyecto);
                    })
                    .ToList();

                colaboradores.Add(new SeguimientoColaboradorDto(
                    e.Id,
                    e.IdpersonaNavigation.Nombres + " " + e.IdpersonaNavigation.Apellidos,
                    proyectosStr,
                    clientesStr,
                    lideresStr,
                    calculo.HorasRegistradas,
                    calculo.Estado,
                    calculo.DiasConReporte,
                    calculo.DiasACompletar,
                    // sm - Nuevos valores para la métrica "Horas por registrar" del frontend.
                    calculo.HorasJornada,
                    calculo.HorasEsperadas,
                    calculo.HorasPorRegistrar,
                    // sm - Valores para la métrica "Promedio por día" (horas en días laborables ÷ días laborables del periodo).
                    calculo.DiasLaborables,
                    calculo.HorasRegistradas,
                    proyectosDto
                ));
            }

            return Results.Ok(colaboradores);
        });

        // sm - Se comenta el endpoint de aprobación de horas: la funcionalidad de aprobar se retira de Seguimiento.
        // groupSeguimiento.MapPost("/aprobar", async (AprobarHorasRequest request, ApplicationDbContext db) =>
        // {
        //     var actividades = await db.TblTimeReportActividadDiaria
        //         .Where(a => request.Ids.Contains(a.Idempleado) && a.Fechaaprobacion == null)
        //         .ToListAsync();
        //
        //     foreach(var act in actividades)
        //     {
        //         act.Fechaaprobacion = DateTime.UtcNow;
        //         act.Aprobadopor = 1; // Id del usuario logueado
        //     }
        //
        //     await db.SaveChangesAsync();
        //     return Results.NoContent();
        // });

        groupSeguimiento.MapGet("/colaborador/{id:int}/actividades", async (int id, DateOnly fechaDesde, DateOnly fechaHasta, int? idProyecto, ApplicationDbContext db) =>
        {
            var registros = await db.TblTimeReportActividadDiaria
                // sm - idProyecto filtra el reporte a un solo proyecto (una fila de Seguimiento = un proyecto);
                // sin idProyecto se mantiene el comportamiento anterior (todas las actividades del colaborador).
                .Where(a => a.Activo && a.Idempleado == id && a.Fechaactividad >= fechaDesde && a.Fechaactividad <= fechaHasta
                    && (idProyecto == null || a.Idproyecto == idProyecto))
                .Select(a => new {
                    a.Idproyecto,
                    a.Fechaactividad,
                    Proyecto = a.IdproyectoNavigation != null ? a.IdproyectoNavigation.Nombre : "Sin Proyecto",
                    TipoActividad = a.IdtipoactividadNavigation != null ? a.IdtipoactividadNavigation.Nombretipo : "Otro",
                    CodigoRequerimiento = a.Codigorequerimiento ?? "",
                    Horas = a.Cantidadhoras,
                    Descripcion = a.Descripcionactividad ?? "",
                    Notas = a.Notas ?? "",
                    EsBillable = a.Esbillable == true ? "Sí" : "No",
                    ClienteProyecto = a.IdproyectoNavigation != null && a.IdproyectoNavigation.IdclienteNavigation != null
                        ? (a.IdproyectoNavigation.IdclienteNavigation.Nombrecomercial ?? a.IdproyectoNavigation.IdclienteNavigation.Razonsocial ?? "Sin Cliente")
                        : "Sin Cliente"
                })
                .ToListAsync();

            // sm - Se comenta el líder anterior: tomaba el primer líder activo del proyecto (de cualquier colaborador, sin
            // orden ni fechas), así que en "Revisado y Aprobado por" podía salir un líder que no era el del colaborador.
            // LiderProyecto = a.IdproyectoNavigation.TblTimeReportAsignacionProyectos
            //     .Where(ep => ep.Activo && ep.Idlider != null && ...).Select(ep => nombre del líder).FirstOrDefault() ?? "Sin Líder"
            // sm - Nuevo: el líder de la asignación de ESTE colaborador en ese proyecto vigente en la fecha de la actividad.
            // Si ninguna asignación cubre esa fecha, se usa la asignación más reciente del colaborador en el proyecto.
            var asignaciones = await db.TblTimeReportAsignacionProyectos
                .Where(ep => ep.Activo && ep.Idempleado == id
                    && ep.IdliderNavigation != null && ep.IdliderNavigation.IdpersonaNavigation != null)
                .Select(ep => new
                {
                    ep.Idproyecto,
                    ep.Fechaasignacion,
                    ep.Fechafinasignacion,
                    Lider = ep.IdliderNavigation!.IdpersonaNavigation.Nombres + " " + ep.IdliderNavigation.IdpersonaNavigation.Apellidos
                })
                .ToListAsync();

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

            var actividades = registros.Select(a => new {
                IdProyecto = a.Idproyecto,
                Fecha = a.Fechaactividad.ToString("yyyy-MM-dd"),
                a.Proyecto,
                a.TipoActividad,
                a.CodigoRequerimiento,
                a.Horas,
                a.Descripcion,
                a.Notas,
                a.EsBillable,
                LiderProyecto = LiderDe(a.Idproyecto, a.Fechaactividad),
                a.ClienteProyecto
            }).ToList();

            var feriados = await db.TblTimeReportFeriados
                .Where(f => f.Activo && f.Fechaferiado >= fechaDesde && f.Fechaferiado <= fechaHasta)
                .Select(f => f.Fechaferiado)
                .ToListAsync();

            var feriadosList = feriados.Select(f => f.ToString("yyyy-MM-dd")).ToList();

            return Results.Ok(new { Actividades = actividades, Feriados = feriadosList });
        });
    }

    // sm - Id del empleado del usuario de la sesión (usuario → persona → empleado activo), con la misma regla que el login.
    // null si el token no trae usuario o el usuario no tiene un empleado activo.
    private static async Task<int?> ObtenerEmpleadoSesionAsync(ClaimsPrincipal user, ApplicationDbContext db)
    {
        var sub = user.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value
                  ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(sub, out var idUsuario)) return null;

        var idPersona = await db.TblAutenticacionUsuarios
            .Where(u => u.Id == idUsuario && u.Activo)
            .Select(u => u.Idpersona)
            .FirstOrDefaultAsync();
        if (idPersona is null) return null;

        return await db.TblAdministracionEmpleados
            .Where(e => e.Idpersona == idPersona && e.Activo)
            .Select(e => (int?)e.Id)
            .FirstOrDefaultAsync();
    }

    private static async Task<IResult> ObtenerActividadesReporteAsync(
        int idEmpleado,
        DateOnly fechaDesde,
        DateOnly fechaHasta,
        ApplicationDbContext db)
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
                ClienteProyecto = a.IdproyectoNavigation != null && a.IdproyectoNavigation.IdclienteNavigation != null
                    ? (a.IdproyectoNavigation.IdclienteNavigation.Nombrecomercial ?? a.IdproyectoNavigation.IdclienteNavigation.Razonsocial ?? "Sin Cliente")
                    : "Sin Cliente"
            })
            .ToListAsync();

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
            .ToListAsync();

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

        var actividades = registros.Select(a => new
        {
            IdProyecto = a.Idproyecto,
            Fecha = a.Fechaactividad.ToString("yyyy-MM-dd"),
            a.Proyecto,
            a.TipoActividad,
            a.CodigoRequerimiento,
            a.Horas,
            a.Descripcion,
            a.Notas,
            a.EsBillable,
            LiderProyecto = LiderDe(a.Idproyecto, a.Fechaactividad),
            a.ClienteProyecto
        }).ToList();

        var feriados = await db.TblTimeReportFeriados
            .Where(f => f.Activo && f.Fechaferiado >= fechaDesde && f.Fechaferiado <= fechaHasta)
            .Select(f => f.Fechaferiado)
            .ToListAsync();

        return Results.Ok(new
        {
            Actividades = actividades,
            Feriados = feriados.Select(f => f.ToString("yyyy-MM-dd")).ToList()
        });
    }

    // sm - Puede ver el calendario/métricas de un empleado: él mismo, o quien tiene el permiso de Seguimiento
    // (el modal "Ver detalle" de Seguimiento es de solo lectura).
    private static async Task<bool> PuedeVerEmpleadoAsync(ClaimsPrincipal user, int idEmpleado, ApplicationDbContext db) =>
        user.HasClaim("permission", "SEGUIMIENTO_READ")
        || await ObtenerEmpleadoSesionAsync(user, db) == idEmpleado;
}
