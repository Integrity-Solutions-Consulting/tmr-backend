using Microsoft.EntityFrameworkCore;
using tmr_backend.Features.ActualizacionDatos.Domain;
using tmr_backend.Features.ActualizacionDatos.DTOs;
using tmr_backend.Infrastructure.Database;

namespace tmr_backend.Features.ActualizacionDatos.Services;

public sealed class ExpedienteService(ApplicationDbContext db)
{
    public async Task<ExpedienteResponse?> GetAsync(int id, bool includeRestricted, CancellationToken ct)
    {
        var employee = await db.TblAdministracionEmpleados
            .Include(x => x.IdpersonaNavigation).ThenInclude(x => x!.IdgeneroNavigation)
            .Include(x => x.IdpersonaNavigation).ThenInclude(x => x!.IdnacionalidadNavigation)
            .Include(x => x.IdcargoNavigation).ThenInclude(x => x!.IddepartamentoNavigation)
            .Include(x => x.IdempresacatalogoNavigation).Include(x => x.IdmodotrabajoNavigation).Include(x => x.IdcategoriaempleadoNavigation)
            .AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (employee is null) return null;

        var projects = await db.TblTimeReportAsignacionProyectos.AsNoTracking()
            .Include(x => x.IdproyectoNavigation).ThenInclude(x => x!.IdclienteNavigation)
            .Where(x => x.Idempleado == id && x.Activo)
            .Select(x => new ProyectoExpedienteResponse(x.IdproyectoNavigation!.Id, x.IdproyectoNavigation.Nombre, x.IdproyectoNavigation.IdclienteNavigation!.Nombrecomercial ?? x.IdproyectoNavigation.IdclienteNavigation.Razonsocial ?? "", x.IdproyectoNavigation.Activo ? "Activo" : "Inactivo", x.Estadoasignacion))
            .ToListAsync(ct);

        var general = new GeneralExpedienteResponse(employee.IdpersonaNavigation.Numeroidentificacion, employee.IdpersonaNavigation.Fechanacimiento?.ToString("yyyy-MM-dd"), employee.IdpersonaNavigation.IdgeneroNavigation?.Valor, employee.IdpersonaNavigation.IdnacionalidadNavigation?.Valor, employee.IdpersonaNavigation.Email, employee.IdpersonaNavigation.Telefono, employee.IdpersonaNavigation.Direccion, employee.IdempresacatalogoNavigation?.Valor, employee.Fechaingreso?.ToString("yyyy-MM-dd"), employee.IdmodotrabajoNavigation?.Valor, employee.IdcategoriaempleadoNavigation?.Valor, employee.Aniosexperiencia, projects);
        if (!includeRestricted) return new(employee.Id, employee.Codigoempleado, $"{employee.IdpersonaNavigation.Nombres} {employee.IdpersonaNavigation.Apellidos}".Trim(), employee.IdcargoNavigation?.Nombrecargo ?? "", employee.IdcargoNavigation?.IddepartamentoNavigation?.Valor ?? "", employee.Activo, employee.Activo ? "Activo" : "Inactivo", general, true, [], [], [], []);

        var directFiles = await db.Set<ColaboradorDocumento>().AsNoTracking().Where(x => x.IdEmpleado == id && x.Activo).ToListAsync(ct);
        var formacionesRows = await db.Set<ColaboradorFormacion>().AsNoTracking().Where(x => x.IdEmpleado == id && x.Activo).ToListAsync(ct);
        var certificationsRows = await db.Set<ColaboradorCertificacion>().AsNoTracking().Where(x => x.IdEmpleado == id && x.Activo).ToListAsync(ct);
        var formaciones = formacionesRows.Select(x => new ExpedienteFormacionResponse(x.Id,x.Nivel,x.Institucion,x.Titulo,x.FechaInicio,x.FechaFin,directFiles.Where(d=>d.Seccion=="Formacion" && (d.ReferenciaId==x.Id || d.ReferenciaClave==x.Clave)).Select(ToArchivo).ToList())).ToList();
        var certifications = certificationsRows.Select(x => new ExpedienteCertificacionResponse(x.Id,x.Nombre,x.EntidadEmisora,x.FechaEmision,x.FechaVencimiento,directFiles.Where(d=>d.Seccion=="Certificacion" && (d.ReferenciaId==x.Id || d.ReferenciaClave==x.Clave)).Select(ToArchivo).ToList())).ToList();
        var requests = await db.Set<ActualizacionSolicitud>().AsNoTracking().Where(x => x.IdEmpleado == id).OrderByDescending(x => x.Fechamodificacion).Select(x => new ExpedienteSolicitudResponse(x.Id,x.Estado.ToString(),x.Observacion,x.Fechamodificacion,x.Version)).ToListAsync(ct);
        var files = await (from a in db.Set<ActualizacionArchivo>().AsNoTracking() join s in db.Set<ActualizacionSolicitud>() on a.IdSolicitud equals s.Id where s.IdEmpleado == id select new ExpedienteArchivoResponse(a.Id,a.IdSolicitud,a.NombreOriginal,a.ContentType,a.Tamano,a.Fechacreacion,a.Seccion,a.ReferenciaId)).ToListAsync(ct);
        var familyInfo = await db.Set<ColaboradorInformacionFamiliar>().AsNoTracking().SingleOrDefaultAsync(x => x.IdEmpleado == id, ct);
        var familyRows = await db.Set<ColaboradorFamiliar>().AsNoTracking().Where(x => x.IdEmpleado == id && x.Activo).Select(x => new FamiliarResponse(x.Id, x.TipoRelacion, x.Nombres, x.Apellidos, x.FechaNacimiento, null, x.TieneDiscapacidad, x.TipoDiscapacidad, x.PorcentajeDiscapacidad, x.Identificacion, x.Clave, null)).ToListAsync(ct);
        familyRows = familyRows.Select(x => x with { Archivos = directFiles.Where(d=>d.Seccion=="Familiar" && (d.ReferenciaId==x.Id || d.ReferenciaClave==x.Clave)).Select(d => new DocumentoResponse(d.Id,d.NombreOriginal,d.ContentType,d.Tamano,d.FechaCreacion,d.Seccion,d.ReferenciaId,d.ReferenciaClave)).ToList() }).ToList();
        var family = familyRows.Select(x => x with { Edad = Age(x.FechaNacimiento) }).ToList();
        var directRequests = await db.Set<ColaboradorSolicitud>().AsNoTracking().Include(x => x.Archivos).Where(x=>x.IdEmpleado==id).ToListAsync(ct);
        var directRequestResponses = directRequests.Select(x => new ExpedienteSolicitudResponse(x.Id, x.Enviada ? "Enviada" : "Borrador", null, x.FechaCreacion, 0, x.Asunto, x.Descripcion, x.Enviada, x.Archivos.Select(a => new ExpedienteArchivoResponse(a.Id, a.IdSolicitud, a.NombreOriginal, a.ContentType, a.Tamano, a.FechaCreacion, "Solicitud", null)).ToList())).ToList();
        files.AddRange(directFiles.Select(ToArchivo));
        files.AddRange(directRequests.SelectMany(x => x.Archivos.Select(a => new ExpedienteArchivoResponse(a.Id, a.IdSolicitud, a.NombreOriginal, a.ContentType, a.Tamano, a.FechaCreacion, "Solicitud", null))));
        return new(employee.Id, employee.Codigoempleado, $"{employee.IdpersonaNavigation.Nombres} {employee.IdpersonaNavigation.Apellidos}".Trim(), employee.IdcargoNavigation?.Nombrecargo ?? "", employee.IdcargoNavigation?.IddepartamentoNavigation?.Valor ?? "", employee.Activo, employee.Activo ? "Activo" : "Inactivo", general, false, formaciones, certifications, files, requests.Concat(directRequestResponses).OrderByDescending(x=>x.Fecha).ToList(), new InformacionFamiliarResponse(familyInfo?.EstadoCivil, familyInfo?.TieneHijos, familyInfo?.TieneDiscapacidad, familyInfo?.TipoDiscapacidad, familyInfo?.PorcentajeDiscapacidad, family, family.FirstOrDefault(x=>x.TipoRelacion=="Conyuge" || x.TipoRelacion=="Pareja")));
    }

    private static ExpedienteArchivoResponse ToArchivo(ColaboradorDocumento x) => new(x.Id, 0, x.NombreOriginal, x.ContentType, x.Tamano, x.FechaCreacion, x.Seccion, x.ReferenciaId);

    private static int? Age(DateOnly? date)
    {
        if (date is null) return null;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var age = today.Year - date.Value.Year;
        if (today < date.Value.AddYears(age)) age--;
        return age;
    }
}
