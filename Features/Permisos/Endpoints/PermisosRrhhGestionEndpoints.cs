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

        // B. Historial completo editable de permisos y vacaciones (incluye carga histórica)
        group.MapGet("/rrhh/historial", async (
            [FromQuery] int? idEmpleado,
            [FromQuery] int? anio,
            [FromQuery] bool? soloPendientesVerificar,
            [FromQuery] bool? soloHistoricos,
            ApplicationDbContext db,
            ICurrentUserService currentUser,
            CancellationToken ct) =>
        {
            var actor = await ResolverActorActualAsync(db, currentUser, ct);
            if (actor is null)
                return Results.Json(ApiResponse<object>.Fail(401, "Sesión no válida.", []), statusCode: 401);
            if (!actor.EsRrhh && !actor.EsAdmin)
                return Results.Json(ApiResponse<object>.Fail(403, "Acceso exclusivo para Talento Humano o Administradores.", []), statusCode: 403);

            var query = ConsultarSolicitudesBase(db).Where(s => s.Activo);

            if (idEmpleado is > 0)
                query = query.Where(s => s.IdEmpleado == idEmpleado.Value);
            if (anio is >= 2000 and <= 2100)
                query = query.Where(s => s.FechaInicio.Year == anio.Value || s.FechaFin.Year == anio.Value);
            if (soloPendientesVerificar == true)
                query = query.Where(s => !s.VerificadoRrhh);
            if (soloHistoricos == true)
                query = query.Where(s => s.EsHistorico);

            var items = await query
                .OrderByDescending(s => s.FechaInicio)
                .ThenByDescending(s => s.Id)
                .ToListAsync(ct);

            var dtos = items.Select(s => MapearSolicitudDto(s, null)).ToList();
            return Results.Ok(ApiResponse<IReadOnlyList<PermisoItemDto>>.Ok(dtos));
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
            return Results.Ok(ApiResponse<PermisoItemDto>.Ok(
                MapearSolicitudDto(cargada, null),
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
            return Results.Ok(ApiResponse<PermisoItemDto>.Ok(
                MapearSolicitudDto(cargada, null),
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

        // F. Tablero de Saldos y Ciclos de Vacaciones por Colaborador Activo
        group.MapGet("/rrhh/saldos", async (
            ApplicationDbContext db,
            ICurrentUserService currentUser,
            CancellationToken ct) =>
        {
            var actor = await ResolverActorActualAsync(db, currentUser, ct);
            if (actor is null)
                return Results.Json(ApiResponse<object>.Fail(401, "Sesión no válida.", []), statusCode: 401);
            if (!actor.EsRrhh && !actor.EsAdmin && !actor.EsGerente)
                return Results.Json(ApiResponse<object>.Fail(403, "Acceso exclusivo para Talento Humano, Gerencia o Administradores.", []), statusCode: 403);

            var empleados = await db.TblAdministracionEmpleados
                .AsNoTracking()
                .Where(e => e.Activo && e.Fechaterminacion == null)
                .Include(e => e.IdpersonaNavigation)
                .Include(e => e.IdcargoNavigation)
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

            var resultado = empleados.Select(e =>
            {
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
                    Ajustes: ajustesDto);
            }).ToList();

            return Results.Ok(ApiResponse<IReadOnlyList<SaldoVacacionesColaboradorDto>>.Ok(resultado));
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
}
