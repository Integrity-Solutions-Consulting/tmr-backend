using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using tmr_backend.Features.Permisos.DTOs;
using tmr_backend.Features.Permisos.Services;
using tmr_backend.Features.TimeReport.Services;
using tmr_backend.Infrastructure.Database;
using tmr_backend.Infrastructure.Database.Entities;
using tmr_backend.Infrastructure.Shared;
using tmr_backend.Shared.Wrappers;

namespace tmr_backend.Features.Permisos.Endpoints;

public static class PermisosEndpoints
{
    public static void MapPermisosEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/permisos").WithTags("Permisos");

        // 1. Contexto del colaborador autenticado (datos para el formulario oficial + proyectos/clientes/líderes)
        group.MapGet("/contexto", async (
            ApplicationDbContext db,
            ICurrentUserService currentUser,
            CancellationToken ct) =>
        {
            if (currentUser.UserId <= 0)
                return Results.Json(ApiResponse<object>.Fail(401, "Sesión no válida.", []), statusCode: 401);

            var usuario = await db.TblAutenticacionUsuarios
                .AsNoTracking()
                .Include(u => u.IdpersonaNavigation)
                .FirstOrDefaultAsync(u => u.Id == currentUser.UserId, ct);

            if (usuario is null)
                return Results.Json(ApiResponse<object>.Fail(404, "Usuario no encontrado.", []), statusCode: 404);

            var empleado = await db.TblAdministracionEmpleados
                .AsNoTracking()
                .DeLaPersona(usuario.Idpersona)
                .Include(e => e.IdpersonaNavigation)
                .Include(e => e.IdcargoNavigation)
                    .ThenInclude(c => c!.IddepartamentoNavigation)
                .FirstOrDefaultAsync(ct);

            var proyectosAsignados = new List<ProyectoAsignadoPermisoDto>();
            if (empleado is not null)
            {
                var asignaciones = await db.TblTimeReportAsignacionProyectos
                    .AsNoTracking()
                    .Where(a => a.Idempleado == empleado.Id && a.Activo && a.IdproyectoNavigation.Activo)
                    .Include(a => a.IdproyectoNavigation)
                        .ThenInclude(p => p.IdclienteNavigation)
                    .Include(a => a.IdliderNavigation)
                        .ThenInclude(l => l!.IdpersonaNavigation)
                    .Include(a => a.IdliderNavigation)
                        .ThenInclude(l => l!.IdtipoNavigation)
                    .ToListAsync(ct);

                foreach (var a in asignaciones)
                {
                    var lider = a.IdliderNavigation;
                    var liderNombre = ObtenerNombreLider(lider);
                    var liderTipo = EsLiderExterno(lider) ? "EXT" : "INT";
                    var liderCorreo = lider?.IdpersonaNavigation?.Email;

                    proyectosAsignados.Add(new ProyectoAsignadoPermisoDto(
                        a.Idproyecto,
                        a.IdproyectoNavigation.Nombre,
                        a.IdproyectoNavigation.Idcliente,
                        ObtenerNombreCliente(a.IdproyectoNavigation.IdclienteNavigation),
                        a.Idlider,
                        liderNombre,
                        liderTipo,
                        liderCorreo));
                }
            }

            var lideresDb = await db.TblAdministracionLiders
                .AsNoTracking()
                .Where(l => l.Activo)
                .Include(l => l.IdpersonaNavigation)
                .Include(l => l.IdtipoNavigation)
                .ToListAsync(ct);

            var lideresDisponibles = lideresDb
                .Select(l => new LiderDisponiblePermisoDto(
                    l.Id,
                    ObtenerNombreLider(l),
                    EsLiderExterno(l) ? "EXT" : "INT",
                    l.IdpersonaNavigation?.Email,
                    null,
                    null))
                .OrderBy(l => l.Nombre)
                .ToList();

            var misPermisos = empleado is null
                ? []
                : await db.TblTimeReportPermisos
                    .AsNoTracking()
                    .Where(p => p.Idempleado == empleado.Id && p.Activo)
                    .Include(p => p.IdestadoaprobacionNavigation)
                    .Include(p => p.IdtipopermisoNavigation)
                    .ToListAsync(ct);

            var diasVacacionesTomados = misPermisos
                .Where(p => p.IdtipopermisoNavigation.Codigovalor == "VAC" &&
                            p.IdestadoaprobacionNavigation.Codigovalor != "REC")
                .Sum(p => p.Totaldias);

            var diasDisponibles = Math.Max(0, 15m - diasVacacionesTomados);
            var enProceso = misPermisos.Count(p =>
                p.IdestadoaprobacionNavigation.Codigovalor is "PEN" or "RRH" or "OBS");
            var aprobados = misPermisos.Count(p =>
                p.IdestadoaprobacionNavigation.Codigovalor is "CON" or "APR");
            var horasAcumuladas = misPermisos
                .Where(p => p.IdestadoaprobacionNavigation.Codigovalor != "REC")
                .Sum(p => p.Totalhoras ?? (p.Totaldias * 8m));

            var persona = empleado?.IdpersonaNavigation ?? usuario.IdpersonaNavigation;
            var apellidosNombres = $"{persona.Apellidos} {persona.Nombres}".Trim();
            if (string.IsNullOrWhiteSpace(apellidosNombres))
                apellidosNombres = usuario.Email;

            var dto = new ContextoColaboradorPermisoDto(
                IdEmpleado: empleado?.Id ?? 0,
                CodigoEmpleado: empleado?.Codigoempleado ?? "EMP-000",
                ApellidosNombres: apellidosNombres,
                NumeroCedula: persona.Numeroidentificacion ?? "",
                Cargo: empleado?.IdcargoNavigation?.Nombrecargo ?? "Colaborador ISC",
                Area: empleado?.IdcargoNavigation?.IddepartamentoNavigation?.Valor,
                Correo: empleado?.Emailcorporativo ?? usuario.Email,
                DiasDisponibles: diasDisponibles,
                EnProceso: enProceso,
                Aprobados: aprobados,
                HorasAcumuladas: horasAcumuladas,
                ProyectosAsignados: proyectosAsignados,
                LideresDisponibles: lideresDisponibles);

            return Results.Ok(ApiResponse<ContextoColaboradorPermisoDto>.Ok(dto));
        });

        // 2. Listar Mis Permisos (Colaborador actual)
        group.MapGet("/mis-permisos", async (
            ApplicationDbContext db,
            ICurrentUserService currentUser,
            CancellationToken ct) =>
        {
            if (currentUser.UserId <= 0)
                return Results.Json(ApiResponse<object>.Fail(401, "Sesión no válida.", []), statusCode: 401);

            var usuario = await db.TblAutenticacionUsuarios
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == currentUser.UserId, ct);

            if (usuario is null)
                return Results.Ok(ApiResponse<IReadOnlyList<PermisoItemDto>>.Ok([]));

            var empleado = await db.TblAdministracionEmpleados
                .AsNoTracking()
                .DeLaPersona(usuario.Idpersona)
                .FirstOrDefaultAsync(ct);

            if (empleado is null)
                return Results.Ok(ApiResponse<IReadOnlyList<PermisoItemDto>>.Ok([]));

            var items = await ConsultarPermisosBase(db)
                .Where(p => p.Idempleado == empleado.Id && p.Activo)
                .OrderByDescending(p => p.Fechacreacion)
                .ToListAsync(ct);

            var dtos = items.Select(p => MapearPermisoDto(p, null)).ToList();
            return Results.Ok(ApiResponse<IReadOnlyList<PermisoItemDto>>.Ok(dtos));
        });

        // 3. Crear nueva solicitud (JSON)
        group.MapPost("", async (
            [FromBody] CrearPermisoRequest request,
            ApplicationDbContext db,
            ICurrentUserService currentUser,
            IPermisosNotificacionService notificacionService,
            CancellationToken ct) =>
        {
            return await CrearSolicitudInternaAsync(request, null, db, currentUser, null, notificacionService, ct);
        });

        // 4. Crear nueva solicitud con archivo adjunto (multipart/form-data)
        group.MapPost("/con-evidencia", async (
            HttpRequest httpRequest,
            ApplicationDbContext db,
            ICurrentUserService currentUser,
            IPermisosStorageService storageService,
            IPermisosNotificacionService notificacionService,
            CancellationToken ct) =>
        {
            if (!httpRequest.HasFormContentType)
                return Results.BadRequest(ApiResponse<object>.Fail(400, "Se esperaba multipart/form-data.", []));

            var form = await httpRequest.ReadFormAsync(ct);
            int? ParseInt(string key) => int.TryParse(form[key].FirstOrDefault(), out var v) && v > 0 ? v : null;
            decimal? ParseDec(string key) => decimal.TryParse(form[key].FirstOrDefault(), NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : null;

            var req = new CrearPermisoRequest(
                IdProyecto: ParseInt("idProyecto"),
                IdCliente: ParseInt("idCliente"),
                IdLider: ParseInt("idLider"),
                TipoOficial: form["tipoOficial"].FirstOrDefault() ?? "VACACIONES",
                FechaInicio: form["fechaInicio"].FirstOrDefault() ?? "",
                FechaFin: form["fechaFin"].FirstOrDefault() ?? "",
                FechaRegreso: form["fechaRegreso"].FirstOrDefault(),
                Horario: form["horario"].FirstOrDefault() ?? "COMPLETA",
                DetalleHorario: form["detalleHorario"].FirstOrDefault(),
                TotalDias: ParseDec("totalDias"),
                TotalHoras: ParseDec("totalHoras"),
                Motivo: form["motivo"].FirstOrDefault() ?? "");

            var archivo = form.Files.GetFile("evidencia") ?? form.Files.FirstOrDefault();
            return await CrearSolicitudInternaAsync(req, archivo, db, currentUser, storageService, notificacionService, ct);
        }).DisableAntiforgery();

        // 5. Subir o reemplazar evidencia de un permiso existente
        group.MapPost("/{id:int}/evidencia", async (
            int id,
            IFormFile archivo,
            ApplicationDbContext db,
            IPermisosStorageService storageService,
            CancellationToken ct) =>
        {
            var permiso = await db.TblTimeReportPermisos.FirstOrDefaultAsync(p => p.Id == id && p.Activo, ct);
            if (permiso is null)
                return Results.NotFound(ApiResponse<object>.Fail(404, "Solicitud de permiso no encontrada.", []));

            var guardado = await storageService.GuardarEvidenciaAsync(archivo, permiso.Codigo ?? $"PER-{permiso.Id:D4}", ct);
            permiso.Evidencianombre = guardado.NombreOriginal;
            permiso.Evidenciaurl = guardado.UrlAlmacenamiento;
            permiso.Evidenciamimetype = guardado.MimeType;
            permiso.Fechamodificacion = DateTime.UtcNow;

            await db.SaveChangesAsync(ct);

            var recargado = await ConsultarPermisosBase(db).FirstAsync(p => p.Id == id, ct);
            return Results.Ok(ApiResponse<PermisoItemDto>.Ok(MapearPermisoDto(recargado, null), "Evidencia adjuntada exitosamente."));
        }).DisableAntiforgery();

        // 6. Descargar evidencia adjunta
        group.MapGet("/{id:int}/evidencia", async (
            int id,
            ApplicationDbContext db,
            IPermisosStorageService storageService,
            CancellationToken ct) =>
        {
            var permiso = await db.TblTimeReportPermisos
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id && p.Activo, ct);

            if (permiso is null || string.IsNullOrWhiteSpace(permiso.Evidenciaurl))
                return Results.NotFound(ApiResponse<object>.Fail(404, "Esta solicitud no tiene archivo de evidencia adjunto.", []));

            var descarga = await storageService.ObtenerEvidenciaAsync(
                permiso.Evidenciaurl,
                permiso.Evidencianombre,
                permiso.Evidenciamimetype,
                ct);

            if (descarga is null)
                return Results.NotFound(ApiResponse<object>.Fail(404, "No se encontró el archivo físico de evidencia.", []));

            return Results.File(descarga.Contenido, descarga.MimeType, descarga.NombreArchivo);
        });

        // 7. Descargar PDF oficial de una solicitud
        group.MapGet("/{id:int}/pdf", async (
            int id,
            ApplicationDbContext db,
            IPermisosPdfService pdfService,
            CancellationToken ct) =>
        {
            var permiso = await ConsultarPermisosBase(db)
                .FirstOrDefaultAsync(p => p.Id == id && p.Activo, ct);

            if (permiso is null)
                return Results.NotFound(ApiResponse<object>.Fail(404, "Solicitud de permiso no encontrada.", []));

            var dto = MapearPermisoDto(permiso, null);
            var pdfBytes = pdfService.GenerarFormularioOficialPdf(dto);
            var fileName = $"Formulario_Permiso_{dto.FolioOrCodigo()}.pdf";

            return Results.File(pdfBytes, "application/pdf", fileName);
        });

        // 8. Bandeja de Aprobación Líder
        group.MapGet("/equipo", async (
            ApplicationDbContext db,
            ICurrentUserService currentUser,
            CancellationToken ct) =>
        {
            if (currentUser.UserId <= 0)
                return Results.Json(ApiResponse<object>.Fail(401, "Sesión no válida.", []), statusCode: 401);

            var usuario = await db.TblAutenticacionUsuarios
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == currentUser.UserId, ct);

            var esAdmin = currentUser.Roles.Any(r =>
                r.Equals("ADMINISTRADOR", StringComparison.OrdinalIgnoreCase) ||
                r.Equals("ADMIN", StringComparison.OrdinalIgnoreCase));

            int? idLiderActual = null;
            if (usuario is not null)
            {
                var lider = await db.TblAdministracionLiders
                    .AsNoTracking()
                    .FirstOrDefaultAsync(l => l.Idpersona == usuario.Idpersona && l.Activo, ct);
                idLiderActual = lider?.Id;
            }

            var query = ConsultarPermisosBase(db).Where(p => p.Activo);
            if (!esAdmin && idLiderActual.HasValue)
            {
                query = query.Where(p => p.Idlider == idLiderActual.Value);
            }

            var lista = await query
                .OrderByDescending(p => p.Fechacreacion)
                .ToListAsync(ct);

            var dtos = lista.Select(p =>
            {
                var traslape = CalcularAlertaTraslape(p, lista);
                return MapearPermisoDto(p, traslape);
            }).ToList();

            return Results.Ok(ApiResponse<IReadOnlyList<PermisoItemDto>>.Ok(dtos));
        });

        // 9. Decisión del Líder (aprobar | observar | rechazar)
        group.MapPost("/{id:int}/decision-lider", async (
            int id,
            [FromBody] DecisionLiderRequest request,
            ApplicationDbContext db,
            ICurrentUserService currentUser,
            IPermisosNotificacionService notificacionService,
            CancellationToken ct) =>
        {
            var permiso = await db.TblTimeReportPermisos
                .FirstOrDefaultAsync(p => p.Id == id && p.Activo, ct);

            if (permiso is null)
                return Results.NotFound(ApiResponse<object>.Fail(404, "Solicitud de permiso no encontrada.", []));

            var accion = (request.Accion ?? "aprobar").Trim().ToLowerInvariant();
            var codigoEstadoDestino = accion switch
            {
                "aprobar" => "RRH",
                "observar" => "OBS",
                "rechazar" => "REC",
                _ => "RRH"
            };

            var idEstado = await ObtenerIdEstadoPermisoAsync(db, codigoEstadoDestino, ct);
            permiso.Idestadoaprobacion = idEstado;
            permiso.Comentariolider = request.Comentario;
            permiso.Observacion = request.Comentario;
            permiso.Fechaaprobacionlider = DateTime.UtcNow;
            permiso.Metodolider = "Cuenta TMR Líder";
            permiso.Aprobadoporlider = accion == "aprobar";

            if (accion == "aprobar")
            {
                permiso.Fechaaprobacion = DateTime.UtcNow;
            }

            if (currentUser.UserId > 0)
            {
                var u = await db.TblAutenticacionUsuarios.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == currentUser.UserId, ct);
                if (u is not null)
                {
                    var empAprobador = await db.TblAdministracionEmpleados.AsNoTracking()
                        .DeLaPersona(u.Idpersona)
                        .FirstOrDefaultAsync(ct);
                    if (empAprobador is not null)
                        permiso.Aprobadopor = empAprobador.Id;
                }
            }

            await db.SaveChangesAsync(ct);

            var recargado = await ConsultarPermisosBase(db).FirstAsync(p => p.Id == id, ct);
            var dto = MapearPermisoDto(recargado, null);
            var correoColab = recargado.IdempleadoNavigation.Emailcorporativo
                              ?? recargado.IdempleadoNavigation.IdpersonaNavigation.Email;

            var evento = accion switch
            {
                "aprobar" => EventoNotificacionPermiso.AprobadaPorLider,
                "observar" => EventoNotificacionPermiso.ObservadaPorLider,
                _ => EventoNotificacionPermiso.RechazadaPorLider
            };

            await notificacionService.NotificarCambioEstadoAsync(dto, correoColab, evento, ct);

            return Results.Ok(ApiResponse<PermisoItemDto>.Ok(dto, "Decisión del líder registrada correctamente."));
        });

        // 10. Bandeja de Validación Talento Humano (RRHH)
        group.MapGet("/rrhh", async (
            ApplicationDbContext db,
            CancellationToken ct) =>
        {
            var lista = await ConsultarPermisosBase(db)
                .Where(p => p.Activo)
                .OrderByDescending(p => p.Fechacreacion)
                .ToListAsync(ct);

            var dtos = lista.Select(p => MapearPermisoDto(p, null)).ToList();
            return Results.Ok(ApiResponse<IReadOnlyList<PermisoItemDto>>.Ok(dtos));
        });

        // 11. Decisión de Talento Humano RRHH (confirmar | devolver)
        group.MapPost("/{id:int}/decision-rrhh", async (
            int id,
            [FromBody] DecisionRrhhRequest request,
            ApplicationDbContext db,
            ICurrentUserService currentUser,
            IPermisosNotificacionService notificacionService,
            CancellationToken ct) =>
        {
            var permiso = await db.TblTimeReportPermisos
                .FirstOrDefaultAsync(p => p.Id == id && p.Activo, ct);

            if (permiso is null)
                return Results.NotFound(ApiResponse<object>.Fail(404, "Solicitud de permiso no encontrada.", []));

            var confirmar = !string.Equals(request.Accion, "devolver", StringComparison.OrdinalIgnoreCase);
            var codigoEstadoDestino = confirmar ? "CON" : "OBS";
            var idEstado = await ObtenerIdEstadoPermisoAsync(db, codigoEstadoDestino, ct);

            permiso.Idestadoaprobacion = idEstado;
            permiso.Confirmadoporrrhh = confirmar;
            permiso.Fechaconfirmacionrrhh = DateTime.UtcNow;
            permiso.Observacionrrhh = request.Observacion;
            if (!string.IsNullOrWhiteSpace(request.ImpactoTimeReport))
                permiso.Impactotimereport = request.ImpactoTimeReport;

            if (confirmar)
            {
                permiso.Aprobadoporlider = true;
                permiso.Fechaaprobacion = DateTime.UtcNow;
            }

            if (currentUser.UserId > 0)
            {
                var u = await db.TblAutenticacionUsuarios.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == currentUser.UserId, ct);
                if (u is not null)
                {
                    var empRrhh = await db.TblAdministracionEmpleados.AsNoTracking()
                        .DeLaPersona(u.Idpersona)
                        .FirstOrDefaultAsync(ct);
                    if (empRrhh is not null)
                        permiso.Idempleadorrhh = empRrhh.Id;
                }
            }

            await db.SaveChangesAsync(ct);

            var recargado = await ConsultarPermisosBase(db).FirstAsync(p => p.Id == id, ct);
            var dto = MapearPermisoDto(recargado, null);
            var correoColab = recargado.IdempleadoNavigation.Emailcorporativo
                              ?? recargado.IdempleadoNavigation.IdpersonaNavigation.Email;

            await notificacionService.NotificarCambioEstadoAsync(
                dto,
                correoColab,
                confirmar ? EventoNotificacionPermiso.ConfirmadaPorRrhh : EventoNotificacionPermiso.DevueltaPorRrhh,
                ct);

            return Results.Ok(ApiResponse<PermisoItemDto>.Ok(
                dto,
                confirmar
                    ? "Solicitud confirmada definitivamente por Talento Humano."
                    : "Solicitud devuelta con observaciones."));
        });

        // 12. Reenviar enlace externo Magic Link
        group.MapPost("/{id:int}/reenviar-enlace", async (
            int id,
            ApplicationDbContext db,
            IPermisosNotificacionService notificacionService,
            CancellationToken ct) =>
        {
            var permiso = await db.TblTimeReportPermisos
                .FirstOrDefaultAsync(p => p.Id == id && p.Activo, ct);

            if (permiso is null)
                return Results.NotFound(ApiResponse<object>.Fail(404, "Solicitud de permiso no encontrada.", []));

            if (string.IsNullOrWhiteSpace(permiso.Tokenexterno))
                permiso.Tokenexterno = Guid.NewGuid().ToString("N");
            permiso.Tokenexternoexpira = DateTime.UtcNow.AddHours(48);

            await db.SaveChangesAsync(ct);

            var recargado = await ConsultarPermisosBase(db).FirstAsync(p => p.Id == id, ct);
            var dto = MapearPermisoDto(recargado, null);
            var correoColab = recargado.IdempleadoNavigation.Emailcorporativo
                              ?? recargado.IdempleadoNavigation.IdpersonaNavigation.Email;

            await notificacionService.NotificarCambioEstadoAsync(
                dto,
                correoColab,
                EventoNotificacionPermiso.ReenvioMagicLink,
                ct);

            return Results.Ok(ApiResponse<PermisoItemDto>.Ok(dto, "Magic Link reenviado exitosamente al líder externo."));
        });

        // 13. Historial General (Gerencia)
        group.MapGet("/gerencia", async (
            ApplicationDbContext db,
            CancellationToken ct) =>
        {
            var lista = await ConsultarPermisosBase(db)
                .Where(p => p.Activo)
                .OrderByDescending(p => p.Fechainicio)
                .ThenByDescending(p => p.Fechacreacion)
                .ToListAsync(ct);

            var dtos = lista.Select(p => MapearPermisoDto(p, null)).ToList();
            return Results.Ok(ApiResponse<IReadOnlyList<PermisoItemDto>>.Ok(dtos));
        });

        // 14. Endpoints públicos para Revisión Externa (Magic Link)
        group.MapGet("/externo/{token}", async (
            string token,
            ApplicationDbContext db,
            CancellationToken ct) =>
        {
            var permiso = await ConsultarPermisosBase(db)
                .FirstOrDefaultAsync(p => p.Tokenexterno == token && p.Activo, ct);

            if (permiso is null)
                return Results.NotFound(ApiResponse<object>.Fail(404, "El enlace de revisión no existe o ha expirado.", []));

            return Results.Ok(ApiResponse<PermisoItemDto>.Ok(MapearPermisoDto(permiso, null)));
        }).AllowAnonymous();

        group.MapPost("/externo/{token}/decision", async (
            string token,
            [FromBody] DecisionExternaRequest request,
            ApplicationDbContext db,
            IPermisosNotificacionService notificacionService,
            CancellationToken ct) =>
        {
            var permiso = await db.TblTimeReportPermisos
                .FirstOrDefaultAsync(p => p.Tokenexterno == token && p.Activo, ct);

            if (permiso is null)
                return Results.NotFound(ApiResponse<object>.Fail(404, "El enlace de revisión no existe o ha expirado.", []));

            var aprobar = !string.Equals(request.Accion, "rechazar", StringComparison.OrdinalIgnoreCase);
            var codigoEstadoDestino = aprobar ? "RRH" : "REC";
            var idEstado = await ObtenerIdEstadoPermisoAsync(db, codigoEstadoDestino, ct);

            permiso.Idestadoaprobacion = idEstado;
            permiso.Aprobadoporlider = aprobar;
            permiso.Fechaaprobacionlider = DateTime.UtcNow;
            permiso.Metodolider = "Magic Link Externo";
            permiso.Comentariolider = request.Comentario;
            permiso.Observacion = request.Comentario;
            if (aprobar)
            {
                permiso.Fechaaprobacion = DateTime.UtcNow;
            }

            await db.SaveChangesAsync(ct);

            var recargado = await ConsultarPermisosBase(db).FirstAsync(p => p.Id == permiso.Id, ct);
            var dto = MapearPermisoDto(recargado, null);
            var correoColab = recargado.IdempleadoNavigation.Emailcorporativo
                              ?? recargado.IdempleadoNavigation.IdpersonaNavigation.Email;

            await notificacionService.NotificarCambioEstadoAsync(
                dto,
                correoColab,
                aprobar ? EventoNotificacionPermiso.AprobadaPorLider : EventoNotificacionPermiso.RechazadaPorLider,
                ct);

            return Results.Ok(ApiResponse<PermisoItemDto>.Ok(
                dto,
                aprobar
                    ? "Solicitud aprobada exitosamente vía Magic Link."
                    : "Solicitud rechazada vía Magic Link."));
        }).AllowAnonymous();

        group.MapGet("/externo/{token}/pdf", async (
            string token,
            ApplicationDbContext db,
            IPermisosPdfService pdfService,
            CancellationToken ct) =>
        {
            var permiso = await ConsultarPermisosBase(db)
                .FirstOrDefaultAsync(p => p.Tokenexterno == token && p.Activo, ct);

            if (permiso is null)
                return Results.NotFound(ApiResponse<object>.Fail(404, "El enlace de revisión no existe.", []));

            var dto = MapearPermisoDto(permiso, null);
            var pdfBytes = pdfService.GenerarFormularioOficialPdf(dto);
            return Results.File(pdfBytes, "application/pdf", $"Formulario_Permiso_{dto.FolioOrCodigo()}.pdf");
        }).AllowAnonymous();
    }

    private static async Task<IResult> CrearSolicitudInternaAsync(
        CrearPermisoRequest request,
        IFormFile? archivoEvidencia,
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        IPermisosStorageService? storageService,
        IPermisosNotificacionService notificacionService,
        CancellationToken ct)
    {
        if (currentUser.UserId <= 0)
            return Results.Json(ApiResponse<object>.Fail(401, "Sesión no válida.", []), statusCode: 401);

        var usuario = await db.TblAutenticacionUsuarios
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == currentUser.UserId, ct);

        if (usuario is null)
            return Results.Json(ApiResponse<object>.Fail(404, "Usuario no encontrado.", []), statusCode: 404);

        var empleado = await db.TblAdministracionEmpleados
            .AsNoTracking()
            .DeLaPersona(usuario.Idpersona)
            .FirstOrDefaultAsync(ct);

        if (empleado is null)
        {
            empleado = await db.TblAdministracionEmpleados
                .AsNoTracking()
                .Where(e => e.Activo)
                .OrderBy(e => e.Id)
                .FirstOrDefaultAsync(ct);

            if (empleado is null)
                return Results.BadRequest(ApiResponse<object>.Fail(400, "No existe una ficha de colaborador asociada a tu cuenta.", []));
        }

        var fechaInicio = ParsearFecha(request.FechaInicio) ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var fechaFin = ParsearFecha(request.FechaFin) ?? fechaInicio;
        if (fechaFin < fechaInicio)
            fechaFin = fechaInicio;

        var fechaRegreso = ParsearFecha(request.FechaRegreso) ?? CalcularSiguienteDiaLaborable(fechaFin);
        var horario = string.IsNullOrWhiteSpace(request.Horario) ? "COMPLETA" : request.Horario.Trim().ToUpperInvariant();

        var diasHabiles = request.TotalDias is > 0
            ? request.TotalDias.Value
            : horario is "MANANA" or "TARDE"
                ? 0.5m
                : CalcularDiasLaborables(fechaInicio, fechaFin);

        var horasTotales = request.TotalHoras is > 0
            ? request.TotalHoras.Value
            : horario is "MANANA" or "TARDE"
                ? 4m
                : diasHabiles * 8m;

        var idTipoPermiso = await ObtenerIdTipoPermisoAsync(db, request.TipoOficial, ct);
        var idEstadoPendiente = await ObtenerIdEstadoPermisoAsync(db, "PEN", ct);

        int? idProyecto = request.IdProyecto;
        int? idCliente = request.IdCliente;
        int? idLider = request.IdLider;

        if (!idProyecto.HasValue || !idLider.HasValue)
        {
            var asig = await db.TblTimeReportAsignacionProyectos
                .AsNoTracking()
                .Include(a => a.IdproyectoNavigation)
                .Where(a => a.Idempleado == empleado.Id && a.Activo)
                .OrderByDescending(a => a.Id)
                .FirstOrDefaultAsync(ct);

            if (asig is not null)
            {
                idProyecto ??= asig.Idproyecto;
                idCliente ??= asig.IdproyectoNavigation.Idcliente;
                idLider ??= asig.Idlider;
            }
        }

        if (idProyecto.HasValue && !idCliente.HasValue)
        {
            var proy = await db.TblTimeReportProyectos.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == idProyecto.Value, ct);
            idCliente = proy?.Idcliente;
        }

        var anio = fechaInicio.Year;
        var consecutivo = await db.TblTimeReportPermisos.CountAsync(p => p.Fechainicio.Year == anio, ct) + 1;
        var codigo = $"PER-{anio}-{consecutivo:D3}";
        while (await db.TblTimeReportPermisos.AnyAsync(p => p.Codigo == codigo, ct))
        {
            consecutivo++;
            codigo = $"PER-{anio}-{consecutivo:D3}";
        }

        var tokenExterno = Guid.NewGuid().ToString("N");

        var nuevo = new TblTimeReportPermiso
        {
            Codigo = codigo,
            Idempleado = empleado.Id,
            Idproyecto = idProyecto,
            Idcliente = idCliente,
            Idlider = idLider,
            Idtipopermiso = idTipoPermiso,
            Idestadoaprobacion = idEstadoPendiente,
            Fechainicio = fechaInicio,
            Fechafin = fechaFin,
            Fecharegreso = fechaRegreso,
            Horario = horario,
            Detallehorario = request.DetalleHorario,
            Totaldias = diasHabiles,
            Totalhoras = horasTotales,
            Espagado = true,
            Descripcion = request.Motivo?.Trim(),
            Aprobadoporlider = false,
            Confirmadoporrrhh = false,
            Tokenexterno = tokenExterno,
            Tokenexternoexpira = DateTime.UtcNow.AddHours(48),
            Impactotimereport = "RESTA_META",
            Activo = true,
            Usuariocreacion = usuario.Email,
            Fechacreacion = DateTime.UtcNow,
            Ipcreacion = "127.0.0.1"
        };

        if (archivoEvidencia is not null && archivoEvidencia.Length > 0 && storageService is not null)
        {
            var evidenciaGuardada = await storageService.GuardarEvidenciaAsync(archivoEvidencia, codigo, ct);
            nuevo.Evidencianombre = evidenciaGuardada.NombreOriginal;
            nuevo.Evidenciaurl = evidenciaGuardada.UrlAlmacenamiento;
            nuevo.Evidenciamimetype = evidenciaGuardada.MimeType;
        }

        db.TblTimeReportPermisos.Add(nuevo);
        await db.SaveChangesAsync(ct);

        var creado = await ConsultarPermisosBase(db).FirstAsync(p => p.Id == nuevo.Id, ct);
        var dto = MapearPermisoDto(creado, null);
        var correoColab = creado.IdempleadoNavigation.Emailcorporativo
                          ?? creado.IdempleadoNavigation.IdpersonaNavigation.Email
                          ?? usuario.Email;

        await notificacionService.NotificarCambioEstadoAsync(
            dto,
            correoColab,
            EventoNotificacionPermiso.SolicitudCreada,
            ct);

        return Results.Ok(ApiResponse<PermisoItemDto>.Ok(dto, "Solicitud de permiso registrada y notificada exitosamente."));
    }

    private static IQueryable<TblTimeReportPermiso> ConsultarPermisosBase(ApplicationDbContext db)
    {
        return db.TblTimeReportPermisos
            .AsNoTracking()
            .Include(p => p.IdempleadoNavigation)
                .ThenInclude(e => e.IdpersonaNavigation)
            .Include(p => p.IdempleadoNavigation)
                .ThenInclude(e => e.IdcargoNavigation)
                    .ThenInclude(c => c!.IddepartamentoNavigation)
            .Include(p => p.IdproyectoNavigation)
                .ThenInclude(pr => pr!.IdclienteNavigation)
            .Include(p => p.IdclienteNavigation)
            .Include(p => p.IdliderNavigation)
                .ThenInclude(l => l!.IdpersonaNavigation)
            .Include(p => p.IdliderNavigation)
                .ThenInclude(l => l!.IdtipoNavigation)
            .Include(p => p.IdempleadorrhhNavigation)
                .ThenInclude(er => er!.IdpersonaNavigation)
            .Include(p => p.IdtipopermisoNavigation)
            .Include(p => p.IdestadoaprobacionNavigation);
    }

    private static PermisoItemDto MapearPermisoDto(TblTimeReportPermiso p, string? traslapeAlerta)
    {
        var persona = p.IdempleadoNavigation.IdpersonaNavigation;
        var colaborador = $"{persona.Apellidos} {persona.Nombres}".Trim();
        var cedula = persona.Numeroidentificacion ?? "";
        var cargo = p.IdempleadoNavigation.IdcargoNavigation?.Nombrecargo ?? "Colaborador ISC";
        var area = p.IdempleadoNavigation.IdcargoNavigation?.IddepartamentoNavigation?.Valor ?? "Operaciones & Tecnología";

        var cliente = ObtenerNombreCliente(p.IdclienteNavigation)
                      ?? ObtenerNombreCliente(p.IdproyectoNavigation?.IdclienteNavigation)
                      ?? "Integrity Solutions";

        var proyecto = p.IdproyectoNavigation?.Nombre ?? "General ISC";

        var lider = p.IdliderNavigation;
        var liderNombre = ObtenerNombreLider(lider);
        var esLiderExt = EsLiderExterno(lider);
        var liderTipo = esLiderExt ? "EXT" : "INT";
        var liderCorreo = lider?.IdpersonaNavigation?.Email;
        var liderEmpresa = cliente;

        var tipoCodigo = p.IdtipopermisoNavigation.Codigovalor?.Trim() ?? "VAC";
        var tipoOficial = !string.IsNullOrWhiteSpace(p.IdtipopermisoNavigation.Valorextra)
            ? p.IdtipopermisoNavigation.Valorextra.Trim()
            : MapearCodigoATipoOficial(tipoCodigo);
        var tipoNombre = p.IdtipopermisoNavigation.Valor;

        var fechaCreacionLocal = p.Fechacreacion;
        var fechaSolicitudStr = fechaCreacionLocal.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        var diaSol = fechaCreacionLocal.ToString("dd", CultureInfo.InvariantCulture);
        var mesSol = fechaCreacionLocal.ToString("MM", CultureInfo.InvariantCulture);
        var anioSol = fechaCreacionLocal.ToString("yyyy", CultureInfo.InvariantCulture);

        var fechaInicioStr = p.Fechainicio.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        var fechaFinStr = p.Fechafin.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        var fechaRegreso = p.Fecharegreso ?? CalcularSiguienteDiaLaborable(p.Fechafin);
        var fechaRegresoStr = fechaRegreso.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

        var fechasLegible = p.Fechainicio == p.Fechafin
            ? (!string.IsNullOrWhiteSpace(p.Detallehorario) ? $"{fechaInicioStr} ({p.Detallehorario})" : fechaInicioStr)
            : $"{fechaInicioStr} - {fechaFinStr}";

        var fechaAprobStr = p.Fechaconfirmacionrrhh?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
                            ?? p.Fechaaprobacionlider?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
                            ?? p.Fechaaprobacion?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
                            ?? "Pendiente";

        var totalDias = p.Totaldias;
        var totalHoras = p.Totalhoras ?? (totalDias * 8m);
        var diasStr = totalDias == 1m
            ? "1 día laborable"
            : $"{totalDias.ToString("0.##", CultureInfo.InvariantCulture)} días laborables";
        var horasStr = $"{totalHoras.ToString("0.##", CultureInfo.InvariantCulture)} horas";
        var duracionStr = $"{diasStr} ({totalHoras.ToString("0.##", CultureInfo.InvariantCulture)} h)";

        var estadoCod = (p.IdestadoaprobacionNavigation.Codigovalor ?? "PEN").Trim().ToUpperInvariant();
        var (estadoClase, estadoTexto, estadoRrhh, estadoLider) = ResolverEstados(estadoCod, p.Aprobadoporlider, p.Confirmadoporrrhh, esLiderExt);

        var metodoLider = !string.IsNullOrWhiteSpace(p.Metodolider)
            ? p.Metodolider
            : esLiderExt
                ? "Magic Link Externo"
                : "Cuenta TMR Líder";

        var responsableRrhh = p.IdempleadorrhhNavigation is not null
            ? $"{p.IdempleadorrhhNavigation.IdpersonaNavigation.Nombres} {p.IdempleadorrhhNavigation.IdpersonaNavigation.Apellidos}".Trim()
            : "Talento Humano ISC";

        var codigoFolio = string.IsNullOrWhiteSpace(p.Codigo)
            ? $"PER-{p.Fechainicio.Year}-{p.Id:D3}"
            : p.Codigo;

        var comentarios = p.Observacionrrhh ?? p.Comentariolider ?? p.Observacion;

        return new PermisoItemDto(
            Id: p.Id,
            Codigo: codigoFolio,
            IdEmpleado: p.Idempleado,
            Colaborador: colaborador,
            Cedula: cedula,
            CodigoColaborador: p.IdempleadoNavigation.Codigoempleado,
            Cargo: cargo,
            Area: area,
            IdCliente: p.Idcliente ?? p.IdproyectoNavigation?.Idcliente,
            Cliente: cliente,
            IdProyecto: p.Idproyecto,
            Proyecto: proyecto,
            IdLider: p.Idlider,
            LiderNombre: liderNombre,
            LiderTipo: liderTipo,
            LiderCorreo: liderCorreo,
            LiderEmpresa: liderEmpresa,
            TipoCodigo: tipoCodigo,
            Tipo: tipoNombre,
            TipoOficial: tipoOficial,
            Motivo: p.Descripcion ?? "",
            FechaSolicitud: fechaSolicitudStr,
            DiaSolicitud: diaSol,
            MesSolicitud: mesSol,
            AnioSolicitud: anioSol,
            FechaInicio: fechaInicioStr,
            FechaFin: fechaFinStr,
            FechaRegreso: fechaRegresoStr,
            Fechas: fechasLegible,
            FechaAprobacion: fechaAprobStr,
            DiasSolicitados: diasStr,
            TotalDias: totalDias,
            Horario: string.IsNullOrWhiteSpace(p.Horario) ? "COMPLETA" : p.Horario.ToUpperInvariant(),
            DetalleHorario: p.Detallehorario,
            HorasSolicitadas: horasStr,
            TotalHoras: totalHoras,
            Duracion: duracionStr,
            EvidenciaNombre: p.Evidencianombre,
            EvidenciaUrl: p.Evidenciaurl,
            TieneEvidencia: !string.IsNullOrWhiteSpace(p.Evidenciaurl) || !string.IsNullOrWhiteSpace(p.Evidencianombre),
            AprobadoLider: p.Aprobadoporlider || estadoCod is "RRH" or "CON" or "APR",
            FechaAprobacionLider: p.Fechaaprobacionlider?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
            MetodoLider: metodoLider,
            ComentarioLider: p.Comentariolider,
            ConfirmadoRrhh: p.Confirmadoporrrhh || estadoCod is "CON" or "APR",
            ResponsableRrhh: responsableRrhh,
            FechaConfirmacionRrhh: p.Fechaconfirmacionrrhh?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
            ObservacionRrhh: p.Observacionrrhh,
            ImpactoTimeReport: string.IsNullOrWhiteSpace(p.Impactotimereport) ? "RESTA_META" : p.Impactotimereport,
            EstadoCodigo: estadoCod,
            EstadoClase: estadoClase,
            EstadoTexto: estadoTexto,
            EstadoRrhh: estadoRrhh,
            EstadoLider: estadoLider,
            Comentarios: comentarios,
            TraslapeAlerta: traslapeAlerta,
            TokenExterno: p.Tokenexterno);
    }

    private static (string EstadoClase, string EstadoTexto, string EstadoRrhh, string EstadoLider) ResolverEstados(
        string codigo,
        bool aprobadoLider,
        bool confirmadoRrhh,
        bool esLiderExterno)
    {
        if (confirmadoRrhh || codigo is "CON" or "APR")
            return ("confirmado", "Confirmado", "APROBADO_RRHH", "Aprobado");

        if (codigo == "REC")
            return ("rechazado", "Rechazado", "RECHAZADO", "Rechazado");

        if (codigo == "OBS")
            return ("observado", "Observado", "OBSERVADO_RRHH", "Observado");

        if (aprobadoLider || codigo == "RRH")
            return ("rrhh", "En Talento Humano", "CONFIRMADO_LIDER", "Aprobado");

        return (
            "pendiente",
            "Pendiente Jefe",
            esLiderExterno ? "PENDIENTE_LIDER_EXT" : "PENDIENTE_LIDER_INT",
            "Pendiente");
    }

    private static string ObtenerNombreCliente(TblAdministracionCliente? cliente)
    {
        if (cliente is null) return "Integrity Solutions";
        if (!string.IsNullOrWhiteSpace(cliente.Nombrecomercial)) return cliente.Nombrecomercial.Trim();
        if (!string.IsNullOrWhiteSpace(cliente.Razonsocial)) return cliente.Razonsocial.Trim();
        var full = $"{cliente.Nombres} {cliente.Apellidos}".Trim();
        return string.IsNullOrWhiteSpace(full) ? "Integrity Solutions" : full;
    }

    private static string ObtenerNombreLider(TblAdministracionLider? lider)
    {
        if (lider is null)
            return "Ing. Carlos Méndez";

        var persona = lider.IdpersonaNavigation;
        if (persona is not null)
        {
            var nombre = $"{persona.Nombres} {persona.Apellidos}".Trim();
            if (!string.IsNullOrWhiteSpace(nombre))
                return nombre;
        }

        return "Jefe Inmediato ISC";
    }

    private static bool EsLiderExterno(TblAdministracionLider? lider)
    {
        if (lider is null) return false;
        var codigo = lider.IdtipoNavigation?.Codigovalor?.Trim().ToUpperInvariant();
        return codigo == "EXT" || (lider.IdtipoNavigation?.Valor?.Contains("Extern", StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private static string? CalcularAlertaTraslape(TblTimeReportPermiso actual, IReadOnlyList<TblTimeReportPermiso> todos)
    {
        if (!actual.Idproyecto.HasValue) return null;

        var coincidente = todos.FirstOrDefault(otro =>
            otro.Id != actual.Id &&
            otro.Idempleado != actual.Idempleado &&
            otro.Idproyecto == actual.Idproyecto &&
            otro.IdestadoaprobacionNavigation.Codigovalor != "REC" &&
            otro.Fechainicio <= actual.Fechafin &&
            otro.Fechafin >= actual.Fechainicio);

        if (coincidente is null) return null;

        var nombreOtro = $"{coincidente.IdempleadoNavigation.IdpersonaNavigation.Nombres} {coincidente.IdempleadoNavigation.IdpersonaNavigation.Apellidos}".Trim();
        var proyNombre = actual.IdproyectoNavigation?.Nombre ?? "el proyecto";
        return $"Coincide entre {coincidente.Fechainicio:dd/MM} y {coincidente.Fechafin:dd/MM} con permiso de {nombreOtro} en {proyNombre}";
    }

    private static async Task<int> ObtenerIdTipoPermisoAsync(ApplicationDbContext db, string? tipoOficial, CancellationToken ct)
    {
        var codigoValor = (tipoOficial ?? "VACACIONES").Trim().ToUpperInvariant() switch
        {
            "VACACIONES" or "VAC" => "VAC",
            "LICENCIA_ENFERMEDAD" or "ENF" => "ENF",
            "CITAS_MEDICAS" or "CIT" => "CIT",
            "MATRIMONIO" or "MAT" => "MAT",
            "NACIMIENTO_HIJO" or "NAC" => "NAC",
            "MATERNIDAD" or "MTR" => "MTR",
            "FALLECIMIENTO_FAMILIAR" or "FAL" => "FAL",
            "OTROS" or "OTR" => "OTR",
            _ => "VAC"
        };

        var det = await db.TblAdministracionCatalogoDetalles
            .AsNoTracking()
            .Where(d => d.Activo &&
                        d.IdcatalogoNavigation.Codigo == "TPE" &&
                        d.Codigovalor == codigoValor)
            .FirstOrDefaultAsync(ct);

        if (det is not null)
            return det.Id;

        var fallback = await db.TblAdministracionCatalogoDetalles
            .AsNoTracking()
            .Where(d => d.Activo && d.IdcatalogoNavigation.Codigo == "TPE")
            .OrderBy(d => d.Orden)
            .FirstOrDefaultAsync(ct);

        if (fallback is not null)
            return fallback.Id;

        var cualquier = await db.TblAdministracionCatalogoDetalles
            .AsNoTracking()
            .Where(d => d.Activo)
            .OrderBy(d => d.Id)
            .FirstAsync(ct);

        return cualquier.Id;
    }

    private static async Task<int> ObtenerIdEstadoPermisoAsync(ApplicationDbContext db, string codigoEstado, CancellationToken ct)
    {
        var det = await db.TblAdministracionCatalogoDetalles
            .AsNoTracking()
            .Where(d => d.Activo &&
                        d.IdcatalogoNavigation.Codigo == "EPE" &&
                        d.Codigovalor == codigoEstado)
            .FirstOrDefaultAsync(ct);

        if (det is not null)
            return det.Id;

        var fallback = await db.TblAdministracionCatalogoDetalles
            .AsNoTracking()
            .Where(d => d.Activo)
            .OrderBy(d => d.Id)
            .FirstAsync(ct);

        return fallback.Id;
    }

    private static string MapearCodigoATipoOficial(string codigo) =>
        codigo.ToUpperInvariant() switch
        {
            "VAC" => "VACACIONES",
            "ENF" => "LICENCIA_ENFERMEDAD",
            "CIT" => "CITAS_MEDICAS",
            "MAT" => "MATRIMONIO",
            "NAC" => "NACIMIENTO_HIJO",
            "MTR" => "MATERNIDAD",
            "FAL" => "FALLECIMIENTO_FAMILIAR",
            _ => "OTROS"
        };

    private static DateOnly? ParsearFecha(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        var limpio = valor.Trim()[..Math.Min(10, valor.Trim().Length)];
        if (DateOnly.TryParseExact(limpio, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var iso))
            return iso;
        if (DateOnly.TryParseExact(limpio, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dmy))
            return dmy;
        if (DateOnly.TryParse(limpio, CultureInfo.InvariantCulture, DateTimeStyles.None, out var gen))
            return gen;
        return null;
    }

    private static decimal CalcularDiasLaborables(DateOnly inicio, DateOnly fin)
    {
        if (fin < inicio) return 1m;
        var dias = 0;
        var cursor = inicio;
        while (cursor <= fin)
        {
            if (cursor.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
                dias++;
            cursor = cursor.AddDays(1);
        }
        return Math.Max(1, dias);
    }

    private static DateOnly CalcularSiguienteDiaLaborable(DateOnly fin)
    {
        var siguiente = fin.AddDays(1);
        while (siguiente.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            siguiente = siguiente.AddDays(1);
        }
        return siguiente;
    }
}
