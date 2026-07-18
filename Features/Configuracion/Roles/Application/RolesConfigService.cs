using Microsoft.EntityFrameworkCore;
using tmr_backend.Features.Configuracion.Roles.Domain;
using tmr_backend.Features.Configuracion.Roles.DTOs;
using tmr_backend.Features.Configuracion.Roles.Domain.Exceptions;
using tmr_backend.Infrastructure.Database;
using tmr_backend.Infrastructure.Database.Entities;

namespace tmr_backend.Features.Configuracion.Roles.Application;

public interface IRolesConfigService
{
    Task<SuccessResponse> CrearRolAsync(CreateRolRequest request, string usuarioActual, string ipActual);
    Task<SuccessResponse> ActualizarRolAsync(int id, UpdateRolRequest request, string usuarioActual, string ipActual);
    Task<RolResponse> GetByIdAsync(int id);
    Task<RolResponse> ObtenerRolPorIdAsync(int id);
    Task<List<RolResponse>> ObtenerRolesAsync();
    Task<List<ModuloResponse>> ObtenerModulosAsync();
    Task<SuccessResponse> ActualizarEstadoAsync(int id, ActivarRolRequest request, string usuarioActual, string ipActual);
    Task<bool> TieneUsuariosAsignadosAsync(int id);
    Task<SuccessResponse> EliminarRolAsync(int id, string usuarioActual, string ipActual);
}

public class RolesConfigService : IRolesConfigService
{
    private readonly ApplicationDbContext _dbContext;

    public RolesConfigService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<SuccessResponse> CrearRolAsync(CreateRolRequest request, string usuarioActual, string ipActual)
    {
        var rolDominio = RolConfiguracion.Crear(request.nombre, request.descripcion, request.modulosids);
        var nombreNormalizado = rolDominio.Nombre.Trim().ToUpper();

        var existeRol = await _dbContext.TblAutenticacionRols
            .AnyAsync(r => r.Nombre.ToUpper() == nombreNormalizado && r.Activo);

        if (existeRol)
            throw new RolYaExisteException(request.nombre);

        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            var nuevoRol = new TblAutenticacionRol
            {
                Nombre = rolDominio.Nombre,
                Descripcion = rolDominio.Descripcion,
                Essistema = false,
                Activo = true,
                Usuariocreacion = usuarioActual,
                Fechacreacion = DateTime.UtcNow,
                Ipcreacion = ipActual
            };

            _dbContext.TblAutenticacionRols.Add(nuevoRol);
            await _dbContext.SaveChangesAsync();

            // Opción 1: Obtener permisos para los módulos especificados y crear rol_permiso
            var permisos = await _dbContext.TblAutenticacionPermisos
                .Where(p => rolDominio.ModulosIds.Contains(p.Idmodulo) && p.Activo)
                .ToListAsync();

            if (permisos.Count == 0)
                throw new InvalidOperationException("No se encontraron permisos para los módulos especificados.");

            var rolesPermisos = permisos.Select(perm => new TblAutenticacionRolPermiso
            {
                Idrol = nuevoRol.Id,
                Idpermiso = perm.Id,
                Otorgado = DateTime.UtcNow,
                Activo = true,
                Usuariocreacion = usuarioActual,
                Fechacreacion = DateTime.UtcNow,
                Ipcreacion = ipActual
            }).ToList();

            _dbContext.TblAutenticacionRolPermisos.AddRange(rolesPermisos);
            await _dbContext.SaveChangesAsync();

            await transaction.CommitAsync();
            return new SuccessResponse("Rol creado exitosamente.");
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<SuccessResponse> ActualizarRolAsync(int id, UpdateRolRequest request, string usuarioActual, string ipActual)
    {
        var rolEntity = await _dbContext.TblAutenticacionRols
            .FirstOrDefaultAsync(r => r.Id == id && r.Activo);

        if (rolEntity == null)
            throw new RolNoEncontradoException(id);

        var rolDominio = RolConfiguracion.Crear(request.nombre, request.descripcion, request.modulosids);
        var nombreNormalizado = rolDominio.Nombre.Trim().ToUpper();

        var existeOtroRol = await _dbContext.TblAutenticacionRols
            .AnyAsync(r => r.Nombre.ToUpper() == nombreNormalizado && r.Id != id && r.Activo);

        if (existeOtroRol)
            throw new RolYaExisteException(request.nombre);

        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            rolEntity.Nombre = rolDominio.Nombre;
            rolEntity.Descripcion = rolDominio.Descripcion;
            rolEntity.Usuariomodificacion = usuarioActual;
            rolEntity.Fechamodificacion = DateTime.UtcNow;
            rolEntity.Ipmodificacion = ipActual;

            _dbContext.TblAutenticacionRols.Update(rolEntity);

            // Opción 1: Remover permisos antiguos y crear nuevos desde módulos
            var permisosAntiguos = await _dbContext.TblAutenticacionRolPermisos
                .Where(rp => rp.Idrol == id)
                .ToListAsync();

            _dbContext.TblAutenticacionRolPermisos.RemoveRange(permisosAntiguos);

            var permisosNuevos = await _dbContext.TblAutenticacionPermisos
                .Where(p => rolDominio.ModulosIds.Contains(p.Idmodulo) && p.Activo)
                .ToListAsync();

            if (permisosNuevos.Count == 0)
                throw new InvalidOperationException("No se encontraron permisos para los módulos especificados.");

            var rolesPermisosNuevos = permisosNuevos.Select(perm => new TblAutenticacionRolPermiso
            {
                Idrol = id,
                Idpermiso = perm.Id,
                Otorgado = DateTime.UtcNow,
                Activo = true,
                Usuariocreacion = usuarioActual,
                Fechacreacion = DateTime.UtcNow,
                Ipcreacion = ipActual
            }).ToList();

            _dbContext.TblAutenticacionRolPermisos.AddRange(rolesPermisosNuevos);
            await _dbContext.SaveChangesAsync();

            await transaction.CommitAsync();
            return new SuccessResponse("Rol actualizado exitosamente.");
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<RolResponse> ObtenerRolPorIdAsync(int id)
    {
        return await GetByIdAsync(id);
    }

    public async Task<RolResponse> GetByIdAsync(int id)
    {
        var rolEntity = await _dbContext.TblAutenticacionRols
            .FirstOrDefaultAsync(r => r.Id == id && r.Activo);

        if (rolEntity == null)
            throw new RolNoEncontradoException(id);

        // Opción 1: Obtener módulos inferidos desde permisos (rol → rol_permiso → permiso → módulo)
        var modulos = await _dbContext.TblAutenticacionRolPermisos
            .Where(rp => rp.Idrol == id && rp.Activo)
            .Join(_dbContext.TblAutenticacionPermisos,
                  rp => rp.Idpermiso,
                  p => p.Id,
                  (rp, p) => new { Permiso = p })
            .Join(_dbContext.TblAutenticacionModulos,
                  x => x.Permiso.Idmodulo,
                  m => m.Id,
                  (x, m) => new ModuloResponse(m.Id, m.Nombremodulo))
            .Distinct()
            .ToListAsync();

        return new RolResponse(
            id: rolEntity.Id,
            nombre: rolEntity.Nombre,
            descripcion: rolEntity.Descripcion ?? string.Empty,
            modulos: modulos,
            activo: rolEntity.Activo
        );
    }

    public async Task<SuccessResponse> ActualizarEstadoAsync(int id, ActivarRolRequest request, string usuarioActual, string ipActual)
    {
        var rolEntity = await _dbContext.TblAutenticacionRols
            .FirstOrDefaultAsync(r => r.Id == id);

        if (rolEntity == null)
            throw new RolNoEncontradoException(id);

        if (rolEntity.Essistema && !request.activo)
            throw new DatosInvalidosRolException("No se puede desactivar un rol de sistema.");

        if (!request.activo && await TieneUsuariosAsignadosAsync(id))
            throw new DatosInvalidosRolException("No se puede desactivar un rol con usuarios asignados.");

        var fecha = DateTime.UtcNow;
        rolEntity.Activo = request.activo;
        rolEntity.Usuariomodificacion = usuarioActual;
        rolEntity.Fechamodificacion = fecha;
        rolEntity.Ipmodificacion = ipActual;

        _dbContext.TblAutenticacionRols.Update(rolEntity);
        await _dbContext.SaveChangesAsync();

        return new SuccessResponse(request.activo ? "Rol activado exitosamente." : "Rol desactivado exitosamente.");
    }

    public async Task<bool> TieneUsuariosAsignadosAsync(int id)
    {
        return await _dbContext.TblAutenticacionUsuarioRols
            .AnyAsync(ur => ur.Idrol == id && ur.Activo && ur.IdusuarioNavigation.Activo);
    }

    public async Task<SuccessResponse> EliminarRolAsync(int id, string usuarioActual, string ipActual)
    {
        var rolEntity = await _dbContext.TblAutenticacionRols
            .FirstOrDefaultAsync(r => r.Id == id);

        if (rolEntity == null)
            throw new RolNoEncontradoException(id);

        if (rolEntity.Essistema)
            throw new DatosInvalidosRolException("No se puede eliminar un rol de sistema.");

        if (await TieneUsuariosAsignadosAsync(id))
            throw new DatosInvalidosRolException("No se puede eliminar un rol con usuarios asignados.");

        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            var fecha = DateTime.UtcNow;
            rolEntity.Activo = false;
            rolEntity.Usuariomodificacion = usuarioActual;
            rolEntity.Fechamodificacion = fecha;
            rolEntity.Ipmodificacion = ipActual;

            var permisosRol = await _dbContext.TblAutenticacionRolPermisos
                .Where(rp => rp.Idrol == id)
                .ToListAsync();

            foreach (var permisoRol in permisosRol)
            {
                permisoRol.Activo = false;
            }

            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            return new SuccessResponse("Rol eliminado exitosamente.");
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<List<RolResponse>> ObtenerRolesAsync()
    {
        var roles = await _dbContext.TblAutenticacionRols
            .OrderByDescending(r => r.Fechacreacion)
            .ToListAsync();

        var result = new List<RolResponse>();

        foreach (var rol in roles)
        {
            // Opción 1: Obtener módulos inferidos desde permisos (rol → rol_permiso → permiso → módulo)
            var modulos = await _dbContext.TblAutenticacionRolPermisos
                .Where(rp => rp.Idrol == rol.Id && rp.Activo)
                .Join(_dbContext.TblAutenticacionPermisos,
                      rp => rp.Idpermiso,
                      p => p.Id,
                      (rp, p) => new { Permiso = p })
                .Join(_dbContext.TblAutenticacionModulos,
                      x => x.Permiso.Idmodulo,
                      m => m.Id,
                      (x, m) => new ModuloResponse(m.Id, m.Nombremodulo))
                .Distinct()
                .ToListAsync();

            result.Add(new RolResponse(
                id: rol.Id,
                nombre: rol.Nombre,
                descripcion: rol.Descripcion ?? string.Empty,
                modulos: modulos,
                activo: rol.Activo
            ));
        }

        return result;
    }

    public async Task<List<ModuloResponse>> ObtenerModulosAsync()
    {
        return await _dbContext.TblAutenticacionModulos
            .Where(m => m.Activo)
            .Select(m => new ModuloResponse(m.Id, m.Nombremodulo))
            .ToListAsync();
    }
}
