using FluentValidation;
using Microsoft.EntityFrameworkCore;
using tmr_backend.Features.Configuracion.Usuarios.Domain;
using tmr_backend.Features.Configuracion.Usuarios.DTOs;
using tmr_backend.Features.Configuracion.Usuarios.Domain.Exceptions;
using tmr_backend.Infrastructure.Database;
using tmr_backend.Infrastructure.Database.Entities;
using tmr_backend.Infrastructure.Security;

namespace tmr_backend.Features.Configuracion.Usuarios.Application;

public interface IUsuariosConfigService
{
    Task<CrearUsuarioConfigResponse> CrearUsuarioAsync(CrearUsuarioConfigRequest request, string usuarioActual, string ipActual, int? idUsuarioActual);
    Task<SuccessResponse> ActualizarUsuarioAsync(int idPersona, UpdateUsuarioRequest request, string usuarioActual, string ipActual, int? idUsuarioActual);
    Task<SuccessResponse> DesactivarUsuarioAsync(int idPersona, string usuarioActual, string ipActual);
    Task<UsuarioDetalleResponse> ObtenerUsuarioPorIdAsync(int idPersona);
    Task<PaginatedResponse<UsuarioListaResponse>> ObtenerUsuariosPaginadosAsync(ObtenerUsuariosQuery query);
}

public class UsuariosConfigService : IUsuariosConfigService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IValidator<CrearUsuarioConfigRequest> _crearUsuarioValidator;

    public UsuariosConfigService(
        ApplicationDbContext dbContext,
        IPasswordHasher passwordHasher,
        IValidator<CrearUsuarioConfigRequest> crearUsuarioValidator)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _crearUsuarioValidator = crearUsuarioValidator;
    }

    public async Task<CrearUsuarioConfigResponse> CrearUsuarioAsync(CrearUsuarioConfigRequest request, string usuarioActual, string ipActual, int? idUsuarioActual)
    {
        var validation = await _crearUsuarioValidator.ValidateAsync(request);
        if (!validation.IsValid)
            throw new ValidationException(validation.Errors);

        var normalizedEmail = request.email.Trim().ToLowerInvariant();

        var emailExiste = await _dbContext.TblAutenticacionUsuarios
            .AnyAsync(u => u.Email == normalizedEmail)
            || await _dbContext.TblAdministracionPersonas
                .AnyAsync(p => p.Email != null && p.Email.ToLower() == normalizedEmail);

        if (emailExiste)
            throw new UsuarioEmailYaExisteException(request.email);

        var identificacionExiste = await _dbContext.TblAdministracionPersonas
            .AnyAsync(p => p.Numeroidentificacion == request.numeroidentificacion);

        if (identificacionExiste)
            throw new UsuarioIdentificacionYaExisteException(request.numeroidentificacion);

        await ValidarRolesAsync(request.rolesids);

        var rolesIds = request.rolesids!.Distinct().ToList();
        var passwordHash = _passwordHasher.Hash(request.password);

        var usuarioDominio = UsuarioConfiguracion.Crear(
            numeroidentificacion: request.numeroidentificacion,
            nombres: request.nombres,
            apellidos: request.apellidos,
            email: normalizedEmail,
            hashPassword: passwordHash,
            idTipoIdentificacion: request.idtipoidentificacion,
            idGenero: request.idgenero,
            idNacionalidad: request.idnacionalidad,
            fechaNacimiento: request.fechanacimiento,
            telefono: request.telefono,
            direccion: request.direccion,
            usuarioCreacion: usuarioActual,
            ipCreacion: ipActual,
            rolesIds: rolesIds
        );

        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            var personaEntity = new TblAdministracionPersona
            {
                Numeroidentificacion = usuarioDominio.NumeroIdentificacion,
                Idtipoidentificacion = usuarioDominio.IdTipoIdentificacion,
                Idgenero = usuarioDominio.IdGenero,
                Idnacionalidad = usuarioDominio.IdNacionalidad,
                Tipopersona = "NATURAL",
                Nombres = usuarioDominio.Nombres,
                Apellidos = usuarioDominio.Apellidos,
                Fechanacimiento = usuarioDominio.FechaNacimiento,
                Email = usuarioDominio.Email,
                Telefono = usuarioDominio.Telefono,
                Direccion = usuarioDominio.Direccion,
                Activo = usuarioDominio.Activo,
                Usuariocreacion = usuarioDominio.UsuarioCreacion,
                Fechacreacion = usuarioDominio.FechaCreacion,
                Ipcreacion = usuarioDominio.IpCreacion ?? "127.0.0.1"
            };

            _dbContext.TblAdministracionPersonas.Add(personaEntity);
            await _dbContext.SaveChangesAsync();

            var usuarioEntity = new TblAutenticacionUsuario
            {
                Idpersona = personaEntity.Id,
                Email = usuarioDominio.Email,
                Hashpassword = usuarioDominio.HashPassword,
                Emailverificado = false,
                Intentosfallidos = 0,
                Bloqueadohasta = null,
                Debecambiarpassword = usuarioDominio.DebeCambiarPassword,
                Activo = usuarioDominio.Activo,
                Usuariocreacion = usuarioDominio.UsuarioCreacion,
                Fechacreacion = usuarioDominio.FechaCreacion,
                Ipcreacion = usuarioDominio.IpCreacion ?? "127.0.0.1"
            };

            _dbContext.TblAutenticacionUsuarios.Add(usuarioEntity);
            await _dbContext.SaveChangesAsync();

            var rolesEntities = usuarioDominio.RolesIds.Select(rolId => new TblAutenticacionUsuarioRol
            {
                Idusuario = usuarioEntity.Id,
                Idrol = rolId,
                Asignadopor = idUsuarioActual,
                Asignadoen = usuarioDominio.FechaCreacion,
                Activo = true,
                Usuariocreacion = usuarioDominio.UsuarioCreacion,
                Fechacreacion = usuarioDominio.FechaCreacion,
                Ipcreacion = usuarioDominio.IpCreacion ?? "127.0.0.1"
            }).ToList();

            _dbContext.TblAutenticacionUsuarioRols.AddRange(rolesEntities);

            _dbContext.TblAutenticacionPasswordHistorials.Add(new TblAutenticacionPasswordHistorial
            {
                Idusuario = usuarioEntity.Id,
                Hashpassword = usuarioDominio.HashPassword,
                Fechacambio = usuarioDominio.FechaCreacion,
                Activo = true,
                Usuariocreacion = usuarioDominio.UsuarioCreacion,
                Fechacreacion = usuarioDominio.FechaCreacion,
                Ipcreacion = usuarioDominio.IpCreacion ?? "127.0.0.1"
            });

            await _dbContext.SaveChangesAsync();

            await transaction.CommitAsync();

            var roles = await ObtenerRolesPorIdsAsync(usuarioDominio.RolesIds);

            return new CrearUsuarioConfigResponse(
                id: personaEntity.Id,
                idpersona: personaEntity.Id,
                idusuario: usuarioEntity.Id,
                numeroidentificacion: personaEntity.Numeroidentificacion,
                nombres: personaEntity.Nombres,
                apellidos: personaEntity.Apellidos,
                email: usuarioEntity.Email,
                roles: roles,
                activo: usuarioEntity.Activo,
                debecambiarpassword: usuarioEntity.Debecambiarpassword,
                fechacreacion: usuarioEntity.Fechacreacion
            );
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<SuccessResponse> ActualizarUsuarioAsync(int idPersona, UpdateUsuarioRequest request, string usuarioActual, string ipActual, int? idUsuarioActual)
    {
        var persona = await _dbContext.TblAdministracionPersonas.FindAsync(idPersona);
        if (persona == null)
            throw new UsuarioNoEncontradoException(idPersona);

        var usuario = await _dbContext.TblAutenticacionUsuarios.FirstOrDefaultAsync(u => u.Idpersona == idPersona);
        if (usuario == null)
            throw new DatosInvalidosException("La persona no tiene un usuario de autenticación vinculado.");

        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            var fecha = DateTime.UtcNow;

            persona.Nombres = request.nombres;
            persona.Apellidos = request.apellidos;
            persona.Idgenero = request.idgenero;
            persona.Idnacionalidad = request.idnacionalidad;
            persona.Fechanacimiento = request.fechanacimiento;
            persona.Telefono = request.telefono;
            persona.Direccion = request.direccion;
            persona.Usuariomodificacion = usuarioActual;
            persona.Fechamodificacion = fecha;
            persona.Ipmodificacion = ipActual;

            _dbContext.TblAdministracionPersonas.Update(persona);

            if (request.rolesids != null)
            {
                await ValidarRolesAsync(request.rolesids);

                var rolesActuales = await _dbContext.TblAutenticacionUsuarioRols
                    .Where(r => r.Idusuario == usuario.Id)
                    .ToListAsync();
                
                _dbContext.TblAutenticacionUsuarioRols.RemoveRange(rolesActuales);

                var nuevosRoles = request.rolesids.Distinct().Select(rolId => new TblAutenticacionUsuarioRol
                {
                    Idusuario = usuario.Id,
                    Idrol = rolId,
                    Asignadopor = idUsuarioActual,
                    Asignadoen = fecha,
                    Activo = true,
                    Usuariocreacion = usuarioActual,
                    Fechacreacion = fecha,
                    Ipcreacion = ipActual
                }).ToList();

                _dbContext.TblAutenticacionUsuarioRols.AddRange(nuevosRoles);
            }

            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            return new SuccessResponse("Usuario actualizado correctamente.", DateTime.UtcNow);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<SuccessResponse> DesactivarUsuarioAsync(int idPersona, string usuarioActual, string ipActual)
    {
        var persona = await _dbContext.TblAdministracionPersonas.FindAsync(idPersona);
        if (persona == null)
            throw new UsuarioNoEncontradoException(idPersona);

        var usuario = await _dbContext.TblAutenticacionUsuarios.FirstOrDefaultAsync(u => u.Idpersona == idPersona);
        if (usuario == null)
            throw new DatosInvalidosException("La persona no tiene un usuario de autenticación vinculado.");

        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            var fecha = DateTime.UtcNow;

            persona.Activo = false;
            persona.Usuariomodificacion = usuarioActual;
            persona.Fechamodificacion = fecha;
            persona.Ipmodificacion = ipActual;

            usuario.Activo = false;
            usuario.Usuariomodificacion = usuarioActual;
            usuario.Fechamodificacion = fecha;
            usuario.Ipmodificacion = ipActual;

            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            return new SuccessResponse("Usuario desactivado correctamente.", DateTime.UtcNow);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<UsuarioDetalleResponse> ObtenerUsuarioPorIdAsync(int idPersona)
    {
        var persona = await _dbContext.TblAdministracionPersonas
            .FirstOrDefaultAsync(p => p.Id == idPersona);

        if (persona == null)
            throw new UsuarioNoEncontradoException(idPersona);

        var usuario = await _dbContext.TblAutenticacionUsuarios
            .FirstOrDefaultAsync(u => u.Idpersona == idPersona);

        if (usuario == null)
            throw new DatosInvalidosException("La persona no tiene un usuario asociado.");

        var rolesIds = await _dbContext.TblAutenticacionUsuarioRols
            .Where(r => r.Idusuario == usuario.Id && r.Activo == true)
            .Select(r => r.Idrol)
            .ToListAsync();

        var rolesData = await _dbContext.TblAutenticacionRols
            .Where(c => rolesIds.Contains(c.Id))
            .Select(c => new RolResponse(c.Id, c.Nombre, c.Descripcion))
            .ToListAsync();

        return new UsuarioDetalleResponse(
            id: persona.Id,
            numeroidentificacion: persona.Numeroidentificacion,
            nombres: persona.Nombres,
            apellidos: persona.Apellidos,
            email: usuario.Email,
            idtipoidentificacion: persona.Idtipoidentificacion,
            tipoidentificacionvalor: null, 
            idgenero: persona.Idgenero,
            generovalor: null,
            idnacionalidad: persona.Idnacionalidad,
            nacionalidadvalor: null,
            fechanacimiento: persona.Fechanacimiento,
            telefono: persona.Telefono,
            direccion: persona.Direccion,
            roles: rolesData,
            activo: usuario.Activo,
            debecambiarpassword: usuario.Debecambiarpassword,
            ultimologin: usuario.Ultimologin,
            fechacreacion: usuario.Fechacreacion,
            usuariocreacion: usuario.Usuariocreacion,
            fechamodificacion: usuario.Fechamodificacion,
            usuariomodificacion: usuario.Usuariomodificacion
        );
    }

    public async Task<PaginatedResponse<UsuarioListaResponse>> ObtenerUsuariosPaginadosAsync(ObtenerUsuariosQuery query)
    {
        var dbQuery = _dbContext.TblAdministracionPersonas
            .Join(_dbContext.TblAutenticacionUsuarios,
                p => p.Id,
                u => u.Idpersona,
                (p, u) => new { Persona = p, Usuario = u })
            .AsQueryable();

        if (query.activo.HasValue)
        {
            dbQuery = dbQuery.Where(x => x.Persona.Activo == query.activo.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.search))
        {
            var search = query.search.ToLower();
            dbQuery = dbQuery.Where(x => 
                x.Persona.Nombres.ToLower().Contains(search) ||
                x.Persona.Apellidos.ToLower().Contains(search) ||
                x.Persona.Numeroidentificacion.Contains(search) ||
                x.Usuario.Email.ToLower().Contains(search));
        }

        var totalCount = await dbQuery.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)query.pagesize);

        var items = await dbQuery
            .OrderByDescending(x => x.Persona.Fechacreacion)
            .Skip((query.pagenumber - 1) * query.pagesize)
            .Take(query.pagesize)
            .ToListAsync();

        var resultItems = new List<UsuarioListaResponse>();

        foreach (var item in items)
        {
            var rolesIds = await _dbContext.TblAutenticacionUsuarioRols
                .Where(r => r.Idusuario == item.Usuario.Id && r.Activo == true)
                .Select(r => r.Idrol)
                .ToListAsync();

            var rolesNombres = await _dbContext.TblAutenticacionRols
                .Where(c => rolesIds.Contains(c.Id))
                .Select(c => c.Nombre)
                .ToListAsync();

            resultItems.Add(new UsuarioListaResponse(
                id: item.Persona.Id,
                numeroidentificacion: item.Persona.Numeroidentificacion,
                nombres: item.Persona.Nombres,
                apellidos: item.Persona.Apellidos,
                email: item.Usuario.Email,
                roles: rolesNombres,
                activo: item.Usuario.Activo,
                ultimologin: item.Usuario.Ultimologin
            ));
        }

        return new PaginatedResponse<UsuarioListaResponse>(
            items: resultItems,
            totalcount: totalCount,
            pagenumber: query.pagenumber,
            pagesize: query.pagesize,
            totalpages: totalPages
        );
    }

    private async Task ValidarRolesAsync(List<int>? rolesIds)
    {
        if (rolesIds == null || rolesIds.Count == 0)
            throw new UsuarioSinRolesException();

        var rolesUnicos = rolesIds.Distinct().ToList();
        var rolesExistentes = await _dbContext.TblAutenticacionRols
            .Where(r => rolesUnicos.Contains(r.Id) && r.Activo)
            .Select(r => r.Id)
            .ToListAsync();

        var rolesInvalidos = rolesUnicos.Except(rolesExistentes).ToList();
        if (rolesInvalidos.Count > 0)
            throw new DatosInvalidosException($"Roles invalidos o inactivos: {string.Join(", ", rolesInvalidos)}.");
    }

    private async Task<List<RolResponse>> ObtenerRolesPorIdsAsync(IEnumerable<int> rolesIds)
    {
        var ids = rolesIds.Distinct().ToList();
        return await _dbContext.TblAutenticacionRols
            .Where(r => ids.Contains(r.Id))
            .Select(r => new RolResponse(r.Id, r.Nombre, r.Descripcion))
            .ToListAsync();
    }
}
