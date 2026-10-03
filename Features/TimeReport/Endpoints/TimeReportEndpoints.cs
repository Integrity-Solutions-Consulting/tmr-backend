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

            // sm - Confirmado (2026-10-02): nadie debe cargar horas en un proyecto donde no está asignado. Antes Administrador,
            // Recursos Humanos, Gerente y Líder veían todos los proyectos (o los que lideran) al registrar SUS horas.
            // Esta lista se usa para registrar actividades y para el reporte propio: ahora es la misma para todos los roles.
            var proyectosAsignados = await db.TblTimeReportAsignacionProyectos
                .AsNoTracking()
                .Where(ep => ep.Idempleado == empleado.Id && ep.Activo && ep.IdproyectoNavigation.Activo)
                .Select(ep => new ProyectoLookupDto(ep.Idproyecto, ep.IdproyectoNavigation.Nombre, ep.IdproyectoNavigation.Codigo))
                .Distinct()
                .ToListAsync();
            return Results.Ok(proyectosAsignados);

            // sm - Lógica anterior por rol (comentada por el motivo de arriba):
            // if (roles.Contains("ADMINISTRADOR") || roles.Contains("RECURSOS HUMANOS") || roles.Contains("RECURSOS_HUMANOS"))
            // {
            //     var proyectos = await db.TblTimeReportProyectos
            //         .AsNoTracking()
            //         .Where(p => p.Activo)
            //         .Select(p => new ProyectoLookupDto(p.Id, p.Nombre, p.Codigo))
            //         .ToListAsync();
            //     return Results.Ok(proyectos);
            // }
            // else if (roles.Contains("GERENTE"))
            // {
            //     var proyectos = await db.TblTimeReportProyectos
            //         .AsNoTracking()
            //         .Where(p => p.Activo && p.TblTimeReportAsignacionProyectos.Any(ep => ep.Activo && ep.Idlider != null))
            //         .Select(p => new ProyectoLookupDto(p.Id, p.Nombre, p.Codigo))
            //         .ToListAsync();
            //     return Results.Ok(proyectos);
            // }
            // else if (roles.Contains("LIDER"))
            // {
            //     var lider = await db.TblAdministracionLiders
            //         .AsNoTracking()
            //         .FirstOrDefaultAsync(l => l.Idpersona == empleado.Idpersona && l.Activo);
//
            //     if (lider == null) return Results.Ok(new List<ProyectoLookupDto>());
//
            //     var proyectos = await db.TblTimeReportProyectos
            //         .AsNoTracking()
            //         .Where(p => p.Activo && p.TblTimeReportAsignacionProyectos.Any(ep => ep.Activo && ep.Idlider == lider.Id))
            //         .Select(p => new ProyectoLookupDto(p.Id, p.Nombre, p.Codigo))
            //         .ToListAsync();
            //     return Results.Ok(proyectos);
            // }
            // else
            // {
            //     var proyectos = await db.TblTimeReportAsignacionProyectos
            //         .AsNoTracking()
            //         .Where(ep => ep.Idempleado == empleado.Id && ep.Activo && ep.IdproyectoNavigation.Activo)
            //         .Select(ep => new ProyectoLookupDto(ep.Idproyecto, ep.IdproyectoNavigation.Nombre, ep.IdproyectoNavigation.Codigo))
            //         .Distinct()
            //         .ToListAsync();
            //     return Results.Ok(proyectos);
            // }
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

        groupActividades.MapGet("/resumen", async (int idEmpleado, int? anio, int? mes, int? idProyecto, ClaimsPrincipal user, ApplicationDbContext db) =>
        {
            // sm - Solo el propio colaborador o quien tiene permiso de Seguimiento (modal "Ver detalle", solo lectura).
            if (!await PuedeVerEmpleadoAsync(user, idEmpleado, db)) return Results.Forbid();

            // sm - Métricas del mes que muestra el calendario con la MISMA regla de negocio que Seguimiento
            // (CalculoHorasPeriodo): jornada 8 h / 6 h pasante, lunes a viernes sin feriados, ingreso/salida y hasta hoy.
            // sm - idProyecto (opcional): el modal de solo-lectura de Seguimiento lo manda para medir el cumplimiento
            // de ESE proyecto puntual (misma jornada completa que exige esa fila), no el total de todos los proyectos
            // del colaborador. La propia página de Actividades (el colaborador viendo su calendario) no lo manda.
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
                .Where(a => a.Activo && a.Idempleado == idEmpleado && a.Fechaactividad >= inicio && a.Fechaactividad <= fin
                    && (idProyecto == null || a.Idproyecto == idProyecto))
                .ToListAsync();
            var feriadosPeriodo = await db.TblTimeReportFeriados
                .Where(f => f.Activo && f.Fechaferiado >= inicio && f.Fechaferiado <= fin)
                .Select(f => f.Fechaferiado)
                .ToListAsync();

            var jornadas = await CalculoHorasPeriodo.CargarJornadasAsync(db, [idEmpleado]);
            var calculo = CalculoHorasPeriodo.Calcular(empleado, inicio, fin, actividadesPeriodo, feriadosPeriodo, hoyEcuador, jornadas[idEmpleado]);
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

            // sm - Solo se registran horas en proyectos donde el colaborador está asignado y dentro de las fechas de la
            // asignación (primero se asigna y luego se carga; no al revés).
            if (request.IdProyecto.HasValue
                && await ValidarAsignacionAsync(db, request.IdEmpleado, request.IdProyecto.Value, request.FechaActividad) is { } errorAsignacion)
                return Results.BadRequest(new { Mensaje = errorAsignacion });

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

            // sm - Si se cambia el proyecto o la fecha, deben corresponder a una asignación vigente en esa fecha. Si no cambian,
            // se permite editar (hay actividades antiguas de asignaciones que ya no están activas y deben poder corregirse).
            if (request.IdProyecto.HasValue
                && (request.IdProyecto != actividad.Idproyecto || request.FechaActividad != actividad.Fechaactividad)
                && await ValidarAsignacionAsync(db, actividad.Idempleado, request.IdProyecto.Value, request.FechaActividad) is { } errorAsignacion)
                return Results.BadRequest(new { Mensaje = errorAsignacion });

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
            // sm - Historial de contratos: la jornada de cada día es la del contrato vigente ese día (8 h / 6 h pasante).
            var jornadas = await CalculoHorasPeriodo.CargarJornadasAsync(db, empIds);

            // sm - "Hoy" según la hora de Ecuador (ver CalculoHorasPeriodo): no se cuentan los días futuros del rango.
            var hoyEcuador = CalculoHorasPeriodo.HoyEcuador();

            // sm - Seguimiento: una fila por colaborador+proyecto+cliente (no una sola fila por colaborador con
            // todos sus proyectos mezclados en un string), porque cada proyecto exige su propia jornada completa
            // (8 h, o 6 h si el contrato es Pasantía) y sus propios "días con reporte"/"días a completar". Si un
            // colaborador tiene 2 proyectos, cada uno se evalúa por separado contra su propia jornada de 8 h: no
            // se suman las horas de ambos proyectos en un mismo "día completo" del colaborador.
            foreach (var e in employees)
            {
                var empActividades = actividades.Where(a => a.Idempleado == e.Id).ToList();
                var nombreCompleto = e.IdpersonaNavigation.Nombres + " " + e.IdpersonaNavigation.Apellidos;

                // Project details (sm - asignaciones ya filtradas a las activas del rango en el Include)
                var empProys = e.TblTimeReportAsignacionProyectos.ToList();

                // sm - Sin proyecto asignado en el rango: una única fila "Sin Proyecto" con todas sus actividades
                // (no hay un proyecto específico contra el cual separar horas/días).
                if (!empProys.Any())
                {
                    var calculoSinProyecto = CalculoHorasPeriodo.Calcular(e, filtro.FechaDesde, filtro.FechaHasta, empActividades, feriados, hoyEcuador, jornadas[e.Id]);
                    colaboradores.Add(new SeguimientoColaboradorDto(
                        e.Id,
                        nombreCompleto,
                        "Sin Proyecto",
                        "Sin Cliente",
                        "Sin Líder",
                        calculoSinProyecto.HorasRegistradas,
                        calculoSinProyecto.Estado,
                        calculoSinProyecto.DiasConReporte,
                        calculoSinProyecto.DiasACompletar,
                        calculoSinProyecto.HorasJornada,
                        calculoSinProyecto.HorasEsperadas,
                        calculoSinProyecto.HorasPorRegistrar,
                        calculoSinProyecto.DiasLaborables,
                        calculoSinProyecto.HorasRegistradas,
                        null
                    ));
                    continue;
                }

                foreach (var g in empProys.GroupBy(ep => ep.Idproyecto))
                {
                    var clienteNombre = g.First().IdproyectoNavigation.IdclienteNavigation?.Nombrecomercial
                        ?? g.First().IdproyectoNavigation.IdclienteNavigation?.Razonsocial;
                    if (string.IsNullOrWhiteSpace(clienteNombre)) clienteNombre = "Sin Cliente";

                    var liderNombre = string.Join(", ", g
                        .Where(ep => ep.IdliderNavigation != null)
                        .Select(ep => ep.IdliderNavigation.IdpersonaNavigation.Nombres + " " + ep.IdliderNavigation.IdpersonaNavigation.Apellidos)
                        .Distinct());
                    if (string.IsNullOrWhiteSpace(liderNombre)) liderNombre = "Sin Líder";

                    // sm - Horas/días/estado de ESTE proyecto únicamente (se filtran las actividades por Idproyecto
                    // antes de pasarlas a CalculoHorasPeriodo), para que la jornada de 8 h (o 6 h pasante) se exija
                    // por proyecto y no se mezcle con las horas de otros proyectos del mismo colaborador.
                    var actividadesProyecto = empActividades.Where(a => a.Idproyecto == g.Key).ToList();
                    var calculo = CalculoHorasPeriodo.Calcular(e, filtro.FechaDesde, filtro.FechaHasta, actividadesProyecto, feriados, hoyEcuador, jornadas[e.Id]);

                    var proyectoDto = new SeguimientoProyectoDto(g.Key, g.First().IdproyectoNavigation.Nombre, clienteNombre, liderNombre, calculo.HorasRegistradas);

                    colaboradores.Add(new SeguimientoColaboradorDto(
                        e.Id,
                        nombreCompleto,
                        g.First().IdproyectoNavigation.Nombre,
                        clienteNombre,
                        liderNombre,
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
                        new List<SeguimientoProyectoDto> { proyectoDto }
                    ));
                }
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
    private const string MensajeSinAsignacion =
        "No está asignado a este proyecto. Pida a su líder que lo agregue como integrante antes de registrar horas.";

    // sm - Valida que el colaborador tenga una asignación activa en el proyecto (la misma regla de la lista de proyectos
    // disponibles) y que la fecha de la actividad esté dentro de su vigencia (desde la fecha de asignación hasta la fecha
    // fin, si la tiene). Devuelve el mensaje de error o null si es válido.
    private static async Task<string?> ValidarAsignacionAsync(ApplicationDbContext db, int idEmpleado, int idProyecto, DateOnly fecha)
    {
        var asignaciones = await db.TblTimeReportAsignacionProyectos
            .AsNoTracking()
            .Where(ep => ep.Idempleado == idEmpleado && ep.Idproyecto == idProyecto && ep.Activo && ep.IdproyectoNavigation.Activo)
            .Select(ep => new { ep.Fechaasignacion, ep.Fechafinasignacion })
            .ToListAsync();

        if (asignaciones.Count == 0) return MensajeSinAsignacion;
        if (asignaciones.Any(a => (a.Fechaasignacion == null || a.Fechaasignacion <= fecha)
                                  && (a.Fechafinasignacion == null || a.Fechafinasignacion >= fecha)))
            return null;

        var vigencias = string.Join(", ", asignaciones.Select(a =>
            a.Fechafinasignacion.HasValue
                ? $"del {a.Fechaasignacion:dd/MM/yyyy} al {a.Fechafinasignacion:dd/MM/yyyy}"
                : $"desde el {a.Fechaasignacion:dd/MM/yyyy}"));
        return $"El {fecha:dd/MM/yyyy} no estaba asignado a este proyecto (su asignación rige {vigencias}). " +
               "Si trabajó en esa fecha, pida a su líder que corrija la fecha de asignación.";
    }

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
