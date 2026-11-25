using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using tmr_backend.Features.Permisos.DTOs;
using tmr_backend.Features.Permisos.Services;
using tmr_backend.Features.TimeReport.Services;
using tmr_backend.Infrastructure.Database;
using tmr_backend.Infrastructure.Database.Entities;
using tmr_backend.Infrastructure.Shared;
using tmr_backend.Shared.Wrappers;

namespace tmr_backend.Features.Permisos.Endpoints;

public static partial class PermisosEndpoints
{
    private static void MapRrhhGestionEndpoints(RouteGroupBuilder group)
    {
        // A. Catálogo de colaboradores activos para selector de RRHH (crear/editar historial)
        group.MapGet("/rrhh/colaboradores", async (
            ApplicationDbContext db,
            ICurrentUserService currentUser,
            CancellationToken ct) =>
        {
            var actor = await ResolverActorActualAsync(db, currentUser, ct);
            if (actor is null)
                return Results.Json(ApiResponse<object>.Fail(401, "Sesión no válida.", []), statusCode: 401);
            if (!actor.EsRrhh && !actor.EsAdmin)
                return Results.Json(ApiResponse<object>.Fail(403, "Acceso exclusivo para Talento Humano o Administradores.", []), statusCode: 403);

            var empleados = await db.TblAdministracionEmpleados
                .AsNoTracking()
                .Where(e => e.Activo && e.Fechaterminacion == null)
                .Include(e => e.IdpersonaNavigation)
                .Include(e => e.IdtipocontratoNavigation)
                .Include(e => e.IdempresacatalogoNavigation)
                .OrderBy(e => e.IdpersonaNavigation.Apellidos)
                .ThenBy(e => e.IdpersonaNavigation.Nombres)
                .ToListAsync(ct);

            var empIds = empleados.Select(e => e.Id).ToList();
            var contratos = await db.TblAdministracionEmpleadoContratos
                .AsNoTracking()
                .Where(c => empIds.Contains(c.Idempleado) && c.Activo)
                .ToListAsync(ct);
            var contratosPorEmp = contratos.GroupBy(c => c.Idempleado).ToDictionary(g => g.Key, g => (IReadOnlyCollection<TblAdministracionEmpleadoContrato>)g.ToList());

            var catIds = new HashSet<int>();
            foreach (var e in empleados)
            {
                if (e.Idtipocontrato.HasValue) catIds.Add(e.Idtipocontrato.Value);
                if (e.Idempresacatalogo.HasValue) catIds.Add(e.Idempresacatalogo.Value);
            }
            foreach (var c in contratos) catIds.Add(c.Idtipocontrato);

            var catalogos = catIds.Count == 0
                ? new Dictionary<int, TblAdministracionCatalogoDetalle>()
                : await db.TblAdministracionCatalogoDetalles.AsNoTracking().Where(d => catIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, ct);

            var result = empleados.Select(e =>
            {
                var saldo = VacacionesSaldoService.CalcularSaldo(
                    e,
                    contratosPorEmp.GetValueOrDefault(e.Id) ?? [],
                    catalogos,
                    [],
                    []);

                return new ColaboradorSelectorPermisoDto(
                    IdEmpleado: e.Id,
                    CodigoEmpleado: e.Codigoempleado,
                    Cedula: e.IdpersonaNavigation?.Numeroidentificacion ?? "",
                    ApellidosNombres: e.IdpersonaNavigation is not null
                        ? $"{e.IdpersonaNavigation.Apellidos} {e.IdpersonaNavigation.Nombres}".Trim()
                        : e.Codigoempleado,
                    Regimen: saldo.Regimen,
                    TipoContrato: saldo.TipoContratoNombre,
                    Empresa: saldo.EmpresaNombre,
                    Activo: e.Activo);
            }).ToList();

            return Results.Ok(ApiResponse<IReadOnlyList<ColaboradorSelectorPermisoDto>>.Ok(result));
        }).RequireAuthorization("PERMISOS_RRHH_READ");

        // B. Historial editable de permisos y vacaciones (incluye carga histórica), paginado en servidor
        group.MapGet("/rrhh/historial", async (
            [FromQuery] int? idEmpleado,
            [FromQuery] int? anio,
            [FromQuery] string? desde,
            [FromQuery] string? hasta,
            [FromQuery] string? regimen,
            [FromQuery] bool? soloPendientesVerificar,
            [FromQuery] bool? soloHistoricos,
            [FromQuery] string? buscar,
            [FromQuery] string? tipo,
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            ApplicationDbContext db,
            ICurrentUserService currentUser,
            CancellationToken ct) =>
        {
            var actor = await ResolverActorActualAsync(db, currentUser, ct);
            if (actor is null)
                return Results.Json(ApiResponse<object>.Fail(401, "Sesión no válida.", []), statusCode: 401);
            if (!actor.EsRrhh && !actor.EsAdmin)
                return Results.Json(ApiResponse<object>.Fail(403, "Acceso exclusivo para Talento Humano o Administradores.", []), statusCode: 403);

            var filtro = db.TblTimeReportSolicitudes.AsNoTracking().Where(s => s.Activo);

            if (idEmpleado is > 0)
                filtro = filtro.Where(s => s.IdEmpleado == idEmpleado.Value);
            if (anio is >= 2000 and <= 2100)
                filtro = filtro.Where(s => s.FechaInicio.Year == anio.Value || s.FechaFin.Year == anio.Value);
            var fechaDesde = ParsearFecha(desde);
            var fechaHasta = ParsearFecha(hasta);
            if (fechaDesde.HasValue)
                filtro = filtro.Where(s => s.FechaFin >= fechaDesde.Value);
            if (fechaHasta.HasValue)
                filtro = filtro.Where(s => s.FechaInicio <= fechaHasta.Value);
            if (soloPendientesVerificar == true)
                filtro = filtro.Where(s => !s.VerificadoRrhh);
            if (soloHistoricos == true)
                filtro = filtro.Where(s => s.EsHistorico);
            if (!string.IsNullOrWhiteSpace(tipo) && !string.Equals(tipo, "TODOS", StringComparison.OrdinalIgnoreCase))
            {
                var tipoCodigo = tipo.Trim().ToUpperInvariant();
                filtro = filtro.Where(s => s.TipoPermiso.Codigo == tipoCodigo);
            }
            filtro = AplicarBusquedaSolicitudes(filtro, buscar);

            var regimenFiltro = string.IsNullOrWhiteSpace(regimen) || string.Equals(regimen, "TODOS", StringComparison.OrdinalIgnoreCase)
                ? null
                : regimen.Trim().ToUpperInvariant();

            Dictionary<int, string>? regimenesResueltos = null;
            if (regimenFiltro is not null)
            {
                var empIdsFiltro = await filtro.Select(s => s.IdEmpleado).Distinct().ToListAsync(ct);
                regimenesResueltos = await ResolverRegimenesPorEmpleadoAsync(db, empIdsFiltro, ct);
                var empIdsCoincidentes = regimenesResueltos
                    .Where(kv => string.Equals(kv.Value, regimenFiltro, StringComparison.OrdinalIgnoreCase))
                    .Select(kv => kv.Key)
                    .ToList();
                filtro = filtro.Where(s => empIdsCoincidentes.Contains(s.IdEmpleado));
            }

            var ordenados = filtro.OrderByDescending(s => s.FechaInicio).ThenByDescending(s => s.Id);

            // Sin "page" se devuelve la lista completa filtrada (usada para exportación Excel/PDF)
            if (page is null)
            {
                var idsTodos = await ordenados.Select(s => s.Id).ToListAsync(ct);
                var dtosTodos = await CargarSolicitudesConRegimenPorIdsAsync(db, idsTodos, regimenesResueltos, ct);
                return Results.Ok(ApiResponse<IReadOnlyList<PermisoItemDto>>.Ok(dtosTodos));
            }

            var (pagina, tamano) = NormalizarPaginacion(page, pageSize);
            var total = await filtro.CountAsync(ct);
            var idsPagina = await ordenados.Skip((pagina - 1) * tamano).Take(tamano).Select(s => s.Id).ToListAsync(ct);
            var dtos = await CargarSolicitudesConRegimenPorIdsAsync(db, idsPagina, regimenesResueltos, ct);

            return ResponderPagina(dtos, pagina, tamano, total);
        }).RequireAuthorization("PERMISOS_RRHH_READ");

        // B2. Resumen ligero para KPIs y contadores de pestañas (evita descargar listas completas)
        group.MapGet("/rrhh/resumen", async (
            ApplicationDbContext db,
            ICurrentUserService currentUser,
            CancellationToken ct) =>
        {
            var actor = await ResolverActorActualAsync(db, currentUser, ct);
            if (actor is null)
                return Results.Json(ApiResponse<object>.Fail(401, "Sesión no válida.", []), statusCode: 401);
            if (!actor.EsRrhh && !actor.EsAdmin)
                return Results.Json(ApiResponse<object>.Fail(403, "Acceso exclusivo para Talento Humano o Administradores.", []), statusCode: 403);

            var activas = db.TblTimeReportSolicitudes.AsNoTracking().Where(s => s.Activo);
            var totalHistorial = await activas.CountAsync(ct);
            var porVerificar = await activas.CountAsync(s => !s.VerificadoRrhh, ct);

            var aniosInicio = await activas.Select(s => s.FechaInicio.Year).Distinct().ToListAsync(ct);
            var aniosFin = await activas.Select(s => s.FechaFin.Year).Distinct().ToListAsync(ct);
            var anios = aniosInicio.Union(aniosFin).OrderByDescending(a => a).ToList();

            var estados = (await ConsultarClasificacionBandeja(activas).ToListAsync(ct))
                .Select(ResolverEstadoBandejaRrhh)
                .ToList();

            var totalColaboradores = await db.TblAdministracionEmpleados
                .AsNoTracking()
                .CountAsync(e => e.Activo && e.Fechaterminacion == null, ct);

            var dto = new ResumenGestionRrhhDto(
                PorConfirmar: estados.Count(e => e == "CONFIRMADO_LIDER"),
                PendientesLiderExterno: estados.Count(e => e == "PENDIENTE_LIDER_EXT"),
                Confirmadas: estados.Count(e => e == "APROBADO_RRHH"),
                TotalHistorial: totalHistorial,
                PorVerificar: porVerificar,
                TotalColaboradores: totalColaboradores,
                AniosDisponibles: anios);

            return Results.Ok(ApiResponse<ResumenGestionRrhhDto>.Ok(dto));
        }).RequireAuthorization("PERMISOS_RRHH_READ");

        // El cliente anterior conserva /historial durante el despliegue escalonado.
        group.MapGet("/rrhh/historial/pagina", async (
            int? pagina, int? tamano, int? idEmpleado, int? anio, string? tipo,
            string? buscar, bool? soloPendientesVerificar, bool? soloHistoricos,
            ApplicationDbContext db, ICurrentUserService currentUser, CancellationToken ct) =>
        {
            var actor = await ResolverActorActualAsync(db, currentUser, ct);
            if (actor is null)
                return Results.Unauthorized();
            if (!actor.EsRrhh && !actor.EsAdmin)
                return Results.Forbid();
            if (pagina is < 1 || tamano is < 1 or > 100 || buscar?.Length > 200)
                return Results.BadRequest(ApiResponse<object>.Fail(400, "Página o filtros inválidos. El tamaño permitido es de 1 a 100.", []));

            var activas = db.TblTimeReportSolicitudes.AsNoTracking().Where(s => s.Activo);
            var query = activas;
            if (idEmpleado.HasValue)
                query = query.Where(s => s.IdEmpleado == idEmpleado);
            if (anio.HasValue)
                query = query.Where(s => s.FechaInicio.Year == anio || s.FechaFin.Year == anio);
            if (!string.IsNullOrWhiteSpace(tipo))
                query = query.Where(s => s.TipoPermiso.Codigo == tipo);
            if (soloPendientesVerificar == true)
                query = query.Where(s => !s.VerificadoRrhh);
            if (soloHistoricos == true)
                query = query.Where(s => s.EsHistorico);

            foreach (var palabra in (buscar ?? "").ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            {
                query = query.Where(s => ApplicationDbContext.NormalizarBusqueda((
                    s.Codigo + " " + s.Empleado.IdpersonaNavigation.Apellidos + " " + s.Empleado.IdpersonaNavigation.Nombres
                    + " " + s.Empleado.IdpersonaNavigation.Numeroidentificacion + " " + s.Empleado.Codigoempleado
                    + " " + s.TipoPermiso.Nombre + " " + s.Motivo + " " + (s.ObservacionMigracion ?? "")
                    + " " + ApplicationDbContext.FormatearFecha(s.FechaInicio, "DD/MM/YYYY")
                    + " " + ApplicationDbContext.FormatearFecha(s.FechaFin, "DD/MM/YYYY")
                    + " " + (s.Estado.Codigo == "APROBADA" ? "Confirmado" : s.Estado.Codigo == "PENDIENTE_TH" ? "En Talento Humano"
                        : s.Estado.Codigo == "PENDIENTE_JEFE" ? "Pendiente Jefe" : s.Estado.Codigo == "OBSERVADA" ? "Observado" : s.Estado.Codigo == "RECHAZADA" ? "Rechazado" : s.Estado.Nombre)
                    ).ToLower(), "áéíóúüñ", "aeiouun").Contains(
                        ApplicationDbContext.NormalizarBusqueda(palabra, "áéíóúüñ", "aeiouun")));
            }

            var total = await query.CountAsync(ct);
            var numero = pagina ?? 1;
            var limite = tamano ?? 25;
            var offset = ((long)numero - 1) * limite;
            var items = new List<PermisoItemDto>();
            if (offset < total)
            {
                var ids = query.OrderByDescending(s => s.FechaInicio).ThenByDescending(s => s.Id)
                    .Skip((int)offset).Take(limite).Select(s => s.Id);
                var filas = await ConsultarSolicitudesBase(db).Where(s => ids.Contains(s.Id))
                    .OrderByDescending(s => s.FechaInicio).ThenByDescending(s => s.Id).AsSplitQuery().ToListAsync(ct);
                items.AddRange(filas.Select(s => MapearSolicitudDto(s, null)));
            }

            var resumen = await activas.GroupBy(s => 1).Select(g => new {
                registros = g.Count(), porVerificar = g.Count(s => !s.VerificadoRrhh),
                porConfirmar = g.Count(s => s.Estado.Codigo == "PENDIENTE_TH"),
                confirmados = g.Count(s => s.Estado.Codigo == "APROBADA")
            }).SingleOrDefaultAsync(ct);
            var anios = await activas.Select(s => s.FechaInicio.Year).Union(activas.Select(s => s.FechaFin.Year))
                .OrderByDescending(a => a).ToListAsync(ct);
            return Results.Ok(ApiResponse<object>.Ok(new { items, total, pagina = numero, tamano = limite, resumen, anios }));
        }).RequireAuthorization("PERMISOS_RRHH_READ");

        // C. Crear registro directo de permiso/vacación por RRHH (histórico o regularización)
        group.MapPost("/rrhh/registro", async (
            [FromBody] GuardarPermisoRrhhRequest request,
            HttpContext httpContext,
            ApplicationDbContext db,
            ICurrentUserService currentUser,
            CancellationToken ct) =>
        {
            var actor = await ResolverActorActualAsync(db, currentUser, ct);
            if (actor is null)
                return Results.Json(ApiResponse<object>.Fail(401, "Sesión no válida.", []), statusCode: 401);
            if (!actor.EsRrhh && !actor.EsAdmin)
                return Results.Json(ApiResponse<object>.Fail(403, "Solo Talento Humano o Administradores pueden crear registros directos.", []), statusCode: 403);

            var validacion = await ValidarYResolverDatosRrhhAsync(db, request, ct);
            if (validacion.Error is not null)
                return validacion.Error;

            var datos = validacion.Datos!;
            var anio = datos.FechaInicio.Year;
            var prefijo = request.EsHistorico ? "HIST" : "PER";

            TblTimeReportSolicitud? nueva = null;
            for (var intento = 1; intento <= 5; intento++)
            {
                var codigo = await GenerarSiguienteCodigoConPrefijoAsync(db, prefijo, anio, intento - 1, ct);
                nueva = new TblTimeReportSolicitud
                {
                    Codigo = codigo,
                    IdEmpleado = datos.Empleado.Id,
                    IdTipoPermiso = datos.TipoPermiso.Id,
                    IdEstado = datos.IdEstado,
                    FechaSolicitud = datos.FechaInicio,
                    Motivo = datos.Motivo,
                    FechaInicio = datos.FechaInicio,
                    FechaFin = datos.FechaFin,
                    FechaRegreso = datos.FechaRegreso,
                    DiasSolicitados = datos.TotalDias,
                    Horario = datos.Horario,
                    DetalleHorario = datos.DetalleHorario,
                    HorasSolicitadas = datos.TotalHoras,
                    ImpactoTimeReport = datos.ImpactoTimeReport,
                    DescuentaVacaciones = request.DescuentaVacaciones,
                    EsRecuperable = request.EsRecuperable,
                    EsAnticipada = request.EsAnticipada,
                    EsHistorico = request.EsHistorico,
                    VerificadoRrhh = request.VerificadoRrhh,
                    ObservacionMigracion = string.IsNullOrWhiteSpace(request.ObservacionMigracion) ? null : request.ObservacionMigracion.Trim(),
                    Activo = true,
                    UsuarioCreacion = actor.Email,
                    FechaCreacion = DateTime.UtcNow,
                    IpCreacion = ObtenerIpCliente(httpContext)
                };

                db.TblTimeReportSolicitudes.Add(nueva);
                try
                {
                    await db.SaveChangesAsync(ct);
                    break;
                }
                catch (DbUpdateException ex) when (
                    intento < 5
                    && ex.InnerException is PostgresException pg
                    && pg.SqlState == PostgresErrorCodes.UniqueViolation)
                {
                    db.Entry(nueva).State = EntityState.Detached;
                }
            }

            var cargada = await ConsultarSolicitudesBase(db).FirstAsync(s => s.Id == nueva!.Id, ct);
            var regimenes = await ResolverRegimenesPorEmpleadoAsync(db, [cargada.IdEmpleado], ct);
            return Results.Ok(ApiResponse<PermisoItemDto>.Ok(
                MapearSolicitudDto(cargada, null) with { Regimen = regimenes.GetValueOrDefault(cargada.IdEmpleado) },
                "Registro guardado correctamente en el historial."));
        }).RequireAuthorization("PERMISOS_RRHH_CONFIRM");

        // D. Editar cualquier registro de permiso/vacación desde Talento Humano
        group.MapPut("/rrhh/{id:int}", async (
            int id,
            [FromBody] GuardarPermisoRrhhRequest request,
            HttpContext httpContext,
            ApplicationDbContext db,
            ICurrentUserService currentUser,
            CancellationToken ct) =>
        {
            var actor = await ResolverActorActualAsync(db, currentUser, ct);
            if (actor is null)
                return Results.Json(ApiResponse<object>.Fail(401, "Sesión no válida.", []), statusCode: 401);
            if (!actor.EsRrhh && !actor.EsAdmin)
                return Results.Json(ApiResponse<object>.Fail(403, "Solo Talento Humano o Administradores pueden editar registros.", []), statusCode: 403);

            var solicitud = await db.TblTimeReportSolicitudes
                .FirstOrDefaultAsync(s => s.Id == id && s.Activo, ct);

            if (solicitud is null)
                return Results.NotFound(ApiResponse<object>.Fail(404, "Registro de permiso no encontrado.", []));

            var validacion = await ValidarYResolverDatosRrhhAsync(db, request, ct);
            if (validacion.Error is not null)
                return validacion.Error;

            var datos = validacion.Datos!;
            solicitud.IdEmpleado = datos.Empleado.Id;
            solicitud.IdTipoPermiso = datos.TipoPermiso.Id;
            if (!string.IsNullOrWhiteSpace(request.EstadoCodigo))
                solicitud.IdEstado = datos.IdEstado;
            solicitud.Motivo = datos.Motivo;
            solicitud.FechaInicio = datos.FechaInicio;
            solicitud.FechaFin = datos.FechaFin;
            solicitud.FechaRegreso = datos.FechaRegreso;
            solicitud.DiasSolicitados = datos.TotalDias;
            solicitud.Horario = datos.Horario;
            solicitud.DetalleHorario = datos.DetalleHorario;
            solicitud.HorasSolicitadas = datos.TotalHoras;
            if (!string.IsNullOrWhiteSpace(request.ImpactoTimeReport))
                solicitud.ImpactoTimeReport = datos.ImpactoTimeReport;
            solicitud.DescuentaVacaciones = request.DescuentaVacaciones;
            solicitud.EsRecuperable = request.EsRecuperable;
            solicitud.EsAnticipada = request.EsAnticipada;
            solicitud.EsHistorico = request.EsHistorico;
            solicitud.VerificadoRrhh = request.VerificadoRrhh;
            solicitud.ObservacionMigracion = string.IsNullOrWhiteSpace(request.ObservacionMigracion)
                ? null
                : request.ObservacionMigracion.Trim();
            solicitud.UsuarioModificacion = actor.Email;
            solicitud.FechaModificacion = DateTime.UtcNow;
            solicitud.IpModificacion = ObtenerIpCliente(httpContext);

            await db.SaveChangesAsync(ct);

            var cargada = await ConsultarSolicitudesBase(db).FirstAsync(s => s.Id == id, ct);
            var regimenes = await ResolverRegimenesPorEmpleadoAsync(db, [cargada.IdEmpleado], ct);
            return Results.Ok(ApiResponse<PermisoItemDto>.Ok(
                MapearSolicitudDto(cargada, null) with { Regimen = regimenes.GetValueOrDefault(cargada.IdEmpleado) },
                "Registro actualizado correctamente."));
        }).RequireAuthorization("PERMISOS_RRHH_CONFIRM");

        // E. Anulación lógica de un registro duplicado o erróneo desde Talento Humano
        group.MapDelete("/rrhh/{id:int}", async (
            int id,
            HttpContext httpContext,
            ApplicationDbContext db,
            ICurrentUserService currentUser,
            CancellationToken ct) =>
        {
            var actor = await ResolverActorActualAsync(db, currentUser, ct);
            if (actor is null)
                return Results.Json(ApiResponse<object>.Fail(401, "Sesión no válida.", []), statusCode: 401);
            if (!actor.EsRrhh && !actor.EsAdmin)
                return Results.Json(ApiResponse<object>.Fail(403, "Solo Talento Humano o Administradores pueden anular registros.", []), statusCode: 403);

            var solicitud = await db.TblTimeReportSolicitudes
                .FirstOrDefaultAsync(s => s.Id == id && s.Activo, ct);

            if (solicitud is null)
                return Results.NotFound(ApiResponse<object>.Fail(404, "Registro no encontrado.", []));

            solicitud.Activo = false;
            solicitud.UsuarioModificacion = actor.Email;
            solicitud.FechaModificacion = DateTime.UtcNow;
            solicitud.IpModificacion = ObtenerIpCliente(httpContext);

            await db.SaveChangesAsync(ct);
            return Results.Ok(ApiResponse<object>.Ok(new { id }, "Registro anulado correctamente."));
        }).RequireAuthorization("PERMISOS_RRHH_CONFIRM");

        // F. Tablero de Saldos y Ciclos de Vacaciones por Colaborador Activo (paginado en servidor)
        group.MapGet("/rrhh/saldos", async (
            [FromQuery] string? buscar,
            [FromQuery] string? regimen,
            [FromQuery] bool? soloSaldoNegativo,
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            ApplicationDbContext db,
            ICurrentUserService currentUser,
            CancellationToken ct) =>
        {
            var actor = await ResolverActorActualAsync(db, currentUser, ct);
            if (actor is null)
                return Results.Json(ApiResponse<object>.Fail(401, "Sesión no válida.", []), statusCode: 401);
            if (!actor.EsRrhh && !actor.EsAdmin && !actor.EsGerente)
                return Results.Json(ApiResponse<object>.Fail(403, "Acceso exclusivo para Talento Humano, Gerencia o Administradores.", []), statusCode: 403);

            var filtro = db.TblAdministracionEmpleados
                .AsNoTracking()
                .Where(e => e.Activo && e.Fechaterminacion == null);

            foreach (var patron in PatronesBusqueda(buscar))
            {
                filtro = filtro.Where(e =>
                    System.Text.RegularExpressions.Regex.IsMatch(e.IdpersonaNavigation.Apellidos + " " + e.IdpersonaNavigation.Nombres, patron, System.Text.RegularExpressions.RegexOptions.IgnoreCase)
                    || System.Text.RegularExpressions.Regex.IsMatch(e.IdpersonaNavigation.Numeroidentificacion, patron, System.Text.RegularExpressions.RegexOptions.IgnoreCase)
                    || System.Text.RegularExpressions.Regex.IsMatch(e.Codigoempleado, patron, System.Text.RegularExpressions.RegexOptions.IgnoreCase)
                    || (e.IdcargoNavigation != null && System.Text.RegularExpressions.Regex.IsMatch(e.IdcargoNavigation.Nombrecargo, patron, System.Text.RegularExpressions.RegexOptions.IgnoreCase)));
            }

            var ordenados = filtro
                .OrderBy(e => e.IdpersonaNavigation.Apellidos)
                .ThenBy(e => e.IdpersonaNavigation.Nombres)
                .ThenBy(e => e.Id);

            var regimenFiltro = string.IsNullOrWhiteSpace(regimen) || string.Equals(regimen, "TODOS", StringComparison.OrdinalIgnoreCase)
                ? null
                : regimen.Trim().ToUpperInvariant();
            var filtraCalculado = regimenFiltro is not null || soloSaldoNegativo == true;
            var (pagina, tamano) = NormalizarPaginacion(page, pageSize);

            List<int> idsEmpleados;
            int total;
            if (page is not null && !filtraCalculado)
            {
                // Caso común: se paginan los colaboradores en SQL y solo se calculan los saldos de la página
                total = await filtro.CountAsync(ct);
                idsEmpleados = await ordenados.Skip((pagina - 1) * tamano).Take(tamano).Select(e => e.Id).ToListAsync(ct);
            }
            else
            {
                idsEmpleados = await ordenados.Select(e => e.Id).ToListAsync(ct);
                total = idsEmpleados.Count;
            }

            var saldos = await CalcularSaldosColaboradoresAsync(db, idsEmpleados, ct);

            if (filtraCalculado)
            {
                saldos = saldos
                    .Where(s => regimenFiltro is null || s.Regimen == regimenFiltro)
                    .Where(s => soloSaldoNegativo != true || s.SaldoDisponible < 0)
                    .ToList();
                total = saldos.Count;
                if (page is not null)
                    saldos = saldos.Skip((pagina - 1) * tamano).Take(tamano).ToList();
            }

            if (page is null)
                return Results.Ok(ApiResponse<IReadOnlyList<SaldoVacacionesColaboradorDto>>.Ok(saldos));
            return ResponderPagina(saldos, pagina, tamano, total);
        }).RequireAuthorization("PERMISOS_RRHH_READ");

        // G. Registrar ajuste manual de saldo o excepción de factura por colaborador
        group.MapPost("/rrhh/saldos/{idEmpleado:int}/ajuste", async (
            int idEmpleado,
            [FromBody] CrearAjusteVacacionesRequest request,
            HttpContext httpContext,
            ApplicationDbContext db,
            ICurrentUserService currentUser,
            CancellationToken ct) =>
        {
            var actor = await ResolverActorActualAsync(db, currentUser, ct);
            if (actor is null)
                return Results.Json(ApiResponse<object>.Fail(401, "Sesión no válida.", []), statusCode: 401);
            if (!actor.EsRrhh && !actor.EsAdmin)
                return Results.Json(ApiResponse<object>.Fail(403, "Solo Talento Humano o Administradores pueden registrar ajustes de saldo.", []), statusCode: 403);

            var empleadoExiste = await db.TblAdministracionEmpleados.AsNoTracking().AnyAsync(e => e.Id == idEmpleado, ct);
            if (!empleadoExiste)
                return Results.NotFound(ApiResponse<object>.Fail(404, "Colaborador no encontrado.", []));

            var tipo = request.TipoAjuste?.Trim().ToUpperInvariant() ?? "";
            if (tipo is not ("AJUSTE_DIAS" or "EXCEPCION_FACTURA" or "FECHA_AFILIACION"))
                return Results.BadRequest(ApiResponse<object>.Fail(400, "Tipo de ajuste inválido. Use AJUSTE_DIAS, EXCEPCION_FACTURA o FECHA_AFILIACION.", []));

            var motivo = request.Motivo?.Replace("\0", "").Trim();
            if (string.IsNullOrWhiteSpace(motivo))
                return Results.BadRequest(ApiResponse<object>.Fail(400, "Debes indicar el motivo del ajuste o excepción.", []));

            if (tipo == "AJUSTE_DIAS" && (request.DiasAjuste == 0m || Math.Abs(request.DiasAjuste) > 365m))
                return Results.BadRequest(ApiResponse<object>.Fail(400, "Los días de ajuste deben ser distintos de 0 y estar dentro de +/- 365 días.", []));

            if (tipo == "EXCEPCION_FACTURA" && !request.PermiteVacaciones.HasValue)
                return Results.BadRequest(ApiResponse<object>.Fail(400, "Debes indicar si se habilita o revoca la excepción de vacaciones.", []));

            DateOnly? fechaAfiliacion = null;
            if (tipo == "FECHA_AFILIACION")
            {
                fechaAfiliacion = ParsearFecha(request.FechaAfiliacionBase);
                if (!fechaAfiliacion.HasValue)
                    return Results.BadRequest(ApiResponse<object>.Fail(400, "Debes indicar una fecha de afiliación IESS válida.", []));
                if (fechaAfiliacion.Value < new DateOnly(1980, 1, 1) || fechaAfiliacion.Value > CalculoHorasPeriodo.HoyEcuador())
                    return Results.BadRequest(ApiResponse<object>.Fail(400, "La fecha de afiliación no puede ser futura ni anterior a 1980.", []));
            }

            var entidad = new TblTimeReportAjusteVacaciones
            {
                IdEmpleado = idEmpleado,
                TipoAjuste = tipo,
                DiasAjuste = tipo == "AJUSTE_DIAS" ? Math.Round(request.DiasAjuste, 2) : 0m,
                PermiteVacaciones = tipo == "EXCEPCION_FACTURA" ? request.PermiteVacaciones : null,
                FechaAfiliacionBase = fechaAfiliacion,
                Motivo = motivo,
                Activo = true,
                UsuarioCreacion = actor.Email,
                FechaCreacion = DateTime.UtcNow,
                IpCreacion = ObtenerIpCliente(httpContext)
            };

            db.TblTimeReportAjustesVacaciones.Add(entidad);
            await db.SaveChangesAsync(ct);

            var dto = new AjusteVacacionesItemDto(
                entidad.Id,
                entidad.TipoAjuste,
                entidad.DiasAjuste,
                entidad.PermiteVacaciones,
                entidad.Motivo,
                entidad.UsuarioCreacion,
                entidad.FechaCreacion.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture),
                entidad.FechaAfiliacionBase?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture));

            return Results.Ok(ApiResponse<AjusteVacacionesItemDto>.Ok(dto, "Ajuste registrado correctamente."));
        }).RequireAuthorization("PERMISOS_RRHH_CONFIRM");

        // H. Anular un ajuste manual de vacaciones
        group.MapDelete("/rrhh/saldos/ajustes/{idAjuste:int}", async (
            int idAjuste,
            HttpContext httpContext,
            ApplicationDbContext db,
            ICurrentUserService currentUser,
            CancellationToken ct) =>
        {
            var actor = await ResolverActorActualAsync(db, currentUser, ct);
            if (actor is null)
                return Results.Json(ApiResponse<object>.Fail(401, "Sesión no válida.", []), statusCode: 401);
            if (!actor.EsRrhh && !actor.EsAdmin)
                return Results.Json(ApiResponse<object>.Fail(403, "Solo Talento Humano o Administradores pueden anular ajustes.", []), statusCode: 403);

            var ajuste = await db.TblTimeReportAjustesVacaciones.FirstOrDefaultAsync(a => a.Id == idAjuste && a.Activo, ct);
            if (ajuste is null)
                return Results.NotFound(ApiResponse<object>.Fail(404, "Ajuste no encontrado.", []));

            ajuste.Activo = false;
            ajuste.UsuarioModificacion = actor.Email;
            ajuste.FechaModificacion = DateTime.UtcNow;
            ajuste.IpModificacion = ObtenerIpCliente(httpContext);
            await db.SaveChangesAsync(ct);

            return Results.Ok(ApiResponse<object>.Ok(new { id = idAjuste }, "Ajuste anulado correctamente."));
        }).RequireAuthorization("PERMISOS_RRHH_CONFIRM");
    }

    private sealed record DatosValidadosRrhh(
        TblAdministracionEmpleado Empleado,
        TblTimeReportTipoPermiso TipoPermiso,
        int IdEstado,
        DateOnly FechaInicio,
        DateOnly FechaFin,
        DateOnly FechaRegreso,
        decimal TotalDias,
        decimal TotalHoras,
        string Horario,
        string DetalleHorario,
        string Motivo,
        string ImpactoTimeReport);

    private static async Task<(DatosValidadosRrhh? Datos, IResult? Error)> ValidarYResolverDatosRrhhAsync(
        ApplicationDbContext db,
        GuardarPermisoRrhhRequest request,
        CancellationToken ct)
    {
        if (request.IdEmpleado <= 0)
            return (null, Results.BadRequest(ApiResponse<object>.Fail(400, "Debes seleccionar un colaborador.", [])));

        var empleado = await db.TblAdministracionEmpleados.AsNoTracking().FirstOrDefaultAsync(e => e.Id == request.IdEmpleado, ct);
        if (empleado is null)
            return (null, Results.BadRequest(ApiResponse<object>.Fail(400, "El colaborador seleccionado no existe.", [])));

        var tipoPermiso = await ObtenerTipoPermisoAsync(db, request.TipoOficial, ct);
        if (tipoPermiso is null)
            return (null, Results.BadRequest(ApiResponse<object>.Fail(400, "Tipo de permiso no válido.", [])));

        var fechaInicio = ParsearFecha(request.FechaInicio);
        var fechaFin = ParsearFecha(request.FechaFin);
        if (!fechaInicio.HasValue || !fechaFin.HasValue)
            return (null, Results.BadRequest(ApiResponse<object>.Fail(400, "Debes indicar fechas de inicio y fin válidas.", [])));
        if (fechaFin.Value < fechaInicio.Value)
            return (null, Results.BadRequest(ApiResponse<object>.Fail(400, "La fecha de fin no puede ser anterior a la fecha de inicio.", [])));

        var motivo = request.Motivo?.Replace("\0", "").Trim();
        if (string.IsNullOrWhiteSpace(motivo))
            return (null, Results.BadRequest(ApiResponse<object>.Fail(400, "Debes indicar el motivo del permiso.", [])));

        var feriados = await CargarFeriadosRangoAsync(db, fechaInicio.Value, fechaFin.Value.AddDays(15), ct);
        var fechaRegreso = ParsearFecha(request.FechaRegreso) ?? CalcularSiguienteDiaLaborable(fechaFin.Value, feriados);

        var esVacaciones = tipoPermiso.Codigo == "VACACIONES";
        var horario = string.IsNullOrWhiteSpace(request.Horario) ? "COMPLETA" : request.Horario.Trim().ToUpperInvariant();
        if (horario is not ("COMPLETA" or "MANANA" or "TARDE" or "OTROS"))
            horario = "COMPLETA";

        decimal totalDias;
        if (request.TotalDias is > 0m)
        {
            totalDias = Math.Round(request.TotalDias.Value, 2);
        }
        else if (esVacaciones || request.DescuentaVacaciones)
        {
            totalDias = fechaFin.Value.DayNumber - fechaInicio.Value.DayNumber + 1;
        }
        else
        {
            var lab = CalcularDiasLaborablesSinMinimo(fechaInicio.Value, fechaFin.Value, feriados);
            totalDias = lab > 0 ? lab : (fechaFin.Value.DayNumber - fechaInicio.Value.DayNumber + 1);
        }

        if (totalDias <= 0m || totalDias > 365m)
            return (null, Results.BadRequest(ApiResponse<object>.Fail(400, "La cantidad de días debe estar entre 0.01 y 365.", [])));

        var totalHoras = Math.Round(totalDias * 8m, 2);
        var detalleHorario = string.IsNullOrWhiteSpace(request.DetalleHorario)
            ? (esVacaciones ? "Días calendario" : "08:30 - 17:30")
            : request.DetalleHorario.Trim();

        var estadoCod = string.IsNullOrWhiteSpace(request.EstadoCodigo) ? "APROBADA" : request.EstadoCodigo.Trim().ToUpperInvariant();
        if (estadoCod is not ("PENDIENTE_JEFE" or "PENDIENTE_TH" or "OBSERVADA" or "APROBADA" or "RECHAZADA"))
            estadoCod = "APROBADA";
        var idEstado = await ObtenerIdEstadoSolicitudAsync(db, estadoCod, ct);

        var impacto = string.IsNullOrWhiteSpace(request.ImpactoTimeReport)
            ? (request.EsHistorico ? "SIN_IMPACTO" : "RESTA_META")
            : request.ImpactoTimeReport.Trim().ToUpperInvariant();
        if (impacto is not ("RESTA_META" or "SUMA_REPORTADAS" or "SIN_IMPACTO"))
            impacto = "RESTA_META";

        return (new DatosValidadosRrhh(
            empleado,
            tipoPermiso,
            idEstado,
            fechaInicio.Value,
            fechaFin.Value,
            fechaRegreso,
            totalDias,
            totalHoras,
            horario,
            detalleHorario,
            motivo,
            impacto), null);
    }

    private static async Task<string> GenerarSiguienteCodigoConPrefijoAsync(
        ApplicationDbContext db,
        string prefijo,
        int anio,
        int offset,
        CancellationToken ct)
    {
        var basePrefijo = $"{prefijo}-{anio}-";
        var codigos = await db.TblTimeReportSolicitudes
            .AsNoTracking()
            .Where(s => s.Codigo.StartsWith(basePrefijo))
            .Select(s => s.Codigo)
            .ToListAsync(ct);

        var max = 0;
        foreach (var cod in codigos)
        {
            var sufijo = cod[basePrefijo.Length..];
            if (int.TryParse(sufijo, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n > max)
                max = n;
        }

        return $"{basePrefijo}{(max + 1 + offset):000}";
    }

    // =========================================================================
    // Paginación y búsqueda en servidor para Gestión Talento Humano
    // =========================================================================
    private const int TamanoPaginaPorDefecto = 10;
    private const int TamanoPaginaMaximo = 100;

    /// <summary>
    /// Respuesta paginada: la página va dentro de "data" (el interceptor del frontend descarta
    /// el resto del sobre) y también en "meta" por consistencia con el resto de la API.
    /// </summary>
    private static IResult ResponderPagina<T>(IReadOnlyList<T> items, int pagina, int tamano, int total)
    {
        var meta = new PaginationMeta(pagina, tamano, total);
        var dto = new PaginaResultadoDto<T>(items, pagina, tamano, total, meta.TotalPages);
        return Results.Ok(ApiResponse<PaginaResultadoDto<T>>.Ok(dto, meta: meta));
    }

    private static (int Pagina, int Tamano) NormalizarPaginacion(int? page, int? pageSize)
    {
        var tamano = Math.Clamp(pageSize is > 0 ? pageSize.Value : TamanoPaginaPorDefecto, 1, TamanoPaginaMaximo);
        var pagina = Math.Max(1, page ?? 1);
        return (pagina, tamano);
    }

    /// <summary>
    /// Convierte el texto de búsqueda en expresiones regulares de PostgreSQL (~*, sin distinguir
    /// mayúsculas), una por palabra; todas deben coincidir. No distingue tildes: cada vocal y la ñ
    /// se buscan como clase ("guzman" encuentra "Guzmán"). El resto de caracteres se escapa.
    /// </summary>
    private static IReadOnlyList<string> PatronesBusqueda(string? buscar)
    {
        if (string.IsNullOrWhiteSpace(buscar)) return [];
        return buscar
            .Replace("\0", "")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(t => t.Length > 0)
            .Take(6)
            .Select(t => t.Length > 60 ? t[..60] : t)
            .Select(PatronSinTildes)
            .ToList();
    }

    private static string PatronSinTildes(string palabra)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var ch in palabra.Normalize(System.Text.NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(char.ToLowerInvariant(ch) switch
            {
                'a' => "[aáàä]",
                'e' => "[eéèë]",
                'i' => "[iíìï]",
                'o' => "[oóòö]",
                'u' => "[uúùü]",
                'n' => "[nñ]",
                var c => System.Text.RegularExpressions.Regex.Escape(c.ToString()),
            });
        }
        return sb.ToString();
    }

    private static IQueryable<TblTimeReportSolicitud> AplicarBusquedaSolicitudes(
        IQueryable<TblTimeReportSolicitud> query,
        string? buscar)
    {
        const System.Text.RegularExpressions.RegexOptions ci = System.Text.RegularExpressions.RegexOptions.IgnoreCase;
        foreach (var patron in PatronesBusqueda(buscar))
        {
            query = query.Where(s =>
                System.Text.RegularExpressions.Regex.IsMatch(s.Empleado.IdpersonaNavigation.Apellidos + " " + s.Empleado.IdpersonaNavigation.Nombres, patron, ci)
                || System.Text.RegularExpressions.Regex.IsMatch(s.Empleado.IdpersonaNavigation.Numeroidentificacion, patron, ci)
                || System.Text.RegularExpressions.Regex.IsMatch(s.Empleado.Codigoempleado, patron, ci)
                || System.Text.RegularExpressions.Regex.IsMatch(s.Codigo, patron, ci)
                || System.Text.RegularExpressions.Regex.IsMatch(s.Motivo, patron, ci)
                || System.Text.RegularExpressions.Regex.IsMatch(s.TipoPermiso.Nombre, patron, ci)
                || (s.ObservacionMigracion != null && System.Text.RegularExpressions.Regex.IsMatch(s.ObservacionMigracion, patron, ci)));
        }
        return query;
    }

    /// <summary>Carga completa (con todas las relaciones) solo de los IDs indicados, respetando su orden.</summary>
    private static async Task<IReadOnlyList<PermisoItemDto>> CargarSolicitudesPorIdsAsync(
        ApplicationDbContext db,
        IReadOnlyList<int> ids,
        CancellationToken ct)
    {
        if (ids.Count == 0) return [];
        var entidades = await ConsultarSolicitudesBase(db)
            .Where(s => ids.Contains(s.Id))
            .AsSplitQuery()
            .ToListAsync(ct);
        var porId = entidades.ToDictionary(s => s.Id);
        return ids.Where(porId.ContainsKey).Select(id => MapearSolicitudDto(porId[id], null)).ToList();
    }

    private static async Task<IReadOnlyList<PermisoItemDto>> CargarSolicitudesConRegimenPorIdsAsync(
        ApplicationDbContext db,
        IReadOnlyList<int> ids,
        Dictionary<int, string>? regimenesPrecargados,
        CancellationToken ct)
    {
        var dtos = await CargarSolicitudesPorIdsAsync(db, ids, ct);
        if (dtos.Count == 0) return dtos;

        var regimenes = regimenesPrecargados;
        if (regimenes is null)
        {
            var empIds = dtos.Select(d => d.IdEmpleado).Distinct().ToList();
            regimenes = await ResolverRegimenesPorEmpleadoAsync(db, empIds, ct);
        }

        return dtos
            .Select(d => d with { Regimen = regimenes.GetValueOrDefault(d.IdEmpleado) })
            .ToList();
    }

    private static async Task<Dictionary<int, string>> ResolverRegimenesPorEmpleadoAsync(
        ApplicationDbContext db,
        IReadOnlyCollection<int> empIds,
        CancellationToken ct)
    {
        if (empIds.Count == 0) return [];

        var empleados = await db.TblAdministracionEmpleados
            .AsNoTracking()
            .Where(e => empIds.Contains(e.Id))
            .Include(e => e.IdtipocontratoNavigation)
            .Include(e => e.IdempresacatalogoNavigation)
            .ToListAsync(ct);

        var contratos = await db.TblAdministracionEmpleadoContratos
            .AsNoTracking()
            .Where(c => empIds.Contains(c.Idempleado) && c.Activo)
            .ToListAsync(ct);

        var catIds = new HashSet<int>();
        foreach (var e in empleados)
        {
            if (e.Idtipocontrato.HasValue) catIds.Add(e.Idtipocontrato.Value);
            if (e.Idempresacatalogo.HasValue) catIds.Add(e.Idempresacatalogo.Value);
        }
        foreach (var c in contratos) catIds.Add(c.Idtipocontrato);

        var catalogos = catIds.Count == 0
            ? new Dictionary<int, TblAdministracionCatalogoDetalle>()
            : await db.TblAdministracionCatalogoDetalles
                .AsNoTracking()
                .Where(d => catIds.Contains(d.Id))
                .ToDictionaryAsync(d => d.Id, ct);

        var contratosPorEmp = contratos
            .GroupBy(c => c.Idempleado)
            .ToDictionary(g => g.Key, g => (IReadOnlyCollection<TblAdministracionEmpleadoContrato>)g.ToList());

        return empleados.ToDictionary(
            e => e.Id,
            e => VacacionesSaldoService.ResolverRegimen(
                e,
                contratosPorEmp.GetValueOrDefault(e.Id) ?? [],
                catalogos));
    }

    /// <summary>
    /// Consulta ligera con lo mínimo para clasificar una solicitud en la bandeja de Talento Humano
    /// (estado, revisiones y tipo de líder), sin adjuntos ni datos de presentación.
    /// </summary>
    private static IQueryable<TblTimeReportSolicitud> ConsultarClasificacionBandeja(IQueryable<TblTimeReportSolicitud> query) =>
        query
            .Include(s => s.Estado)
            .Include(s => s.Revisiones)
            .Include(s => s.Lider)
                .ThenInclude(l => l!.IdtipoNavigation)
            .Include(s => s.AsignacionProyecto)
                .ThenInclude(a => a!.IdliderNavigation)
                    .ThenInclude(l => l!.IdtipoNavigation)
            .AsSplitQuery();

    /// <summary>Calcula el saldo de vacaciones de los colaboradores indicados, respetando el orden recibido.</summary>
    private static async Task<List<SaldoVacacionesColaboradorDto>> CalcularSaldosColaboradoresAsync(
        ApplicationDbContext db,
        IReadOnlyList<int> empIds,
        CancellationToken ct)
    {
        if (empIds.Count == 0) return [];

        var empleados = await db.TblAdministracionEmpleados
            .AsNoTracking()
            .Where(e => empIds.Contains(e.Id))
            .Include(e => e.IdpersonaNavigation)
            .Include(e => e.IdcargoNavigation)
            .Include(e => e.IdtipocontratoNavigation)
            .Include(e => e.IdempresacatalogoNavigation)
            .ToListAsync(ct);

        var contratos = await db.TblAdministracionEmpleadoContratos
            .AsNoTracking()
            .Where(c => empIds.Contains(c.Idempleado) && c.Activo)
            .ToListAsync(ct);

        var solicitudes = await db.TblTimeReportSolicitudes
            .AsNoTracking()
            .Where(s => empIds.Contains(s.IdEmpleado) && s.Activo)
            .Include(s => s.Estado)
            .Include(s => s.TipoPermiso)
            .ToListAsync(ct);

        var ajustes = await db.TblTimeReportAjustesVacaciones
            .AsNoTracking()
            .Where(a => empIds.Contains(a.IdEmpleado) && a.Activo)
            .OrderByDescending(a => a.FechaCreacion)
            .ToListAsync(ct);

        var catIds = new HashSet<int>();
        foreach (var e in empleados)
        {
            if (e.Idtipocontrato.HasValue) catIds.Add(e.Idtipocontrato.Value);
            if (e.Idempresacatalogo.HasValue) catIds.Add(e.Idempresacatalogo.Value);
        }
        foreach (var c in contratos) catIds.Add(c.Idtipocontrato);

        var catalogos = catIds.Count == 0
            ? new Dictionary<int, TblAdministracionCatalogoDetalle>()
            : await db.TblAdministracionCatalogoDetalles
                .AsNoTracking()
                .Where(d => catIds.Contains(d.Id))
                .ToDictionaryAsync(d => d.Id, ct);

        var contratosPorEmp = contratos.GroupBy(c => c.Idempleado).ToDictionary(g => g.Key, g => (IReadOnlyCollection<TblAdministracionEmpleadoContrato>)g.ToList());
        var solicitudesPorEmp = solicitudes.GroupBy(s => s.IdEmpleado).ToDictionary(g => g.Key, g => (IReadOnlyCollection<TblTimeReportSolicitud>)g.ToList());
        var ajustesPorEmp = ajustes.GroupBy(a => a.IdEmpleado).ToDictionary(g => g.Key, g => (IReadOnlyCollection<TblTimeReportAjusteVacaciones>)g.ToList());
        var empleadosPorId = empleados.ToDictionary(e => e.Id);

        return empIds.Where(empleadosPorId.ContainsKey).Select(id =>
        {
            var e = empleadosPorId[id];
            var solEmp = solicitudesPorEmp.GetValueOrDefault(e.Id) ?? [];
            var ajEmp = ajustesPorEmp.GetValueOrDefault(e.Id) ?? [];
            var resumen = VacacionesSaldoService.CalcularSaldo(
                e,
                contratosPorEmp.GetValueOrDefault(e.Id) ?? [],
                catalogos,
                solEmp,
                ajEmp);

            var ajustesDto = ajEmp.Select(a => new AjusteVacacionesItemDto(
                Id: a.Id,
                TipoAjuste: a.TipoAjuste,
                DiasAjuste: a.DiasAjuste,
                PermiteVacaciones: a.PermiteVacaciones,
                Motivo: a.Motivo,
                UsuarioCreacion: a.UsuarioCreacion,
                FechaCreacion: a.FechaCreacion.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture),
                FechaAfiliacionBase: a.FechaAfiliacionBase?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
            )).ToList();

            return new SaldoVacacionesColaboradorDto(
                IdEmpleado: e.Id,
                CodigoEmpleado: e.Codigoempleado,
                Cedula: e.IdpersonaNavigation?.Numeroidentificacion ?? "",
                ApellidosNombres: e.IdpersonaNavigation is not null
                    ? $"{e.IdpersonaNavigation.Apellidos} {e.IdpersonaNavigation.Nombres}".Trim()
                    : e.Codigoempleado,
                Cargo: e.IdcargoNavigation?.Nombrecargo ?? "",
                Empresa: resumen.EmpresaNombre,
                TipoContrato: resumen.TipoContratoNombre,
                Regimen: resumen.Regimen,
                TieneDerechoVacaciones: resumen.TieneDerechoVacaciones,
                ExcepcionFacturaActiva: resumen.ExcepcionFacturaActiva,
                FechaIngresoOriginal: resumen.FechaIngresoOriginal?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                FechaInicioVacaciones: resumen.FechaInicioVacaciones?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                AniosAntiguedad: resumen.AniosAntiguedad,
                DiasGanadosAcumulados: resumen.DiasGanadosAcumulados,
                DiasProporcionalesCiclo: resumen.DiasProporcionalesCicloActual,
                DiasConsumidos: resumen.DiasConsumidos,
                DiasAjuste: resumen.DiasAjuste,
                SaldoDisponible: resumen.SaldoDisponible,
                TotalRegistrosHistorial: solEmp.Count,
                RegistrosPorVerificar: solEmp.Count(s => !s.VerificadoRrhh),
                Ajustes: ajustesDto,
                OrigenFechaInicioVacaciones: resumen.OrigenFechaInicioVacaciones);
        }).ToList();
    }
}
