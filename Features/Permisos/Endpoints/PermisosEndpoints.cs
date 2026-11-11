using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Npgsql;
using tmr_backend.Features.Notificaciones.Application.Estado;
using tmr_backend.Features.Notificaciones.Domain;
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
    public static void MapPermisosEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/permisos").WithTags("Permisos");
        group.AddEndpointFilter<PermisoNotificacionTransactionFilter>();
        MapEnviosPermisoEndpoints(group);

        // 0. Diagnóstico de conexión a MinIO S3 (restringido a RRHH / Administradores)
        group.MapGet("/storage/estado", async (
            ApplicationDbContext db,
            ICurrentUserService currentUser,
            IPermisosStorageService storageService,
            CancellationToken ct) =>
        {
            var actor = await ResolverActorActualAsync(db, currentUser, ct);
            if (actor is null)
                return Results.Json(ApiResponse<object>.Fail(401, "Sesión no válida.", []), statusCode: 401);

            if (!actor.EsAdmin && !actor.EsRrhh)
                return Results.Json(ApiResponse<object>.Fail(403, "Acceso exclusivo para Administradores o Talento Humano.", []), statusCode: 403);

            var estado = await storageService.VerificarConexionAsync(ct);
            return Results.Ok(ApiResponse<EstadoConexionMinio>.Ok(estado, estado.Mensaje));
        }).RequireAuthorization("PERMISOS_RRHH_READ");

        // 1. Contexto del colaborador autenticado (RF-001)
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

            var rolesUsuario = await db.TblAutenticacionUsuarioRols
                .AsNoTracking()
                .Where(ur => ur.Idusuario == usuario.Id && ur.Activo && ur.IdrolNavigation.Activo)
                .Select(ur => ur.IdrolNavigation.Nombre)
                .ToListAsync(ct);

            TblAdministracionEmpleado? empleado = null;
            if (usuario.Idpersona.HasValue)
            {
                empleado = await db.TblAdministracionEmpleados
                    .AsNoTracking()
                    .DeLaPersona(usuario.Idpersona)
                    .Include(e => e.IdpersonaNavigation)
                    .Include(e => e.IdcargoNavigation)
                        .ThenInclude(c => c!.IddepartamentoNavigation)
                    .Include(e => e.IdtipocontratoNavigation)
                    .FirstOrDefaultAsync(ct);
            }

            if (empleado is null && !string.IsNullOrWhiteSpace(usuario.Email))
            {
                var emailLower = usuario.Email.Trim().ToLowerInvariant();
                empleado = await db.TblAdministracionEmpleados
                    .AsNoTracking()
                    .Where(e => e.Activo && e.Emailcorporativo != null && e.Emailcorporativo.ToLower() == emailLower)
                    .Include(e => e.IdpersonaNavigation)
                    .Include(e => e.IdcargoNavigation)
                        .ThenInclude(c => c!.IddepartamentoNavigation)
                    .Include(e => e.IdtipocontratoNavigation)
                    .FirstOrDefaultAsync(ct);
            }

            var proyectosAsignados = new List<ProyectoAsignadoPermisoDto>();
            var proyectosYaAgregados = new HashSet<int>();
            var esNomina = true;
            var tipoContratoNombre = "";

            // Cargar todas las asignaciones activas de proyectos en una sola consulta limpia (RF-001)
            var todasAsignacionesActivas = await db.TblTimeReportAsignacionProyectos
                .AsNoTracking()
                .Where(a => a.Activo && a.IdproyectoNavigation.Activo)
                .Include(a => a.IdproyectoNavigation)
                    .ThenInclude(p => p.IdclienteNavigation)
                .Include(a => a.IdliderNavigation)
                    .ThenInclude(l => l!.IdpersonaNavigation)
                .Include(a => a.IdliderNavigation)
                    .ThenInclude(l => l!.IdtipoNavigation)
                .ToListAsync(ct);

            // Líder principal por proyecto según convención de ProyectosEndpoints (fila cabecera Idempleado == null o primera con Idlider)
            var liderPrincipalPorProyecto = todasAsignacionesActivas
                .GroupBy(a => a.Idproyecto)
                .ToDictionary(
                    g => g.Key,
                    g => (g.FirstOrDefault(x => x.Idempleado == null && x.Idlider != null && x.IdliderNavigation is not null)
                          ?? g.FirstOrDefault(x => x.Idlider != null && x.IdliderNavigation is not null))?.IdliderNavigation);

            if (empleado is not null)
            {
                (esNomina, tipoContratoNombre) = await ResolverRegimenContratoAsync(db, empleado, ct);

                var misAsignaciones = todasAsignacionesActivas
                    .Where(a => a.Idempleado == empleado.Id && a.Estadoasignacion)
                    .ToList();

                if (misAsignaciones.Count == 0)
                {
                    misAsignaciones = todasAsignacionesActivas
                        .Where(a => a.Idempleado == empleado.Id)
                        .ToList();
                }

                foreach (var a in misAsignaciones)
                {
                    if (proyectosYaAgregados.Contains(a.Idproyecto))
                        continue;

                    var lider = a.IdliderNavigation ?? liderPrincipalPorProyecto.GetValueOrDefault(a.Idproyecto);
                    proyectosAsignados.Add(new ProyectoAsignadoPermisoDto(
                        IdProyecto: a.Idproyecto,
                        ProyectoNombre: a.IdproyectoNavigation.Nombre,
                        IdCliente: a.IdproyectoNavigation.Idcliente,
                        ClienteNombre: ObtenerNombreCliente(a.IdproyectoNavigation.IdclienteNavigation),
                        IdLider: lider?.Id,
                        LiderNombre: ObtenerNombreLider(lider),
                        LiderTipo: EsLiderExterno(lider) ? "EXT" : "INT",
                        LiderCorreo: lider?.IdpersonaNavigation?.Email,
                        IdAsignacionProyecto: a.Id,
                        EsAsignacionPropia: true));
                    proyectosYaAgregados.Add(a.Idproyecto);
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
                .Where(l => !string.IsNullOrWhiteSpace(l.Nombre))
                .OrderBy(l => l.Nombre)
                .ToList();

            var tiposPermisoCatalogo = await db.TblTimeReportTiposPermiso
                .AsNoTracking()
                .Where(t => t.Activo)
                .OrderBy(t => t.Orden)
                .Select(t => new TipoPermisoCatalogoDto(
                    t.Id,
                    t.Codigo,
                    t.CodigoCorto,
                    t.Nombre,
                    t.RequiereAdjunto,
                    t.Orden))
                .ToListAsync(ct);

            List<TblTimeReportSolicitud> misSolicitudes = [];
            if (empleado is not null)
            {
                misSolicitudes = await db.TblTimeReportSolicitudes
                    .AsNoTracking()
                    .Where(s => s.IdEmpleado == empleado.Id && s.Activo)
                    .Include(s => s.Estado)
                    .Include(s => s.TipoPermiso)
                    .ToListAsync(ct);
            }

            var diasDisponibles = CalcularDiasVacacionesDisponibles(empleado, misSolicitudes);
            var enProceso = misSolicitudes.Count(s => s.Estado.Semaforo == "AMARILLO");
            var aprobados = misSolicitudes.Count(s => s.Estado.Codigo == "APROBADA");
            var horasAcumuladas = misSolicitudes
                .Where(s => s.Estado.Codigo != "RECHAZADA"
                            && s.Estado.Codigo != "OBSERVADA"
                            && s.Horario is "MANANA" or "TARDE" or "OTROS")
                .Sum(s => s.HorasSolicitadas ?? (s.DiasSolicitados * 8m));

            var persona = empleado?.IdpersonaNavigation ?? usuario.IdpersonaNavigation;
            var apellidosNombres = persona is not null
                ? $"{persona.Apellidos} {persona.Nombres}".Trim()
                : "";
            if (string.IsNullOrWhiteSpace(apellidosNombres))
                apellidosNombres = usuario.Email;

            var cargoReal = empleado?.IdcargoNavigation?.Nombrecargo
                            ?? rolesUsuario.FirstOrDefault()
                            ?? "";

            var dto = new ContextoColaboradorPermisoDto(
                IdEmpleado: empleado?.Id ?? 0,
                CodigoEmpleado: empleado?.Codigoempleado ?? "",
                ApellidosNombres: apellidosNombres,
                NumeroCedula: persona?.Numeroidentificacion ?? "",
                Cargo: cargoReal,
                Area: empleado?.IdcargoNavigation?.IddepartamentoNavigation?.Valor,
                Correo: empleado?.Emailcorporativo ?? usuario.Email,
                DiasDisponibles: diasDisponibles,
                EnProceso: enProceso,
                Aprobados: aprobados,
                HorasAcumuladas: horasAcumuladas,
                ProyectosAsignados: proyectosAsignados,
                LideresDisponibles: lideresDisponibles,
                EsNomina: esNomina,
                TipoContrato: tipoContratoNombre,
                TiposPermiso: tiposPermisoCatalogo,
                RrhhDisponibles: await ObtenerCatalogoUsuariosRrhhAsync(db, ct));

            return Results.Ok(ApiResponse<ContextoColaboradorPermisoDto>.Ok(dto));
        }).RequireAuthorization("MIS_PERMISOS_READ");

        // 2. Listar Mis Solicitudes (Colaborador actual)
        group.MapGet("/mis-permisos", async (
            ApplicationDbContext db,
            ICurrentUserService currentUser,
            CancellationToken ct) =>
        {
            if (currentUser.UserId <= 0)
                return Results.Json(ApiResponse<object>.Fail(401, "Sesión no válida.", []), statusCode: 401);

            var actor = await ResolverActorActualAsync(db, currentUser, ct);
            if (actor?.IdEmpleado is null)
                return Results.Ok(ApiResponse<IReadOnlyList<PermisoItemDto>>.Ok([]));

            var items = await ConsultarSolicitudesBase(db)
                .Where(s => s.IdEmpleado == actor.IdEmpleado.Value && s.Activo)
                .OrderByDescending(s => s.FechaCreacion)
                .ToListAsync(ct);

            var dtos = items.Select(s => MapearSolicitudDto(s, null)).ToList();
            return Results.Ok(ApiResponse<IReadOnlyList<PermisoItemDto>>.Ok(dtos));
        }).RequireAuthorization("MIS_PERMISOS_READ");

        // 3. Crear nueva solicitud sin adjunto (JSON)
        group.MapPost("", async (
            [FromBody] CrearPermisoRequest request,
            HttpContext httpContext,
            ApplicationDbContext db,
            ICurrentUserService currentUser,
            INotificacionInternaNotifier sseNotifier,
            IPermisosNotificacionService notificacionService,
            CancellationToken ct) =>
        {
            return await CrearSolicitudInternaAsync(
                request,
                [],
                httpContext,
                db,
                currentUser,
                null,
                sseNotifier,
                notificacionService,
                ct);
        }).RequireAuthorization("MIS_PERMISOS_CREATE");

        // 4. Crear nueva solicitud con uno o más archivos adjuntos (multipart/form-data -> MinIO)
        group.MapPost("/con-evidencia", async (
            HttpContext httpContext,
            ApplicationDbContext db,
            ICurrentUserService currentUser,
            IPermisosStorageService storageService,
            INotificacionInternaNotifier sseNotifier,
            IPermisosNotificacionService notificacionService,
            CancellationToken ct) =>
        {
            var httpRequest = httpContext.Request;
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
                Motivo: form["motivo"].FirstOrDefault() ?? "",
                IdUsuarioRrhh: ParseInt("idUsuarioRrhh"));

            var archivos = form.Files.Where(f => f.Length > 0).ToList();
            return await CrearSolicitudInternaAsync(
                req,
                archivos,
                httpContext,
                db,
                currentUser,
                storageService,
                sseNotifier,
                notificacionService,
                ct);
        })
        .DisableAntiforgery()
        .RequireAuthorization("MIS_PERMISOS_CREATE");

        // 5. Subir o agregar evidencia a una solicitud existente (Anti-IDOR + MinIO + limpieza si falla BD)
        group.MapPost("/{id:int}/evidencia", async (
            int id,
            IFormFile archivo,
            ApplicationDbContext db,
            ICurrentUserService currentUser,
            IPermisosStorageService storageService,
            CancellationToken ct) =>
        {
            var actor = await ResolverActorActualAsync(db, currentUser, ct);
            if (actor is null)
                return Results.Json(ApiResponse<object>.Fail(401, "Sesión no válida.", []), statusCode: 401);

            var solicitud = await db.TblTimeReportSolicitudes
                .Include(s => s.Estado)
                .FirstOrDefaultAsync(s => s.Id == id && s.Activo, ct);

            if (solicitud is null)
                return Results.NotFound(ApiResponse<object>.Fail(404, "Solicitud no encontrada.", []));

            var esDueno = actor.IdEmpleado.HasValue && solicitud.IdEmpleado == actor.IdEmpleado.Value;
            if (!esDueno && !actor.EsRrhh && !actor.EsAdmin)
                return Results.Json(ApiResponse<object>.Fail(403, "No tienes permiso para adjuntar archivos a esta solicitud.", []), statusCode: 403);

            ArchivoAdjuntoSubido? subido = null;
            try
            {
                var validado = await storageService.ValidarYLeerArchivoAsync(archivo, ct);
                subido = await storageService.SubirAdjuntoValidadoAsync(validado, solicitud.Id, solicitud.FechaInicio.Year, ct);

                db.TblTimeReportSolicitudAdjuntos.Add(new TblTimeReportSolicitudAdjunto
                {
                    IdSolicitud = solicitud.Id,
                    NombreOriginal = subido.NombreOriginal,
                    BucketName = subido.BucketName,
                    ObjectKey = subido.ObjectKey,
                    ContentType = subido.ContentType,
                    TamanoBytes = subido.TamanoBytes,
                    Sha256 = subido.Sha256,
                    SubidoPorUsuarioId = actor.IdUsuario,
                    Activo = true,
                    FechaCreacion = DateTime.UtcNow
                });

                solicitud.FechaModificacion = DateTime.UtcNow;
                solicitud.UsuarioModificacion = actor.Email;
                await db.SaveChangesAsync(ct);

                var recargada = await ConsultarSolicitudesBase(db).FirstAsync(s => s.Id == id, ct);
                return Results.Ok(ApiResponse<PermisoItemDto>.Ok(MapearSolicitudDto(recargada, null), "Archivo adjunto guardado correctamente."));
            }
            catch (InvalidOperationException ex)
            {
                if (subido is not null)
                    await storageService.EliminarAdjuntoAsync(subido.BucketName, subido.ObjectKey, CancellationToken.None);
                RegistrarErrorInfraestructura(db, ex, $"Validación o subida rechazada para usuario {currentUser.UserId}");
                return Results.BadRequest(ApiResponse<object>.Fail(400, TraducirErrorInfraestructuraAmigable(ex, tieneArchivo: true), []));
            }
            catch (Exception ex)
            {
                if (subido is not null)
                    await storageService.EliminarAdjuntoAsync(subido.BucketName, subido.ObjectKey, CancellationToken.None);
                RegistrarErrorInfraestructura(db, ex, $"Error al adjuntar archivo en solicitud {id}");
                return Results.BadRequest(ApiResponse<object>.Fail(400, TraducirErrorInfraestructuraAmigable(ex, tieneArchivo: true), []));
            }
        })
        .DisableAntiforgery()
        .RequireAuthorization("MIS_PERMISOS_CREATE");

        // 6. Descargar evidencia adjunta desde MinIO (RNF-005 Anti-IDOR)
        group.MapGet("/{id:int}/evidencia", async (
            int id,
            [FromQuery] int? adjuntoId,
            ApplicationDbContext db,
            ICurrentUserService currentUser,
            IPermisosStorageService storageService,
            CancellationToken ct) =>
        {
            var actor = await ResolverActorActualAsync(db, currentUser, ct);
            if (actor is null)
                return Results.Json(ApiResponse<object>.Fail(401, "Sesión no válida.", []), statusCode: 401);

            var solicitud = await db.TblTimeReportSolicitudes
                .AsNoTracking()
                .Include(s => s.Adjuntos).Include(s => s.Lider).ThenInclude(l => l!.IdtipoNavigation)
                .FirstOrDefaultAsync(s => s.Id == id && s.Activo, ct);

            if (solicitud is null)
                return Results.NotFound(ApiResponse<object>.Fail(404, "Solicitud no encontrada.", []));

            if (!PuedeConsultarSolicitud(actor, solicitud))
                return Results.Json(ApiResponse<object>.Fail(403, "No tienes autorización para ver los adjuntos de esta solicitud.", []), statusCode: 403);

            var adjunto = adjuntoId.HasValue
                ? solicitud.Adjuntos.FirstOrDefault(a => a.Id == adjuntoId.Value && a.Activo)
                : solicitud.Adjuntos.Where(a => a.Activo).OrderByDescending(a => a.FechaCreacion).FirstOrDefault();

            if (adjunto is null)
                return Results.NotFound(ApiResponse<object>.Fail(404, "Esta solicitud no tiene archivo de evidencia adjunto.", []));

            var descarga = await storageService.ObtenerAdjuntoAsync(
                adjunto.BucketName,
                adjunto.ObjectKey,
                adjunto.NombreOriginal,
                adjunto.ContentType,
                ct);

            if (descarga is null)
                return Results.NotFound(ApiResponse<object>.Fail(404, "El archivo adjunto no se encuentra disponible en este momento.", []));

            return Results.File(descarga.Contenido, descarga.MimeType, descarga.NombreArchivo);
        }).RequireAuthorization("PERMISOS_VIEW");

        // 7. Descargar PDF oficial de una solicitud (RNF-005 Anti-IDOR)
        group.MapGet("/{id:int}/pdf", async (
            int id,
            ApplicationDbContext db,
            ICurrentUserService currentUser,
            IPermisosPdfService pdfService,
            CancellationToken ct) =>
        {
            var actor = await ResolverActorActualAsync(db, currentUser, ct);
            if (actor is null)
                return Results.Json(ApiResponse<object>.Fail(401, "Sesión no válida.", []), statusCode: 401);

            var solicitud = await ConsultarSolicitudesBase(db)
                .FirstOrDefaultAsync(s => s.Id == id && s.Activo, ct);

            if (solicitud is null)
                return Results.NotFound(ApiResponse<object>.Fail(404, "Solicitud no encontrada.", []));

            if (!PuedeConsultarSolicitud(actor, solicitud))
                return Results.Json(ApiResponse<object>.Fail(403, "No tienes autorización para descargar este formulario.", []), statusCode: 403);

            var dto = MapearSolicitudDto(solicitud, null);
            var pdfBytes = pdfService.GenerarFormularioOficialPdf(dto);
            return Results.File(pdfBytes, "application/pdf", $"Formulario_Permiso_{dto.FolioOrCodigo()}.pdf");
        }).RequireAuthorization("PERMISOS_VIEW");

        // 8. Bandeja de Aprobación Líder (Paso 2)
        group.MapGet("/equipo", async (
            ApplicationDbContext db,
            ICurrentUserService currentUser,
            CancellationToken ct) =>
        {
            var actor = await ResolverActorActualAsync(db, currentUser, ct);
            if (actor is null)
                return Results.Json(ApiResponse<object>.Fail(401, "Sesión no válida.", []), statusCode: 401);

            if (!actor.EsAdmin && !actor.EsLider)
                return Results.Json(ApiResponse<object>.Fail(403, "Acceso exclusivo para líderes o administradores.", []), statusCode: 403);

            var query = ConsultarSolicitudesBase(db).Where(s => s.Activo);

            if (!actor.EsAdmin)
            {
                if (!actor.IdLider.HasValue)
                    return Results.Ok(ApiResponse<IReadOnlyList<PermisoItemDto>>.Ok([]));

                query = query.Where(s => s.IdLider == actor.IdLider.Value);
            }

            var lista = await query
                .OrderByDescending(s => s.FechaCreacion)
                .ToListAsync(ct);

            var dtos = lista.Select(s => MapearSolicitudDto(s, CalcularAlertaTraslape(s, lista))).ToList();
            return Results.Ok(ApiResponse<IReadOnlyList<PermisoItemDto>>.Ok(dtos));
        }).RequireAuthorization("PERMISOS_LIDER_READ");

        // 9. Decisión del Jefe Inmediato / Líder (Paso 2: aprobar | observar | rechazar)
        group.MapPost("/{id:int}/decision-lider", async (
            int id,
            [FromBody] DecisionLiderRequest request,
            HttpContext httpContext,
            ApplicationDbContext db,
            ICurrentUserService currentUser,
            INotificacionInternaNotifier sseNotifier,
            IPermisosNotificacionService notificacionService,
            CancellationToken ct) =>
        {
            var actor = await ResolverActorActualAsync(db, currentUser, ct);
            if (actor is null)
                return Results.Json(ApiResponse<object>.Fail(401, "Sesión no válida.", []), statusCode: 401);

            if (!actor.EsAdmin && !actor.EsLider)
                return Results.Json(ApiResponse<object>.Fail(403, "No tienes permisos para aprobar solicitudes de equipo.", []), statusCode: 403);

            var solicitud = await db.TblTimeReportSolicitudes
                .Include(s => s.Estado)
                .FirstOrDefaultAsync(s => s.Id == id && s.Activo, ct);

            if (solicitud is null)
                return Results.NotFound(ApiResponse<object>.Fail(404, "Solicitud no encontrada.", []));

            if (!actor.EsAdmin && (!actor.IdLider.HasValue || solicitud.IdLider != actor.IdLider.Value))
                return Results.Json(ApiResponse<object>.Fail(403, "Esta solicitud no pertenece a tu bandeja de líder.", []), statusCode: 403);

            // Nadie aprueba su propia solicitud, ni siquiera un administrador
            if (actor.IdEmpleado.HasValue && solicitud.IdEmpleado == actor.IdEmpleado.Value)
                return Results.Json(ApiResponse<object>.Fail(403, "No puedes decidir sobre tu propia solicitud.", []), statusCode: 403);

            // Máquina de estados estricta: el Jefe Inmediato solo decide cuando está en PENDIENTE_JEFE
            if (solicitud.Estado.Codigo != "PENDIENTE_JEFE")
            {
                return Results.BadRequest(ApiResponse<object>.Fail(
                    400,
                    $"No se puede registrar la decisión del Jefe Inmediato porque la solicitud se encuentra en estado '{solicitud.Estado.Nombre}'.",
                    []));
            }

            var accion = (request.Accion ?? "aprobar").Trim().ToLowerInvariant();
            if (accion is not ("aprobar" or "observar" or "rechazar"))
                return Results.BadRequest(ApiResponse<object>.Fail(400, "Acción inválida. Use 'aprobar', 'observar' o 'rechazar'.", []));

            var comentario = request.Comentario?.Replace("\0", "").Trim(); if (comentario?.Length > 2000) return Results.BadRequest(ApiResponse<object>.Fail(400, "El comentario no puede superar los 2000 caracteres.", []));
            // RF-007: Comentario obligatorio en caso de rechazo u observación
            if (accion != "aprobar" && string.IsNullOrWhiteSpace(comentario))
                return Results.BadRequest(ApiResponse<object>.Fail(400, "Por favor ingresa el motivo en los comentarios para continuar.", []));

            var (codigoEstadoDestino, decisionTabla) = accion switch
            {
                "aprobar" => ("PENDIENTE_TH", "APROBADO"),
                "observar" => ("OBSERVADA", "OBSERVADO"),
                _ => ("RECHAZADA", "RECHAZADO")
            };

            var idEstadoDestino = await ObtenerIdEstadoSolicitudAsync(db, codigoEstadoDestino, ct);
            solicitud.IdEstado = idEstadoDestino;
            solicitud.UsuarioModificacion = actor.Email;
            solicitud.FechaModificacion = DateTime.UtcNow;
            solicitud.IpModificacion = ObtenerIpCliente(httpContext);

            db.TblTimeReportSolicitudRevisiones.Add(new TblTimeReportSolicitudRevision
            {
                IdSolicitud = solicitud.Id,
                EtapaRevision = "JEFE_INMEDIATO",
                IdUsuarioRevisor = actor.IdUsuario,
                MetodoRevision = "CUENTA_TMR",
                Decision = decisionTabla,
                FechaRevision = DateTime.UtcNow,
                Comentario = string.IsNullOrWhiteSpace(comentario) ? null : comentario
            });

            await db.SaveChangesAsync(ct);

            var recargada = await ConsultarSolicitudesBase(db).FirstAsync(s => s.Id == id, ct);
            var dto = MapearSolicitudDto(recargada, null);

            var campanaOk = await EmitirNotificacionesInternasPaso2SeguraAsync(db, sseNotifier, recargada, dto, decisionTabla, ct);

            var correoColab = recargada.Empleado.Emailcorporativo ?? recargada.Empleado.IdpersonaNavigation.Email;
            var evento = accion switch
            {
                "aprobar" => EventoNotificacionPermiso.AprobadaPorLider,
                "observar" => EventoNotificacionPermiso.ObservadaPorLider,
                _ => EventoNotificacionPermiso.RechazadaPorLider
            };

            var correoOk = await NotificarCorreoSeguroAsync(db, notificacionService, dto, correoColab, evento, ct);

            return ResponderPostCommit(dto, "Tu decisión fue registrada correctamente.", campanaOk && correoOk);
        }).RequireAuthorization("PERMISOS_LIDER_APPROVE");

        // 10. Bandeja de Validación Talento Humano (Paso 3)
        group.MapGet("/rrhh", async (
            ApplicationDbContext db,
            ICurrentUserService currentUser,
            CancellationToken ct) =>
        {
            var actor = await ResolverActorActualAsync(db, currentUser, ct);
            if (actor is null)
                return Results.Json(ApiResponse<object>.Fail(401, "Sesión no válida.", []), statusCode: 401);

            if (!actor.EsAdmin && !actor.EsRrhh)
                return Results.Json(ApiResponse<object>.Fail(403, "Acceso exclusivo para Talento Humano (RRHH).", []), statusCode: 403);

            var lista = await ConsultarSolicitudesBase(db)
                .Where(s => s.Activo)
                .OrderByDescending(s => s.FechaCreacion)
                .ToListAsync(ct);

            var dtos = lista.Select(s => MapearSolicitudDto(s, null)).ToList();
            return Results.Ok(ApiResponse<IReadOnlyList<PermisoItemDto>>.Ok(dtos));
        }).RequireAuthorization("PERMISOS_RRHH_READ");

        // 11. Decisión de Talento Humano RRHH (Paso 3: confirmar | devolver)
        group.MapPost("/{id:int}/decision-rrhh", async (
            int id,
            [FromBody] DecisionRrhhRequest request,
            HttpContext httpContext,
            ApplicationDbContext db,
            ICurrentUserService currentUser,
            INotificacionInternaNotifier sseNotifier,
            IPermisosNotificacionService notificacionService,
            CancellationToken ct) =>
        {
            var actor = await ResolverActorActualAsync(db, currentUser, ct);
            if (actor is null)
                return Results.Json(ApiResponse<object>.Fail(401, "Sesión no válida.", []), statusCode: 401);

            if (!actor.EsAdmin && !actor.EsRrhh)
                return Results.Json(ApiResponse<object>.Fail(403, "No tienes autorización para confirmar en Talento Humano.", []), statusCode: 403);

            var solicitud = await db.TblTimeReportSolicitudes
                .Include(s => s.Estado)
                .Include(s => s.Revisiones)
                .FirstOrDefaultAsync(s => s.Id == id && s.Activo, ct);

            if (solicitud is null)
                return Results.NotFound(ApiResponse<object>.Fail(404, "Solicitud no encontrada.", []));

            // Nadie confirma su propia solicitud, ni siquiera un administrador
            if (actor.IdEmpleado.HasValue && solicitud.IdEmpleado == actor.IdEmpleado.Value)
                return Results.Json(ApiResponse<object>.Fail(403, "No puedes decidir sobre tu propia solicitud.", []), statusCode: 403);

            var accion = (request.Accion ?? "confirmar").Trim().ToLowerInvariant();
            if (accion is not ("confirmar" or "devolver"))
                return Results.BadRequest(ApiResponse<object>.Fail(400, "Acción inválida. Use 'confirmar' o 'devolver'.", []));

            var confirmar = accion == "confirmar";
            var observacion = request.Observacion?.Replace("\0", "").Trim(); if (observacion?.Length > 2000) return Results.BadRequest(ApiResponse<object>.Fail(400, "La observación no puede superar los 2000 caracteres.", []));

            if (!confirmar && string.IsNullOrWhiteSpace(observacion))
                return Results.BadRequest(ApiResponse<object>.Fail(400, "Por favor escribe la observación antes de devolver la solicitud.", []));

            // Máquina de estados estricta: RRHH solo decide sobre solicitudes en PENDIENTE_TH
            // (o PENDIENTE_JEFE únicamente cuando la solicitud no tiene líder asignado).
            if (solicitud.Estado.Codigo is "APROBADA" or "RECHAZADA" or "OBSERVADA")
            {
                return Results.BadRequest(ApiResponse<object>.Fail(
                    400,
                    $"La solicitud ya fue procesada en estado '{solicitud.Estado.Nombre}' y no admite una nueva decisión.",
                    []));
            }

            if (solicitud.Estado.Codigo != "PENDIENTE_TH")
            {
                if (solicitud.IdLider.HasValue)
                {
                    return Results.BadRequest(ApiResponse<object>.Fail(
                        400,
                        "La solicitud aún requiere la aprobación previa del Jefe Inmediato (Paso 2).",
                        []));
                }
            }

            var codigoEstadoDestino = confirmar ? "APROBADA" : "OBSERVADA";
            solicitud.IdEstado = await ObtenerIdEstadoSolicitudAsync(db, codigoEstadoDestino, ct);

            if (!string.IsNullOrWhiteSpace(request.ImpactoTimeReport))
            {
                var impacto = request.ImpactoTimeReport.Trim().ToUpperInvariant();
                if (impacto is "RESTA_META" or "SUMA_REPORTADAS" or "SIN_IMPACTO")
                    solicitud.ImpactoTimeReport = impacto;
            }

            solicitud.UsuarioModificacion = actor.Email;
            solicitud.FechaModificacion = DateTime.UtcNow;
            solicitud.IpModificacion = ObtenerIpCliente(httpContext);

            db.TblTimeReportSolicitudRevisiones.Add(new TblTimeReportSolicitudRevision
            {
                IdSolicitud = solicitud.Id,
                EtapaRevision = "TALENTO_HUMANO",
                IdUsuarioRevisor = actor.IdUsuario,
                MetodoRevision = "CUENTA_TMR",
                Decision = confirmar ? "APROBADO" : "OBSERVADO",
                FechaRevision = DateTime.UtcNow,
                Comentario = string.IsNullOrWhiteSpace(observacion) ? null : observacion
            });

            await db.SaveChangesAsync(ct);

            var recargada = await ConsultarSolicitudesBase(db).FirstAsync(s => s.Id == id, ct);
            var dto = MapearSolicitudDto(recargada, null);

            var campanaOk = await EmitirNotificacionesInternasPaso3SeguraAsync(db, sseNotifier, recargada, dto, confirmar, ct);

            var correoColab = recargada.Empleado.Emailcorporativo ?? recargada.Empleado.IdpersonaNavigation.Email;
            var correoOk = await NotificarCorreoSeguroAsync(db, notificacionService,
                dto,
                correoColab,
                confirmar ? EventoNotificacionPermiso.ConfirmadaPorRrhh : EventoNotificacionPermiso.DevueltaPorRrhh,
                ct);

            return ResponderPostCommit(
                dto,
                confirmar
                    ? "Solicitud confirmada definitivamente por Talento Humano."
                    : "Solicitud devuelta con observaciones.", campanaOk && correoOk);
        }).RequireAuthorization("PERMISOS_RRHH_CONFIRM");

        // 12. Reenviar enlace externo Magic Link (regenera vigencia y limpia uso previo)
        group.MapPost("/{id:int}/reenviar-enlace", async (
            int id,
            ApplicationDbContext db,
            ICurrentUserService currentUser,
            IPermisosNotificacionService notificacionService,
            CancellationToken ct) =>
        {
            var actor = await ResolverActorActualAsync(db, currentUser, ct);
            if (actor is null || (!actor.EsAdmin && !actor.EsRrhh))
                return Results.Json(ApiResponse<object>.Fail(403, "No autorizado.", []), statusCode: 403);

            var solicitud = await db.TblTimeReportSolicitudes
                .Include(s => s.Estado)
                .Include(s => s.Lider)
                    .ThenInclude(l => l!.IdtipoNavigation)
                .FirstOrDefaultAsync(s => s.Id == id && s.Activo, ct);

            if (solicitud is null)
                return Results.NotFound(ApiResponse<object>.Fail(404, "Solicitud no encontrada.", []));

            if (!EsLiderExterno(solicitud.Lider))
                return Results.BadRequest(ApiResponse<object>.Fail(400, "El Jefe Inmediato de esta solicitud es interno; no usa enlace externo.", []));

            if (solicitud.Estado.Codigo != "PENDIENTE_JEFE")
                return Results.BadRequest(ApiResponse<object>.Fail(400, "La solicitud ya no está pendiente del Jefe Inmediato.", []));

            solicitud.TokenExterno = Guid.NewGuid().ToString("N");
            solicitud.TokenExternoExpira = DateTime.UtcNow.AddHours(72);
            solicitud.TokenExternoUsadoEn = null;

            await db.SaveChangesAsync(ct);

            var recargada = await ConsultarSolicitudesBase(db).FirstAsync(s => s.Id == id, ct);
            var dtoCorreo = MapearSolicitudDto(recargada, null) with { TokenExterno = recargada.TokenExterno };
            var correoColab = recargada.Empleado.Emailcorporativo ?? recargada.Empleado.IdpersonaNavigation.Email;

            var correoOk = await NotificarCorreoSeguroAsync(db, notificacionService,
                dtoCorreo,
                correoColab,
                EventoNotificacionPermiso.ReenvioMagicLink,
                ct);

            return ResponderPostCommit(
                dtoCorreo with { TokenExterno = null },
                "Se envió un nuevo enlace de revisión al correo del jefe inmediato.", correoOk);
        }).RequireAuthorization("PERMISOS_RRHH_CONFIRM");

        // 13. Historial General (Gerencia / RRHH / Admin)
        group.MapGet("/gerencia", async (
            ApplicationDbContext db,
            ICurrentUserService currentUser,
            CancellationToken ct) =>
        {
            var actor = await ResolverActorActualAsync(db, currentUser, ct);
            if (actor is null)
                return Results.Json(ApiResponse<object>.Fail(401, "Sesión no válida.", []), statusCode: 401);

            if (!actor.EsAdmin && !actor.EsGerente && !actor.EsRrhh)
                return Results.Json(ApiResponse<object>.Fail(403, "Acceso exclusivo para Gerencia, Talento Humano o Administradores.", []), statusCode: 403);

            var lista = await ConsultarSolicitudesBase(db)
                .Where(s => s.Activo)
                .OrderByDescending(s => s.FechaInicio)
                .ThenByDescending(s => s.FechaCreacion)
                .ToListAsync(ct);

            var dtos = lista.Select(s => MapearSolicitudDto(s, null)).ToList();
            return Results.Ok(ApiResponse<IReadOnlyList<PermisoItemDto>>.Ok(dtos));
        }).RequireAuthorization("PERMISOS_GERENCIA_READ");

        // 14. Portal externo para Líderes de Clientes vía Magic Link (con expiración y un solo uso)
        group.MapGet("/externo/{token}", async (
            string token,
            ApplicationDbContext db,
            CancellationToken ct) =>
        {
            if (!EsTokenExternoValido(token)) return Results.NotFound(ApiResponse<object>.Fail(404, "El enlace de revisión no existe o ha expirado.", []));
            var solicitud = await ConsultarSolicitudesBase(db)
                .FirstOrDefaultAsync(s => s.TokenExterno == token && s.Activo, ct);

            if (!EsTokenExternoValido(token) || solicitud is null || !EsLiderExterno(solicitud.Lider) || (solicitud.TokenExternoExpira.HasValue && solicitud.TokenExternoExpira.Value < DateTime.UtcNow))
                return Results.NotFound(ApiResponse<object>.Fail(404, "El enlace de revisión no existe o ha expirado.", []));

            return Results.Ok(ApiResponse<PermisoItemDto>.Ok(MapearSolicitudDto(solicitud, null)));
        }).AllowAnonymous().RequireRateLimiting("magic-link");

        group.MapPost("/externo/{token}/decision", async (
            string token,
            [FromBody] DecisionExternaRequest request, HttpContext httpContext,
            ApplicationDbContext db,
            INotificacionInternaNotifier sseNotifier,
            IPermisosNotificacionService notificacionService,
            CancellationToken ct) =>
        {
            var solicitud = await db.TblTimeReportSolicitudes
                .Include(s => s.Estado)
                .Include(s => s.Lider)
                    .ThenInclude(l => l!.IdtipoNavigation)
                .FirstOrDefaultAsync(s => s.TokenExterno == token && s.Activo, ct);

            if (!EsTokenExternoValido(token) || solicitud is null || !EsLiderExterno(solicitud.Lider) || (solicitud.TokenExternoExpira.HasValue && solicitud.TokenExternoExpira.Value < DateTime.UtcNow))
                return Results.NotFound(ApiResponse<object>.Fail(404, "El enlace de revisión no existe o ha expirado.", []));

            if (solicitud.TokenExternoUsadoEn.HasValue)
                return Results.BadRequest(ApiResponse<object>.Fail(400, "Este enlace de aprobación ya fue utilizado anteriormente.", []));

            // El magic link es solo para jefes de cliente externo; los internos deciden desde su cuenta TMR
            if (!EsLiderExterno(solicitud.Lider))
                return Results.BadRequest(ApiResponse<object>.Fail(400, "Esta solicitud debe ser revisada por su Jefe Inmediato desde TMR.", []));

            if (solicitud.Estado.Codigo != "PENDIENTE_JEFE")
                return Results.BadRequest(ApiResponse<object>.Fail(400, $"La solicitud ya no está pendiente del Jefe Inmediato (estado actual: '{solicitud.Estado.Nombre}').", []));

            var accion = (request.Accion ?? "aprobar").Trim().ToLowerInvariant();
            if (accion is not ("aprobar" or "observar" or "rechazar"))
                return Results.BadRequest(ApiResponse<object>.Fail(400, "Acción inválida. Use 'aprobar', 'observar' o 'rechazar'.", []));

            var aprobar = accion == "aprobar";
            var comentario = request.Comentario?.Replace("\0", "").Trim(); if (comentario?.Length > 2000) return Results.BadRequest(ApiResponse<object>.Fail(400, "El comentario no puede superar los 2000 caracteres.", []));
            if (!aprobar && string.IsNullOrWhiteSpace(comentario))
                return Results.BadRequest(ApiResponse<object>.Fail(400, "Por favor indica el motivo en los comentarios para continuar.", []));

            var codigoEstadoDestino = accion switch { "aprobar" => "PENDIENTE_TH", "observar" => "OBSERVADA", _ => "RECHAZADA" }; var decisionExterna = accion switch { "aprobar" => "APROBADO", "observar" => "OBSERVADO", _ => "RECHAZADO" };
            solicitud.IdEstado = await ObtenerIdEstadoSolicitudAsync(db, codigoEstadoDestino, ct);
            solicitud.TokenExternoUsadoEn = DateTime.UtcNow; solicitud.UsuarioModificacion = "MAGIC_LINK_EXTERNO"; solicitud.IpModificacion = ObtenerIpCliente(httpContext);
            solicitud.FechaModificacion = DateTime.UtcNow;

            db.TblTimeReportSolicitudRevisiones.Add(new TblTimeReportSolicitudRevision
            {
                IdSolicitud = solicitud.Id,
                EtapaRevision = "JEFE_INMEDIATO",
                IdUsuarioRevisor = null,
                MetodoRevision = "MAGIC_LINK_EXTERNO",
                Decision = decisionExterna,
                FechaRevision = DateTime.UtcNow,
                Comentario = string.IsNullOrWhiteSpace(comentario) ? null : comentario
            });

            await db.SaveChangesAsync(ct);

            var recargada = await ConsultarSolicitudesBase(db).FirstAsync(s => s.Id == solicitud.Id, ct);
            var dto = MapearSolicitudDto(recargada, null);

            var campanaOk = await EmitirNotificacionesInternasPaso2SeguraAsync(db, sseNotifier, recargada, dto, decisionExterna, ct);

            var correoColab = recargada.Empleado.Emailcorporativo ?? recargada.Empleado.IdpersonaNavigation.Email;
            var correoOk = await NotificarCorreoSeguroAsync(db, notificacionService,
                dto,
                correoColab,
                accion switch { "aprobar" => EventoNotificacionPermiso.AprobadaPorLider, "observar" => EventoNotificacionPermiso.ObservadaPorLider, _ => EventoNotificacionPermiso.RechazadaPorLider },
                ct);

            return ResponderPostCommit(
                dto,
                aprobar
                    ? "¡Gracias! Tu aprobación quedó registrada correctamente."
                    : "Tu decisión quedó registrada correctamente.", campanaOk && correoOk);
        }).AllowAnonymous().RequireRateLimiting("magic-link");

        group.MapGet("/externo/{token}/pdf", async (
            string token,
            ApplicationDbContext db,
            IPermisosPdfService pdfService,
            CancellationToken ct) =>
        {
            if (!EsTokenExternoValido(token)) return Results.NotFound(ApiResponse<object>.Fail(404, "El enlace de revisión no existe o ha expirado.", []));
            var solicitud = await ConsultarSolicitudesBase(db)
                .FirstOrDefaultAsync(s => s.TokenExterno == token && s.Activo, ct);

            if (!EsTokenExternoValido(token) || solicitud is null || !EsLiderExterno(solicitud.Lider) || (solicitud.TokenExternoExpira.HasValue && solicitud.TokenExternoExpira.Value < DateTime.UtcNow))
                return Results.NotFound(ApiResponse<object>.Fail(404, "El enlace de revisión no existe o ha expirado.", []));

            var dto = MapearSolicitudDto(solicitud, null);
            var pdfBytes = pdfService.GenerarFormularioOficialPdf(dto);
            return Results.File(pdfBytes, "application/pdf", $"Formulario_Permiso_{dto.FolioOrCodigo()}.pdf");
        }).AllowAnonymous().RequireRateLimiting("magic-link");

        group.MapGet("/externo/{token}/evidencia", async (
            string token,
            [FromQuery] int? adjuntoId,
            ApplicationDbContext db,
            IPermisosStorageService storageService,
            CancellationToken ct) =>
        {
            var solicitud = await db.TblTimeReportSolicitudes
                .AsNoTracking()
                .Include(s => s.Adjuntos).Include(s => s.Lider).ThenInclude(l => l!.IdtipoNavigation)
                .FirstOrDefaultAsync(s => s.TokenExterno == token && s.Activo, ct);

            if (!EsTokenExternoValido(token) || solicitud is null || !EsLiderExterno(solicitud.Lider) || (solicitud.TokenExternoExpira.HasValue && solicitud.TokenExternoExpira.Value < DateTime.UtcNow))
                return Results.NotFound(ApiResponse<object>.Fail(404, "El enlace de revisión no existe o ha expirado.", []));

            var adjunto = adjuntoId.HasValue
                ? solicitud.Adjuntos.FirstOrDefault(a => a.Id == adjuntoId.Value && a.Activo)
                : solicitud.Adjuntos.Where(a => a.Activo).OrderByDescending(a => a.FechaCreacion).FirstOrDefault();
            if (adjunto is null)
                return Results.NotFound(ApiResponse<object>.Fail(404, "Sin evidencia adjunta.", []));

            var descarga = await storageService.ObtenerAdjuntoAsync(
                adjunto.BucketName,
                adjunto.ObjectKey,
                adjunto.NombreOriginal,
                adjunto.ContentType,
                ct);

            return descarga is null
                ? Results.NotFound(ApiResponse<object>.Fail(404, "El archivo adjunto no se encuentra disponible en este momento.", []))
                : Results.File(descarga.Contenido, descarga.MimeType, descarga.NombreArchivo);
        }).AllowAnonymous().RequireRateLimiting("magic-link");
    }

    // =========================================================================
    // Creación transaccional (Solicitud + MinIO + Adjuntos + Campana SSE)
    // =========================================================================
    private static async Task<IResult> CrearSolicitudInternaAsync(
        CrearPermisoRequest request,
        IReadOnlyList<IFormFile> archivosAdjuntos,
        HttpContext httpContext,
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        IPermisosStorageService? storageService,
        INotificacionInternaNotifier sseNotifier,
        IPermisosNotificacionService notificacionService,
        CancellationToken ct)
    {
        if (currentUser.UserId <= 0)
            return Results.Json(ApiResponse<object>.Fail(401, "Sesión no válida.", []), statusCode: 401);

        var usuario = await db.TblAutenticacionUsuarios
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == currentUser.UserId && u.Activo, ct);

        if (usuario is null)
            return Results.Json(ApiResponse<object>.Fail(404, "Usuario no encontrado.", []), statusCode: 404);

        TblAdministracionEmpleado? empleado = null;
        if (usuario.Idpersona.HasValue)
        {
            empleado = await db.TblAdministracionEmpleados
                .AsNoTracking()
                .DeLaPersona(usuario.Idpersona)
                .Include(e => e.IdtipocontratoNavigation)
                .FirstOrDefaultAsync(ct);
        }

        if (empleado is null && !string.IsNullOrWhiteSpace(usuario.Email))
        {
            var emailLower = usuario.Email.Trim().ToLowerInvariant();
            empleado = await db.TblAdministracionEmpleados
                .AsNoTracking()
                .Where(e => e.Activo && e.Emailcorporativo != null && e.Emailcorporativo.ToLower() == emailLower)
                .Include(e => e.IdtipocontratoNavigation)
                .FirstOrDefaultAsync(ct);
        }

        if (empleado is null)
        {
            return Results.BadRequest(ApiResponse<object>.Fail(
                400,
                "Tu usuario no tiene una ficha de colaborador activa asociada. Solicita a Talento Humano o al Administrador que vincule tu ficha de empleado antes de registrar solicitudes.",
                []));
        }

        var motivoLimpio = request.Motivo?.Replace("\0", "").Trim();
        if (string.IsNullOrWhiteSpace(motivoLimpio))
            return Results.BadRequest(ApiResponse<object>.Fail(400, "Por favor escribe el motivo de tu solicitud.", []));
        if (motivoLimpio.Length > 2000)
            return Results.BadRequest(ApiResponse<object>.Fail(400, "El motivo no puede superar los 2000 caracteres.", []));
        var detalleHorarioLimpio = string.IsNullOrWhiteSpace(request.DetalleHorario) ? null : request.DetalleHorario.Replace("\0", "").Trim();
        if (detalleHorarioLimpio?.Length > 120)
            return Results.BadRequest(ApiResponse<object>.Fail(400, "El detalle del horario no puede superar los 120 caracteres.", []));

        var fechaInicio = ParsearFecha(request.FechaInicio);
        var fechaFin = ParsearFecha(request.FechaFin);
        if (!fechaInicio.HasValue || !fechaFin.HasValue)
            return Results.BadRequest(ApiResponse<object>.Fail(400, "Debe indicar una fecha de inicio y finalización válidas.", []));

        if (fechaFin.Value < fechaInicio.Value)
            return Results.BadRequest(ApiResponse<object>.Fail(400, "La fecha de finalización no puede ser anterior a la fecha de inicio.", []));

        var horario = string.IsNullOrWhiteSpace(request.Horario) ? "COMPLETA" : request.Horario.Trim().ToUpperInvariant();
        if (horario is not ("COMPLETA" or "MANANA" or "TARDE" or "OTROS"))
            horario = "COMPLETA";

        // Cargar feriados activos del rango para descontarlos de los días hábiles y fecha de regreso
        var feriados = await CargarFeriadosRangoAsync(db, fechaInicio.Value, fechaFin.Value.AddDays(15), ct);

        var diasLaborablesRango = CalcularDiasLaborablesSinMinimo(fechaInicio.Value, fechaFin.Value, feriados);
        if (diasLaborablesRango > 90)
            return Results.BadRequest(ApiResponse<object>.Fail(400, "El período solicitado no puede superar los 90 días hábiles.", []));
        if (diasLaborablesRango <= 0)
        {
            return Results.BadRequest(ApiResponse<object>.Fail(
                400,
                "El período seleccionado solo contiene fines de semana o días feriados.",
                []));
        }

        var fechaRegresoSolicitada = ParsearFecha(request.FechaRegreso);
        var fechaRegreso = fechaRegresoSolicitada.HasValue
                           && fechaRegresoSolicitada.Value > fechaFin.Value
                           && EsDiaLaborable(fechaRegresoSolicitada.Value, feriados)
            ? fechaRegresoSolicitada.Value
            : CalcularSiguienteDiaLaborable(fechaFin.Value, feriados);

        decimal diasHabiles;
        decimal horasTotales;
        if (horario is "MANANA" or "TARDE")
        {
            if (fechaInicio.Value != fechaFin.Value) return Results.BadRequest(ApiResponse<object>.Fail(400, "Los permisos de media jornada (mañana o tarde) deben solicitarse para un único día.", []));
            diasHabiles = 0.5m;
            horasTotales = 4m;
        }
        else if (horario == "OTROS")
        {
            var horasDesdeDetalle = ExtraerHorasDesdeDetalleHorario(detalleHorarioLimpio);
            if (horasDesdeDetalle.HasValue)
            {
                if (horasDesdeDetalle.Value < 0.5m || horasDesdeDetalle.Value >= 8m)
                    return Results.BadRequest(ApiResponse<object>.Fail(400, "Revisa el horario: elige horas dentro de la jornada laboral, con la hora de fin posterior al inicio. El permiso debe durar entre 30 minutos y 7 horas y 30 minutos por día.", []));
                horasTotales = Math.Round(horasDesdeDetalle.Value * diasLaborablesRango, 2);
            }
            else if (request.TotalHoras is > 0)
            {
                horasTotales = Math.Round(request.TotalHoras.Value, 2);
                if (horasTotales < 0.5m || horasTotales >= diasLaborablesRango * 8m)
                    return Results.BadRequest(ApiResponse<object>.Fail(400, "Revisa la duración solicitada: debe ser de al menos 30 minutos y menor al total de horas laborables del período.", []));
            }
            else
            {
                return Results.BadRequest(ApiResponse<object>.Fail(400, "Para la jornada 'Otros', debes indicar un rango de horas válido (por ejemplo: 09:00 - 11:00).", []));
            }

            diasHabiles = Math.Round(horasTotales / 8m, 2);
        }
        else
        {
            diasHabiles = diasLaborablesRango;
            horasTotales = diasHabiles * 8m;
        }

        // Validar que el mismo colaborador no tenga otra solicitud activa traslapada en esas fechas
        var solicitudesMismoEmpleado = await db.TblTimeReportSolicitudes
            .AsNoTracking()
            .Include(s => s.Estado)
            .Include(s => s.TipoPermiso)
            .Where(s => s.Activo
                        && s.IdEmpleado == empleado.Id
                        && s.Estado.Codigo != "RECHAZADA"
                        && s.Estado.Codigo != "OBSERVADA"
                        && s.FechaInicio <= fechaFin.Value
                        && s.FechaFin >= fechaInicio.Value)
            .ToListAsync(ct);

        var traslapePropio = solicitudesMismoEmpleado.FirstOrDefault(s =>
            !(s.FechaInicio == fechaInicio.Value
              && s.FechaFin == fechaFin.Value
              && ((s.Horario == "MANANA" && horario == "TARDE") || (s.Horario == "TARDE" && horario == "MANANA"))));

        if (traslapePropio is not null)
        {
            return Results.BadRequest(ApiResponse<object>.Fail(
                400,
                $"Ya tienes una solicitud activa ({traslapePropio.Codigo} - {traslapePropio.TipoPermiso.Nombre}, del {traslapePropio.FechaInicio:dd/MM/yyyy} al {traslapePropio.FechaFin:dd/MM/yyyy}) que se traslapa con las fechas seleccionadas.",
                []));
        }

        var tipoPermiso = await ObtenerTipoPermisoAsync(db, request.TipoOficial, ct);
        if (tipoPermiso is null)
            return Results.BadRequest(ApiResponse<object>.Fail(400, "El catálogo de tipos de permiso no está inicializado.", []));

        var hoyEcuadorValidacion = CalculoHorasPeriodo.HoyEcuador();
        var esPermisoPlanificado = tipoPermiso.Codigo is "VACACIONES" or "MATRIMONIO" or "MATERNIDAD" or "PERMISO_ESTUDIOS";
        if (esPermisoPlanificado && fechaInicio.Value < hoyEcuadorValidacion)
        {
            return Results.BadRequest(ApiResponse<object>.Fail(
                400,
                $"Las solicitudes de {tipoPermiso.Nombre} deben registrarse desde la fecha actual ({hoyEcuadorValidacion:dd/MM/yyyy}) en adelante.",
                []));
        }

        if (!esPermisoPlanificado && fechaInicio.Value < hoyEcuadorValidacion.AddDays(-60))
        {
            return Results.BadRequest(ApiResponse<object>.Fail(
                400,
                "La fecha de inicio para permisos retroactivos o imprevistos no puede superar los 60 días de antigüedad.",
                []));
        }

        // Validar saldo anual de vacaciones disponibles (base 15 días/año + antigüedad)
        if (tipoPermiso.Codigo == "VACACIONES")
        {
            var solicitudesAnioEmpleado = await db.TblTimeReportSolicitudes
                .AsNoTracking()
                .Include(s => s.Estado)
                .Include(s => s.TipoPermiso)
                .Where(s => s.Activo && s.IdEmpleado == empleado.Id)
                .ToListAsync(ct);

            var diasDisponiblesVacaciones = CalcularDiasVacacionesDisponibles(empleado, solicitudesAnioEmpleado);
            if (diasHabiles > diasDisponiblesVacaciones)
            {
                return Results.BadRequest(ApiResponse<object>.Fail(
                    400,
                    $"No cuentas con suficientes días de vacaciones disponibles. Solicitas {diasHabiles:0.##} día(s), pero tu saldo disponible actual es de {diasDisponiblesVacaciones:0.##} día(s).",
                    []));
            }
        }

        // RF-004: Certificado médico obligatorio para Nómina en Licencia por enfermedad > 1 día
        var (esNomina, _) = await ResolverRegimenContratoAsync(db, empleado, ct);
        if (esNomina
            && tipoPermiso.Codigo == "LICENCIA_ENFERMEDAD"
            && diasHabiles > 1m
            && archivosAdjuntos.Count == 0)
        {
            return Results.BadRequest(ApiResponse<object>.Fail(
                400,
                "Para licencias por enfermedad mayores a 1 día es necesario adjuntar el certificado médico.",
                []));
        }

        // RNF-003: Validar tamaño <= 5 MB, MIME y cabecera binaria ANTES de abrir transacción
        var archivosValidados = new List<ArchivoValidadoPreSubida>();
        if (archivosAdjuntos.Count > 5)
        {
            return Results.BadRequest(ApiResponse<object>.Fail(
                400,
                "Puedes adjuntar hasta un máximo de 5 archivos por solicitud (15 MB máximo cada uno).",
                []));
        }
        if (archivosAdjuntos.Count > 0)
        {
            if (storageService is null)
                return Results.BadRequest(ApiResponse<object>.Fail(400, "Servicio de almacenamiento no disponible.", []));

            try
            {
                foreach (var f in archivosAdjuntos)
                {
                    archivosValidados.Add(await storageService.ValidarYLeerArchivoAsync(f, ct));
                }
            }
            catch (InvalidOperationException ex)
            {
                RegistrarErrorInfraestructura(db, ex, $"Validación o subida rechazada para usuario {currentUser.UserId}");
                return Results.BadRequest(ApiResponse<object>.Fail(400, TraducirErrorInfraestructuraAmigable(ex, tieneArchivo: true), []));
            }
        }

        var idEstadoPendiente = await ObtenerIdEstadoSolicitudAsync(db, "PENDIENTE_JEFE", ct);

        // Resolver asignación de proyecto, cliente y líder (RF-001).
        int? idAsignacion = null;
        int? idCliente = null;
        int? idLider = null;
        int? idProyecto = request.IdProyecto is > 0 ? request.IdProyecto : null;
        int? idLiderSolicitado = request.IdLider is > 0 ? request.IdLider : null;

        var asignacionesEmpleado = await db.TblTimeReportAsignacionProyectos
            .AsNoTracking()
            .Where(a => a.Idempleado == empleado.Id && a.Activo)
            .OrderByDescending(a => a.Id)
            .ToListAsync(ct);

        // Sin proyecto elegido se usa la asignación activa más reciente del colaborador
        idProyecto ??= asignacionesEmpleado.FirstOrDefault()?.Idproyecto;

        if (idProyecto.HasValue)
        {
            var proyecto = await db.TblTimeReportProyectos.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == idProyecto.Value && p.Activo, ct);
            if (proyecto is null)
                return Results.BadRequest(ApiResponse<object>.Fail(400, "El proyecto seleccionado no existe o no está activo.", []));

            idCliente = proyecto.Idcliente;

            var asigPropia = asignacionesEmpleado.FirstOrDefault(a => a.Idproyecto == idProyecto.Value);
            idAsignacion = asigPropia?.Id;

            // Líderes del proyecto: el de la asignación propia, el de la fila cabecera (Idempleado == null) o cualquier fila activa
            var filasConLider = await db.TblTimeReportAsignacionProyectos
                .AsNoTracking()
                .Where(a => a.Activo && a.Idproyecto == idProyecto.Value && a.Idlider != null)
                .ToListAsync(ct);

            if (filasConLider.Count > 0)
            {
                if (idLiderSolicitado.HasValue && filasConLider.All(a => a.Idlider != idLiderSolicitado.Value))
                    return Results.BadRequest(ApiResponse<object>.Fail(400, "El Jefe Inmediato indicado no es líder del proyecto seleccionado.", []));

                var filaLider = idLiderSolicitado.HasValue
                    ? filasConLider
                        .Where(a => a.Idlider == idLiderSolicitado.Value)
                        .OrderByDescending(a => a.Idempleado == empleado.Id)
                        .ThenByDescending(a => a.Idempleado == null)
                        .First()
                    : asigPropia?.Idlider is not null
                        ? asigPropia
                        : filasConLider.OrderByDescending(a => a.Idempleado == null).First();

                idLider = filaLider.Idlider;
                idAsignacion ??= filaLider.Id;
            }
        }

        if (!idLider.HasValue && idLiderSolicitado.HasValue)
        {
            var liderActivo = await db.TblAdministracionLiders
                .AnyAsync(l => l.Id == idLiderSolicitado.Value && l.Activo, ct);
            if (!liderActivo)
                return Results.BadRequest(ApiResponse<object>.Fail(400, "El Jefe Inmediato indicado no existe o no está activo.", []));

            idLider = idLiderSolicitado;
        }

        var esLiderExternoAsignado = idLider.HasValue && await db.TblAdministracionLiders.AsNoTracking().Include(l => l.IdtipoNavigation).FirstOrDefaultAsync(l => l.Id == idLider.Value && l.Activo, ct) is { } liderEnt && EsLiderExterno(liderEnt);
        var anio = fechaInicio.Value.Year;
        int nuevaSolicitudId = 0;
        var objetosSubidos = new List<ArchivoAdjuntoSubido>();

        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            try
            {
                TblTimeReportSolicitud? nueva = null;
                for (var intento = 1; intento <= 5; intento++)
                {
                    var codigo = await GenerarSiguienteCodigoSolicitudAsync(db, anio, intento - 1, ct);
                    nueva = new TblTimeReportSolicitud
                    {
                        Codigo = codigo,
                        IdEmpleado = empleado.Id,
                        IdAsignacionProyecto = idAsignacion,
                        IdProyecto = idProyecto,
                        IdCliente = idCliente,
                        IdLider = idLider,
                        IdTipoPermiso = tipoPermiso.Id,
                        IdEstado = idEstadoPendiente,
                        FechaSolicitud = CalculoHorasPeriodo.HoyEcuador(),
                        Motivo = motivoLimpio,
                        FechaInicio = fechaInicio.Value,
                        FechaFin = fechaFin.Value,
                        FechaRegreso = fechaRegreso,
                        DiasSolicitados = diasHabiles,
                        Horario = horario,
                        DetalleHorario = detalleHorarioLimpio,
                        HorasSolicitadas = horasTotales,
                        ImpactoTimeReport = "RESTA_META",
                        TokenExterno = esLiderExternoAsignado ? Guid.NewGuid().ToString("N") : null,
                        TokenExternoExpira = esLiderExternoAsignado ? DateTime.UtcNow.AddHours(72) : null,
                        Activo = true,
                        UsuarioCreacion = usuario.Email,
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

                nuevaSolicitudId = nueva!.Id;

                // Subir a MinIO con llave UUID estructurada solicitudes/{anio}/{id}/{uuid}.{ext}
                if (archivosValidados.Count > 0 && storageService is not null)
                {
                    foreach (var val in archivosValidados)
                    {
                        var subido = await storageService.SubirAdjuntoValidadoAsync(val, nueva.Id, anio, ct);
                        objetosSubidos.Add(subido);

                        db.TblTimeReportSolicitudAdjuntos.Add(new TblTimeReportSolicitudAdjunto
                        {
                            IdSolicitud = nueva.Id,
                            NombreOriginal = subido.NombreOriginal,
                            BucketName = subido.BucketName,
                            ObjectKey = subido.ObjectKey,
                            ContentType = subido.ContentType,
                            TamanoBytes = subido.TamanoBytes,
                            Sha256 = subido.Sha256,
                            SubidoPorUsuarioId = usuario.Id,
                            Activo = true,
                            FechaCreacion = DateTime.UtcNow
                        });
                    }
                    await db.SaveChangesAsync(ct);
                }

                var creada = await ConsultarSolicitudesBase(db).FirstAsync(s => s.Id == nuevaSolicitudId, ct);
                var dto = MapearSolicitudDto(creada, null);

                // Campana SSE en tiempo real (RF-010)
                var campanaOk = await EmitirNotificacionesInternasPaso1SeguraAsync(db, sseNotifier, creada, dto, ct, request.IdUsuarioRrhh);

                // Correo / Apprise (encolado asíncronamente en el Outbox de mensajería)
                var correoColab = creada.Empleado.Emailcorporativo
                                  ?? creada.Empleado.IdpersonaNavigation.Email
                                  ?? usuario.Email;

                // Solo el correo al líder externo usa el token; la respuesta HTTP no lo incluye
                var correoOk = await NotificarCorreoSeguroAsync(db, notificacionService,
                    dto with { TokenExterno = creada.TokenExterno },
                    correoColab,
                    EventoNotificacionPermiso.SolicitudCreada, request.IdUsuarioRrhh,
                    ct);

                if (!campanaOk || !correoOk) throw new InvalidOperationException("No se pudo registrar el aviso de la solicitud.");
                await tx.CommitAsync(ct);
                return Results.Ok(ApiResponse<PermisoItemDto>.Ok(dto, "Tu solicitud fue registrada correctamente."));
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(CancellationToken.None);
                if (storageService is not null && objetosSubidos.Count > 0)
                {
                    foreach (var obj in objetosSubidos)
                        await storageService.EliminarAdjuntoAsync(obj.BucketName, obj.ObjectKey, CancellationToken.None);
                }
                RegistrarErrorInfraestructura(db, ex, $"Validación o subida rechazada para usuario {currentUser.UserId}");
                return Results.BadRequest(ApiResponse<object>.Fail(400, TraducirErrorInfraestructuraAmigable(ex, tieneArchivo: true), []));
            }
        }

    }

    private static async Task<string> GenerarSiguienteCodigoSolicitudAsync(
        ApplicationDbContext db,
        int anio,
        int desplazamientoReintento,
        CancellationToken ct)
    {
        var prefijo = $"PER-{anio}-";
        var codigosAnio = await db.TblTimeReportSolicitudes
            .AsNoTracking()
            .Where(s => s.Codigo.StartsWith(prefijo))
            .Select(s => s.Codigo)
            .ToListAsync(ct);

        var maxConsecutivo = 0;
        foreach (var cod in codigosAnio)
        {
            var sufijo = cod[prefijo.Length..];
            if (int.TryParse(sufijo, NumberStyles.Integer, CultureInfo.InvariantCulture, out var num) && num > maxConsecutivo)
                maxConsecutivo = num;
        }

        var siguiente = maxConsecutivo + 1 + desplazamientoReintento;
        return $"{prefijo}{siguiente:D3}";
    }

    // =========================================================================
    // Consultas y Mapeo desde las 5 Tablas Normalizadas (3FN)
    // =========================================================================
    internal static IQueryable<TblTimeReportSolicitud> ConsultarSolicitudesBase(ApplicationDbContext db)
    {
        return db.TblTimeReportSolicitudes
            .AsNoTracking()
            .Include(s => s.Empleado)
                .ThenInclude(e => e.IdpersonaNavigation)
            .Include(s => s.Empleado)
                .ThenInclude(e => e.IdcargoNavigation)
                    .ThenInclude(c => c!.IddepartamentoNavigation)
            .Include(s => s.AsignacionProyecto)
                .ThenInclude(a => a!.IdproyectoNavigation)
                    .ThenInclude(p => p.IdclienteNavigation)
            .Include(s => s.AsignacionProyecto)
                .ThenInclude(a => a!.IdliderNavigation)
                    .ThenInclude(l => l!.IdpersonaNavigation)
            .Include(s => s.AsignacionProyecto)
                .ThenInclude(a => a!.IdliderNavigation)
                    .ThenInclude(l => l!.IdtipoNavigation)
            .Include(s => s.Proyecto)
                .ThenInclude(p => p!.IdclienteNavigation)
            .Include(s => s.Cliente)
            .Include(s => s.Lider)
                .ThenInclude(l => l!.IdpersonaNavigation)
            .Include(s => s.Lider)
                .ThenInclude(l => l!.IdtipoNavigation)
            .Include(s => s.TipoPermiso)
            .Include(s => s.Estado)
            .Include(s => s.Adjuntos)
            .Include(s => s.Revisiones)
                .ThenInclude(r => r.UsuarioRevisor)
                    .ThenInclude(u => u!.IdpersonaNavigation);
    }

    internal static PermisoItemDto MapearSolicitudDto(TblTimeReportSolicitud s, string? traslapeAlerta)
    {
        var persona = s.Empleado.IdpersonaNavigation;
        var colaborador = $"{persona.Apellidos} {persona.Nombres}".Trim();
        var cedula = persona.Numeroidentificacion ?? "";
        var cargo = s.Empleado.IdcargoNavigation?.Nombrecargo ?? "Colaborador ISC";
        var area = s.Empleado.IdcargoNavigation?.IddepartamentoNavigation?.Valor ?? "Operaciones & Tecnología";

        var proyEfectivo = s.Proyecto ?? s.AsignacionProyecto?.IdproyectoNavigation;
        var cliEfectivo = s.Cliente ?? proyEfectivo?.IdclienteNavigation;
        var liderEfectivo = s.Lider ?? s.AsignacionProyecto?.IdliderNavigation;

        var cliente = ObtenerNombreCliente(cliEfectivo);
        var proyecto = proyEfectivo?.Nombre ?? "General ISC";

        var liderNombre = ObtenerNombreLider(liderEfectivo);
        var esLiderExt = EsLiderExterno(liderEfectivo);
        var liderTipo = esLiderExt ? "EXT" : "INT";
        var liderCorreo = liderEfectivo?.IdpersonaNavigation?.Email;

        var revLider = s.Revisiones
            .Where(r => r.EtapaRevision == "JEFE_INMEDIATO")
            .OrderByDescending(r => r.FechaRevision)
            .FirstOrDefault();

        var revTh = s.Revisiones
            .Where(r => r.EtapaRevision == "TALENTO_HUMANO")
            .OrderByDescending(r => r.FechaRevision)
            .FirstOrDefault();

        var adjuntosActivos = s.Adjuntos
            .Where(a => a.Activo)
            .OrderByDescending(a => a.FechaCreacion)
            .ToList();

        var adjuntoPrincipal = adjuntosActivos.FirstOrDefault();

        var fechaSol = s.FechaSolicitud;
        var fechaSolicitudStr = fechaSol.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        var diaSol = fechaSol.ToString("dd", CultureInfo.InvariantCulture);
        var mesSol = fechaSol.ToString("MM", CultureInfo.InvariantCulture);
        var anioSol = fechaSol.ToString("yyyy", CultureInfo.InvariantCulture);

        var fechaInicioStr = s.FechaInicio.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        var fechaFinStr = s.FechaFin.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        var fechaRegresoStr = s.FechaRegreso.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

        var fechasLegible = s.FechaInicio == s.FechaFin
            ? (!string.IsNullOrWhiteSpace(s.DetalleHorario) ? $"{fechaInicioStr} ({s.DetalleHorario})" : fechaInicioStr)
            : $"{fechaInicioStr} - {fechaFinStr}";

        var fechaAprobStr = revTh?.FechaRevision.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
                            ?? revLider?.FechaRevision.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
                            ?? "Pendiente";

        var totalDias = s.DiasSolicitados;
        var totalHoras = s.HorasSolicitadas ?? (totalDias * 8m);
        var diasStr = totalDias == 1m
            ? "1 día laborable"
            : $"{totalDias.ToString("0.##", CultureInfo.InvariantCulture)} días laborables";
        var horasStr = $"{totalHoras.ToString("0.##", CultureInfo.InvariantCulture)} horas";
        var duracionStr = $"{diasStr} ({totalHoras.ToString("0.##", CultureInfo.InvariantCulture)} h)";

        var aprobadoLider = revLider?.Decision == "APROBADO" || s.Estado.Codigo is "PENDIENTE_TH" or "APROBADA";
        var confirmadoRrhh = revTh?.Decision == "APROBADO" || s.Estado.Codigo == "APROBADA";

        var estadoCodCorto = s.Estado.CodigoCorto;
        var (estadoClase, estadoTexto, estadoRrhh, estadoLider) = ResolverEstados(
            s.Estado.Codigo,
            aprobadoLider,
            confirmadoRrhh,
            esLiderExt,
            revLider?.Decision,
            revTh?.Decision);

        var metodoLider = revLider?.MetodoRevision == "MAGIC_LINK_EXTERNO" || (revLider is null && esLiderExt)
            ? "Magic Link Externo"
            : "Cuenta TMR Líder";

        var responsableRrhh = revTh?.UsuarioRevisor?.IdpersonaNavigation is { } pTh
            ? $"{pTh.Nombres} {pTh.Apellidos}".Trim()
            : "Talento Humano ISC";

        var comentarios = revTh?.Comentario ?? revLider?.Comentario;

        var adjuntosDto = adjuntosActivos
            .Select(a => new AdjuntoPermisoDto(
                a.Id,
                a.NombreOriginal,
                a.ContentType,
                a.TamanoBytes,
                a.FechaCreacion.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)))
            .ToList();

        return new PermisoItemDto(
            Id: s.Id,
            Codigo: s.Codigo,
            IdEmpleado: s.IdEmpleado,
            Colaborador: colaborador,
            Cedula: cedula,
            CodigoColaborador: s.Empleado.Codigoempleado,
            Cargo: cargo,
            Area: area,
            IdCliente: s.IdCliente ?? proyEfectivo?.Idcliente,
            Cliente: cliente,
            IdProyecto: s.IdProyecto ?? proyEfectivo?.Id,
            Proyecto: proyecto,
            IdLider: s.IdLider ?? liderEfectivo?.Id,
            LiderNombre: liderNombre,
            LiderTipo: liderTipo,
            LiderCorreo: liderCorreo,
            LiderEmpresa: cliente,
            TipoCodigo: s.TipoPermiso.CodigoCorto,
            Tipo: s.TipoPermiso.Nombre,
            TipoOficial: s.TipoPermiso.Codigo,
            Motivo: s.Motivo,
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
            Horario: string.IsNullOrWhiteSpace(s.Horario) ? "COMPLETA" : s.Horario.ToUpperInvariant(),
            DetalleHorario: s.DetalleHorario,
            HorasSolicitadas: horasStr,
            TotalHoras: totalHoras,
            Duracion: duracionStr,
            EvidenciaNombre: adjuntoPrincipal?.NombreOriginal,
            EvidenciaUrl: adjuntoPrincipal is not null ? $"/api/permisos/{s.Id}/evidencia" : null,
            TieneEvidencia: adjuntoPrincipal is not null,
            AprobadoLider: aprobadoLider,
            FechaAprobacionLider: revLider?.FechaRevision.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
            MetodoLider: metodoLider,
            ComentarioLider: revLider?.Comentario,
            ConfirmadoRrhh: confirmadoRrhh,
            ResponsableRrhh: responsableRrhh,
            FechaConfirmacionRrhh: revTh?.FechaRevision.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
            ObservacionRrhh: revTh?.Comentario,
            ImpactoTimeReport: s.ImpactoTimeReport,
            EstadoCodigo: estadoCodCorto,
            EstadoClase: estadoClase,
            EstadoTexto: estadoTexto,
            EstadoRrhh: estadoRrhh,
            EstadoLider: estadoLider,
            Comentarios: comentarios,
            TraslapeAlerta: traslapeAlerta,
            // El token del magic link nunca sale en respuestas de la API: solo se agrega
            // (con `with`) al DTO que se usa para el correo al líder externo.
            TokenExterno: null,
            Adjuntos: adjuntosDto);
    }

    // =========================================================================
    // Seguridad Anti-IDOR (RNF-005), Reglas de Contrato (RF-004) y Campana SSE
    // =========================================================================
    private sealed record ActorActual(
        int IdUsuario,
        string Email,
        int? IdEmpleado,
        int? IdLider,
        bool EsAdmin,
        bool EsLider,
        bool EsRrhh,
        bool EsGerente);

    private static async Task<ActorActual?> ResolverActorActualAsync(
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        CancellationToken ct)
    {
        if (currentUser.UserId <= 0) return null;

        var usuario = await db.TblAutenticacionUsuarios
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == currentUser.UserId && u.Activo, ct);

        if (usuario is null) return null;

        var empleado = await db.TblAdministracionEmpleados
            .AsNoTracking()
            .DeLaPersona(usuario.Idpersona)
            .Include(e => e.IdcargoNavigation)
            .FirstOrDefaultAsync(ct);

        int? idLider = null;
        if (usuario.Idpersona.HasValue)
        {
            var lider = await db.TblAdministracionLiders
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.Idpersona == usuario.Idpersona && l.Activo, ct);
            idLider = lider?.Id;
        }

        bool TieneRol(params string[] nombres) =>
            currentUser.Roles.Any(r => nombres.Any(n => r.Equals(n, StringComparison.OrdinalIgnoreCase)));

        var cargoNombre = empleado?.IdcargoNavigation?.Nombrecargo?.Trim();
        var esCargoLiderTh = string.Equals(cargoNombre, "Líder de Talento Humano", StringComparison.OrdinalIgnoreCase)
                             || string.Equals(cargoNombre, "Lider de Talento Humano", StringComparison.OrdinalIgnoreCase);

        var esAdmin = TieneRol("ADMINISTRADOR", "ADMIN");
        var esRrhh = esCargoLiderTh || TieneRol("RECURSOS HUMANOS", "RECURSOS_HUMANOS");
        var esLider = idLider.HasValue || TieneRol("LIDER", "LÍDER");
        var esGerente = TieneRol("GERENTE", "GERENCIA");

        return new ActorActual(
            usuario.Id,
            usuario.Email,
            empleado?.Id,
            idLider,
            esAdmin,
            esLider,
            esRrhh,
            esGerente);
    }

    private static bool PuedeConsultarSolicitud(ActorActual actor, TblTimeReportSolicitud solicitud)
    {
        if (actor.EsAdmin || actor.EsRrhh || actor.EsGerente)
            return true;

        if (actor.IdEmpleado.HasValue && solicitud.IdEmpleado == actor.IdEmpleado.Value)
            return true;

        if (actor.IdLider.HasValue && solicitud.IdLider == actor.IdLider.Value)
            return true;

        return false;
    }

    private static async Task<(bool EsNomina, string NombreContrato)> ResolverRegimenContratoAsync(
        ApplicationDbContext db,
        TblAdministracionEmpleado empleado,
        CancellationToken ct)
    {
        int? idTipoContrato = empleado.Idtipocontrato;

        var contratoVigente = await db.TblAdministracionEmpleadoContratos
            .AsNoTracking()
            .Where(c => c.Idempleado == empleado.Id && c.Activo && c.Fechahasta == null)
            .OrderByDescending(c => c.Fechadesde)
            .FirstOrDefaultAsync(ct);

        if (contratoVigente is not null)
            idTipoContrato = contratoVigente.Idtipocontrato;

        if (!idTipoContrato.HasValue)
            return (true, "Relación de dependencia (Nómina)");

        var detalle = empleado.IdtipocontratoNavigation?.Id == idTipoContrato.Value
            ? empleado.IdtipocontratoNavigation
            : await db.TblAdministracionCatalogoDetalles
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == idTipoContrato.Value, ct);

        if (detalle is null)
            return (true, "Relación de dependencia (Nómina)");

        var codigo = detalle.Codigovalor?.Trim().ToUpperInvariant() ?? "";
        var valor = detalle.Valor ?? "";

        // En el catálogo TCT (script 02): COS = Consultor (Prestación de servicios / honorarios)
        var esServiciosProf = codigo == "COS"
                              || valor.Contains("Consultor", StringComparison.OrdinalIgnoreCase)
                              || valor.Contains("Servicios", StringComparison.OrdinalIgnoreCase)
                              || valor.Contains("Honorarios", StringComparison.OrdinalIgnoreCase);

        return (!esServiciosProf, detalle.Valor ?? "Relación de dependencia (Nómina)");
    }

    private static decimal CalcularDiasVacacionesDisponibles(
        TblAdministracionEmpleado? empleado,
        IReadOnlyCollection<TblTimeReportSolicitud> misSolicitudes)
    {
        var hoy = CalculoHorasPeriodo.HoyEcuador();
        var diasBaseAnual = 15m;

        if (empleado?.Fechaingreso is { } ingreso && ingreso < hoy)
        {
            var aniosCompletos = hoy.Year - ingreso.Year;
            if (hoy < ingreso.AddYears(aniosCompletos))
                aniosCompletos--;

            // Código del Trabajo (Art. 69): 1 día adicional por cada año que exceda de 5, hasta 15 adicionales
            if (aniosCompletos > 5)
                diasBaseAnual += Math.Min(15, aniosCompletos - 5);
        }

        var diasVacacionesTomadosAnio = misSolicitudes
            .Where(s => s.TipoPermiso.Codigo == "VACACIONES"
                        && s.Estado.Codigo != "RECHAZADA"
                        && s.Estado.Codigo != "OBSERVADA"
                        && s.FechaInicio.Year == hoy.Year)
            .Sum(s => s.DiasSolicitados);

        return Math.Max(0m, diasBaseAnual - diasVacacionesTomadosAnio);
    }

    private static decimal? ExtraerHorasDesdeDetalleHorario(string? detalleHorario)
    {
        if (string.IsNullOrWhiteSpace(detalleHorario))
            return null;

        var match = System.Text.RegularExpressions.Regex.Match(
            detalleHorario,
            @"(\d{1,2}):(\d{2})\s*(?:[-–—]|a)\s*(\d{1,2}):(\d{2})",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (!match.Success)
            return null;

        if (!int.TryParse(match.Groups[1].Value, out var hIni)
            || !int.TryParse(match.Groups[2].Value, out var mIni)
            || !int.TryParse(match.Groups[3].Value, out var hFin)
            || !int.TryParse(match.Groups[4].Value, out var mFin))
        {
            return null;
        }

        var minInicio = (hIni * 60) + mIni;
        var minFin = (hFin * 60) + mFin;
        if (minFin <= minInicio)
            return 0m;

        var minutosEfectivos = minFin - minInicio;
        if (minInicio <= (12 * 60) + 30 && minFin >= (13 * 60) + 30 && minutosEfectivos > 270)
            minutosEfectivos -= 60;

        return Math.Round(minutosEfectivos / 60m, 2);
    }

    private static async Task EmitirNotificacionesInternasPaso1LegacyUnusedAsync(
        ApplicationDbContext db,
        INotificacionInternaNotifier sseNotifier,
        TblTimeReportSolicitud solicitud,
        PermisoItemDto dto,
        CancellationToken ct)
    {
        var destinatarios = new Dictionary<int, string>();

        // Jefe inmediato interno (si tiene usuario TMR)
        var personaLiderId = solicitud.Lider?.Idpersona ?? solicitud.AsignacionProyecto?.IdliderNavigation?.Idpersona;
        if (personaLiderId.HasValue)
        {
            var idUsuarioLider = await db.TblAutenticacionUsuarios
                .AsNoTracking()
                .Where(u => u.Idpersona == personaLiderId.Value && u.Activo)
                .Select(u => (int?)u.Id)
                .FirstOrDefaultAsync(ct);

            if (idUsuarioLider.HasValue)
                destinatarios[idUsuarioLider.Value] = "/permisos/equipo";
        }

    }

    private static async Task EmitirNotificacionesInternasPaso1Async(
        ApplicationDbContext db,
        INotificacionInternaNotifier sseNotifier,
        TblTimeReportSolicitud solicitud,
        PermisoItemDto dto,
        CancellationToken ct,
        int? idUsuarioRrhhElegido = null)
    {
        var destinatarios = new Dictionary<int, string>();

        var personaLiderId = solicitud.Lider?.Idpersona ?? solicitud.AsignacionProyecto?.IdliderNavigation?.Idpersona;
        if (personaLiderId.HasValue)
        {
            var idUsuarioLider = await db.TblAutenticacionUsuarios
                .AsNoTracking()
                .Where(u => u.Idpersona == personaLiderId.Value && u.Activo)
                .Select(u => (int?)u.Id)
                .FirstOrDefaultAsync(ct);

            if (idUsuarioLider.HasValue)
                destinatarios[idUsuarioLider.Value] = "/permisos/equipo";
        }

        if (idUsuarioRrhhElegido is > 0
            && await EsUsuarioRrhhValidoAsync(db, idUsuarioRrhhElegido.Value, ct))
        {
            destinatarios.TryAdd(idUsuarioRrhhElegido.Value, "/permisos/rrhh");
        }
        else
        {
            var idsRrhhFallback = await ObtenerIdsUsuariosRrhhAsync(db, ct);
            foreach (var idRrhh in idsRrhhFallback)
                destinatarios.TryAdd(idRrhh, "/permisos/rrhh");
        }

        if (destinatarios.Count == 0) return;

        foreach (var (idUsuarioDestino, ruta) in destinatarios)
        {
            db.NotificacionesUsuario.Add(new NotificacionUsuario
            {
                IdUsuarioDestino = idUsuarioDestino,
                IdSolicitud = solicitud.Id,
                Titulo = $"Nueva solicitud {dto.Codigo} ({dto.Tipo})",
                Mensaje = $"{dto.Colaborador} registró una solicitud para {dto.Fechas}.",
                UrlDestino = ruta,
                Leida = false,
                FechaCreacion = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync(ct);
        await CampanaPermisoEncolada.GuardarAsync(db, destinatarios.Keys, ct);
    }

    private static async Task EmitirNotificacionesInternasPaso2Async(
        ApplicationDbContext db,
        INotificacionInternaNotifier sseNotifier,
        TblTimeReportSolicitud solicitud,
        PermisoItemDto dto,
        string decision,
        CancellationToken ct)
    {
        var idsNotificados = new List<int>();

        // Al colaborador dueño
        var idUsuarioColab = await ObtenerIdUsuarioDeEmpleadoAsync(db, solicitud.Empleado.Idpersona, ct);
        if (idUsuarioColab.HasValue)
        {
            var accionTxt = decision switch
            {
                "APROBADO" => "aprobada por tu Jefe Inmediato y enviada a Talento Humano",
                "OBSERVADO" => "observada por tu Jefe Inmediato",
                _ => "rechazada por tu Jefe Inmediato"
            };

            db.NotificacionesUsuario.Add(new NotificacionUsuario
            {
                IdUsuarioDestino = idUsuarioColab.Value,
                IdSolicitud = solicitud.Id,
                Titulo = $"Solicitud {dto.Codigo}: {dto.EstadoTexto}",
                Mensaje = $"Tu solicitud de {dto.Tipo} ({dto.Fechas}) fue {accionTxt}.",
                UrlDestino = "/permisos",
                Leida = false,
                FechaCreacion = DateTime.UtcNow
            });
            idsNotificados.Add(idUsuarioColab.Value);
        }

        // A Talento Humano (respetando el destinatario RRHH seleccionado en Paso 1 si existe)
        var idsRrhhElegidos = await db.NotificacionesUsuario
            .AsNoTracking()
            .Where(n => n.IdSolicitud == solicitud.Id && n.UrlDestino == "/permisos/rrhh")
            .Select(n => n.IdUsuarioDestino)
            .Distinct()
            .ToListAsync(ct);

        foreach (var idRrhhElegido in idsRrhhElegidos)
        {
            if (idRrhhElegido == idUsuarioColab) continue;
            db.NotificacionesUsuario.Add(new NotificacionUsuario
            {
                IdUsuarioDestino = idRrhhElegido,
                IdSolicitud = solicitud.Id,
                Titulo = decision == "APROBADO"
                    ? $"Pendiente confirmación TH: {dto.Codigo}"
                    : $"Solicitud {dto.Codigo} {dto.EstadoTexto}",
                Mensaje = $"{dto.LiderNombre} marcó la solicitud de {dto.Colaborador} como {decision}.",
                UrlDestino = "/permisos/rrhh",
                Leida = false,
                FechaCreacion = DateTime.UtcNow
            });
            idsNotificados.Add(idRrhhElegido);
        }

        if (idsRrhhElegidos.Count == 0)
        {
        var idsRrhh = await ObtenerIdsUsuariosRrhhAsync(db, ct);
        foreach (var idRrhh in idsRrhh)
        {
            if (idRrhh == idUsuarioColab) continue;

            db.NotificacionesUsuario.Add(new NotificacionUsuario
            {
                IdUsuarioDestino = idRrhh,
                IdSolicitud = solicitud.Id,
                Titulo = decision == "APROBADO"
                    ? $"Pendiente confirmación TH: {dto.Codigo}"
                    : $"Solicitud {dto.Codigo} {dto.EstadoTexto}",
                Mensaje = $"{dto.LiderNombre} marcó la solicitud de {dto.Colaborador} como {decision}.",
                UrlDestino = "/permisos/rrhh",
                Leida = false,
                FechaCreacion = DateTime.UtcNow
            });
            idsNotificados.Add(idRrhh);
        }

        }
        if (idsNotificados.Count > 0)
        {
            await db.SaveChangesAsync(ct);
            await CampanaPermisoEncolada.GuardarAsync(db, idsNotificados, ct);
        }
    }

    private static async Task EmitirNotificacionesInternasPaso3Async(
        ApplicationDbContext db,
        INotificacionInternaNotifier sseNotifier,
        TblTimeReportSolicitud solicitud,
        PermisoItemDto dto,
        bool confirmada,
        CancellationToken ct)
    {
        var idUsuarioColab = await ObtenerIdUsuarioDeEmpleadoAsync(db, solicitud.Empleado.Idpersona, ct);
        if (!idUsuarioColab.HasValue) return;

        db.NotificacionesUsuario.Add(new NotificacionUsuario
        {
            IdUsuarioDestino = idUsuarioColab.Value,
            IdSolicitud = solicitud.Id,
            Titulo = confirmada
                ? $"Solicitud {dto.Codigo} confirmada en TH"
                : $"Solicitud {dto.Codigo} observada por TH",
            Mensaje = confirmada
                ? $"Talento Humano registró y confirmó tu solicitud de {dto.Tipo} ({dto.Fechas})."
                : TruncarTexto($"Talento Humano devolvió tu solicitud con observaciones: {dto.ObservacionRrhh}", 500),
            UrlDestino = "/permisos",
            Leida = false,
            FechaCreacion = DateTime.UtcNow
        });

        await db.SaveChangesAsync(ct);
        await CampanaPermisoEncolada.GuardarAsync(db, [idUsuarioColab.Value], ct);
    }

    private static async Task<int?> ObtenerIdUsuarioDeEmpleadoAsync(ApplicationDbContext db, int idPersona, CancellationToken ct) =>
        await db.TblAutenticacionUsuarios
            .AsNoTracking()
            .Where(u => u.Idpersona == idPersona && u.Activo)
            .Select(u => (int?)u.Id)
            .FirstOrDefaultAsync(ct);

    private static async Task<List<int>> ObtenerIdsUsuariosRrhhAsync(ApplicationDbContext db, CancellationToken ct)
    {
        var idsLiderTh = await db.TblAdministracionEmpleados
            .AsNoTracking()
            .Where(e => e.Activo
                        && e.IdcargoNavigation != null
                        && e.IdcargoNavigation.Activo
                        && (e.IdcargoNavigation.Nombrecargo.ToUpper() == "LÍDER DE TALENTO HUMANO"
                            || e.IdcargoNavigation.Nombrecargo.ToUpper() == "LIDER DE TALENTO HUMANO"))
            .SelectMany(e => e.IdpersonaNavigation.TblAutenticacionUsuarios
                .Where(u => u.Activo)
                .Select(u => u.Id))
            .Distinct()
            .ToListAsync(ct);

        if (idsLiderTh.Count > 0)
            return idsLiderTh;

        return await db.TblAutenticacionUsuarioRols
            .AsNoTracking()
            .Where(ur => ur.Activo
                         && ur.IdusuarioNavigation.Activo
                         && ur.IdrolNavigation.Activo
                         && (ur.IdrolNavigation.Nombre.ToUpper() == "RECURSOS HUMANOS"
                             || ur.IdrolNavigation.Nombre.ToUpper() == "RECURSOS_HUMANOS"))
            .Select(ur => ur.Idusuario)
            .Distinct()
            .ToListAsync(ct);
    }

    // =========================================================================
    private static async Task<IReadOnlyList<RrhhDisponiblePermisoDto>> ObtenerCatalogoUsuariosRrhhAsync(
        ApplicationDbContext db,
        CancellationToken ct)
    {
        var idsPorRol = await db.TblAutenticacionUsuarioRols
            .AsNoTracking()
            .Where(ur => ur.Activo
                         && ur.IdusuarioNavigation.Activo
                         && ur.IdrolNavigation.Activo
                         && (ur.IdrolNavigation.Nombre.ToUpper() == "RECURSOS HUMANOS"
                             || ur.IdrolNavigation.Nombre.ToUpper() == "RECURSOS_HUMANOS"))
            .Select(ur => ur.Idusuario)
            .Distinct()
            .ToListAsync(ct);

        var empleadosLiderTh = await db.TblAdministracionEmpleados
            .AsNoTracking()
            .Where(e => e.Activo
                        && e.IdcargoNavigation != null
                        && e.IdcargoNavigation.Activo
                        && (e.IdcargoNavigation.Nombrecargo.ToUpper() == "LÍDER DE TALENTO HUMANO"
                            || e.IdcargoNavigation.Nombrecargo.ToUpper() == "LIDER DE TALENTO HUMANO"))
            .Select(e => e.Idpersona)
            .Distinct()
            .ToListAsync(ct);

        var idsPorCargo = empleadosLiderTh.Count > 0
            ? await db.TblAutenticacionUsuarios
                .AsNoTracking()
                .Where(u => u.Activo && u.Idpersona.HasValue && empleadosLiderTh.Contains(u.Idpersona.Value))
                .Select(u => u.Id)
                .ToListAsync(ct)
            : [];

        var todosIds = idsPorRol.Union(idsPorCargo).Distinct().ToList();
        if (todosIds.Count == 0)
            return [];

        var usuarios = await db.TblAutenticacionUsuarios
            .AsNoTracking()
            .Include(u => u.IdpersonaNavigation)
            .Where(u => todosIds.Contains(u.Id) && u.Activo)
            .ToListAsync(ct);

        var idsPersonas = usuarios
            .Where(u => u.Idpersona.HasValue)
            .Select(u => u.Idpersona!.Value)
            .Distinct()
            .ToList();

        var empleadosPorPersona = idsPersonas.Count > 0
            ? await db.TblAdministracionEmpleados
                .AsNoTracking()
                .Include(e => e.IdcargoNavigation)
                .Where(e => e.Activo && idsPersonas.Contains(e.Idpersona))
                .ToListAsync(ct)
            : [];

        var mapaEmpleado = empleadosPorPersona
            .GroupBy(e => e.Idpersona)
            .ToDictionary(g => g.Key, g => g.First());

        var lista = new List<RrhhDisponiblePermisoDto>();
        foreach (var u in usuarios)
        {
            var emp = u.Idpersona.HasValue ? mapaEmpleado.GetValueOrDefault(u.Idpersona.Value) : null;
            var nombre = u.IdpersonaNavigation is { } p
                ? $"{p.Nombres} {p.Apellidos}".Trim()
                : "";
            if (string.IsNullOrWhiteSpace(nombre))
                nombre = u.Email;

            var cargo = emp?.IdcargoNavigation?.Nombrecargo?.Trim();
            var esLiderTh = string.Equals(cargo, "Líder de Talento Humano", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(cargo, "Lider de Talento Humano", StringComparison.OrdinalIgnoreCase);

            var correo = !string.IsNullOrWhiteSpace(emp?.Emailcorporativo)
                ? emp!.Emailcorporativo!.Trim()
                : !string.IsNullOrWhiteSpace(u.Email)
                    ? u.Email.Trim()
                    : u.IdpersonaNavigation?.Email?.Trim();

            lista.Add(new RrhhDisponiblePermisoDto(
                IdUsuario: u.Id,
                Nombre: nombre,
                Cargo: string.IsNullOrWhiteSpace(cargo) ? "Talento Humano" : cargo,
                Correo: correo,
                EsPrincipal: esLiderTh));
        }

        var ordenada = lista
            .OrderByDescending(x => x.EsPrincipal)
            .ThenBy(x => x.Nombre)
            .ToList();

        if (ordenada.Count > 0 && ordenada.All(x => !x.EsPrincipal))
        {
            ordenada[0] = ordenada[0] with { EsPrincipal = true };
        }

        return ordenada;
    }

    // Helpers de Catálogo Normalizado, Feriados y Fechas
    // =========================================================================
    private static (string EstadoClase, string EstadoTexto, string EstadoRrhh, string EstadoLider) ResolverEstados(
        string codigoEstado,
        bool aprobadoLider,
        bool confirmadoRrhh,
        bool esLiderExterno,
        string? decisionLider,
        string? decisionTh)
    {
        if (confirmadoRrhh || codigoEstado == "APROBADA")
            return ("confirmado", "Confirmado", "APROBADO_RRHH", "Aprobado");

        if (codigoEstado == "RECHAZADA")
            return ("rechazado", "Rechazado", "RECHAZADO", "Rechazado");

        if (codigoEstado == "OBSERVADA")
        {
            var estadoLiderObs = decisionLider == "APROBADO" ? "Aprobado" : "Observado";
            return ("observado", "Observado", "OBSERVADO_RRHH", estadoLiderObs);
        }

        if (aprobadoLider || codigoEstado == "PENDIENTE_TH")
            return ("rrhh", "En Talento Humano", "CONFIRMADO_LIDER", "Aprobado");

        return (
            "pendiente",
            "Pendiente Jefe",
            esLiderExterno ? "PENDIENTE_LIDER_EXT" : "PENDIENTE_LIDER_INT",
            decisionTh is not null ? "Observado" : "Pendiente");
    }

    private static string ObtenerNombreCliente(TblAdministracionCliente? cliente)
    {
        if (cliente is null) return "";
        if (!string.IsNullOrWhiteSpace(cliente.Nombrecomercial)) return cliente.Nombrecomercial.Trim();
        if (!string.IsNullOrWhiteSpace(cliente.Razonsocial)) return cliente.Razonsocial.Trim();
        var full = $"{cliente.Nombres} {cliente.Apellidos}".Trim();
        return full;
    }

    private static string ObtenerNombreLider(TblAdministracionLider? lider)
    {
        if (lider?.IdpersonaNavigation is { } persona)
        {
            var nombre = $"{persona.Nombres} {persona.Apellidos}".Trim();
            if (!string.IsNullOrWhiteSpace(nombre))
                return nombre;
        }
        return "";
    }

    private static bool EsLiderExterno(TblAdministracionLider? lider)
    {
        if (lider is null) return false;
        var codigo = lider.IdtipoNavigation?.Codigovalor?.Trim().ToUpperInvariant();
        return codigo == "EXT" || (lider.IdtipoNavigation?.Valor?.Contains("Extern", StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private static bool EsTokenExternoValido(string? token) =>
        !string.IsNullOrWhiteSpace(token)
        && token.Length == 32
        && Guid.TryParseExact(token, "N", out _);

    private static string TruncarTexto(string texto, int maxLen) =>
        texto.Length <= maxLen ? texto : string.Concat(texto.AsSpan(0, maxLen - 3), "...");

    internal static async Task<bool> EsUsuarioRrhhValidoAsync(ApplicationDbContext db, int idUsuario, CancellationToken ct)
    {
        var catalogo = await ObtenerCatalogoUsuariosRrhhAsync(db, ct);
        return catalogo.Any(x => x.IdUsuario == idUsuario);
    }

    private static string? CalcularAlertaTraslape(TblTimeReportSolicitud actual, IReadOnlyList<TblTimeReportSolicitud> todos)
    {
        var idProyActual = actual.IdProyecto ?? actual.AsignacionProyecto?.Idproyecto;
        if (!idProyActual.HasValue) return null;

        var coincidente = todos.FirstOrDefault(otro =>
            otro.Id != actual.Id &&
            otro.IdEmpleado != actual.IdEmpleado &&
            (otro.IdProyecto ?? otro.AsignacionProyecto?.Idproyecto) == idProyActual &&
            otro.Estado.Codigo != "RECHAZADA" &&
            otro.FechaInicio <= actual.FechaFin &&
            otro.FechaFin >= actual.FechaInicio);

        if (coincidente is null) return null;

        var nombreOtro = $"{coincidente.Empleado.IdpersonaNavigation.Nombres} {coincidente.Empleado.IdpersonaNavigation.Apellidos}".Trim();
        var proyNombre = actual.Proyecto?.Nombre ?? actual.AsignacionProyecto?.IdproyectoNavigation.Nombre ?? "el proyecto";
        return $"Coincide entre {coincidente.FechaInicio:dd/MM} y {coincidente.FechaFin:dd/MM} con solicitud de {nombreOtro} en {proyNombre}";
    }

    private static async Task<TblTimeReportTipoPermiso?> ObtenerTipoPermisoAsync(
        ApplicationDbContext db,
        string? tipoOficial,
        CancellationToken ct)
    {
        var clave = (tipoOficial ?? "VACACIONES").Trim().ToUpperInvariant();
        var codigo = clave switch
        {
            "VAC" or "VACACIONES" => "VACACIONES",
            "ENF" or "LICENCIA_ENFERMEDAD" => "LICENCIA_ENFERMEDAD",
            "CIT" or "CITA_MEDICA" or "CITAS_MEDICAS" => "CITAS_MEDICAS",
            "MAT" or "MATRIMONIO" => "MATRIMONIO",
            "NAC" or "NACIMIENTO" or "NACIMIENTO_HIJO" => "NACIMIENTO_HIJO",
            "MTR" or "MATERNIDAD" => "MATERNIDAD",
            "FAL" or "FALLECIMIENTO" or "FALLECIMIENTO_FAMILIAR" => "FALLECIMIENTO_FAMILIAR",
            _ => "OTROS"
        };

        return await db.TblTimeReportTiposPermiso
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Activo && t.Codigo == codigo, ct)
            ?? await db.TblTimeReportTiposPermiso
                .AsNoTracking()
                .Where(t => t.Activo)
                .OrderBy(t => t.Orden)
                .FirstOrDefaultAsync(ct);
    }

    private static async Task<int> ObtenerIdEstadoSolicitudAsync(
        ApplicationDbContext db,
        string codigoEstado,
        CancellationToken ct)
    {
        var estado = await db.TblTimeReportEstadoSolicitudes
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Activo && e.Codigo == codigoEstado, ct)
            ?? await db.TblTimeReportEstadoSolicitudes
                .AsNoTracking()
                .OrderBy(e => e.Orden)
                .FirstAsync(ct);

        return estado.Id;
    }

    private static async Task<HashSet<DateOnly>> CargarFeriadosRangoAsync(
        ApplicationDbContext db,
        DateOnly desde,
        DateOnly hasta,
        CancellationToken ct)
    {
        var feriadosDb = await db.TblTimeReportFeriados
            .AsNoTracking()
            .Where(f => f.Activo && ((f.Fechaferiado >= desde && f.Fechaferiado <= hasta) || f.Esrecurrente == true))
            .Select(f => new { f.Fechaferiado, f.Esrecurrente })
            .ToListAsync(ct);

        var set = new HashSet<DateOnly>();
        foreach (var f in feriadosDb)
        {
            if (f.Fechaferiado >= desde && f.Fechaferiado <= hasta)
                set.Add(f.Fechaferiado);

            if (f.Esrecurrente == true)
            {
                for (var anio = desde.Year; anio <= hasta.Year; anio++)
                {
                    var diasEnMes = DateTime.DaysInMonth(anio, f.Fechaferiado.Month);
                    var proyectada = new DateOnly(anio, f.Fechaferiado.Month, Math.Min(f.Fechaferiado.Day, diasEnMes));
                    if (proyectada >= desde && proyectada <= hasta)
                        set.Add(proyectada);
                }
            }
        }

        return set;
    }

    private static string ObtenerIpCliente(HttpContext httpContext) =>
        httpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

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

    private static bool EsDiaLaborable(DateOnly fecha, IReadOnlySet<DateOnly> feriados) =>
        fecha.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday
        && !feriados.Contains(fecha);

    private static decimal CalcularDiasLaborablesSinMinimo(DateOnly inicio, DateOnly fin, IReadOnlySet<DateOnly> feriados)
    {
        if (fin < inicio) return 0m;
        var dias = 0;
        var cursor = inicio;
        while (cursor <= fin)
        {
            if (EsDiaLaborable(cursor, feriados))
                dias++;
            cursor = cursor.AddDays(1);
        }
        return dias;
    }

    private static DateOnly CalcularSiguienteDiaLaborable(DateOnly fin, IReadOnlySet<DateOnly> feriados)
    {
        var siguiente = fin.AddDays(1);
        while (!EsDiaLaborable(siguiente, feriados))
        {
            siguiente = siguiente.AddDays(1);
        }
        return siguiente;
    }

    // =========================================================================
    // Blindaje Post-Commit y Traducción de Mensajes Amigables (UX sin errores 500)
    // =========================================================================
    private const string MensajeAvisoNotificacionDiferida =
        "Tu gestión quedó guardada correctamente. El aviso automático por correo puede tardar unos minutos en llegar.";

    private static IResult ResponderPostCommit(
        PermisoItemDto dto,
        string mensajeExito,
        bool notificacionesCompletas)
    {
        if (notificacionesCompletas)
            return Results.Ok(ApiResponse<PermisoItemDto>.Ok(dto, mensajeExito));

        var dtoConAviso = dto with { AvisoNotificacion = MensajeAvisoNotificacionDiferida };
        return Results.Ok(ApiResponse<PermisoItemDto>.Ok(dtoConAviso, MensajeAvisoNotificacionDiferida));
    }

    private static async Task<bool> EmitirNotificacionesInternasPaso1SeguraAsync(
        ApplicationDbContext db,
        INotificacionInternaNotifier sseNotifier,
        TblTimeReportSolicitud solicitud,
        PermisoItemDto dto,
        CancellationToken ct,
        int? idUsuarioRrhhElegido = null)
    {
        try
        {
            await EmitirNotificacionesInternasPaso1Async(db, sseNotifier, solicitud, dto, ct, idUsuarioRrhhElegido);
            return true;
        }
        catch (Exception ex)
        {
            if (db.Database.CurrentTransaction is not null) throw;
            db.ChangeTracker.Clear();
            RegistrarAdvertenciaPostCommit(db, ex, $"campana interna Paso 1 ({dto.Codigo})");
            return false;
        }
    }

    private static async Task<bool> EmitirNotificacionesInternasPaso2SeguraAsync(
        ApplicationDbContext db,
        INotificacionInternaNotifier sseNotifier,
        TblTimeReportSolicitud solicitud,
        PermisoItemDto dto,
        string decision,
        CancellationToken ct)
    {
        try
        {
            await EmitirNotificacionesInternasPaso2Async(db, sseNotifier, solicitud, dto, decision, ct);
            return true;
        }
        catch (Exception ex)
        {
            if (db.Database.CurrentTransaction is not null) throw;
            db.ChangeTracker.Clear();
            RegistrarAdvertenciaPostCommit(db, ex, $"campana interna Paso 2 ({dto.Codigo})");
            return false;
        }
    }

    private static async Task<bool> EmitirNotificacionesInternasPaso3SeguraAsync(
        ApplicationDbContext db,
        INotificacionInternaNotifier sseNotifier,
        TblTimeReportSolicitud solicitud,
        PermisoItemDto dto,
        bool confirmada,
        CancellationToken ct)
    {
        try
        {
            await EmitirNotificacionesInternasPaso3Async(db, sseNotifier, solicitud, dto, confirmada, ct);
            return true;
        }
        catch (Exception ex)
        {
            if (db.Database.CurrentTransaction is not null) throw;
            db.ChangeTracker.Clear();
            RegistrarAdvertenciaPostCommit(db, ex, $"campana interna Paso 3 ({dto.Codigo})");
            return false;
        }
    }

    private static async Task<bool> NotificarCorreoSeguroAsync(
        ApplicationDbContext db,
        IPermisosNotificacionService notificacionService,
        PermisoItemDto dto,
        string? correoColab,
        EventoNotificacionPermiso evento,
        int? idUsuarioRrhhElegido,
        CancellationToken ct) =>
        await NotificarCorreoSeguroAsync(db, notificacionService, dto, correoColab, evento, ct, idUsuarioRrhhElegido);

    private static async Task<bool> NotificarCorreoSeguroAsync(
        ApplicationDbContext db,
        IPermisosNotificacionService notificacionService,
        PermisoItemDto dto,
        string? correoColab,
        EventoNotificacionPermiso evento,
        CancellationToken ct,
        int? idUsuarioRrhhElegido = null)
    {
        try
        {
            return await notificacionService.NotificarCambioEstadoAsync(dto, correoColab, evento, ct, idUsuarioRrhhElegido);
        }
        catch (Exception ex)
        {
            if (db.Database.CurrentTransaction is not null) throw;
            db.ChangeTracker.Clear();
            RegistrarAdvertenciaPostCommit(db, ex, $"encolado de correo Outbox ({dto.Codigo} - {evento})");
            return false;
        }
    }

    private static void RegistrarAdvertenciaPostCommit(ApplicationDbContext db, Exception ex, string etapa)
    {
        try
        {
            var logger = db.GetService<ILoggerFactory>().CreateLogger("PermisosEndpoints");
            logger.LogWarning(
                ex,
                "La transacción principal se guardó, pero hubo una intermitencia al despachar {Etapa}.",
                etapa);
        }
        catch
        {
            // Jamás propagar un error de logging post-commit al usuario
        }
    }

    private static void RegistrarErrorInfraestructura(ApplicationDbContext db, Exception ex, string contexto)
    {
        try
        {
            var logger = db.GetService<ILoggerFactory>().CreateLogger("PermisosEndpoints");
            logger.LogError(ex, "{Contexto}", contexto);
        }
        catch
        {
            // Silenciar fallos secundarios de logging
        }
    }

    private static string TraducirErrorInfraestructuraAmigable(Exception ex, bool tieneArchivo)
    {
        if (ex is InvalidOperationException ioe
            && !ioe.Message.Contains("MinIO", StringComparison.OrdinalIgnoreCase)
            && !ioe.Message.Contains("S3", StringComparison.OrdinalIgnoreCase)
            && !ioe.Message.Contains("Bucket", StringComparison.OrdinalIgnoreCase)
            && !ioe.Message.Contains("Endpoint", StringComparison.OrdinalIgnoreCase))
        {
            return ioe.Message;
        }

        if (tieneArchivo)
        {
            return "En este momento no pudimos guardar el archivo adjunto. Tu información no fue duplicada; por favor inténtalo nuevamente en unos segundos.";
        }

        return "Tuvimos un inconveniente temporal al procesar tu solicitud. Por favor inténtalo nuevamente en unos segundos.";
    }
}
