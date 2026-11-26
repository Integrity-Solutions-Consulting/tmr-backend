using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using tmr_backend.Features.ActualizacionDatos.DTOs;
using tmr_backend.Features.ActualizacionDatos.Domain;
using tmr_backend.Features.ActualizacionDatos.Services;
using tmr_backend.Features.ActualizacionDatos.Storage;
using tmr_backend.Infrastructure.Database;
using tmr_backend.Infrastructure.Shared;
using tmr_backend.Shared.Validation;
using tmr_backend.Shared.Wrappers;

namespace tmr_backend.Features.ActualizacionDatos.Endpoints;

public static class ActualizacionDatosEndpoints
{
    public static void MapActualizacionDatosEndpoints(this IEndpointRouteBuilder app)
    {
        var mine = app.MapGroup("/api/mis-datos").RequireAuthorization().WithTags("Mis datos y documentos");
        mine.MapGet("", async (ActualizacionDirectaService service, ActualizacionDatosService legacy, CancellationToken ct) =>
        {
            var data = await service.GetAsync(ct);
            if (data is not null) return Results.Ok(data);
            var inactive = await legacy.HasInactiveEmployeeAsync(ct);
            return Results.Conflict(new ProblemDetails
            {
                Title = inactive ? "Colaborador inactivo" : "Vínculo de colaborador incompleto",
                Detail = inactive
                    ? "Tu usuario está vinculado a un colaborador inactivo. La edición de datos está bloqueada; solicita a RRHH revisar tu estado laboral."
                    : "No existe un colaborador activo asociado exactamente al usuario autenticado.",
                Status = StatusCodes.Status409Conflict,
                Extensions = { ["code"] = inactive ? "EMPLOYEE_INACTIVE" : "EMPLOYEE_LINK_MISSING" }
            });
        });
        mine.MapPut("", async (ActualizacionRequest request, ActualizacionDirectaService service, CancellationToken ct) =>
        {
            try
            {
                var saved = await service.SaveAsync(request, ct);
                return Results.Ok(new { version = saved.Version, validacionEnviada = saved.ValidacionEnviada });
            }
            catch (DbUpdateConcurrencyException ex)
            {
                return Results.Conflict(new ProblemDetails { Detail = ex.Message, Status = 409 });
            }
            catch (Exception ex) when (IsSerializationConflict(ex))
            {
                return Results.Conflict(new ProblemDetails { Detail = "El perfil cambió mientras guardabas. Recarga el perfil y vuelve a intentarlo.", Status = 409 });
            }
            catch (FieldValidationException ex)
            {
                return Results.BadRequest(ApiResponse<object>.Fail(400, "Error de validación", ex.Errors));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return Results.BadRequest(new ProblemDetails { Detail = ex.Message, Status = 400 });
            }
        });
        mine.MapGet("/revisiones", async (ActualizacionDirectaService service, CancellationToken ct) => Results.Ok(await service.MyReviewsAsync(ct)));
        mine.MapGet("/solicitudes", async (ActualizacionDirectaService service, CancellationToken ct) => Results.Ok(await service.SolicitudesAsync(ct)));
        mine.MapPost("/solicitudes", async (CrearSolicitudRequest request, ActualizacionDirectaService service, CancellationToken ct) => { try { return Results.Ok(new { id = await service.CrearSolicitudAsync(request.Asunto, request.Descripcion, request.Enviar, ct) }); } catch (ArgumentException ex) { return Results.BadRequest(new { message = ex.Message }); } });
        mine.MapPost("/solicitudes/{id:long}/archivos", async (long id, IFormFile file, ApplicationDbContext db, IFileStorage storage, ActualizacionDirectaService service, CancellationToken ct) =>
        {
            var owner = await service.Owner(ct); var solicitud = await db.Set<ColaboradorSolicitud>().SingleOrDefaultAsync(x => owner != null && x.Id == id && x.IdEmpleado == owner.Value.Employee.Id, ct); if (solicitud is null) return Results.NotFound();
            if (file.Length == 0 || file.Length > 10 * 1024 * 1024) return Results.BadRequest(new { message = "El archivo debe pesar entre 1 byte y 10 MB." }); var allowed = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase) {{ ".pdf", "application/pdf" }, { ".jpg", "image/jpeg" }, { ".jpeg", "image/jpeg" }, { ".png", "image/png" }}; var ext = Path.GetExtension(file.FileName); if (!allowed.ContainsKey(ext)) return Results.BadRequest(new { message = "Solo se admite PDF, JPG o PNG." });
            await using var input = file.OpenReadStream(); var head = new byte[8]; var n = await input.ReadAsync(head, ct); input.Position = 0; var real = (ext.Equals(".pdf", StringComparison.OrdinalIgnoreCase) && n >= 4 && head[0] == 0x25 && head[1] == 0x50 && head[2] == 0x44 && head[3] == 0x46) || (ext.Equals(".png", StringComparison.OrdinalIgnoreCase) && n >= 8 && head[0] == 137 && head[1] == 80 && head[2] == 78 && head[3] == 71) || ((ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase) || ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)) && n >= 3 && head[0] == 0xff && head[1] == 0xd8 && head[2] == 0xff); if (!real) return Results.BadRequest(new { message = "El contenido no coincide con la extensión." });
            StoredFile stored; try { stored = await storage.SaveAsync(input, ext, allowed[ext], ct); } catch (Exception ex) { return Results.Problem(ex.Message, statusCode: 500); } var row = new ColaboradorSolicitudArchivo { IdSolicitud = id, NombreOriginal = Path.GetFileName(file.FileName), ContentType = allowed[ext], Tamano = stored.Size, StorageKey = stored.Key, Provider = stored.Provider, Sha256 = stored.Sha256, FechaCreacion = DateTime.UtcNow }; db.Add(row); try { await db.SaveChangesAsync(ct); } catch { await storage.DeleteAsync(stored.Key, ct, stored.Provider); throw; } return Results.Ok(new DocumentoResponse(row.Id, row.NombreOriginal, row.ContentType, row.Tamano, row.FechaCreacion, "Solicitud", id));
        }).DisableAntiforgery();
        mine.MapGet("/solicitudes/{id:long}/archivos/{archivoId:long}", async (long id, long archivoId, ApplicationDbContext db, IFileStorage storage, ActualizacionDirectaService service, CancellationToken ct) => { var owner = await service.Owner(ct); var file = await (from a in db.Set<ColaboradorSolicitudArchivo>() join s in db.Set<ColaboradorSolicitud>() on a.IdSolicitud equals s.Id where a.Id == archivoId && s.Id == id && owner != null && s.IdEmpleado == owner.Value.Employee.Id select a).SingleOrDefaultAsync(ct); if (file is null) return Results.NotFound(); var stream = await storage.OpenReadAsync(file.StorageKey, ct, file.Provider); return stream is null ? Results.NotFound() : Results.File(stream, file.ContentType, file.NombreOriginal); });
        mine.MapPost("/archivos", async (string seccion, long? referenciaId, string? referenciaClave, long? replaceId, IFormFile file, ApplicationDbContext db, IFileStorage storage, ActualizacionDirectaService service, CancellationToken ct) =>
        {
            if (file.Length == 0 || file.Length > 10 * 1024 * 1024) return Results.BadRequest(new { message = "El archivo debe pesar entre 1 byte y 10 MB." });
            var allowed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [".pdf"] = "application/pdf", [".jpg"] = "image/jpeg", [".jpeg"] = "image/jpeg", [".png"] = "image/png" }; var ext = Path.GetExtension(file.FileName); if (!allowed.ContainsKey(ext)) return Results.BadRequest(new { message = "Solo se admite PDF, JPG o PNG." });
            var owner = await service.Owner(ct); if (owner is null) return Results.Conflict(new { message = "No existe un colaborador activo asociado." });
            try { await service.EnsureEditable(owner.Value.Employee.Id, ActualizacionDirectaService.DocumentSection(seccion), ct); }
            catch (DbUpdateConcurrencyException ex) { return Results.Conflict(new { message = ex.Message }); }
            var validSections = new[] { "Discapacidad", "Pareja", "Familiar", "Formacion", "Certificacion", "PartidaNacimiento" }; if (!validSections.Contains(seccion, StringComparer.OrdinalIgnoreCase)) return Results.BadRequest(new { message = "La sección documental no es válida." });
            if (string.Equals(seccion, "PartidaNacimiento", StringComparison.OrdinalIgnoreCase))
            {
                seccion = "PartidaNacimiento";
                if (replaceId is not null)
                    return Results.BadRequest(new { message = "Elimina el archivo anterior antes de adjuntar otra partida." });
                if (referenciaId is not null)
                {
                    var child = await db.Set<ColaboradorFamiliar>().AsNoTracking().SingleOrDefaultAsync(x =>
                        x.IdEmpleado == owner.Value.Employee.Id && x.Id == referenciaId && x.Activo && x.TipoRelacion == "Hijo", ct);
                    if (child is null)
                        return Results.BadRequest(new { message = "El hijo no esta disponible." });
                    referenciaClave = child.Clave;
                }
                else if (!Guid.TryParseExact(referenciaClave, "N", out _))
                {
                    return Results.BadRequest(new { message = "Agrega la ficha del hijo antes de adjuntar la partida." });
                }
            }
            await using var input = file.OpenReadStream(); var head = new byte[8]; var n = await input.ReadAsync(head, ct); input.Position = 0; var real = (ext.Equals(".pdf", StringComparison.OrdinalIgnoreCase) && n >= 4 && head[0] == 0x25 && head[1] == 0x50 && head[2] == 0x44 && head[3] == 0x46) || (ext.Equals(".png", StringComparison.OrdinalIgnoreCase) && n >= 8 && head[0] == 137 && head[1] == 80 && head[2] == 78 && head[3] == 71) || ((ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase) || ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)) && n >= 3 && head[0] == 0xff && head[1] == 0xd8 && head[2] == 0xff); if (!real) return Results.BadRequest(new { message = "El contenido no coincide con la extensión." });
            StoredFile stored; try { stored = await storage.SaveAsync(input, ext, allowed[ext], ct); } catch (Exception ex) { return Results.Problem(ex.Message, statusCode: 500); }
            var old = replaceId is null ? null : await db.Set<ColaboradorDocumento>().SingleOrDefaultAsync(x => x.Id == replaceId && x.IdEmpleado == owner.Value.Employee.Id && x.Activo, ct);
            var record = new ColaboradorDocumento { IdEmpleado = owner.Value.Employee.Id, Seccion = seccion, ReferenciaId = referenciaId, ReferenciaClave = referenciaClave, NombreOriginal = Path.GetFileName(file.FileName), ContentType = allowed[ext], Tamano = stored.Size, StorageKey = stored.Key, Provider = stored.Provider, Sha256 = stored.Sha256, FechaCreacion = DateTime.UtcNow, Activo = true }; db.Add(record); if (old is not null) old.Activo = false;
            try { await db.SaveChangesAsync(ct); } catch { await storage.DeleteAsync(stored.Key, ct, stored.Provider); throw; }
            if (old is not null) { try { await storage.DeleteAsync(old.StorageKey, ct, old.Provider); } catch { } }
            return Results.Ok(new DocumentoResponse(record.Id, record.NombreOriginal, record.ContentType, record.Tamano, record.FechaCreacion, record.Seccion, record.ReferenciaId, record.ReferenciaClave));
        }).DisableAntiforgery();
        mine.MapGet("/archivos/{archivoId:long}", async (long archivoId, ApplicationDbContext db, IFileStorage storage, ActualizacionDirectaService service, CancellationToken ct) => { var owner = await service.Owner(ct); var file = await db.Set<ColaboradorDocumento>().SingleOrDefaultAsync(x => owner != null && x.Id == archivoId && x.IdEmpleado == owner.Value.Employee.Id && x.Activo, ct); if (file is null) return Results.NotFound(); var stream = await storage.OpenReadAsync(file.StorageKey, ct, file.Provider); return stream is null ? Results.NotFound() : Results.File(stream, file.ContentType, file.NombreOriginal); });
        mine.MapDelete("/archivos/{archivoId:long}", async (long archivoId, IFileStorage storage, ActualizacionDirectaService service, CancellationToken ct) =>
        {
            try { return await service.DeleteDocumentAsync(archivoId, storage, ct) ? Results.NoContent() : Results.NotFound(); }
            catch (DbUpdateConcurrencyException ex) { return Results.Conflict(new { message = ex.Message }); }
            catch (Exception) { return Results.Problem("No fue posible eliminar el documento. Reintenta sin cerrar la página.", statusCode: 502); }
        });
        mine.MapDelete("/solicitudes/{id:long}/archivos/{archivoId:long}", async (long id, long archivoId, IFileStorage storage, ActualizacionDirectaService service, CancellationToken ct) =>
        {
            try { return await service.DeleteSolicitudDocumentAsync(id, archivoId, storage, ct) ? Results.NoContent() : Results.NotFound(); }
            catch (Exception) { return Results.Problem("No fue posible eliminar el documento. Reintenta sin cerrar la página.", statusCode: 502); }
        });
        mine.MapPost("/legacy/{id:long}/archivos", async (long id, string? seccion, long? referenciaId, IFormFile file, ApplicationDbContext db, IFileStorage storage, ActualizacionDatosService service, CancellationToken ct) => {
            if(file.Length==0) return Results.BadRequest(new {message="Archivo vacío."});
            var allowed = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase) {{".pdf","application/pdf"},{".jpg","image/jpeg"},{".jpeg","image/jpeg"},{".png","image/png"}}; var ext=Path.GetExtension(file.FileName); if(!allowed.ContainsKey(ext)) return Results.BadRequest(new {message="Solo se admite PDF, JPG o PNG."}); if(file.Length>10*1024*1024) return Results.BadRequest(new {message="El archivo supera 10 MB."});
            var owner=await service.GetOwnerAsync(ct); var request=await db.Set<ActualizacionSolicitud>().SingleOrDefaultAsync(x=>x.Id==id && owner!=null && x.IdEmpleado==owner.Value.Employee.Id && x.Estado!=EstadoActualizacion.Aprobada,ct); if(request is null) return Results.NotFound();
            await using var input=file.OpenReadStream(); var head=new byte[8]; var n=await input.ReadAsync(head,ct); input.Position=0; var real=(ext.Equals(".pdf",StringComparison.OrdinalIgnoreCase)&&n>=4&&head[0]==0x25&&head[1]==0x50&&head[2]==0x44&&head[3]==0x46)||(ext.Equals(".png",StringComparison.OrdinalIgnoreCase)&&n>=8&&head[0]==137&&head[1]==80&&head[2]==78&&head[3]==71)||(new[]{".jpg",".jpeg"}.Contains(ext.ToLowerInvariant())&&n>=3&&head[0]==0xff&&head[1]==0xd8&&head[2]==0xff); if(!real) return Results.BadRequest(new {message="El contenido no coincide con la extensión."});
            StoredFile stored; try { stored=await storage.SaveAsync(input,ext,allowed[ext],ct); } catch(Exception ex){return Results.Problem(ex.Message,statusCode:500);} var record=new ActualizacionArchivo{IdSolicitud=id,NombreOriginal=Path.GetFileName(file.FileName),ContentType=allowed[ext],Tamano=stored.Size,StorageKey=stored.Key,Provider=stored.Provider,Sha256=stored.Sha256,Seccion=string.IsNullOrWhiteSpace(seccion)?"General":seccion,ReferenciaId=referenciaId,Fechacreacion=DateTime.UtcNow}; db.Add(record); try{await db.SaveChangesAsync(ct);}catch{await storage.DeleteAsync(stored.Key,ct); throw;} return Results.Ok(new ArchivoResponse(record.Id,record.NombreOriginal,record.ContentType,record.Tamano,record.Fechacreacion));
        }).DisableAntiforgery();
        mine.MapGet("/legacy/archivos/{archivoId:long}", async (long archivoId, ApplicationDbContext db, IFileStorage storage, ActualizacionDatosService service, CancellationToken ct) => { var owner=await service.GetOwnerAsync(ct); var file=await (from a in db.Set<ActualizacionArchivo>() join s in db.Set<ActualizacionSolicitud>() on a.IdSolicitud equals s.Id where a.Id==archivoId && owner!=null && s.IdEmpleado==owner.Value.Employee.Id select a).SingleOrDefaultAsync(ct); if(file is null)return Results.NotFound(); var stream=await storage.OpenReadAsync(file.StorageKey,ct,file.Provider); return stream is null?Results.NotFound():Results.File(stream,file.ContentType,file.NombreOriginal); });

        var review=app.MapGroup("/api/actualizaciones-datos").RequireAuthorization("COLABORADORES_DATOS_REVISAR").WithTags("Revisión de datos");
        review.MapGet("/perfil", async (ActualizacionDirectaService service, CancellationToken ct) => Results.Ok(await service.ReviewQueueAsync(ct)));
        review.MapGet("/perfil/{id:long}", async (long id, ActualizacionDirectaService service, CancellationToken ct) =>
            await service.ReviewDetailAsync(id, ct) is { } detail ? Results.Ok(detail) : Results.NotFound());
        review.MapPut("/perfil/{id:long}", async (long id, PerfilDecision input, ActualizacionDirectaService service, CancellationToken ct) =>
        {
            try { return await service.DecideAsync(id, input.Estado, input.Observacion, ct) ? Results.NoContent() : Results.NotFound(); }
            catch (DbUpdateConcurrencyException ex) { return Results.Conflict(new { message = ex.Message }); }
            catch (Exception ex) when (IsSerializationConflict(ex))
            {
                return Results.Conflict(new { message = "La solicitud cambió mientras la revisabas. Recarga el perfil antes de continuar." });
            }
            catch (ArgumentException ex) { return Results.BadRequest(new { message = ex.Message }); }
            catch (FieldValidationException) { return Results.Conflict(new { message = "Los respaldos ya no estan disponibles." }); }
        });
        review.MapGet("/perfil/{id:long}/archivos/{fileId:long}", async (long id, long fileId, ApplicationDbContext db, IFileStorage storage, ILogger<ActualizacionDirectaService> logger, CancellationToken ct) =>
        {
            var request = await db.ColaboradorSolicitudes.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.Seccion != null, ct);
            if (request is null) return Results.NotFound();
            var file = await db.Set<ColaboradorDocumento>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == fileId && x.IdEmpleado == request.IdEmpleado && x.Activo, ct);
            if (file is null || ActualizacionDirectaService.DocumentSection(file.Seccion) != request.Seccion) return Results.NotFound();
            Stream? stream;
            try
            {
                stream = await storage.OpenReadAsync(file.StorageKey, ct, file.Provider);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "No se pudo leer el respaldo {ArchivoId} de la solicitud de perfil {SolicitudId}", fileId, id);
                return Results.Problem("No fue posible leer el respaldo en el almacenamiento.", statusCode: 502);
            }
            return stream is null ? Results.NotFound() : Results.File(stream, file.ContentType, file.NombreOriginal);
        });
        review.MapGet("", async (string? search,string? status,int page,int size,ActualizacionDatosService service,CancellationToken ct)=>Results.Ok(await service.ListAsync(search,status,Math.Max(1,page),Math.Clamp(size==0?20:size,1,100),ct)));
        review.MapGet("/{id:long}", async (long id,ActualizacionDatosService service,CancellationToken ct)=>(await service.DetailAsync(id,ct)) is { } x ? Results.Ok(x) : Results.NotFound());
        review.MapGet("/{id:long}/archivos/{archivoId:long}", async (long id,long archivoId,ApplicationDbContext db,IFileStorage storage,CancellationToken ct)=>{var file=await db.Set<ActualizacionArchivo>().SingleOrDefaultAsync(x=>x.Id==archivoId&&x.IdSolicitud==id,ct);if(file is null)return Results.NotFound();var stream=await storage.OpenReadAsync(file.StorageKey,ct,file.Provider);return stream is null?Results.NotFound():Results.File(stream,file.ContentType,file.NombreOriginal);});
    }
    public sealed record PerfilDecision(string Estado, string? Observacion);

    private static bool IsSerializationConflict(Exception exception) =>
        exception is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure }
        || exception is DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.SerializationFailure } };
}
