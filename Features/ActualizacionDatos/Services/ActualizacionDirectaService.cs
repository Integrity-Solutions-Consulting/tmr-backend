using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using tmr_backend.Features.ActualizacionDatos.Domain;
using tmr_backend.Features.ActualizacionDatos.DTOs;
using tmr_backend.Features.Notificaciones.Application.Estado;
using tmr_backend.Features.Notificaciones.Domain;
using tmr_backend.Infrastructure.Database;
using tmr_backend.Infrastructure.Database.Entities;
using tmr_backend.Infrastructure.Shared;
using tmr_backend.Features.ActualizacionDatos.Storage;
using tmr_backend.Shared.Validation;
using tmr_backend.Shared.Wrappers;

namespace tmr_backend.Features.ActualizacionDatos.Services;

public sealed class ActualizacionDirectaService(
    ApplicationDbContext db,
    ICurrentUserService currentUser,
    INotificacionInternaNotifier notificaciones)
{
    public async Task<MiDatosResponse?> GetAsync(CancellationToken ct)
    {
        var owner = await Owner(ct);
        if (owner is null) return null;
        return await GetProfileAsync(owner.Value.Employee, owner.Value.Person, ct);
    }

    private async Task<MiDatosResponse> GetProfileAsync(TblAdministracionEmpleado employee, TblAdministracionPersona person, CancellationToken ct)
    {

        var control = await db.Set<ColaboradorDatosControl>().AsNoTracking().SingleOrDefaultAsync(x => x.IdEmpleado == employee.Id, ct);
        var formaciones = await db.Set<ColaboradorFormacion>().AsNoTracking().Where(x => x.IdEmpleado == employee.Id && x.Activo).ToListAsync(ct);
        var certificaciones = await db.Set<ColaboradorCertificacion>().AsNoTracking().Where(x => x.IdEmpleado == employee.Id && x.Activo).ToListAsync(ct);
        var info = await db.Set<ColaboradorInformacionFamiliar>().AsNoTracking().SingleOrDefaultAsync(x => x.IdEmpleado == employee.Id, ct);
        var familiares = await db.Set<ColaboradorFamiliar>().AsNoTracking().Where(x => x.IdEmpleado == employee.Id && x.Activo).ToListAsync(ct);
        var docs = await db.Set<ColaboradorDocumento>().AsNoTracking().Where(x => x.IdEmpleado == employee.Id && x.Activo).ToListAsync(ct);
        var hasCedula = await db.TblAdministracionCatalogoDetalles.AsNoTracking()
            .AnyAsync(x => x.Id == person.Idtipoidentificacion && x.Codigovalor == "C", ct);
        var ownDisability = IsYes(info?.TieneDiscapacidad);
        var persona = new PersonaPropuesta(person.Nombres, person.Apellidos, person.Email, person.Telefono, person.Direccion, info?.EstadoCivil, NormalizeTriState(info?.TieneDiscapacidad), ownDisability ? info?.TipoDiscapacidad : null, ownDisability ? info?.PorcentajeDiscapacidad : null, person.Fechanacimiento);
        var familyRows = familiares.Select(x => ToFamily(x, docs)).ToList();
        var allowsPartner = string.Equals(info?.EstadoCivil, "Casado", StringComparison.OrdinalIgnoreCase) || string.Equals(info?.EstadoCivil, "UnionLibre", StringComparison.OrdinalIgnoreCase);
        var pareja = allowsPartner ? familyRows.FirstOrDefault(x => x.TipoRelacion == "Conyuge" || x.TipoRelacion == "Pareja") : null;
        var family = new InformacionFamiliarResponse(info?.EstadoCivil, NormalizeTriState(info?.TieneHijos), NormalizeTriState(info?.TieneDiscapacidad), ownDisability ? info?.TipoDiscapacidad : null, ownDisability ? info?.PorcentajeDiscapacidad : null, IsYes(info?.TieneHijos) ? familyRows.Where(x => x.TipoRelacion == "Hijo").ToList() : [], pareja);
        var old = await db.Set<ActualizacionSolicitud>().AsNoTracking().Where(x => x.IdEmpleado == employee.Id).OrderByDescending(x => x.Fechamodificacion).ToListAsync(ct);
        var recoverable = old.FirstOrDefault(x => x.Estado != EstadoActualizacion.Aprobada);
        return new(null, "Sin cambios", null, persona, formaciones.Select(x => ToFormacion(x, docs)).ToList(), certificaciones.Select(x => ToCertificacion(x, docs)).ToList(), ownDisability ? docs.Where(d => d.Seccion == "Discapacidad").Select(x => new ArchivoResponse(x.Id, x.NombreOriginal, x.ContentType, x.Tamano, x.FechaCreacion)).ToList() : [], family, control?.Version ?? 0, [], recoverable is null ? null : await LegacyDetail(recoverable, ct), hasCedula ? person.Numeroidentificacion : null, control?.ConocimientosJson is { } json ? JsonSerializer.Deserialize<ConocimientoDto[]>(json) : [], person.Fechanacimiento);
    }

    public async Task<(long Version, bool ValidacionEnviada)> SaveAsync(ActualizacionRequest input, CancellationToken ct)
    {
        var owner = await Owner(ct) ?? throw new UnauthorizedAccessException("El usuario no tiene un colaborador activo asociado.");
        var section = input.Seccion;
        if (section is not (null or "personal" or "familia" or "formacion" or "certificaciones" or "conocimientos"))
            throw new ArgumentException("La seccion no es valida.");
        bool Includes(string name) => section is null || section == name;
        input = input with
        {
            Persona = Includes("personal") ? input.Persona : new PersonaPropuesta("", "", null, null, null),
            Familia = Includes("familia") ? input.Familia : null,
            Formaciones = Includes("formacion") ? input.Formaciones : [],
            Certificaciones = Includes("certificaciones") ? input.Certificaciones : [],
            Conocimientos = Includes("conocimientos") ? input.Conocimientos : null
        };
        Validate(input);
        if (section is null) throw new ArgumentException("Selecciona una seccion.");
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        if (section != "conocimientos") await EnsureEditable(owner.Employee.Id, section, ct);
        var control = await db.Set<ColaboradorDatosControl>().SingleOrDefaultAsync(x => x.IdEmpleado == owner.Employee.Id, ct);
        await ValidateDocuments(input, owner.Employee.Id, ct);
        if ((input.ControlVersion ?? 0) != (control?.Version ?? 0))
            throw new DbUpdateConcurrencyException("Recarga el perfil antes de guardar.");
        if (section == "conocimientos")
        {
            if (input.Conocimientos is null) throw new ArgumentException("Indica los conocimientos que deseas guardar.");
            await ApplyAsync(input, owner.Employee, owner.Person, ct);
            var pending = await db.ColaboradorSolicitudes.Where(x => x.IdEmpleado == owner.Employee.Id &&
                x.Seccion == section && (x.EstadoRevision == "EnRevision" || x.EstadoRevision == "RequiereCorreccion")).ToListAsync(ct);
            foreach (var old in pending) old.EstadoRevision = "Reenviada";
        }
        var previous = await db.ColaboradorSolicitudes.SingleOrDefaultAsync(x =>
            x.IdEmpleado == owner.Employee.Id && x.Seccion == section && x.EstadoRevision == "RequiereCorreccion", ct);
        if (previous is not null)
        {
            previous.EstadoRevision = "Reenviada";
            await db.SaveChangesAsync(ct);
        }
        var now = DateTime.UtcNow;
        db.Add(new ColaboradorSolicitud
        {
            IdEmpleado = owner.Employee.Id, Seccion = section, Asunto = section,
            Descripcion = "Revision de cambios del perfil", Enviada = true,
            FechaCreacion = now, FechaEnvio = now, EstadoRevision = section == "conocimientos" ? "Guardada" : "EnRevision",
            Propuesta = JsonSerializer.Serialize(input)
        });
        List<int> revisores = [];
        if (section != "conocimientos")
        {
            revisores = await db.TblAutenticacionUsuarioRols.AsNoTracking()
                .Where(ur => ur.Activo && ur.IdusuarioNavigation.Activo && ur.IdrolNavigation.Activo &&
                    (ur.IdrolNavigation.Nombre.ToUpper() == "RECURSOS HUMANOS" ||
                     ur.IdrolNavigation.Nombre.ToUpper() == "RECURSOS_HUMANOS") &&
                    ur.IdrolNavigation.TblAutenticacionRolPermisos.Any(rp => rp.Activo &&
                        rp.IdpermisoNavigation.Activo && rp.IdpermisoNavigation.Codigo == "COLABORADORES_DATOS_REVISAR"))
                .Select(ur => ur.Idusuario)
                .Distinct()
                .ToListAsync(ct);

            var colaborador = $"{owner.Person.Nombres} {owner.Person.Apellidos}".Trim();
            db.NotificacionesUsuario.AddRange(revisores.Select(idUsuario => new NotificacionUsuario
            {
                IdUsuarioDestino = idUsuario,
                Titulo = "Solicitud de perfil pendiente de revisión",
                Mensaje = $"{colaborador} envió a revisión la sección {NombreSeccion(section)}.",
                UrlDestino = $"/revision-perfiles/{owner.Employee.Id}",
                FechaCreacion = now
            }));
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        if (revisores.Count > 0) notificaciones.NotificarUsuarios(revisores);
        return (control?.Version ?? db.Set<ColaboradorDatosControl>().Local.SingleOrDefault(x => x.IdEmpleado == owner.Employee.Id)?.Version ?? 0, section != "conocimientos");
    }

    private async Task ApplyAsync(ActualizacionRequest input, TblAdministracionEmpleado employee, TblAdministracionPersona person, CancellationToken ct)
    {
        var section = input.Seccion;
        bool Includes(string name) => section == name;
        await ValidateDocuments(input, employee.Id, ct);
        var control = await db.Set<ColaboradorDatosControl>().SingleOrDefaultAsync(x => x.IdEmpleado == employee.Id, ct);
        if (control is null)
        {
            control = new ColaboradorDatosControl { IdEmpleado = employee.Id };
            db.Add(control);
        }
        control.Version++;
        control.FechaModificacion = DateTime.UtcNow;
        if (input.Conocimientos is { } conocimientos)
            control.ConocimientosJson = JsonSerializer.Serialize(conocimientos.Select(x => x with { Area = x.Area.Trim() }));
        var employeeId = employee.Id;
        if (Includes("personal"))
        {
            person.Email = input.Persona.Email?.Trim();
            person.Telefono = TelefonoRules.Normalize(input.Persona.Telefono);
            person.Direccion = input.Persona.Direccion?.Trim();
            if (input.Persona.FechaNacimiento is { } fechaNacimiento)
                person.Fechanacimiento = fechaNacimiento;
            person.Fechamodificacion = DateTime.UtcNow;
            person.Usuariomodificacion = currentUser.Email;
        }

        if (Includes("personal") || Includes("familia"))
        {
            var info = await db.Set<ColaboradorInformacionFamiliar>().SingleOrDefaultAsync(x => x.IdEmpleado == employeeId, ct)
                ?? new ColaboradorInformacionFamiliar { IdEmpleado = employeeId };
            if (Includes("familia"))
            {
                info.EstadoCivil = input.Familia?.EstadoCivil;
                info.TieneHijos = NormalizeTriState(input.Familia?.TieneHijos);
            }
            if (Includes("personal"))
            {
                info.TieneDiscapacidad = NormalizeTriState(input.Persona.TieneDiscapacidad);
                info.TipoDiscapacidad = IsYes(input.Persona.TieneDiscapacidad) ? input.Persona.TipoDiscapacidad : null;
                info.PorcentajeDiscapacidad = IsYes(input.Persona.TieneDiscapacidad) ? input.Persona.PorcentajeDiscapacidad : null;
            }
            if (db.Entry(info).State == EntityState.Detached) db.Add(info);
        }

        var documentsToDelete = Includes("personal") || Includes("familia")
            ? await DocumentsToDeleteAsync(employeeId, input, ct)
            : [];
        if (Includes("formacion")) await UpsertFormaciones(employeeId, input.Formaciones, ct);
        if (Includes("certificaciones")) await UpsertCertificaciones(employeeId, input.Certificaciones, ct);
        if (Includes("familia")) await UpsertFamilies(employeeId, NormalizeFamily(input.Familia), ct);

        foreach (var document in documentsToDelete) document.Activo = false;
    }

    public async Task EnsureEditable(int employeeId, string section, CancellationToken ct)
    {
        if (await db.ColaboradorSolicitudes.AnyAsync(x => x.IdEmpleado == employeeId &&
            x.Seccion == section && x.EstadoRevision == "EnRevision", ct))
            throw new DbUpdateConcurrencyException("La seccion esta en revision por Recursos Humanos.");
    }

    public static string DocumentSection(string section) => section.ToLowerInvariant() switch
    {
        "discapacidad" => "personal",
        "formacion" => "formacion",
        "certificacion" => "certificaciones",
        _ => "familia"
    };

    public async Task<object> MyReviewsAsync(CancellationToken ct)
    {
        var owner = await Owner(ct);
        if (owner is null) return Array.Empty<object>();
        var rows = await db.ColaboradorSolicitudes.AsNoTracking().Where(x => x.IdEmpleado == owner.Value.Employee.Id && x.Seccion != "conocimientos" &&
            (x.EstadoRevision == "EnRevision" || x.EstadoRevision == "RequiereCorreccion")).ToListAsync(ct);
        var docs = await db.Set<ColaboradorDocumento>().AsNoTracking().Where(x => x.IdEmpleado == owner.Value.Employee.Id && x.Activo).ToListAsync(ct);
        return rows.Select(x => new { x.Seccion, Estado = x.EstadoRevision, Observacion = x.ObservacionRevision,
            Documentos = docs.Where(d => DocumentSection(d.Seccion) == x.Seccion).Select(ToDoc),
            Propuesta = JsonSerializer.Deserialize<ActualizacionRequest>(x.Propuesta!) });
    }

    public async Task<object> ReviewQueueAsync(CancellationToken ct)
    {
        var rows = await (from request in db.ColaboradorSolicitudes.AsNoTracking()
                          join employee in db.TblAdministracionEmpleados on request.IdEmpleado equals employee.Id
                          where request.Seccion != null
                          orderby request.FechaCreacion descending
                          select new { request.Id, request.IdEmpleado, request.FechaCreacion, request.Seccion,
                              Estado = request.EstadoRevision,
                              Nombre = employee.IdpersonaNavigation.Nombres + " " + employee.IdpersonaNavigation.Apellidos,
                              Cedula = employee.IdpersonaNavigation.Numeroidentificacion,
                              Telefono = employee.IdpersonaNavigation.Telefono,
                              Correo = employee.IdpersonaNavigation.Email }).ToListAsync(ct);
        return rows.GroupBy(x => x.IdEmpleado).Select(group => new { IdEmpleado = group.Key,
            group.First().Nombre, group.First().Cedula, group.First().Telefono, group.First().Correo, Solicitudes = group.ToArray() });
    }

    public async Task<object?> ReviewDetailAsync(long id, CancellationToken ct)
    {
        var row = await db.ColaboradorSolicitudes.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.Seccion != null, ct);
        if (row is null) return null;
        var employee = await db.TblAdministracionEmpleados.Include(x => x.IdpersonaNavigation).SingleAsync(x => x.Id == row.IdEmpleado, ct);
        var docs = await db.Set<ColaboradorDocumento>().AsNoTracking().Where(x => x.IdEmpleado == employee.Id && x.Activo).ToListAsync(ct);
        return new { row.Id, row.Seccion, Estado = row.EstadoRevision, Observacion = row.ObservacionRevision,
            row.Revisor, row.FechaRevision,
            Vigente = await GetProfileAsync(employee, employee.IdpersonaNavigation, ct),
            Propuesta = JsonSerializer.Deserialize<ActualizacionRequest>(row.Propuesta!),
            Documentos = docs.Where(x => DocumentSection(x.Seccion) == row.Seccion).Select(ToDoc) };
    }

    private static string NombreSeccion(string seccion) => seccion switch
    {
        "personal" => "Información personal",
        "familia" => "Información familiar",
        "formacion" => "Formación académica",
        "certificaciones" => "Certificaciones",
        _ => seccion
    };

    public async Task<bool> DecideAsync(long id, string estado, string? observacion, CancellationToken ct)
    {
        if (estado is not ("EnRevision" or "Aprobada" or "RequiereCorreccion"))
            throw new ArgumentException("Selecciona un estado valido.");
        if (estado == "RequiereCorreccion" && string.IsNullOrWhiteSpace(observacion))
            throw new ArgumentException("Indica el campo o documento que debe corregirse.");
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var row = await db.ColaboradorSolicitudes.SingleOrDefaultAsync(x => x.Id == id && x.Seccion != null, ct);
        if (row is null) return false;
        if (row.Seccion == "conocimientos") throw new ArgumentException("Los conocimientos se guardan directamente.");
        if (row.EstadoRevision != "EnRevision") throw new DbUpdateConcurrencyException("La solicitud ya fue revisada.");
        List<int> destinatarios = [];
        if (estado == "Aprobada")
        {
            var employee = await db.TblAdministracionEmpleados.Include(x => x.IdpersonaNavigation).SingleAsync(x => x.Id == row.IdEmpleado, ct);
            await ApplyAsync(JsonSerializer.Deserialize<ActualizacionRequest>(row.Propuesta!)!, employee, employee.IdpersonaNavigation, ct);
        }
        if (estado is "Aprobada" or "RequiereCorreccion")
        {
            var seccion = NombreSeccion(row.Seccion);
            var aprobada = estado == "Aprobada";
            destinatarios = await (from usuario in db.TblAutenticacionUsuarios.AsNoTracking()
                                   join empleado in db.TblAdministracionEmpleados.AsNoTracking()
                                       on usuario.Idpersona equals empleado.Idpersona
                                   where empleado.Id == row.IdEmpleado && usuario.Activo
                                   select usuario.Id).ToListAsync(ct);
            db.NotificacionesUsuario.AddRange(destinatarios.Select(idUsuario => new NotificacionUsuario
            {
                IdUsuarioDestino = idUsuario,
                Titulo = aprobada ? "Cambios de perfil aprobados" : "Corrección de perfil solicitada",
                Mensaje = aprobada
                    ? $"Recursos Humanos aprobó los cambios en {seccion}."
                    : $"Recursos Humanos solicitó una corrección en {seccion}: {observacion?.Trim()}",
                UrlDestino = aprobada ? "/mi-perfil" : $"/mis-datos?tab={row.Seccion}",
                FechaCreacion = DateTime.UtcNow
            }));
        }
        row.EstadoRevision = estado;
        row.ObservacionRevision = observacion?.Trim();
        row.Revisor = currentUser.Email;
        row.FechaRevision = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        if (destinatarios.Count > 0) notificaciones.NotificarUsuarios(destinatarios);
        return true;
    }

    public async Task<IReadOnlyList<SolicitudPropiaResponse>> SolicitudesAsync(CancellationToken ct)
    {
        var owner = await Owner(ct); if (owner is null) return [];
        return await db.Set<ColaboradorSolicitud>().AsNoTracking().Include(x => x.Archivos).Where(x => x.IdEmpleado == owner.Value.Employee.Id).OrderByDescending(x => x.FechaCreacion).Select(x => new SolicitudPropiaResponse(x.Id, x.Asunto, x.Descripcion, x.FechaCreacion, x.FechaEnvio, x.Enviada, x.Archivos.Select(a => new DocumentoResponse(a.Id, a.NombreOriginal, a.ContentType, a.Tamano, a.FechaCreacion, "Solicitud", x.Id)).ToList())).ToListAsync(ct);
    }

    public async Task<long> CrearSolicitudAsync(string asunto, string descripcion, bool enviar, CancellationToken ct)
    {
        var owner = await Owner(ct) ?? throw new UnauthorizedAccessException("El usuario no tiene un colaborador activo asociado.");
        if (string.IsNullOrWhiteSpace(asunto) || string.IsNullOrWhiteSpace(descripcion)) throw new ArgumentException("Asunto y descripción son obligatorios.");
        var row = new ColaboradorSolicitud { IdEmpleado = owner.Employee.Id, Asunto = asunto.Trim(), Descripcion = descripcion.Trim(), FechaCreacion = DateTime.UtcNow, Enviada = enviar, FechaEnvio = enviar ? DateTime.UtcNow : null };
        db.Add(row); await db.SaveChangesAsync(ct); return row.Id;
    }

    public async Task<bool> DeleteDocumentAsync(long documentId, IFileStorage storage, CancellationToken ct)
    {
        var owner = await Owner(ct);
        var row = owner is null
            ? null
            : await db.Set<ColaboradorDocumento>().SingleOrDefaultAsync(x => x.Id == documentId && x.IdEmpleado == owner.Value.Employee.Id && x.Activo, ct);
        if (row is null) return false;
        await EnsureEditable(row.IdEmpleado, DocumentSection(row.Seccion), ct);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            // El objeto se elimina antes de confirmar el soft-delete de metadata. Si falla,
            // la transacción se revierte y el documento sigue disponible para reintentar.
            await storage.DeleteAsync(row.StorageKey, ct, row.Provider);
            row.Activo = false;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return true;
        }
        catch
        {
            await tx.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<bool> DeleteSolicitudDocumentAsync(long solicitudId, long documentId, IFileStorage storage, CancellationToken ct)
    {
        var owner = await Owner(ct);
        var row = owner is null
            ? null
            : await (from a in db.Set<ColaboradorSolicitudArchivo>()
                     join s in db.Set<ColaboradorSolicitud>() on a.IdSolicitud equals s.Id
                     where a.Id == documentId && s.Id == solicitudId && s.IdEmpleado == owner.Value.Employee.Id
                     select a).SingleOrDefaultAsync(ct);
        if (row is null) return false;

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            await storage.DeleteAsync(row.StorageKey, ct, row.Provider);
            db.Remove(row);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return true;
        }
        catch
        {
            await tx.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<(TblAdministracionEmpleado Employee, TblAdministracionPersona Person)?> Owner(CancellationToken ct) => await (from u in db.TblAutenticacionUsuarios join e in db.TblAdministracionEmpleados on u.Idpersona equals e.Idpersona where u.Id == currentUser.UserId && u.Activo && e.Activo select new { Employee = e, Person = e.IdpersonaNavigation }).FirstOrDefaultAsync(ct) is { } x ? (x.Employee, x.Person) : null;

    private async Task UpsertFormaciones(int id, IReadOnlyList<FormacionRequest> values, CancellationToken ct)
    {
        var rows = await db.Set<ColaboradorFormacion>().Where(x => x.IdEmpleado == id).ToListAsync(ct); var keys = values.Select(x => x.Clave).Where(x => !string.IsNullOrWhiteSpace(x)).ToHashSet();
        foreach (var row in rows) { if (!keys.Contains(row.Clave) && !values.Any(x => x.Id == row.Id)) row.Activo = false; }
        foreach (var x in values) { var row = rows.FirstOrDefault(r => x.Id == r.Id || (!string.IsNullOrWhiteSpace(x.Clave) && r.Clave == x.Clave)) ?? new ColaboradorFormacion { IdEmpleado = id, Clave = string.IsNullOrWhiteSpace(x.Clave) ? Guid.NewGuid().ToString("N") : x.Clave! }; row.Nivel = x.Nivel.Trim(); row.Institucion = x.Institucion.Trim(); row.Titulo = x.Titulo.Trim(); row.FechaInicio = x.FechaInicio; row.FechaFin = x.FechaFin; row.Activo = true; if (row.Id == 0) db.Add(row); }
    }

    private async Task UpsertCertificaciones(int id, IReadOnlyList<CertificacionRequest> values, CancellationToken ct)
    {
        var rows = await db.Set<ColaboradorCertificacion>().Where(x => x.IdEmpleado == id).ToListAsync(ct); var keys = values.Select(x => x.Clave).Where(x => !string.IsNullOrWhiteSpace(x)).ToHashSet();
        foreach (var row in rows) { if (!keys.Contains(row.Clave) && !values.Any(x => x.Id == row.Id)) row.Activo = false; }
        foreach (var x in values) { var row = rows.FirstOrDefault(r => x.Id == r.Id || (!string.IsNullOrWhiteSpace(x.Clave) && r.Clave == x.Clave)) ?? new ColaboradorCertificacion { IdEmpleado = id, Clave = string.IsNullOrWhiteSpace(x.Clave) ? Guid.NewGuid().ToString("N") : x.Clave! }; row.Nombre = x.Nombre.Trim(); row.EntidadEmisora = x.EntidadEmisora.Trim(); row.FechaEmision = x.FechaEmision; row.FechaVencimiento = x.FechaVencimiento; row.Activo = true; if (row.Id == 0) db.Add(row); }
    }

    private async Task UpsertFamilies(int id, InformacionFamiliarRequest? family, CancellationToken ct)
    {
        if (family is null) return;
        var all = family.Familiares.Concat(family.Pareja is null ? [] : [family.Pareja]).ToList(); var rows = await db.Set<ColaboradorFamiliar>().Where(x => x.IdEmpleado == id && x.Activo).ToListAsync(ct);
        foreach (var row in rows.Where(r => r.TipoRelacion == "Hijo" && !all.Any(v => v.Id == r.Id || (!string.IsNullOrWhiteSpace(v.Clave) && v.Clave == r.Clave)))) row.Activo = false;
        foreach (var x in all) { var row = rows.FirstOrDefault(r => x.Id == r.Id || (!string.IsNullOrWhiteSpace(x.Clave) && x.Clave == r.Clave)) ?? new ColaboradorFamiliar { IdEmpleado = id, Clave = string.IsNullOrWhiteSpace(x.Clave) ? Guid.NewGuid().ToString("N") : x.Clave! }; row.TipoRelacion = x.TipoRelacion; row.Nombres = NombrePersonaRules.Normalize(x.Nombres); row.Apellidos = NombrePersonaRules.Normalize(x.Apellidos); row.Identificacion = x.Identificacion?.Trim(); row.FechaNacimiento = x.FechaNacimiento; row.TieneDiscapacidad = x.TieneDiscapacidad; row.TipoDiscapacidad = x.TipoDiscapacidad; row.PorcentajeDiscapacidad = x.PorcentajeDiscapacidad; row.Activo = true; if (row.Id == 0) db.Add(row); }
    }

    private static readonly HashSet<string> NivelesEducativos = new(StringComparer.Ordinal)
    {
        "Educaci\u00f3n B\u00e1sica (Primaria)",
        "Bachillerato (Secundaria)",
        "T\u00e9cnico / Tecnol\u00f3gico Superior",
        "Universitario / Tercer Nivel (Grado)",
        "Especializaci\u00f3n / Maestr\u00eda",
        "Doctorado (PhD)"
    };


    private static void Validate(ActualizacionRequest x)
    {
        var errors = new List<ApiError>();
        void Add(string field, string message) => errors.Add(new ApiError(field, message, "VALIDATION_ERROR"));

        if (!TelefonoRules.IsValid(x.Persona.Telefono)) Add("persona.telefono", TelefonoRules.Message);
        if (!string.IsNullOrWhiteSpace(x.Persona.Email) && !System.Net.Mail.MailAddress.TryCreate(x.Persona.Email.Trim(), out _)) Add("persona.email", "Ingresa un correo electrónico válido");
        if (IsYes(x.Persona.TieneDiscapacidad))
        {
            if (x.Persona.PorcentajeDiscapacidad is null) Add("persona.porcentajeDiscapacidad", "Ingresa el porcentaje de discapacidad.");
            if (string.IsNullOrWhiteSpace(x.Persona.TipoDiscapacidad)) Add("persona.tipoDiscapacidad", "Selecciona al menos un tipo de discapacidad.");
        }
        if (x.Persona.PorcentajeDiscapacidad is < 0 or > 100) Add("persona.porcentajeDiscapacidad", "El porcentaje debe estar entre 0 y 100.");

        for (var i = 0; i < (x.Conocimientos?.Count ?? 0); i++)
        {
            var item = x.Conocimientos![i];
            if (item is null || string.IsNullOrWhiteSpace(item.Area) || item.Area.Trim().Length > 120 ||
                !System.Text.RegularExpressions.Regex.IsMatch(item.Area, @"\A(?=.*[\p{L}0-9])[\p{L}0-9 ./+#()-]+\z"))
                Add($"conocimientos[{i}].area", "Ingresa un area de conocimiento valida.");
            if (item is not null && (item.Porcentaje is null or < 0 or > 100))
                Add($"conocimientos[{i}].porcentaje", "Ingresa un porcentaje entero entre 0 y 100.");
        }

        for (var i = 0; i < x.Formaciones.Count; i++)
        {
            var formation = x.Formaciones[i];
            if (string.IsNullOrWhiteSpace(formation.Nivel)) Add($"formaciones[{i}].nivel", "Este campo es obligatorio.");
            else if (x.Seccion == "formacion" && !NivelesEducativos.Contains(formation.Nivel))
                Add($"formaciones[{i}].nivel", "Selecciona uno de los niveles educativos de la lista.");
            if (formation.FechaInicio is { } inicio && formation.FechaFin is { } fin && inicio > fin)
            {
                Add($"formaciones[{i}].fechaInicio", "La fecha de inicio debe ser anterior o igual a la fecha de fin.");
                Add($"formaciones[{i}].fechaFin", "La fecha de fin debe ser posterior o igual a la fecha de inicio.");
            }
            if (string.IsNullOrWhiteSpace(formation.Institucion)) Add($"formaciones[{i}].institucion", "Este campo es obligatorio.");
            if (string.IsNullOrWhiteSpace(formation.Titulo)) Add($"formaciones[{i}].titulo", "Este campo es obligatorio.");
        }
        for (var i = 0; i < x.Certificaciones.Count; i++)
        {
            var certification = x.Certificaciones[i];
            if (string.IsNullOrWhiteSpace(certification.Nombre)) Add($"certificaciones[{i}].nombre", "Este campo es obligatorio.");
            if (string.IsNullOrWhiteSpace(certification.EntidadEmisora)) Add($"certificaciones[{i}].entidadEmisora", "Este campo es obligatorio.");
            if (x.Seccion == "certificaciones")
            {
                if (!string.IsNullOrWhiteSpace(certification.Nombre) &&
                    !System.Text.RegularExpressions.Regex.IsMatch(certification.Nombre, @"\A(?=.*[\p{L}0-9])[\p{L}\p{M}0-9 .,:/+#&()'\u2013\u2014-]+\z"))
                    Add($"certificaciones[{i}].nombre", "Ingresa un nombre de certificado con letras, numeros y puntuacion habitual.");
                if (!string.IsNullOrWhiteSpace(certification.EntidadEmisora) &&
                    !System.Text.RegularExpressions.Regex.IsMatch(certification.EntidadEmisora, @"\A(?=.*\p{L})[\p{L}\p{M}0-9 .&()'\u2013\u2014-]+\z"))
                    Add($"certificaciones[{i}].entidadEmisora", "Ingresa una entidad emisora valida.");
            }
        }
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (x.Persona.FechaNacimiento > today)
            Add("persona.fechaNacimiento", "La fecha de nacimiento no puede ser futura.");
        for (var i = 0; i < x.Certificaciones.Count; i++)
        {
            var certification = x.Certificaciones[i];
            if (certification.FechaEmision > today) Add($"certificaciones[{i}].fechaEmision", "La fecha de emisión no puede ser futura.");
            if (certification.FechaVencimiento is { } vencimiento && vencimiento <= certification.FechaEmision)
            {
                Add($"certificaciones[{i}].fechaEmision", "La fecha de emision debe ser anterior al vencimiento.");
                Add($"certificaciones[{i}].fechaVencimiento", "El vencimiento debe ser posterior a la fecha de emision.");
            }
        }
        for (var i = 0; i < (x.Familia?.Familiares.Count ?? 0); i++)
        {
            var family = x.Familia!.Familiares[i];
            var prefix = $"familia.familiares[{i}]";
            if (!NombrePersonaRules.IsValid(family.Nombres)) Add($"{prefix}.nombres", NombrePersonaRules.Message);
            if (!NombrePersonaRules.IsValid(family.Apellidos)) Add($"{prefix}.apellidos", NombrePersonaRules.Message);
            if (family.FechaNacimiento > today) Add($"{prefix}.fechaNacimiento", "La fecha de nacimiento no puede ser futura.");
            if (!string.IsNullOrWhiteSpace(family.Identificacion) && !System.Text.RegularExpressions.Regex.IsMatch(family.Identificacion, "^\\d{10}$")) Add($"{prefix}.identificacion", "La identificación debe contener exactamente 10 dígitos.");
            if (IsYes(family.TieneDiscapacidad))
            {
                if (family.PorcentajeDiscapacidad is null) Add($"{prefix}.porcentajeDiscapacidad", "Ingresa el porcentaje de discapacidad.");
                if (string.IsNullOrWhiteSpace(family.TipoDiscapacidad)) Add($"{prefix}.tipoDiscapacidad", "Selecciona al menos un tipo de discapacidad.");
            }
            if (family.PorcentajeDiscapacidad is < 0 or > 100) Add($"{prefix}.porcentajeDiscapacidad", "El porcentaje debe estar entre 0 y 100.");
        }
        var partner = x.Familia?.Pareja;
        if (partner is not null)
        {
            if (!NombrePersonaRules.IsValid(partner.Nombres, false)) Add("familia.pareja.nombres", NombrePersonaRules.Message);
            if (!NombrePersonaRules.IsValid(partner.Apellidos, false)) Add("familia.pareja.apellidos", NombrePersonaRules.Message);
            if (partner.FechaNacimiento > today) Add("familia.pareja.fechaNacimiento", "La fecha de nacimiento no puede ser futura.");
            if (!string.IsNullOrWhiteSpace(partner.Identificacion) && !System.Text.RegularExpressions.Regex.IsMatch(partner.Identificacion, "^\\d{10}$")) Add("familia.pareja.identificacion", "La identificación debe contener exactamente 10 dígitos.");
        }
        if (errors.Count > 0) throw new FieldValidationException(errors);
    }
    private static InformacionFamiliarRequest? NormalizeFamily(InformacionFamiliarRequest? family)
    {
        if (family is null) return null;
        var hasChildren = IsYes(family.TieneHijos);
        var children = hasChildren
            ? family.Familiares.Where(x => string.Equals(x.TipoRelacion, "Hijo", StringComparison.OrdinalIgnoreCase)).Select(NormalizeFamilyMember).ToList()
            : [];
        var allowsPartner = string.Equals(family.EstadoCivil, "Casado", StringComparison.OrdinalIgnoreCase) || string.Equals(family.EstadoCivil, "UnionLibre", StringComparison.OrdinalIgnoreCase);
        var partner = allowsPartner && family.Pareja is not null ? NormalizeFamilyMember(family.Pareja) : null;
        return family with { TieneHijos = hasChildren ? "Si" : "NoInformado", Familiares = children, Pareja = partner };
    }

    private static FamiliarRequest NormalizeFamilyMember(FamiliarRequest x) => IsYes(x.TieneDiscapacidad)
        ? x with { TieneDiscapacidad = "Si" }
        : x with { TieneDiscapacidad = NormalizeTriState(x.TieneDiscapacidad), TipoDiscapacidad = null, PorcentajeDiscapacidad = null, LimpiarDiscapacidad = true };

    private async Task<List<ColaboradorDocumento>> DocumentsToDeleteAsync(int employeeId, ActualizacionRequest input, CancellationToken ct)
    {
        var active = await db.Set<ColaboradorDocumento>().Where(x => x.IdEmpleado == employeeId && x.Activo).ToListAsync(ct);
        var ids = new HashSet<long>();
        void Add(IEnumerable<ColaboradorDocumento> values) { foreach (var value in values) ids.Add(value.Id); }
        if (input.Seccion is (null or "personal") && (!IsYes(input.Persona.TieneDiscapacidad) || input.LimpiarDiscapacidad))
            Add(active.Where(x => x.Seccion == "Discapacidad"));
        var family = input.Familia;
        if (family is null) return active.Where(x => ids.Contains(x.Id)).ToList();
        var rows = await db.Set<ColaboradorFamiliar>().Where(x => x.IdEmpleado == employeeId && x.Activo).ToListAsync(ct);
        var children = rows.Where(x => x.TipoRelacion == "Hijo").ToList();
        if (!IsYes(family.TieneHijos) || family.LimpiarHijos)
            Add(active.Where(x => (x.Seccion == "Familiar" || x.Seccion == "Pareja") && (children.Any(r => x.ReferenciaId == r.Id || x.ReferenciaClave == r.Clave) || family.ClavesHijosAEliminar?.Contains(x.ReferenciaClave ?? "") == true)));
        var retainedChildIds = IsYes(family.TieneHijos)
            ? family.Familiares.Where(x => x.Id.HasValue && !family.LimpiarHijos).Select(x => x.Id!.Value).ToHashSet()
            : new HashSet<long>();
        Add(active.Where(x => x.Seccion == "PartidaNacimiento" && x.ReferenciaId is { } id && !retainedChildIds.Contains(id)));
        foreach (var child in family.Familiares.Where(x => !IsYes(x.TieneDiscapacidad) || x.LimpiarDiscapacidad))
            Add(active.Where(x => x.Seccion == "Familiar" && ((child.Id.HasValue && x.ReferenciaId == child.Id) || (!string.IsNullOrWhiteSpace(child.Clave) && x.ReferenciaClave == child.Clave))));
        var allowsPartner = string.Equals(family.EstadoCivil, "Casado", StringComparison.OrdinalIgnoreCase) || string.Equals(family.EstadoCivil, "UnionLibre", StringComparison.OrdinalIgnoreCase);
        var partner = rows.Where(x => x.TipoRelacion == "Conyuge" || x.TipoRelacion == "Pareja").ToList();
        if (!allowsPartner || family.LimpiarPareja)
            Add(active.Where(x => (x.Seccion == "Pareja" || x.Seccion == "Familiar") && (partner.Any(r => x.ReferenciaId == r.Id || x.ReferenciaClave == r.Clave) || x.ReferenciaClave == family.ClaveParejaAEliminar)));
        else if (family.Pareja is { } partnerRequest && !IsYes(partnerRequest.TieneDiscapacidad))
            Add(active.Where(x => x.Seccion == "Familiar" && ((partnerRequest.Id.HasValue && x.ReferenciaId == partnerRequest.Id) || (!string.IsNullOrWhiteSpace(partnerRequest.Clave) && x.ReferenciaClave == partnerRequest.Clave))));
        return active.Where(x => ids.Contains(x.Id)).ToList();
    }

    private async Task ValidateDocuments(ActualizacionRequest input, int employeeId, CancellationToken ct)
    {
        if (input.Seccion is not ("personal" or "familia" or "formacion" or "certificaciones")) return;
        var docs = await db.Set<ColaboradorDocumento>().AsNoTracking()
            .Where(doc => doc.IdEmpleado == employeeId && doc.Activo)
            .Select(doc => new { doc.Id, doc.Seccion, doc.ReferenciaId, doc.ReferenciaClave }).ToListAsync(ct);
        var errors = new List<ApiError>();

        var respaldosUsados = new HashSet<long>();

        void Require(string path, string tipo, long? id, string? clave)
        {
            var respaldo = docs.FirstOrDefault(doc => string.Equals(doc.Seccion, tipo, StringComparison.OrdinalIgnoreCase) &&
                !respaldosUsados.Contains(doc.Id) &&
                (tipo == "Discapacidad" ? doc.ReferenciaId is null && string.IsNullOrEmpty(doc.ReferenciaClave) :
                    (id.HasValue && doc.ReferenciaId == id) || (!string.IsNullOrWhiteSpace(clave) && doc.ReferenciaClave == clave)));
            if (respaldo is null)
                errors.Add(new ApiError(path, "Sube los archivos solicitados.", "VALIDATION_ERROR"));
            else
                respaldosUsados.Add(respaldo.Id);
        }

        if (input.Seccion == "personal" && IsYes(input.Persona.TieneDiscapacidad))
            Require("persona.archivos", "Discapacidad", null, null);

        if (input.Seccion == "familia" && input.Familia is { } familia)
        {
            if (familia.EstadoCivil is "Casado" or "UnionLibre")
                Require("familia.pareja.archivos", "Pareja", familia.Pareja?.Id, familia.Pareja?.Clave);
            if (IsYes(familia.TieneHijos) && familia.Familiares.Count == 0)
                errors.Add(new ApiError("familia.tieneHijos", "Agrega al menos un hijo y sube los archivos solicitados.", "VALIDATION_ERROR"));
            if (IsYes(familia.TieneHijos))
                for (var i = 0; i < familia.Familiares.Count; i++)
                {
                    var hijo = familia.Familiares[i];
                    Require($"familia.familiares[{i}].archivos", "PartidaNacimiento", hijo.Id, hijo.Clave);
                    if (IsYes(hijo.TieneDiscapacidad))
                        Require($"familia.familiares[{i}].archivos", "Familiar", hijo.Id, hijo.Clave);
                }
        }
        for (var i = 0; i < input.Formaciones.Count; i++)
            Require($"formaciones[{i}].archivos", "Formacion", input.Formaciones[i].Id, input.Formaciones[i].Clave);
        for (var i = 0; i < input.Certificaciones.Count; i++)
            Require($"certificaciones[{i}].archivos", "Certificacion", input.Certificaciones[i].Id, input.Certificaciones[i].Clave);

        if (errors.Count > 0) throw new FieldValidationException(errors);
    }

    private static bool IsYes(string? value) => string.Equals(value, "Si", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "Sí", StringComparison.OrdinalIgnoreCase);
    private static string NormalizeTriState(string? value) => IsYes(value) ? "Si" : string.Equals(value, "No", StringComparison.OrdinalIgnoreCase) ? "No" : "NoInformado";
    private static FormacionRequest ToFormacion(ColaboradorFormacion x, IReadOnlyList<ColaboradorDocumento> docs) => new(x.Nivel, x.Institucion, x.Titulo, x.FechaInicio, x.FechaFin, x.Id, x.Clave, docs.Where(d => d.Seccion == "Formacion" && (d.ReferenciaId == x.Id || d.ReferenciaClave == x.Clave)).Select(ToDoc).ToList());
    private static CertificacionRequest ToCertificacion(ColaboradorCertificacion x, IReadOnlyList<ColaboradorDocumento> docs) => new(x.Nombre, x.EntidadEmisora, x.FechaEmision, x.FechaVencimiento, x.Id, x.Clave, docs.Where(d => d.Seccion == "Certificacion" && (d.ReferenciaId == x.Id || d.ReferenciaClave == x.Clave)).Select(ToDoc).ToList());
    private static FamiliarResponse ToFamily(ColaboradorFamiliar x, IReadOnlyList<ColaboradorDocumento> docs) => new(x.Id, x.TipoRelacion, x.Nombres, x.Apellidos, x.FechaNacimiento, Age(x.FechaNacimiento), NormalizeTriState(x.TieneDiscapacidad), IsYes(x.TieneDiscapacidad) ? x.TipoDiscapacidad : null, IsYes(x.TieneDiscapacidad) ? x.PorcentajeDiscapacidad : null, x.Identificacion, x.Clave, docs.Where(d => (d.Seccion == "PartidaNacimiento" && x.TipoRelacion == "Hijo" || d.Seccion == "Pareja" && x.TipoRelacion is ("Conyuge" or "Pareja") || d.Seccion == "Familiar" && IsYes(x.TieneDiscapacidad)) && (d.ReferenciaId == x.Id || d.ReferenciaClave == x.Clave)).Select(ToDoc).ToList());
    private static DocumentoResponse ToDoc(ColaboradorDocumento x) => new(x.Id, x.NombreOriginal, x.ContentType, x.Tamano, x.FechaCreacion, x.Seccion, x.ReferenciaId, x.ReferenciaClave);
    private async Task<SolicitudDetalleResponse?> LegacyDetail(ActualizacionSolicitud s, CancellationToken ct) => await db.Set<ActualizacionSolicitud>().Where(x => x.Id == s.Id).Include(x => x.Detalles).Include(x => x.Formaciones).Include(x => x.Certificaciones).Include(x => x.Archivos).Include(x => x.Familiares).Select(x => new SolicitudDetalleResponse(x.Id, x.IdEmpleado, "", x.Estado.ToString(), x.Version, x.Observacion, x.Detalles.Select(d => new ActualizacionDetalleResponse(d.Seccion, d.Campo, d.ValorVigente, d.ValorPropuesto)).ToList(), x.Formaciones.Select(f => new FormacionRequest(f.Nivel, f.Institucion, f.Titulo, f.FechaInicio, f.FechaFin)).ToList(), x.Certificaciones.Select(c => new CertificacionRequest(c.Nombre, c.EntidadEmisora, c.FechaEmision, c.FechaVencimiento)).ToList(), x.Archivos.Select(a => new ArchivoResponse(a.Id, a.NombreOriginal, a.ContentType, a.Tamano, a.Fechacreacion)).ToList(), null)).SingleOrDefaultAsync(ct);
    private static int? Age(DateOnly? d) { if (d is null) return null; var t = DateOnly.FromDateTime(DateTime.UtcNow); var age = t.Year - d.Value.Year; if (t < d.Value.AddYears(age)) age--; return age; }
}
