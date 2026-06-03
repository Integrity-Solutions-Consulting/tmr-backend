using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using tmr_backend.Features.Configuracion.Usuarios.Domain;
using tmr_backend.Features.Configuracion.Usuarios.DTOs;
using tmr_backend.Features.Configuracion.Usuarios.Domain.Exceptions;
using tmr_backend.Infrastructure.Database;
using tmr_backend.Infrastructure.Database.Entities;

namespace tmr_backend.Features.Configuracion.Usuarios.Application;

public interface IUsuariosConfigService
{
    Task<CrearUsuarioResponse> CrearUsuarioAsync(CreateUsuarioRequest request, string usuarioActual, string ipActual);
    Task<SuccessResponse> ActualizarUsuarioAsync(int idPersona, UpdateUsuarioRequest request, string usuarioActual, string ipActual);
    Task<SuccessResponse> DesactivarUsuarioAsync(int idPersona, string usuarioActual, string ipActual);
    Task<UsuarioDetalleResponse> ObtenerUsuarioPorIdAsync(int idPersona);
    Task<PaginatedResponse<UsuarioListaResponse>> ObtenerUsuariosPaginadosAsync(ObtenerUsuariosQuery query);
}

public class UsuariosConfigService : IUsuariosConfigService
{
    private readonly ApplicationDbContext _dbContext;

    public UsuariosConfigService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<CrearUsuarioResponse> CrearUsuarioAsync(CreateUsuarioRequest request, string usuarioActual, string ipActual)
    {
        var emailExiste = await _dbContext.TblAutenticacionUsuarios
            .AnyAsync(u => u.Email == request.email.ToLower());

        if (emailExiste)
            throw new DatosInvalidosException($"El email '{request.email}' ya está registrado.");

        var identificacionExiste = await _dbContext.TblAdministracionPersonas
            .AnyAsync(p => p.Numeroidentificacion == request.numeroidentificacion);

        if (identificacionExiste)
            throw new DatosInvalidosException("El número de identificación ya está registrado.");

        var usuarioDominio = UsuarioConfiguracion.Crear(
            numeroidentificacion: request.numeroidentificacion,
            nombres: request.nombres,
            apellidos: request.apellidos,
            email: request.email,
            hashPassword: request.password, 
            idTipoIdentificacion: request.idtipoidentificacion,
            idGenero: request.idgenero,
            idNacionalidad: request.idnacionalidad,
            fechaNacimiento: request.fechanacimiento,
            telefono: request.telefono,
            direccion: request.direccion,
            usuarioCreacion: usuarioActual,
            ipCreacion: ipActual,
            rolesIds: request.rolesids
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
                Estaactivo = usuarioDominio.Activo,
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
                Activo = true,
                Usuariocreacion = usuarioDominio.UsuarioCreacion,
                Fechacreacion = usuarioDominio.FechaCreacion,
                Ipcreacion = usuarioDominio.IpCreacion ?? "127.0.0.1"
            }).ToList();

            _dbContext.TblAutenticacionUsuarioRols.AddRange(rolesEntities);
            await _dbContext.SaveChangesAsync();

            await transaction.CommitAsync();

            return new CrearUsuarioResponse(
                id: personaEntity.Id,
                numeroidentificacion: personaEntity.Numeroidentificacion,
                nombres: personaEntity.Nombres,
                apellidos: personaEntity.Apellidos,
                email: usuarioEntity.Email,
                roles: new List<RolResponse>(),
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

    public async Task<SuccessResponse> ActualizarUsuarioAsync(int idPersona, UpdateUsuarioRequest request, string usuarioActual, string ipActual)
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
            persona.Nombres = request.nombres;
            persona.Apellidos = request.apellidos;
            persona.Idgenero = request.idgenero;
            persona.Idnacionalidad = request.idnacionalidad;
            persona.Fechanacimiento = request.fechanacimiento;
            persona.Telefono = request.telefono;
            persona.Direccion = request.direccion;
            persona.Usuariomodificacion = usuarioActual;
            persona.Fechamodificacion = DateTime.UtcNow;
            persona.Ipmodificacion = ipActual;

            _dbContext.TblAdministracionPersonas.Update(persona);

            if (request.rolesids != null && request.rolesids.Any())
            {
                var rolesActuales = await _dbContext.TblAutenticacionUsuarioRols
                    .Where(r => r.Idusuario == usuario.Id)
                    .ToListAsync();
                
                _dbContext.TblAutenticacionUsuarioRols.RemoveRange(rolesActuales);

                var nuevosRoles = request.rolesids.Select(rolId => new TblAutenticacionUsuarioRol
                {
                    Idusuario = usuario.Id,
                    Idrol = rolId,
                    Activo = true,
                    Usuariocreacion = usuarioActual,
                    Fechacreacion = DateTime.UtcNow,
                    Ipcreacion = ipActual
                }).ToList();

                _dbContext.TblAutenticacionUsuarioRols.AddRange(nuevosRoles);
            }

            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            return new SuccessResponse("Usuario actualizado correctamente.");
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
            persona.Activo = false;
            persona.Usuariomodificacion = usuarioActual;
            persona.Fechamodificacion = DateTime.UtcNow;
            persona.Ipmodificacion = ipActual;

            usuario.Activo = false;
            usuario.Estaactivo = false;
            usuario.Usuariomodificacion = usuarioActual;
            usuario.Fechamodificacion = DateTime.UtcNow;
            usuario.Ipmodificacion = ipActual;

            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            return new SuccessResponse("Usuario desactivado correctamente.");
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

        var rolesData = await _dbContext.TblAdministracionCatalogoDetalles
            .Where(c => rolesIds.Contains(c.Id))
            .Select(c => new RolResponse(c.Id, c.Codigovalor, c.Valor))
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

            var rolesNombres = await _dbContext.TblAdministracionCatalogoDetalles
                .Where(c => rolesIds.Contains(c.Id))
                .Select(c => c.Codigovalor)
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
}
