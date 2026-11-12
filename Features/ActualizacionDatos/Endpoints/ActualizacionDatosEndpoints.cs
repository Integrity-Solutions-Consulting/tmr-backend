using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using tmr_backend.Features.ActualizacionDatos.DTOs;
using tmr_backend.Features.ActualizacionDatos.Domain;
using tmr_backend.Features.ActualizacionDatos.Services;
using tmr_backend.Features.ActualizacionDatos.Storage;
using tmr_backend.Infrastructure.Database;
using tmr_backend.Infrastructure.Shared;

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
        mine.MapPut("", async (ActualizacionRequest request, ActualizacionDirectaService service, CancellationToken ct) => { try { return Results.Ok(new { version = await service.SaveAsync(request, ct) }); } catch (DbUpdateConcurrencyException ex) { return Results.Conflict(new ProblemDetails { Detail = ex.Message, Status = 409 }); } catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return Results.BadRequest(new ProblemDetails { Detail=ex.Message, Status=400 }); } });
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
            var validSections = new[] { "Discapacidad", "Pareja", "Familiar", "Formacion", "Certificacion" }; if (!validSections.Contains(seccion, StringComparer.OrdinalIgnoreCase)) return Results.BadRequest(new { message = "La sección documental no es válida." });
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
        review.MapGet("", async (string? search,string? status,int page,int size,ActualizacionDatosService service,CancellationToken ct)=>Results.Ok(await service.ListAsync(search,status,Math.Max(1,page),Math.Clamp(size==0?20:size,1,100),ct)));
        review.MapGet("/{id:long}", async (long id,ActualizacionDatosService service,CancellationToken ct)=>(await service.DetailAsync(id,ct)) is { } x ? Results.Ok(x) : Results.NotFound());
        review.MapGet("/{id:long}/archivos/{archivoId:long}", async (long id,long archivoId,ApplicationDbContext db,IFileStorage storage,CancellationToken ct)=>{var file=await db.Set<ActualizacionArchivo>().SingleOrDefaultAsync(x=>x.Id==archivoId&&x.IdSolicitud==id,ct);if(file is null)return Results.NotFound();var stream=await storage.OpenReadAsync(file.StorageKey,ct,file.Provider);return stream is null?Results.NotFound():Results.File(stream,file.ContentType,file.NombreOriginal);});
    }
}
