using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using tmr_backend.Features.ActualizacionDatos.Domain;
using tmr_backend.Features.ActualizacionDatos.DTOs;
using tmr_backend.Infrastructure.Database;
using tmr_backend.Infrastructure.Database.Entities;
using tmr_backend.Infrastructure.Shared;

namespace tmr_backend.Features.ActualizacionDatos.Services;

public sealed class ActualizacionDatosService(ApplicationDbContext db, ICurrentUserService currentUser)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<bool> HasInactiveEmployeeAsync(CancellationToken ct)
    {
        return await (from u in db.TblAutenticacionUsuarios
                      join e in db.TblAdministracionEmpleados on u.Idpersona equals e.Idpersona
                      where u.Id == currentUser.UserId && u.Activo && !e.Activo
                      select e.Id).AnyAsync(ct);
    }

    public async Task<(TblAdministracionEmpleado Employee, TblAdministracionPersona Person)?> GetOwnerAsync(CancellationToken ct)
    {
        var owner = await (from u in db.TblAutenticacionUsuarios
                           join e in db.TblAdministracionEmpleados on u.Idpersona equals e.Idpersona
                           where u.Id == currentUser.UserId && u.Activo && e.Activo
                           select new { Employee = e, Person = e.IdpersonaNavigation }).FirstOrDefaultAsync(ct);
        return owner is null ? null : (owner.Employee, owner.Person);
    }

    public async Task<MiDatosResponse?> GetMineAsync(CancellationToken ct)
    {
        var owner = await GetOwnerAsync(ct); if (owner is null) return null;
        var value = owner.Value;
        var request = await db.Set<ActualizacionSolicitud>().Include(x => x.Formaciones).Include(x => x.Certificaciones).Include(x => x.Archivos).Include(x => x.Familiares).Where(x => x.IdEmpleado == value.Employee.Id && x.Estado != EstadoActualizacion.Aprobada).OrderByDescending(x => x.Id).FirstOrDefaultAsync(ct);
        var formaciones = await db.Set<ColaboradorFormacion>().Where(x => x.IdEmpleado == value.Employee.Id && x.Activo).ToListAsync(ct);
        var certificaciones = await db.Set<ColaboradorCertificacion>().Where(x => x.IdEmpleado == value.Employee.Id && x.Activo).ToListAsync(ct);
        var info = await db.Set<ColaboradorInformacionFamiliar>().AsNoTracking().SingleOrDefaultAsync(x => x.IdEmpleado == value.Employee.Id, ct);
        var familiares = await db.Set<ColaboradorFamiliar>().AsNoTracking().Where(x => x.IdEmpleado == value.Employee.Id && x.Activo).ToListAsync(ct);
        if (request is not null) { formaciones = request.Formaciones.Select(x => new ColaboradorFormacion { Nivel=x.Nivel, Institucion=x.Institucion, Titulo=x.Titulo, FechaInicio=x.FechaInicio, FechaFin=x.FechaFin, Activo=true }).ToList(); certificaciones = request.Certificaciones.Select(x => new ColaboradorCertificacion { Nombre=x.Nombre, EntidadEmisora=x.EntidadEmisora, FechaEmision=x.FechaEmision, FechaVencimiento=x.FechaVencimiento, Activo=true }).ToList(); }
        var persona = new PersonaPropuesta(value.Person.Nombres, value.Person.Apellidos, value.Person.Email, value.Person.Telefono, value.Person.Direccion, info?.EstadoCivil, info?.TieneDiscapacidad, info?.TipoDiscapacidad, info?.PorcentajeDiscapacidad);
        var familia = new InformacionFamiliarResponse(request?.DatosPropuestosJson is not null ? JsonSerializer.Deserialize<ActualizacionRequest>(request.DatosPropuestosJson, JsonOptions)?.Familia?.EstadoCivil : info?.EstadoCivil, request?.DatosPropuestosJson is not null ? JsonSerializer.Deserialize<ActualizacionRequest>(request.DatosPropuestosJson, JsonOptions)?.Familia?.TieneHijos : info?.TieneHijos, info?.TieneDiscapacidad, info?.TipoDiscapacidad, info?.PorcentajeDiscapacidad, (request?.Familiares.Select(ToFamilyResponse) ?? familiares.Select(ToFamilyResponse)).ToList());
        return new(request?.Id, request?.Estado.ToString() switch { "Borrador" => "Borrador", "Enviada" => "Enviada", "Devuelta" => "Devuelta", "Aprobada" => "Aprobada", _ => "Sin cambios" }, request?.Observacion, persona, formaciones.Select(x=>new FormacionRequest(x.Nivel,x.Institucion,x.Titulo,x.FechaInicio,x.FechaFin)).ToList(), certificaciones.Select(x=>new CertificacionRequest(x.Nombre,x.EntidadEmisora,x.FechaEmision,x.FechaVencimiento)).ToList(), request?.Archivos.Select(x=>new ArchivoResponse(x.Id,x.NombreOriginal,x.ContentType,x.Tamano,x.Fechacreacion)).ToList() ?? [], familia);
    }

    public async Task<IReadOnlyList<SolicitudLista>> ListAsync(string? search, string? status, int page, int size, CancellationToken ct)
    {
        var query = from solicitud in db.Set<ActualizacionSolicitud>()
                    join empleado in db.TblAdministracionEmpleados on solicitud.IdEmpleado equals empleado.Id
                    join persona in db.TblAdministracionPersonas on empleado.Idpersona equals persona.Id
                    where solicitud.Estado != EstadoActualizacion.Borrador
                    select new { Solicitud = solicitud, Colaborador = persona.Nombres + " " + persona.Apellidos };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var escapedSearch = search.Replace("!", "!!").Replace("%", "!%").Replace("_", "!_");
            var pattern = $"%{escapedSearch}%";
            query = query.Where(row => EF.Functions.ILike(row.Colaborador, pattern, "!"));
        }

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<EstadoActualizacion>(status, true, out var estado))
            query = query.Where(row => row.Solicitud.Estado == estado);

        var offset = ((long)page - 1) * size;
        if (offset > int.MaxValue)
            return [];

        var rows = await query
            .OrderByDescending(row => row.Solicitud.Fechamodificacion)
            .ThenByDescending(row => row.Solicitud.Id)
            .Skip((int)offset)
            .Take(size)
            .Select(row => new
            {
                row.Solicitud.Id,
                row.Solicitud.IdEmpleado,
                row.Colaborador,
                row.Solicitud.Estado,
                row.Solicitud.Fechamodificacion,
                row.Solicitud.Observacion,
                row.Solicitud.Version
            })
            .ToListAsync(ct);

        return rows.Select(row => new SolicitudLista(row.Id, row.IdEmpleado, row.Colaborador,
            row.Estado.ToString(), row.Fechamodificacion, row.Observacion, row.Version)).ToList();
    }

    public async Task<SolicitudDetalleResponse?> DetailAsync(long id,CancellationToken ct)
    { var s=await db.Set<ActualizacionSolicitud>().Include(x=>x.Detalles).Include(x=>x.Formaciones).Include(x=>x.Certificaciones).Include(x=>x.Archivos).Include(x=>x.Familiares).SingleOrDefaultAsync(x=>x.Id==id,ct); if(s is null)return null; var name=await(from e in db.TblAdministracionEmpleados join p in db.TblAdministracionPersonas on e.Idpersona equals p.Id where e.Id==s.IdEmpleado select p.Nombres+" "+p.Apellidos).SingleAsync(ct); var proposed=JsonSerializer.Deserialize<ActualizacionRequest>(s.DatosPropuestosJson,JsonOptions); var familia=proposed?.Familia is null ? null : new InformacionFamiliarResponse(proposed.Familia.EstadoCivil,proposed.Familia.TieneHijos,proposed.Familia.TieneDiscapacidad,proposed.Familia.TipoDiscapacidad,proposed.Familia.PorcentajeDiscapacidad,s.Familiares.Select(ToFamilyResponse).ToList()); return new(s.Id,s.IdEmpleado,name,s.Estado.ToString(),s.Version,s.Observacion,s.Detalles.Select(x=>new ActualizacionDetalleResponse(x.Seccion,x.Campo,x.ValorVigente,x.ValorPropuesto)).ToList(),s.Formaciones.Select(x=>new FormacionRequest(x.Nivel,x.Institucion,x.Titulo,x.FechaInicio,x.FechaFin)).ToList(),s.Certificaciones.Select(x=>new CertificacionRequest(x.Nombre,x.EntidadEmisora,x.FechaEmision,x.FechaVencimiento)).ToList(),s.Archivos.Select(x=>new ArchivoResponse(x.Id,x.NombreOriginal,x.ContentType,x.Tamano,x.Fechacreacion)).ToList(),familia); }

    private static FamiliarResponse ToFamilyResponse(ColaboradorFamiliar x)=>new(x.Id,x.TipoRelacion,x.Nombres,x.Apellidos,x.FechaNacimiento,Age(x.FechaNacimiento),x.TieneDiscapacidad,x.TipoDiscapacidad,x.PorcentajeDiscapacidad);
    private static FamiliarResponse ToFamilyResponse(ActualizacionFamiliar x)=>new(x.Id,x.TipoRelacion,x.Nombres,x.Apellidos,x.FechaNacimiento,Age(x.FechaNacimiento),x.TieneDiscapacidad,x.TipoDiscapacidad,x.PorcentajeDiscapacidad);
    private static int? Age(DateOnly? date){if(date is null)return null;var today=DateOnly.FromDateTime(DateTime.UtcNow);var age=today.Year-date.Value.Year;if(today<date.Value.AddYears(age))age--;return age;}
}
