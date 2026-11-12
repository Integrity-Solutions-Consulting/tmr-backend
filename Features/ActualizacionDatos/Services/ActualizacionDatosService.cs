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

    public async Task<long> SaveAsync(ActualizacionRequest input, CancellationToken ct)
    {
        var owner = await GetOwnerAsync(ct) ?? throw new UnauthorizedAccessException("El usuario no tiene un colaborador asociado.");
        if (input.Formaciones.Any(x => string.IsNullOrWhiteSpace(x.Nivel) || string.IsNullOrWhiteSpace(x.Institucion) || string.IsNullOrWhiteSpace(x.Titulo))) throw new ArgumentException("Complete los campos obligatorios de formación.");
        ValidateFamilies(input.Familia);
        if (input.Familia?.TieneHijos == "No" && input.Familia.Familiares.Count > 0)
            throw new ArgumentException("La respuesta 'No' contradice los hijos registrados. Seleccione 'Sí' o elimine explícitamente los hijos antes de continuar.");
        var current = await db.Set<ActualizacionSolicitud>().Where(x => x.IdEmpleado == owner.Employee.Id && x.Estado != EstadoActualizacion.Aprobada).OrderByDescending(x => x.Id).FirstOrDefaultAsync(ct);
        if (current is not null && current.Estado == EstadoActualizacion.Enviada) throw new InvalidOperationException("Ya existe una solicitud en revisión.");
        if (current is null) { current = new ActualizacionSolicitud { IdEmpleado=owner.Employee.Id, Fechacreacion=DateTime.UtcNow, Version=1 }; db.Add(current); } else current.Version++;
        current.Estado = input.Enviar ? EstadoActualizacion.Enviada : EstadoActualizacion.Borrador; current.Fechamodificacion=DateTime.UtcNow; current.Observacion=null; current.DatosPropuestosJson=JsonSerializer.Serialize(input, JsonOptions);
        db.RemoveRange(db.Set<ActualizacionDetalle>().Where(x=>x.IdSolicitud==current.Id)); db.RemoveRange(db.Set<ActualizacionFormacion>().Where(x=>x.IdSolicitud==current.Id)); db.RemoveRange(db.Set<ActualizacionCertificacion>().Where(x=>x.IdSolicitud==current.Id)); db.RemoveRange(db.Set<ActualizacionFamiliar>().Where(x=>x.IdSolicitud==current.Id));
        await db.SaveChangesAsync(ct);
        foreach (var (campo, proposed, actual) in new[] {("Nombres",input.Persona.Nombres,owner.Person.Nombres),("Apellidos",input.Persona.Apellidos,owner.Person.Apellidos),("Email",input.Persona.Email,owner.Person.Email),("Telefono",input.Persona.Telefono,owner.Person.Telefono),("Direccion",input.Persona.Direccion,owner.Person.Direccion)}) db.Add(new ActualizacionDetalle { IdSolicitud=current.Id, Seccion="Persona", Campo=campo, ValorVigente=actual, ValorPropuesto=proposed });
        db.AddRange(input.Formaciones.Select(x=>new ActualizacionFormacion { IdSolicitud=current.Id,Nivel=x.Nivel,Institucion=x.Institucion,Titulo=x.Titulo,FechaInicio=x.FechaInicio,FechaFin=x.FechaFin })); db.AddRange(input.Certificaciones.Select(x=>new ActualizacionCertificacion { IdSolicitud=current.Id,Nombre=x.Nombre,EntidadEmisora=x.EntidadEmisora,FechaEmision=x.FechaEmision,FechaVencimiento=x.FechaVencimiento }));
        if (input.Familia is not null) db.AddRange(input.Familia.Familiares.Select(x=>new ActualizacionFamiliar { IdSolicitud=current.Id,TipoRelacion=x.TipoRelacion,Nombres=x.Nombres,Apellidos=x.Apellidos,FechaNacimiento=x.FechaNacimiento,TieneDiscapacidad=x.TieneDiscapacidad,TipoDiscapacidad=x.TipoDiscapacidad,PorcentajeDiscapacidad=x.PorcentajeDiscapacidad }));
        await db.SaveChangesAsync(ct); return current.Id;
    }

    public async Task<IReadOnlyList<SolicitudLista>> ListAsync(string? search,string? status,int page,int size,CancellationToken ct)
    { var rows=await (from s in db.Set<ActualizacionSolicitud>() join e in db.TblAdministracionEmpleados on s.IdEmpleado equals e.Id join p in db.TblAdministracionPersonas on e.Idpersona equals p.Id where s.Estado!=EstadoActualizacion.Borrador select new SolicitudLista(s.Id,s.IdEmpleado,p.Nombres+" "+p.Apellidos,s.Estado.ToString(),s.Fechamodificacion,s.Observacion,s.Version)).ToListAsync(ct); if(!string.IsNullOrWhiteSpace(search))rows=rows.Where(x=>x.Colaborador.Contains(search,StringComparison.OrdinalIgnoreCase)).ToList(); if(!string.IsNullOrWhiteSpace(status)&&Enum.TryParse<EstadoActualizacion>(status,true,out var st))rows=rows.Where(x=>x.Estado==st.ToString()).ToList(); return rows.OrderByDescending(x=>x.Fechamodificacion).Skip((page-1)*size).Take(size).ToList(); }

    public async Task<SolicitudDetalleResponse?> DetailAsync(long id,CancellationToken ct)
    { var s=await db.Set<ActualizacionSolicitud>().Include(x=>x.Detalles).Include(x=>x.Formaciones).Include(x=>x.Certificaciones).Include(x=>x.Archivos).Include(x=>x.Familiares).SingleOrDefaultAsync(x=>x.Id==id,ct); if(s is null)return null; var name=await(from e in db.TblAdministracionEmpleados join p in db.TblAdministracionPersonas on e.Idpersona equals p.Id where e.Id==s.IdEmpleado select p.Nombres+" "+p.Apellidos).SingleAsync(ct); var proposed=JsonSerializer.Deserialize<ActualizacionRequest>(s.DatosPropuestosJson,JsonOptions); var familia=proposed?.Familia is null ? null : new InformacionFamiliarResponse(proposed.Familia.EstadoCivil,proposed.Familia.TieneHijos,proposed.Familia.TieneDiscapacidad,proposed.Familia.TipoDiscapacidad,proposed.Familia.PorcentajeDiscapacidad,s.Familiares.Select(ToFamilyResponse).ToList()); return new(s.Id,s.IdEmpleado,name,s.Estado.ToString(),s.Version,s.Observacion,s.Detalles.Select(x=>new ActualizacionDetalleResponse(x.Seccion,x.Campo,x.ValorVigente,x.ValorPropuesto)).ToList(),s.Formaciones.Select(x=>new FormacionRequest(x.Nivel,x.Institucion,x.Titulo,x.FechaInicio,x.FechaFin)).ToList(),s.Certificaciones.Select(x=>new CertificacionRequest(x.Nombre,x.EntidadEmisora,x.FechaEmision,x.FechaVencimiento)).ToList(),s.Archivos.Select(x=>new ArchivoResponse(x.Id,x.NombreOriginal,x.ContentType,x.Tamano,x.Fechacreacion)).ToList(),familia); }

    public async Task<IResult> ReviewAsync(long id,RevisionRequest input,bool approve,CancellationToken ct)
    { await using var tx=await db.Database.BeginTransactionAsync(ct); var s=await db.Set<ActualizacionSolicitud>().Include(x=>x.Detalles).Include(x=>x.Formaciones).Include(x=>x.Certificaciones).Include(x=>x.Familiares).SingleOrDefaultAsync(x=>x.Id==id,ct); if(s is null)return Results.NotFound(); if(s.Estado!=EstadoActualizacion.Enviada||s.Version!=input.Version)return Results.Conflict(new{message="La solicitud ya fue revisada o cambió de versión."}); if(!approve&&string.IsNullOrWhiteSpace(input.Observacion))return Results.BadRequest(new{message="La observación es obligatoria."}); s.Estado=approve?EstadoActualizacion.Aprobada:EstadoActualizacion.Devuelta;s.Observacion=input.Observacion;s.IdRevisor=currentUser.UserId;s.FechaRevision=DateTime.UtcNow;s.Fechamodificacion=DateTime.UtcNow;
        if(approve){var e=await db.TblAdministracionEmpleados.Include(x=>x.IdpersonaNavigation).SingleAsync(x=>x.Id==s.IdEmpleado,ct);var d=s.Detalles.ToDictionary(x=>x.Campo,x=>x.ValorPropuesto);if(d.TryGetValue("Nombres",out var v))e.IdpersonaNavigation.Nombres=v!;if(d.TryGetValue("Apellidos",out v))e.IdpersonaNavigation.Apellidos=v!;if(d.TryGetValue("Email",out v))e.IdpersonaNavigation.Email=v;if(d.TryGetValue("Telefono",out v))e.IdpersonaNavigation.Telefono=v;if(d.TryGetValue("Direccion",out v))e.IdpersonaNavigation.Direccion=v;var fs=await db.Set<ColaboradorFormacion>().Where(x=>x.IdEmpleado==s.IdEmpleado).ToListAsync(ct);fs.ForEach(x=>x.Activo=false);var cs=await db.Set<ColaboradorCertificacion>().Where(x=>x.IdEmpleado==s.IdEmpleado).ToListAsync(ct);cs.ForEach(x=>x.Activo=false);db.AddRange(s.Formaciones.Select(x=>new ColaboradorFormacion{IdEmpleado=s.IdEmpleado,Nivel=x.Nivel,Institucion=x.Institucion,Titulo=x.Titulo,FechaInicio=x.FechaInicio,FechaFin=x.FechaFin,Activo=true}));db.AddRange(s.Certificaciones.Select(x=>new ColaboradorCertificacion{IdEmpleado=s.IdEmpleado,Nombre=x.Nombre,EntidadEmisora=x.EntidadEmisora,FechaEmision=x.FechaEmision,FechaVencimiento=x.FechaVencimiento,Activo=true}));var info=await db.Set<ColaboradorInformacionFamiliar>().SingleOrDefaultAsync(x=>x.IdEmpleado==s.IdEmpleado,ct)??new ColaboradorInformacionFamiliar{IdEmpleado=s.IdEmpleado};var proposed=JsonSerializer.Deserialize<ActualizacionRequest>(s.DatosPropuestosJson,JsonOptions);info.EstadoCivil=proposed?.Familia?.EstadoCivil;info.TieneHijos=proposed?.Familia?.TieneHijos;info.TieneDiscapacidad=proposed?.Familia?.TieneDiscapacidad;info.TipoDiscapacidad=proposed?.Familia?.TipoDiscapacidad;info.PorcentajeDiscapacidad=proposed?.Familia?.PorcentajeDiscapacidad;if(db.Entry(info).State==EntityState.Detached)db.Add(info);var old=await db.Set<ColaboradorFamiliar>().Where(x=>x.IdEmpleado==s.IdEmpleado).ToListAsync(ct);old.ForEach(x=>x.Activo=false);db.AddRange(s.Familiares.Select(x=>new ColaboradorFamiliar{IdEmpleado=s.IdEmpleado,TipoRelacion=x.TipoRelacion,Nombres=x.Nombres,Apellidos=x.Apellidos,FechaNacimiento=x.FechaNacimiento,TieneDiscapacidad=x.TieneDiscapacidad,TipoDiscapacidad=x.TipoDiscapacidad,PorcentajeDiscapacidad=x.PorcentajeDiscapacidad,Activo=true}));}
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return Results.NoContent(); }

    private static void ValidateFamilies(InformacionFamiliarRequest? f){if(f is null)return;if(f.TieneHijos is not null && f.TieneHijos is not ("Si" or "No" or "NoInformado"))throw new ArgumentException("Tiene hijos debe ser Sí, No o No informado.");foreach(var x in f.Familiares){if(string.IsNullOrWhiteSpace(x.TipoRelacion)||string.IsNullOrWhiteSpace(x.Nombres)||string.IsNullOrWhiteSpace(x.Apellidos))throw new ArgumentException("Complete los datos obligatorios de cada familiar.");if(x.PorcentajeDiscapacidad is < 0 or > 100)throw new ArgumentException("El porcentaje de discapacidad debe estar entre 0 y 100.");}}
    private static FamiliarResponse ToFamilyResponse(ColaboradorFamiliar x)=>new(x.Id,x.TipoRelacion,x.Nombres,x.Apellidos,x.FechaNacimiento,Age(x.FechaNacimiento),x.TieneDiscapacidad,x.TipoDiscapacidad,x.PorcentajeDiscapacidad);
    private static FamiliarResponse ToFamilyResponse(ActualizacionFamiliar x)=>new(x.Id,x.TipoRelacion,x.Nombres,x.Apellidos,x.FechaNacimiento,Age(x.FechaNacimiento),x.TieneDiscapacidad,x.TipoDiscapacidad,x.PorcentajeDiscapacidad);
    private static int? Age(DateOnly? date){if(date is null)return null;var today=DateOnly.FromDateTime(DateTime.UtcNow);var age=today.Year-date.Value.Year;if(today<date.Value.AddYears(age))age--;return age;}
}
