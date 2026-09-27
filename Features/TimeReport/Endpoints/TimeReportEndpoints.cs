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

            // sm - "Hoy" según la hora de Ecuador (UTC−5, sin horario de verano), sin importar la zona horaria del servidor.
            // Se usa para no contar los días futuros del rango.
            var hoyEcuador = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-5));

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

                // sm - Nuevo cálculo por colaborador (regla de negocio):
                // - Periodo = rango filtrado, desde su fecha de ingreso y hasta su fecha de salida si caen dentro del rango,
                //   y nunca después de hoy (fecha de Ecuador): los días futuros del rango no se cuentan.
                // - Días laborables = lunes a viernes del periodo, sin feriados. Sábados, domingos y feriados no cuentan.
                // - Jornada mínima = 8 h por día; 6 h si el tipo de contrato es Pasantía (código PAS).
                // - Días con reporte = días laborables con horas registradas >= jornada.
                // - Días a completar = días laborables con horas < jornada (incluye días sin registro).
                // - Horas por registrar = max(0, días laborables × jornada − horas registradas en esos días).
                var horasJornada = e.IdtipocontratoNavigation?.Codigovalor?.Trim().ToUpper() == "PAS" ? 6m : 8m;

                var inicioPeriodo = e.Fechaingreso.HasValue && e.Fechaingreso.Value > filtro.FechaDesde
                    ? e.Fechaingreso.Value
                    : filtro.FechaDesde;
                var finPeriodo = e.Fechaterminacion.HasValue && e.Fechaterminacion.Value < filtro.FechaHasta
                    ? e.Fechaterminacion.Value
                    : filtro.FechaHasta;
                if (finPeriodo > hoyEcuador) finPeriodo = hoyEcuador;

                // sm - Horas registradas por día dentro del periodo (para comparar cada día contra la jornada).
                var horasPorDia = empActividades
                    .Where(a => a.Fechaactividad >= inicioPeriodo && a.Fechaactividad <= finPeriodo)
                    .GroupBy(a => a.Fechaactividad)
                    .ToDictionary(g => g.Key, g => g.Sum(a => a.Cantidadhoras));

                var diasLaborablesPeriodo = 0;
                var diasConReporte = 0;
                var horasDiasLaborables = 0m;
                for (var dia = inicioPeriodo; dia <= finPeriodo; dia = dia.AddDays(1))
                {
                    var diaSemana = dia.ToDateTime(TimeOnly.MinValue).DayOfWeek;
                    if (diaSemana == DayOfWeek.Saturday || diaSemana == DayOfWeek.Sunday || feriados.Contains(dia))
                        continue;

                    diasLaborablesPeriodo++;
                    horasPorDia.TryGetValue(dia, out var horasDia);
                    horasDiasLaborables += horasDia;
                    if (horasDia >= horasJornada) diasConReporte++;
                }

                var diasACompletar = diasLaborablesPeriodo - diasConReporte;

                // sm - "Horas registradas" de la tabla: todas las horas del rango, pero solo hasta hoy (sin días futuros).
                var nroHoras = empActividades
                    .Where(a => a.Fechaactividad <= hoyEcuador)
                    .Sum(a => a.Cantidadhoras);

                var horasEsperadas = diasLaborablesPeriodo * horasJornada;
                // sm - Se comenta: ahora se descuentan solo las horas de días laborables (fines de semana y feriados no cuentan).
                // var horasRegistradasPeriodo = empActividades
                //     .Where(a => a.Fechaactividad >= inicioPeriodo && a.Fechaactividad <= finPeriodo)
                //     .Sum(a => a.Cantidadhoras);
                // var horasPorRegistrar = Math.Max(0m, horasEsperadas - horasRegistradasPeriodo);
                var horasPorRegistrar = Math.Max(0m, horasEsperadas - horasDiasLaborables);

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
                    diasACompletar,
                    // sm - Nuevos valores para la métrica "Horas por registrar" del frontend.
                    horasJornada,
                    horasEsperadas,
                    horasPorRegistrar,
                    // sm - Valores para la métrica "Promedio por día" (horas en días laborables ÷ días laborables del periodo).
                    diasLaborablesPeriodo,
                    horasDiasLaborables
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

        // La ruta histórica mantiene Excel. PDF tiene una ruta propia para fijar
        // el formato en el servidor y evitar que el DTO caiga a su valor default.
        groupSeguimiento.MapPost("/descarga-multiple", async (DescargarSeguimientoMultipleRequest request, ApplicationDbContext db) =>
            await DescargarReportesMultiples(request, db, request.Formato));
        groupSeguimiento.MapPost("/descarga-multiple-pdf", async (DescargarSeguimientoMultipleRequest request, ApplicationDbContext db) =>
            await DescargarReportesMultiples(request, db, "pdf"));

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

    private static async Task<IResult> DescargarReportesMultiples(
        DescargarSeguimientoMultipleRequest request,
        ApplicationDbContext db,
        string formatoSolicitado)
    {
        var formato = formatoSolicitado.Trim().ToLowerInvariant();
        if (request.Ids is null || request.Ids.Count < 1 || request.FechaDesde > request.FechaHasta || formato is not ("xlsx" or "pdf"))
            return Results.BadRequest(new { message = "Selecciona colaboradores, un rango válido y un formato PDF o Excel." });

        var ids = request.Ids.Distinct().ToList();
        var empleados = await db.TblAdministracionEmpleados
            // sm - Se comenta el filtro anterior porque rechazaba a los inactivos que ahora sí aparecen en Seguimiento.
            // .Where(e => e.Activo && ids.Contains(e.Id))
            // sm - Misma regla que el listado de Seguimiento: activos + inactivos con fecha de terminación dentro del rango.
            .Where(e => ids.Contains(e.Id)
                && (e.Activo
                    || (e.Fechaterminacion.HasValue
                        && e.Fechaterminacion.Value >= request.FechaDesde
                        && e.Fechaterminacion.Value <= request.FechaHasta)))
            .Include(e => e.IdpersonaNavigation)
            .ToListAsync();
        if (empleados.Count != ids.Count)
            return Results.BadRequest(new { message = "Uno o más colaboradores seleccionados no son válidos." });

        var actividades = await db.TblTimeReportActividadDiaria
            .Where(a => a.Activo && ids.Contains(a.Idempleado) && a.Fechaactividad >= request.FechaDesde && a.Fechaactividad <= request.FechaHasta)
            .Select(a => new
            {
                a.Idempleado,
                Fecha = a.Fechaactividad.ToString("yyyy-MM-dd"),
                TipoActividad = a.IdtipoactividadNavigation != null ? a.IdtipoactividadNavigation.Nombretipo : "Otro",
                CodigoRequerimiento = a.Codigorequerimiento ?? "",
                Horas = a.Cantidadhoras,
                Descripcion = a.Descripcionactividad ?? "",
                LiderProyecto = a.IdproyectoNavigation != null
                    ? (a.IdproyectoNavigation.TblTimeReportAsignacionProyectos.Where(ep => ep.Activo && ep.Idlider != null && ep.IdliderNavigation != null && ep.IdliderNavigation.IdpersonaNavigation != null)
                        .Select(ep => ep.IdliderNavigation.IdpersonaNavigation.Nombres + " " + ep.IdliderNavigation.IdpersonaNavigation.Apellidos).FirstOrDefault() ?? "Sin Líder")
                    : "Sin Líder",
                ClienteProyecto = a.IdproyectoNavigation != null && a.IdproyectoNavigation.IdclienteNavigation != null
                    ? (a.IdproyectoNavigation.IdclienteNavigation.Nombrecomercial ?? a.IdproyectoNavigation.IdclienteNavigation.Razonsocial ?? "Sin Cliente")
                    : "Sin Cliente",
                EsRecurrente = false
            })
            .ToListAsync();
        var feriados = (await db.TblTimeReportFeriados
            .Where(f => f.Activo && f.Fechaferiado >= request.FechaDesde && f.Fechaferiado <= request.FechaHasta)
            .Select(f => f.Fechaferiado.ToString("yyyy-MM-dd"))
            .ToListAsync()).ToHashSet();

        var archivos = new List<(string Nombre, byte[] Contenido)>();
        foreach (var empleado in empleados)
        {
            var nombre = $"{empleado.IdpersonaNavigation.Nombres} {empleado.IdpersonaNavigation.Apellidos}".Trim();
            var actividadesEmpleado = actividades.Where(a => a.Idempleado == empleado.Id)
                .Select(a => new SeguimientoActividad(a.Fecha, a.TipoActividad, a.CodigoRequerimiento, a.Horas, a.Descripcion, a.LiderProyecto, a.ClienteProyecto, a.EsRecurrente))
                .ToList();
            var reporte = formato == "pdf"
                ? SeguimientoReportService.CrearReportePdf(nombre, request.FechaDesde, request.FechaHasta, actividadesEmpleado)
                : SeguimientoReportService.CrearReporte(nombre, request.FechaDesde, request.FechaHasta, actividadesEmpleado, feriados);
            var extension = formato == "pdf" ? "pdf" : "xlsx";
            if (formato == "pdf" && !SeguimientoReportService.EsPdf(reporte))
                return Results.Problem("No se pudo generar el reporte PDF solicitado.", statusCode: 500);

            var nombreArchivo = $"Reporte_{SeguimientoReportService.SanitizarNombreArchivo(nombre)}.{extension}";
            var baseNombre = nombreArchivo;
            var sufijo = 1;
            while (archivos.Any(a => a.Nombre.Equals(nombreArchivo, StringComparison.OrdinalIgnoreCase)))
                nombreArchivo = $"{Path.GetFileNameWithoutExtension(baseNombre)}_{sufijo++}.{extension}";
            archivos.Add((nombreArchivo, reporte));
        }

        var zip = SeguimientoReportService.CrearZip(archivos);
        var nombreZip = $"Seguimiento_{formato.ToUpperInvariant()}_{request.FechaDesde:yyyy-MM-dd}_a_{request.FechaHasta:yyyy-MM-dd}.zip";
        return Results.File(zip, "application/zip", nombreZip);
    }
}
