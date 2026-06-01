using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using tmr_backend.Infrastructure.Database;
using tmr_backend.Infrastructure.Shared;

namespace tmr_backend.Features.Auth.Authorization;

/// <summary>
/// Handler que valida si usuario tiene permiso sobre módulo + acción.
/// Consulta: permisos de rol + permisos directos, merge con OR lógico.
/// 
/// Precedencia: Permisos de USUARIO > Permisos de ROL
/// (Ejemplo: Si rol permite READ pero usuario tiene DELETE, resultado = DELETE)
/// </summary>
public class HasModulePermissionHandler : AuthorizationHandler<HasModulePermissionRequirement>
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<HasModulePermissionHandler> _logger;

    public HasModulePermissionHandler(
        ApplicationDbContext db,
        ICurrentUserService currentUserService,
        ILogger<HasModulePermissionHandler> logger)
    {
        _db = db;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        HasModulePermissionRequirement requirement)
    {
        var userId = _currentUserService.UserId;
        if (userId <= 0)
        {
            _logger.LogWarning($"Authorization failed: Invalid UserId {userId}");
            context.Fail();
            return;
        }

        try
        {
            // Obtener módulo por nombre
            var modulo = await _db.TblAutenticacionModulos
                .FirstOrDefaultAsync(m => m.Nombremodulo == requirement.ModuleName && m.Activo == true);

            if (modulo == null)
            {
                _logger.LogWarning($"Module '{requirement.ModuleName}' not found or inactive");
                context.Fail();
                return;
            }

            // Obtener roles del usuario (activos)
            var userRoles = await _db.TblAutenticacionUsuarios
                .Where(u => u.Id == userId && u.Estaactivo == true)
                .SelectMany(u => u.TblAutenticacionUsuarioRols)
                .Where(ur => ur.Activo == true)
                .Select(ur => ur.Idrol)
                .ToListAsync();

            if (!userRoles.Any())
            {
                _logger.LogWarning($"User {userId} has no active roles");
                context.Fail();
                return;
            }

            // Verificar permiso según acción normalizada
            bool hasPermission = await CheckPermissionAsync(modulo.Id, userId, userRoles, requirement.Action);

            if (hasPermission)
            {
                _logger.LogInformation($"Authorization SUCCESS: User {userId} has {requirement.Action} on {requirement.ModuleName}");
                context.Succeed(requirement);
            }
            else
            {
                _logger.LogWarning($"Authorization FAILED: User {userId} denied {requirement.Action} on {requirement.ModuleName}");
                context.Fail();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error during authorization check for user {userId}");
            context.Fail();
        }
    }

    /// <summary>
    /// Verifica si el usuario/rol tiene permiso para la acción especificada.
    /// Normaliza acciones: READ/VIEW → Puedever, CREATE → Puedecrear, UPDATE/EDIT → Puedeeditar, DELETE → Puedeeliminar
    /// </summary>
    private async Task<bool> CheckPermissionAsync(
        int moduloId,
        int userId,
        List<int> userRoles,
        string action)
    {
        var normalizedAction = action.ToUpperInvariant();

        return normalizedAction switch
        {
            "READ" or "VIEW" => await CheckViewPermissionAsync(moduloId, userId, userRoles),
            "CREATE" => await CheckCreatePermissionAsync(moduloId, userId, userRoles),
            "UPDATE" or "EDIT" => await CheckUpdatePermissionAsync(moduloId, userId, userRoles),
            "DELETE" => await CheckDeletePermissionAsync(moduloId, userId, userRoles),
            _ => throw new ArgumentException($"Unknown action: {action}")
        };
    }

    private async Task<bool> CheckViewPermissionAsync(int moduloId, int userId, List<int> userRoles)
    {
        // Permisos de rol
        var roleCanView = await _db.TblAutenticacionRolModulos
            .AnyAsync(rm => rm.Idmodulo == moduloId 
                     && userRoles.Contains(rm.Idrol) 
                     && rm.Puedever == true 
                     && rm.Activo == true);

        // Permisos directos del usuario
        var userCanView = await _db.TblAutenticacionUsuarioModulos
            .AnyAsync(um => um.Idmodulo == moduloId 
                     && um.Idusuario == userId 
                     && um.Puedever == true 
                     && um.Activo == true);

        return roleCanView || userCanView;
    }

    private async Task<bool> CheckCreatePermissionAsync(int moduloId, int userId, List<int> userRoles)
    {
        // Permisos de rol
        var roleCanCreate = await _db.TblAutenticacionRolModulos
            .AnyAsync(rm => rm.Idmodulo == moduloId 
                     && userRoles.Contains(rm.Idrol) 
                     && rm.Puedecrear == true 
                     && rm.Activo == true);

        // Permisos directos del usuario
        var userCanCreate = await _db.TblAutenticacionUsuarioModulos
            .AnyAsync(um => um.Idmodulo == moduloId 
                     && um.Idusuario == userId 
                     && um.Puedecrear == true 
                     && um.Activo == true);

        return roleCanCreate || userCanCreate;
    }

    private async Task<bool> CheckUpdatePermissionAsync(int moduloId, int userId, List<int> userRoles)
    {
        // Permisos de rol
        var roleCanEdit = await _db.TblAutenticacionRolModulos
            .AnyAsync(rm => rm.Idmodulo == moduloId 
                     && userRoles.Contains(rm.Idrol) 
                     && rm.Puedeeditar == true 
                     && rm.Activo == true);

        // Permisos directos del usuario
        var userCanEdit = await _db.TblAutenticacionUsuarioModulos
            .AnyAsync(um => um.Idmodulo == moduloId 
                     && um.Idusuario == userId 
                     && um.Puedeeditar == true 
                     && um.Activo == true);

        return roleCanEdit || userCanEdit;
    }

    private async Task<bool> CheckDeletePermissionAsync(int moduloId, int userId, List<int> userRoles)
    {
        // Permisos de rol
        var roleCanDelete = await _db.TblAutenticacionRolModulos
            .AnyAsync(rm => rm.Idmodulo == moduloId 
                     && userRoles.Contains(rm.Idrol) 
                     && rm.Puedeeliminar == true 
                     && rm.Activo == true);

        // Permisos directos del usuario
        var userCanDelete = await _db.TblAutenticacionUsuarioModulos
            .AnyAsync(um => um.Idmodulo == moduloId 
                     && um.Idusuario == userId 
                     && um.Puedeeliminar == true 
                     && um.Activo == true);

        return roleCanDelete || userCanDelete;
    }
}
