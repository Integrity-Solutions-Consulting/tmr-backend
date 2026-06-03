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
    Task<RolResponse> ObtenerRolPorIdAsync(int id);
    Task<List<RolResponse>> ObtenerRolesAsync();
    Task<List<ModuloResponse>> ObtenerModulosAsync();
}

public class RolesConfigService : IRolesConfigService
{
    private readonly ApplicationDbContext _dbContext;

    public RolesConfigService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    private async Task<int> ObtenerIdCatalogoRolesAsync()
    {
        var catalogo = await _dbContext.TblAdministracionCatalogos
            .FirstOrDefaultAsync(c => c.Codigo == "ROL");
            
        if (catalogo == null)
            throw new DatosInvalidosRolException("El catálogo maestro de Roles no está configurado en la base de datos.");
            
        return catalogo.Id;
    }

    public async Task<SuccessResponse> CrearRolAsync(CreateRolRequest request, string usuarioActual, string ipActual)
    {
        var idCatalogoRoles = await ObtenerIdCatalogoRolesAsync();

        var nombreNormalizado = request.nombre.Trim().ToUpper();
        var existeRol = await _dbContext.TblAdministracionCatalogoDetalles
            .AnyAsync(r => r.Idcatalogo == idCatalogoRoles && r.Valor.ToUpper() == nombreNormalizado);

        if (existeRol)
            throw new RolYaExisteException(request.nombre);

        var rolDominio = RolConfiguracion.Crear(request.nombre, request.descripcion, request.modulosids);

        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            var nuevoRolDetalle = new TblAdministracionCatalogoDetalle
            {
                Idcatalogo = idCatalogoRoles,
                Codigovalor = rolDominio.Nombre.Length > 5 ? rolDominio.Nombre.Substring(0, 5).ToUpper() : rolDominio.Nombre.ToUpper(),
                Valor = rolDominio.Nombre,
                Descripcion = rolDominio.Descripcion,
                Activo = true,
                Usuariocreacion = usuarioActual,
                Fechacreacion = DateTime.UtcNow,
                Ipcreacion = ipActual
            };

            _dbContext.TblAdministracionCatalogoDetalles.Add(nuevoRolDetalle);
            await _dbContext.SaveChangesAsync();

            var modulosEntities = rolDominio.ModulosIds.Select(modId => new TblAutenticacionRolModulo
            {
                Idrol = nuevoRolDetalle.Id,
                Idmodulo = modId,
                Puedever = true,
                Puedecrear = true,
                Puedeeditar = true,
                Puedeeliminar = true,
                Activo = true,
                Usuariocreacion = usuarioActual,
                Fechacreacion = DateTime.UtcNow,
                Ipcreacion = ipActual
            }).ToList();

            _dbContext.TblAutenticacionRolModulos.AddRange(modulosEntities);
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
        var idCatalogoRoles = await ObtenerIdCatalogoRolesAsync();

        var rolEntity = await _dbContext.TblAdministracionCatalogoDetalles
            .FirstOrDefaultAsync(r => r.Id == id && r.Idcatalogo == idCatalogoRoles);

        if (rolEntity == null)
            throw new RolNoEncontradoException(id);

        var nombreNormalizado = request.nombre.Trim().ToUpper();
        var existeOtroRol = await _dbContext.TblAdministracionCatalogoDetalles
            .AnyAsync(r => r.Idcatalogo == idCatalogoRoles && r.Valor.ToUpper() == nombreNormalizado && r.Id != id);

        if (existeOtroRol)
            throw new RolYaExisteException(request.nombre);

        var rolDominio = RolConfiguracion.Crear(request.nombre, request.descripcion, request.modulosids);

        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            rolEntity.Valor = rolDominio.Nombre;
            rolEntity.Descripcion = rolDominio.Descripcion;
            rolEntity.Usuariomodificacion = usuarioActual;
            rolEntity.Fechamodificacion = DateTime.UtcNow;
            rolEntity.Ipmodificacion = ipActual;

            _dbContext.TblAdministracionCatalogoDetalles.Update(rolEntity);

            var modulosActuales = await _dbContext.TblAutenticacionRolModulos
                .Where(rm => rm.Idrol == id)
                .ToListAsync();

            _dbContext.TblAutenticacionRolModulos.RemoveRange(modulosActuales);

            var modulosNuevos = rolDominio.ModulosIds.Select(modId => new TblAutenticacionRolModulo
            {
                Idrol = id,
                Idmodulo = modId,
                Puedever = true,
                Puedecrear = true,
                Puedeeditar = true,
                Puedeeliminar = true,
                Activo = true,
                Usuariocreacion = usuarioActual,
                Fechacreacion = DateTime.UtcNow,
                Ipcreacion = ipActual
            }).ToList();

            _dbContext.TblAutenticacionRolModulos.AddRange(modulosNuevos);

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
        var idCatalogoRoles = await ObtenerIdCatalogoRolesAsync();

        var rolEntity = await _dbContext.TblAdministracionCatalogoDetalles
            .FirstOrDefaultAsync(r => r.Id == id && r.Idcatalogo == idCatalogoRoles);

        if (rolEntity == null)
            throw new RolNoEncontradoException(id);

        var modulos = await _dbContext.TblAutenticacionRolModulos
            .Where(rm => rm.Idrol == id)
            .Join(_dbContext.TblAutenticacionModulos, 
                  rm => rm.Idmodulo, 
                  m => m.Id, 
                  (rm, m) => new ModuloResponse(m.Id, m.Nombremodulo))
            .ToListAsync();

        return new RolResponse(
            id: rolEntity.Id,
            nombre: rolEntity.Valor,
            descripcion: rolEntity.Descripcion ?? "",
            modulos: modulos,
            activo: rolEntity.Activo
        );
    }

    public async Task<List<RolResponse>> ObtenerRolesAsync()
    {
        var idCatalogoRoles = await ObtenerIdCatalogoRolesAsync();

        var roles = await _dbContext.TblAdministracionCatalogoDetalles
            .Where(r => r.Idcatalogo == idCatalogoRoles && r.Activo)
            .ToListAsync();

        var result = new List<RolResponse>();
        foreach (var rol in roles)
        {
            var modulos = await _dbContext.TblAutenticacionRolModulos
                .Where(rm => rm.Idrol == rol.Id)
                .Join(_dbContext.TblAutenticacionModulos, 
                      rm => rm.Idmodulo, 
                      m => m.Id, 
                      (rm, m) => new ModuloResponse(m.Id, m.Nombremodulo))
                .ToListAsync();

            result.Add(new RolResponse(
                id: rol.Id,
                nombre: rol.Valor,
                descripcion: rol.Descripcion ?? "",
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
