using FluentValidation;
using Microsoft.EntityFrameworkCore;
using tmr_backend.Features.Configuracion.Usuarios.Domain.Exceptions;
using tmr_backend.Features.Configuracion.Usuarios.DTOs;
using tmr_backend.Infrastructure.Database;
using tmr_backend.Infrastructure.Database.Entities;
using tmr_backend.Infrastructure.Security;

namespace tmr_backend.Features.Configuracion.Usuarios.Application;

public interface IUsuariosConfigService
{
    Task<CrearUsuarioConfigResponse> CrearUsuarioAsync(CrearUsuarioConfigRequest request, string usuarioActual, string ipActual, int? idUsuarioActual);
    Task<SuccessResponse> ActualizarUsuarioAsync(int idUsuario, UpdateUsuarioRequest request, string usuarioActual, string ipActual, int? idUsuarioActual);
    Task<SuccessResponse> ActualizarEstadoUsuarioAsync(int idUsuario, ActivarUsuarioRequest request, string usuarioActual, string ipActual);
    Task<SuccessResponse> DesactivarUsuarioAsync(int idUsuario, string usuarioActual, string ipActual);
    Task<UsuarioDetalleResponse> ObtenerUsuarioPorIdAsync(int idUsuario);
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
            .AnyAsync(u => u.Email == normalizedEmail);

        if (emailExiste)
            throw new UsuarioEmailYaExisteException(request.email);

        if (request.idPersona.HasValue)
        {
            var personaExiste = await _dbContext.TblAdministracionPersonas
                .AnyAsync(p => p.Id == request.idPersona.Value && p.Activo);

            if (!personaExiste)
                throw new DatosInvalidosException("La persona seleccionada no existe o esta inactiva.");
        }

        await ValidarRolesAsync(request.rolesids);

        var fecha = DateTime.UtcNow;
        var rolesIds = request.rolesids!.Distinct().ToList();
        var passwordHash = _passwordHasher.Hash(request.password);

        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            var usuarioEntity = new TblAutenticacionUsuario
            {
                Idpersona = request.idPersona,
                Email = normalizedEmail,
                Hashpassword = passwordHash,
                Emailverificado = false,
                Intentosfallidos = 0,
                Bloqueadohasta = null,
                Debecambiarpassword = request.debeCambiarPassword ?? true,
                Activo = true,
                Usuariocreacion = usuarioActual,
                Fechacreacion = fecha,
                Ipcreacion = ipActual ?? "127.0.0.1"
            };

            _dbContext.TblAutenticacionUsuarios.Add(usuarioEntity);
            await _dbContext.SaveChangesAsync();

            var rolesEntities = rolesIds.Select(rolId => new TblAutenticacionUsuarioRol
            {
                Idusuario = usuarioEntity.Id,
                Idrol = rolId,
                Asignadopor = idUsuarioActual,
                Asignadoen = fecha,
                Activo = true,
                Usuariocreacion = usuarioActual,
                Fechacreacion = fecha,
                Ipcreacion = ipActual ?? "127.0.0.1"
            }).ToList();

            _dbContext.TblAutenticacionUsuarioRols.AddRange(rolesEntities);

            _dbContext.TblAutenticacionPasswordHistorials.Add(new TblAutenticacionPasswordHistorial
            {
                Idusuario = usuarioEntity.Id,
                Hashpassword = passwordHash,
                Fechacambio = fecha,
                Activo = true,
                Usuariocreacion = usuarioActual,
                Fechacreacion = fecha,
                Ipcreacion = ipActual ?? "127.0.0.1"
            });

            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            var roles = await ObtenerRolesPorIdsAsync(rolesIds);

            return new CrearUsuarioConfigResponse(
                id: usuarioEntity.Id,
                idusuario: usuarioEntity.Id,
                idpersona: usuarioEntity.Idpersona,
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

    public async Task<SuccessResponse> ActualizarUsuarioAsync(int idUsuario, UpdateUsuarioRequest request, string usuarioActual, string ipActual, int? idUsuarioActual)
    {
        var usuario = await _dbContext.TblAutenticacionUsuarios
            .FirstOrDefaultAsync(u => u.Id == idUsuario);

        if (usuario == null)
            throw new UsuarioNoEncontradoException(idUsuario);

        if (request.idPersona.HasValue)
        {
            var personaExiste = await _dbContext.TblAdministracionPersonas
                .AnyAsync(p => p.Id == request.idPersona.Value && p.Activo);

            if (!personaExiste)
                throw new DatosInvalidosException("La persona seleccionada no existe o esta inactiva.");
        }

        if (!string.IsNullOrWhiteSpace(request.email))
        {
            var normalizedEmailCheck = request.email.Trim().ToLowerInvariant();
            var emailDuplicado = await _dbContext.TblAutenticacionUsuarios
                .AnyAsync(u => u.Email == normalizedEmailCheck && u.Id != usuario.Id);

            if (emailDuplicado)
                throw new DatosInvalidosException($"El correo '{request.email}' ya esta registrado por otro usuario.");
        }

        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            var fecha = DateTime.UtcNow;

            usuario.Idpersona = request.idPersona;

            if (!string.IsNullOrWhiteSpace(request.email))
                usuario.Email = request.email.Trim().ToLowerInvariant();

            if (!string.IsNullOrWhiteSpace(request.password))
            {
                usuario.Hashpassword = _passwordHasher.Hash(request.password);

                _dbContext.TblAutenticacionPasswordHistorials.Add(new TblAutenticacionPasswordHistorial
                {
                    Idusuario = usuario.Id,
                    Hashpassword = usuario.Hashpassword,
                    Fechacambio = fecha,
                    Activo = true,
                    Usuariocreacion = usuarioActual,
                    Fechacreacion = fecha,
                    Ipcreacion = ipActual ?? "127.0.0.1"
                });
            }

            if (request.debeCambiarPassword.HasValue)
                usuario.Debecambiarpassword = request.debeCambiarPassword.Value;

            usuario.Usuariomodificacion = usuarioActual;
            usuario.Fechamodificacion = fecha;
            usuario.Ipmodificacion = ipActual;

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
                    Ipcreacion = ipActual ?? "127.0.0.1"
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

    public async Task<SuccessResponse> ActualizarEstadoUsuarioAsync(int idUsuario, ActivarUsuarioRequest request, string usuarioActual, string ipActual)
    {
        var usuario = await _dbContext.TblAutenticacionUsuarios
            .FirstOrDefaultAsync(u => u.Id == idUsuario);

        if (usuario == null)
            throw new UsuarioNoEncontradoException(idUsuario);

        usuario.Activo = request.activo;
        usuario.Usuariomodificacion = usuarioActual;
        usuario.Fechamodificacion = DateTime.UtcNow;
        usuario.Ipmodificacion = ipActual;

        await _dbContext.SaveChangesAsync();

        return new SuccessResponse(request.activo ? "Usuario activado correctamente." : "Usuario desactivado correctamente.", DateTime.UtcNow);
    }

    public async Task<SuccessResponse> DesactivarUsuarioAsync(int idUsuario, string usuarioActual, string ipActual)
    {
        var usuario = await _dbContext.TblAutenticacionUsuarios
            .FirstOrDefaultAsync(u => u.Id == idUsuario);

        if (usuario == null)
            throw new UsuarioNoEncontradoException(idUsuario);

        usuario.Activo = false;
        usuario.Usuariomodificacion = usuarioActual;
        usuario.Fechamodificacion = DateTime.UtcNow;
        usuario.Ipmodificacion = ipActual;

        await _dbContext.SaveChangesAsync();

        return new SuccessResponse("Usuario desactivado correctamente.", DateTime.UtcNow);
    }

    public async Task<UsuarioDetalleResponse> ObtenerUsuarioPorIdAsync(int idUsuario)
    {
        var usuario = await _dbContext.TblAutenticacionUsuarios
            .Include(u => u.IdpersonaNavigation)
            .FirstOrDefaultAsync(u => u.Id == idUsuario);

        if (usuario == null)
            throw new UsuarioNoEncontradoException(idUsuario);

        var rolesData = await ObtenerRolesPorUsuarioAsync(usuario.Id);
        var persona = usuario.IdpersonaNavigation;

        return new UsuarioDetalleResponse(
            id: usuario.Id,
            idUsuario: usuario.Id,
            idPersona: usuario.Idpersona,
            numeroidentificacion: persona?.Numeroidentificacion,
            nombres: persona?.Nombres,
            apellidos: persona?.Apellidos,
            email: usuario.Email,
            idtipoidentificacion: persona?.Idtipoidentificacion,
            tipoidentificacionvalor: null,
            idgenero: persona?.Idgenero,
            generovalor: null,
            idnacionalidad: persona?.Idnacionalidad,
            nacionalidadvalor: null,
            fechanacimiento: persona?.Fechanacimiento,
            telefono: persona?.Telefono,
            direccion: persona?.Direccion,
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
        var dbQuery = _dbContext.TblAutenticacionUsuarios
            .Include(u => u.IdpersonaNavigation)
            .AsQueryable();

        if (query.activo.HasValue)
            dbQuery = dbQuery.Where(u => u.Activo == query.activo.Value);

        if (!string.IsNullOrWhiteSpace(query.search))
        {
            var search = query.search.Trim().ToLower();
            dbQuery = dbQuery.Where(u =>
                u.Email.ToLower().Contains(search) ||
                (u.IdpersonaNavigation != null &&
                    (u.IdpersonaNavigation.Nombres.ToLower().Contains(search) ||
                     u.IdpersonaNavigation.Apellidos.ToLower().Contains(search) ||
                     u.IdpersonaNavigation.Numeroidentificacion.Contains(search))));
        }

        var pageSize = query.pagesize <= 0 ? 10 : query.pagesize;
        var pageNumber = query.pagenumber <= 0 ? 1 : query.pagenumber;
        var totalCount = await dbQuery.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var items = await dbQuery
            .OrderByDescending(u => u.Fechacreacion)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var resultItems = new List<UsuarioListaResponse>();

        foreach (var usuario in items)
        {
            var rolesNombres = await _dbContext.TblAutenticacionUsuarioRols
                .Where(r => r.Idusuario == usuario.Id && r.Activo == true)
                .Include(r => r.IdrolNavigation)
                .Select(r => r.IdrolNavigation.Nombre)
                .ToListAsync();

            var persona = usuario.IdpersonaNavigation;

            resultItems.Add(new UsuarioListaResponse(
                id: usuario.Id,
                idUsuario: usuario.Id,
                idPersona: usuario.Idpersona,
                numeroidentificacion: persona?.Numeroidentificacion,
                nombres: persona?.Nombres,
                apellidos: persona?.Apellidos,
                email: usuario.Email,
                roles: rolesNombres,
                activo: usuario.Activo,
                debecambiarpassword: usuario.Debecambiarpassword,
                ultimologin: usuario.Ultimologin
            ));
        }

        return new PaginatedResponse<UsuarioListaResponse>(
            items: resultItems,
            totalcount: totalCount,
            pagenumber: pageNumber,
            pagesize: pageSize,
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

    private async Task<List<RolResponse>> ObtenerRolesPorUsuarioAsync(int idUsuario)
    {
        var rolesIds = await _dbContext.TblAutenticacionUsuarioRols
            .Where(r => r.Idusuario == idUsuario && r.Activo == true)
            .Select(r => r.Idrol)
            .ToListAsync();

        return await ObtenerRolesPorIdsAsync(rolesIds);
    }
}
