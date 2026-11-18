using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using tmr_backend.Features.ActualizacionDatos.Domain;
using tmr_backend.Features.ActualizacionDatos.DTOs;
using tmr_backend.Infrastructure.Database;
using tmr_backend.Infrastructure.Database.Entities;
using tmr_backend.Infrastructure.Shared;
using tmr_backend.Features.ActualizacionDatos.Storage;
using tmr_backend.Shared.Validation;
using tmr_backend.Shared.Wrappers;

namespace tmr_backend.Features.ActualizacionDatos.Services;

public sealed class ActualizacionDirectaService(ApplicationDbContext db, ICurrentUserService currentUser, IFileStorage storage)
{
    public async Task<MiDatosResponse?> GetAsync(CancellationToken ct)
    {
        var owner = await Owner(ct);
        if (owner is null) return null;
        var (employee, person) = owner.Value;
        var control = await db.Set<ColaboradorDatosControl>().AsNoTracking().SingleOrDefaultAsync(x => x.IdEmpleado == employee.Id, ct);
        var formaciones = await db.Set<ColaboradorFormacion>().AsNoTracking().Where(x => x.IdEmpleado == employee.Id && x.Activo).ToListAsync(ct);
        var certificaciones = await db.Set<ColaboradorCertificacion>().AsNoTracking().Where(x => x.IdEmpleado == employee.Id && x.Activo).ToListAsync(ct);
        var info = await db.Set<ColaboradorInformacionFamiliar>().AsNoTracking().SingleOrDefaultAsync(x => x.IdEmpleado == employee.Id, ct);
        var familiares = await db.Set<ColaboradorFamiliar>().AsNoTracking().Where(x => x.IdEmpleado == employee.Id && x.Activo).ToListAsync(ct);
        var docs = await db.Set<ColaboradorDocumento>().AsNoTracking().Where(x => x.IdEmpleado == employee.Id && x.Activo).ToListAsync(ct);
        var ownDisability = IsYes(info?.TieneDiscapacidad);
        var persona = new PersonaPropuesta(person.Nombres, person.Apellidos, person.Email, person.Telefono, person.Direccion, info?.EstadoCivil, NormalizeTriState(info?.TieneDiscapacidad), ownDisability ? info?.TipoDiscapacidad : null, ownDisability ? info?.PorcentajeDiscapacidad : null);
        var familyRows = IsYes(info?.TieneHijos) ? familiares.Select(x => ToFamily(x, docs)).ToList() : [];
        var allowsPartner = string.Equals(info?.EstadoCivil, "Casado", StringComparison.OrdinalIgnoreCase) || string.Equals(info?.EstadoCivil, "UnionLibre", StringComparison.OrdinalIgnoreCase);
        var pareja = allowsPartner ? familyRows.FirstOrDefault(x => x.TipoRelacion == "Conyuge" || x.TipoRelacion == "Pareja") : null;
        var family = new InformacionFamiliarResponse(info?.EstadoCivil, NormalizeTriState(info?.TieneHijos), NormalizeTriState(info?.TieneDiscapacidad), ownDisability ? info?.TipoDiscapacidad : null, ownDisability ? info?.PorcentajeDiscapacidad : null, familyRows.Where(x => x != pareja).ToList(), pareja);
        var old = await db.Set<ActualizacionSolicitud>().AsNoTracking().Where(x => x.IdEmpleado == employee.Id).OrderByDescending(x => x.Fechamodificacion).ToListAsync(ct);
        var recoverable = old.FirstOrDefault(x => x.Estado != EstadoActualizacion.Aprobada);
        return new(null, "Sin cambios", null, persona, formaciones.Select(x => ToFormacion(x, docs)).ToList(), certificaciones.Select(x => ToCertificacion(x, docs)).ToList(), ownDisability ? docs.Where(d => d.Seccion == "Discapacidad").Select(x => new ArchivoResponse(x.Id, x.NombreOriginal, x.ContentType, x.Tamano, x.FechaCreacion)).ToList() : [], family, control?.Version ?? 0, [], recoverable is null ? null : await LegacyDetail(recoverable, ct));
    }

    public async Task<long> SaveAsync(ActualizacionRequest input, CancellationToken ct)
    {
        var owner = await Owner(ct) ?? throw new UnauthorizedAccessException("El usuario no tiene un colaborador activo asociado.");
        Validate(input);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var control = await db.Set<ColaboradorDatosControl>().SingleOrDefaultAsync(x => x.IdEmpleado == owner.Employee.Id, ct);
        if (control is null)
        {
            control = new ColaboradorDatosControl { IdEmpleado = owner.Employee.Id, Version = 1, FechaModificacion = DateTime.UtcNow };
            db.Add(control);
            await db.SaveChangesAsync(ct);
        }
        else
        {
            if (input.ControlVersion is null || input.ControlVersion.Value != control.Version) throw new DbUpdateConcurrencyException("Los datos cambiaron desde que los cargaste. Recarga la página antes de guardar.");
            control.Version++;
            control.FechaModificacion = DateTime.UtcNow;
        }
        var employee = await db.TblAdministracionEmpleados.Include(x => x.IdpersonaNavigation).SingleAsync(x => x.Id == owner.Employee.Id, ct);
        var person = employee.IdpersonaNavigation;
        person.Nombres = NombrePersonaRules.Normalize(input.Persona.Nombres); person.Apellidos = NombrePersonaRules.Normalize(input.Persona.Apellidos); person.Email = input.Persona.Email?.Trim(); person.Telefono = TelefonoRules.Normalize(input.Persona.Telefono); person.Direccion = input.Persona.Direccion?.Trim(); person.Fechamodificacion = DateTime.UtcNow; person.Usuariomodificacion = currentUser.Email;
        var info = await db.Set<ColaboradorInformacionFamiliar>().SingleOrDefaultAsync(x => x.IdEmpleado == employee.Id, ct) ?? new ColaboradorInformacionFamiliar { IdEmpleado = employee.Id };
        info.EstadoCivil = input.Familia?.EstadoCivil; info.TieneHijos = NormalizeTriState(input.Familia?.TieneHijos); info.TieneDiscapacidad = NormalizeTriState(input.Persona.TieneDiscapacidad); info.TipoDiscapacidad = IsYes(input.Persona.TieneDiscapacidad) ? input.Persona.TipoDiscapacidad : null; info.PorcentajeDiscapacidad = IsYes(input.Persona.TieneDiscapacidad) ? input.Persona.PorcentajeDiscapacidad : null;
        if (db.Entry(info).State == EntityState.Detached) db.Add(info);
        var documentsToDelete = await DocumentsToDeleteAsync(employee.Id, input, ct);
        await UpsertFormaciones(employee.Id, input.Formaciones, ct);
        await UpsertCertificaciones(employee.Id, input.Certificaciones, ct);
        await UpsertFamilies(employee.Id, NormalizeFamily(input.Familia), ct);
        foreach (var document in documentsToDelete)
        {
            await storage.DeleteAsync(document.StorageKey, ct, document.Provider);
            document.Activo = false;
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return control.Version;
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

    private static void Validate(ActualizacionRequest x)
    {
        var errors = new List<ApiError>();
        void Add(string field, string message) => errors.Add(new ApiError(field, message, "VALIDATION_ERROR"));

        if (!NombrePersonaRules.IsValid(x.Persona.Nombres)) Add("persona.nombres", NombrePersonaRules.Message);
        if (!NombrePersonaRules.IsValid(x.Persona.Apellidos)) Add("persona.apellidos", NombrePersonaRules.Message);
        if (!TelefonoRules.IsValid(x.Persona.Telefono)) Add("persona.telefono", TelefonoRules.Message);
        if (!string.IsNullOrWhiteSpace(x.Persona.Email) && !System.Net.Mail.MailAddress.TryCreate(x.Persona.Email.Trim(), out _)) Add("persona.email", "Ingresa un correo electrónico válido");
        if (x.Persona.PorcentajeDiscapacidad is < 0 or > 100) Add("persona.porcentajeDiscapacidad", "El porcentaje debe estar entre 0 y 100.");

        for (var i = 0; i < x.Formaciones.Count; i++)
        {
            var formation = x.Formaciones[i];
            if (string.IsNullOrWhiteSpace(formation.Nivel)) Add($"formaciones[{i}].nivel", "Este campo es obligatorio.");
            if (string.IsNullOrWhiteSpace(formation.Institucion)) Add($"formaciones[{i}].institucion", "Este campo es obligatorio.");
            if (string.IsNullOrWhiteSpace(formation.Titulo)) Add($"formaciones[{i}].titulo", "Este campo es obligatorio.");
        }
        for (var i = 0; i < x.Certificaciones.Count; i++)
        {
            var certification = x.Certificaciones[i];
            if (string.IsNullOrWhiteSpace(certification.Nombre)) Add($"certificaciones[{i}].nombre", "Este campo es obligatorio.");
            if (string.IsNullOrWhiteSpace(certification.EntidadEmisora)) Add($"certificaciones[{i}].entidadEmisora", "Este campo es obligatorio.");
        }
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        for (var i = 0; i < x.Certificaciones.Count; i++)
        {
            var certification = x.Certificaciones[i];
            if (certification.FechaEmision > today) Add($"certificaciones[{i}].fechaEmision", "La fecha de emisión no puede ser futura.");
            if (certification.FechaVencimiento is not null && certification.FechaVencimiento < certification.FechaEmision) Add($"certificaciones[{i}].fechaVencimiento", "El vencimiento no puede ser anterior a la fecha de emisión.");
        }
        for (var i = 0; i < (x.Familia?.Familiares.Count ?? 0); i++)
        {
            var family = x.Familia!.Familiares[i];
            var prefix = $"familia.familiares[{i}]";
            if (!NombrePersonaRules.IsValid(family.Nombres)) Add($"{prefix}.nombres", NombrePersonaRules.Message);
            if (!NombrePersonaRules.IsValid(family.Apellidos)) Add($"{prefix}.apellidos", NombrePersonaRules.Message);
            if (family.FechaNacimiento > today) Add($"{prefix}.fechaNacimiento", "La fecha de nacimiento no puede ser futura.");
            if (!string.IsNullOrWhiteSpace(family.Identificacion) && !System.Text.RegularExpressions.Regex.IsMatch(family.Identificacion, "^\\d{10}$")) Add($"{prefix}.identificacion", "La identificación debe contener exactamente 10 dígitos.");
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
        if (!IsYes(input.Persona.TieneDiscapacidad) || input.LimpiarDiscapacidad)
            Add(active.Where(x => x.Seccion == "Discapacidad"));
        var family = input.Familia;
        if (family is null) return active.Where(x => ids.Contains(x.Id)).ToList();
        var rows = await db.Set<ColaboradorFamiliar>().Where(x => x.IdEmpleado == employeeId && x.Activo).ToListAsync(ct);
        var children = rows.Where(x => x.TipoRelacion == "Hijo").ToList();
        if (!IsYes(family.TieneHijos) || family.LimpiarHijos)
            Add(active.Where(x => (x.Seccion == "Familiar" || x.Seccion == "Pareja") && (children.Any(r => x.ReferenciaId == r.Id || x.ReferenciaClave == r.Clave) || family.ClavesHijosAEliminar?.Contains(x.ReferenciaClave ?? "") == true)));
        foreach (var child in family.Familiares.Where(x => !IsYes(x.TieneDiscapacidad) || x.LimpiarDiscapacidad))
            Add(active.Where(x => x.Seccion == "Familiar" && (x.ReferenciaId == child.Id || (!string.IsNullOrWhiteSpace(child.Clave) && x.ReferenciaClave == child.Clave))));
        var allowsPartner = string.Equals(family.EstadoCivil, "Casado", StringComparison.OrdinalIgnoreCase) || string.Equals(family.EstadoCivil, "UnionLibre", StringComparison.OrdinalIgnoreCase);
        var partner = rows.Where(x => x.TipoRelacion == "Conyuge" || x.TipoRelacion == "Pareja").ToList();
        if (!allowsPartner || family.LimpiarPareja)
            Add(active.Where(x => (x.Seccion == "Pareja" || x.Seccion == "Familiar") && (partner.Any(r => x.ReferenciaId == r.Id || x.ReferenciaClave == r.Clave) || x.ReferenciaClave == family.ClaveParejaAEliminar)));
        else if (family.Pareja is { } partnerRequest && !IsYes(partnerRequest.TieneDiscapacidad))
            Add(active.Where(x => (x.Seccion == "Pareja" || x.Seccion == "Familiar") && (x.ReferenciaId == partnerRequest.Id || x.ReferenciaClave == partnerRequest.Clave)));
        return active.Where(x => ids.Contains(x.Id)).ToList();
    }

    private static bool IsYes(string? value) => string.Equals(value, "Si", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "Sí", StringComparison.OrdinalIgnoreCase);
    private static string NormalizeTriState(string? value) => IsYes(value) ? "Si" : string.Equals(value, "No", StringComparison.OrdinalIgnoreCase) ? "No" : "NoInformado";
    private static FormacionRequest ToFormacion(ColaboradorFormacion x, IReadOnlyList<ColaboradorDocumento> docs) => new(x.Nivel, x.Institucion, x.Titulo, x.FechaInicio, x.FechaFin, x.Id, x.Clave, docs.Where(d => d.Seccion == "Formacion" && (d.ReferenciaId == x.Id || d.ReferenciaClave == x.Clave)).Select(ToDoc).ToList());
    private static CertificacionRequest ToCertificacion(ColaboradorCertificacion x, IReadOnlyList<ColaboradorDocumento> docs) => new(x.Nombre, x.EntidadEmisora, x.FechaEmision, x.FechaVencimiento, x.Id, x.Clave, docs.Where(d => d.Seccion == "Certificacion" && (d.ReferenciaId == x.Id || d.ReferenciaClave == x.Clave)).Select(ToDoc).ToList());
    private static FamiliarResponse ToFamily(ColaboradorFamiliar x, IReadOnlyList<ColaboradorDocumento> docs) => new(x.Id, x.TipoRelacion, x.Nombres, x.Apellidos, x.FechaNacimiento, Age(x.FechaNacimiento), NormalizeTriState(x.TieneDiscapacidad), IsYes(x.TieneDiscapacidad) ? x.TipoDiscapacidad : null, IsYes(x.TieneDiscapacidad) ? x.PorcentajeDiscapacidad : null, x.Identificacion, x.Clave, IsYes(x.TieneDiscapacidad) ? docs.Where(d => d.Seccion == "Familiar" && (d.ReferenciaId == x.Id || d.ReferenciaClave == x.Clave)).Select(ToDoc).ToList() : []);
    private static DocumentoResponse ToDoc(ColaboradorDocumento x) => new(x.Id, x.NombreOriginal, x.ContentType, x.Tamano, x.FechaCreacion, x.Seccion, x.ReferenciaId, x.ReferenciaClave);
    private async Task<SolicitudDetalleResponse?> LegacyDetail(ActualizacionSolicitud s, CancellationToken ct) => await db.Set<ActualizacionSolicitud>().Where(x => x.Id == s.Id).Include(x => x.Detalles).Include(x => x.Formaciones).Include(x => x.Certificaciones).Include(x => x.Archivos).Include(x => x.Familiares).Select(x => new SolicitudDetalleResponse(x.Id, x.IdEmpleado, "", x.Estado.ToString(), x.Version, x.Observacion, x.Detalles.Select(d => new ActualizacionDetalleResponse(d.Seccion, d.Campo, d.ValorVigente, d.ValorPropuesto)).ToList(), x.Formaciones.Select(f => new FormacionRequest(f.Nivel, f.Institucion, f.Titulo, f.FechaInicio, f.FechaFin)).ToList(), x.Certificaciones.Select(c => new CertificacionRequest(c.Nombre, c.EntidadEmisora, c.FechaEmision, c.FechaVencimiento)).ToList(), x.Archivos.Select(a => new ArchivoResponse(a.Id, a.NombreOriginal, a.ContentType, a.Tamano, a.Fechacreacion)).ToList(), null)).SingleOrDefaultAsync(ct);
    private static int? Age(DateOnly? d) { if (d is null) return null; var t = DateOnly.FromDateTime(DateTime.UtcNow); var age = t.Year - d.Value.Year; if (t < d.Value.AddYears(age)) age--; return age; }
}
