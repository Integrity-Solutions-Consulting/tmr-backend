using Microsoft.EntityFrameworkCore;
using tmr_backend.Features.Auth.GetPermissions.DTOs;
using tmr_backend.Infrastructure.Database;
using tmr_backend.Infrastructure.Shared;

namespace tmr_backend.Features.Auth.GetPermissions;

/// <summary>
/// Handler para obtener permisos del usuario autenticado.
/// Consulta roles/módulos/menú con LINQ y joins sobre DbContext.
/// Regla: permisos de usuario prevalecen sobre permisos de rol (OR de flags CRUD).
/// </summary>
public class GetPermissionsHandler
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;

    public GetPermissionsHandler(
        ApplicationDbContext db,
        ICurrentUserService currentUserService)
    {
        _db = db;
        _currentUserService = currentUserService;
    }

    public async Task<UserPermissionsResponse> Handle(CancellationToken ct)
    {
        var userId = _currentUserService.UserId;
        if (userId <= 0)
            throw new UnauthorizedAccessException("Usuario no autenticado.");

        // 1. Obtener roles del usuario
        var userRoles = await _db.TblAutenticacionUsuarios
            .Where(u => u.Id == userId && u.Estaactivo == true)
            .SelectMany(u => u.TblAutenticacionUsuarioRols)
            .Where(ur => ur.Activo)
            .Select(ur => ur.IdrolNavigation!.Valor)
            .Distinct()
            .ToListAsync(ct);

        // 2. Consultar permisos de rol sobre módulos
        var roleModulePermissions = await _db.TblAutenticacionRolModulos
            .Where(rm => rm.Activo && userRoles.Contains(rm.IdrolNavigation.Valor))
            .Include(rm => rm.IdmoduloNavigation)
            .ToListAsync(ct);

        // 3. Consultar permisos directos del usuario sobre módulos
        var userModulePermissions = await _db.TblAutenticacionUsuarioModulos
            .Where(um => um.Idusuario == userId && um.Activo)
            .Include(um => um.IdmoduloNavigation)
            .ToListAsync(ct);

        // 4. Mergear permisos: los permisos de usuario prevalecen sobre los de rol
        var mergedPermissions = new Dictionary<int, (string Name, bool View, bool Create, bool Edit, bool Delete)>();

        // Agregar permisos de rol
        foreach (var rm in roleModulePermissions)
        {
            var moduleId = rm.Idmodulo;
            var moduleName = rm.IdmoduloNavigation.Nombremodulo;

            if (!mergedPermissions.ContainsKey(moduleId))
            {
                mergedPermissions[moduleId] = (moduleName, rm.Puedever ?? false, rm.Puedecrear ?? false, rm.Puedeeditar ?? false, rm.Puedeeliminar ?? false);
            }
        }

        // Override con permisos de usuario (OR lógico para cada flag)
        foreach (var um in userModulePermissions)
        {
            var moduleId = um.Idmodulo;
            var moduleName = um.IdmoduloNavigation.Nombremodulo;

            if (mergedPermissions.ContainsKey(moduleId))
            {
                var existing = mergedPermissions[moduleId];
                mergedPermissions[moduleId] = (
                    moduleName,
                    existing.View || (um.Puedever ?? false),
                    existing.Create || (um.Puedecrear ?? false),
                    existing.Edit || (um.Puedeeditar ?? false),
                    existing.Delete || (um.Puedeeliminar ?? false)
                );
            }
            else
            {
                mergedPermissions[moduleId] = (moduleName, um.Puedever ?? false, um.Puedecrear ?? false, um.Puedeeditar ?? false, um.Puedeeliminar ?? false);
            }
        }

        // 5. Construir array de ModulePermission
        var modules = mergedPermissions
            .Select(kvp =>
            {
                var (moduleName, canView, canCreate, canEdit, canDelete) = kvp.Value;
                var acciones = new List<string>();
                if (canView) acciones.Add("READ");
                if (canCreate) acciones.Add("CREATE");
                if (canEdit) acciones.Add("UPDATE");
                if (canDelete) acciones.Add("DELETE");

                return new ModulePermission(
                    Id: kvp.Key,
                    Nombre: moduleName,
                    Acciones: acciones.ToArray()
                );
            })
            .ToArray();

        // 6. Obtener menú dinámico del usuario
        var menuItems = await GetMenuItemsForUser(userId, userRoles, ct);

        // 7. Retornar respuesta
        return new UserPermissionsResponse(
            Roles: userRoles.ToArray(),
            Modules: modules,
            MenuItems: menuItems
        );
    }

    /// <summary>
    /// Construye el árbol de menú dinámico del usuario.
    /// Prioridad: permisos de usuario > permisos de rol.
    /// </summary>
    private async Task<MenuItem[]> GetMenuItemsForUser(
        int userId,
        List<string> userRoles,
        CancellationToken ct)
    {
        // Obtener menú base activo
        var allMenus = await _db.TblAutenticacionMenus
            .Where(m => m.Activo)
            .ToListAsync(ct);

        // Obtener visibilidad de menú por rol del usuario
        var roleMenuVisibility = await _db.TblAutenticacionMenuRols
            .Where(mr => mr.Activo && userRoles.Contains(mr.IdrolNavigation.Valor))
            .Select(mr => mr.Idmenu)
            .ToListAsync(ct);

        // Obtener visibilidad de menú por usuario (directo)
        var userMenuVisibility = await _db.TblAutenticacionMenuUsuarios
            .Where(mu => mu.Idusuario == userId && mu.Activo)
            .Select(mu => mu.Idmenu)
            .ToListAsync(ct);

        // Menú visible: unión de rol + usuario
        var visibleMenuIds = new HashSet<int>(roleMenuVisibility);
        foreach (var id in userMenuVisibility)
            visibleMenuIds.Add(id);

        // Filtrar menú base solo a elementos visibles
        var visibleMenus = allMenus
            .Where(m => visibleMenuIds.Contains(m.Id))
            .ToList();

        // Construir árbol jerárquico (menú padre -> submenús)
        var rootMenus = visibleMenus
            .Where(m => m.Idmenupadre == null || m.Idmenupadre == 0)
            .OrderBy(m => m.Ordenvisualizacion ?? 0)
            .ToList();

        var menuItems = rootMenus
            .Select(m => BuildMenuItemTree(m, visibleMenus))
            .Where(mi => mi != null)
            .Cast<MenuItem>()
            .ToArray();

        return menuItems;
    }

    /// <summary>
    /// Construye recursivamente el árbol de un MenuItem con sus submenús.
    /// </summary>
    private MenuItem? BuildMenuItemTree(
        Infrastructure.Database.Entities.TblAutenticacionMenu menuEntity,
        List<Infrastructure.Database.Entities.TblAutenticacionMenu> allVisibleMenus)
    {
        // Obtener submenús de este menú
        var submenus = allVisibleMenus
            .Where(m => m.Idmenupadre == menuEntity.Id)
            .OrderBy(m => m.Ordenvisualizacion ?? 0)
            .ToList();

        // Construir recursivamente
        var subItems = submenus
            .Select(m => BuildMenuItemTree(m, allVisibleMenus))
            .Where(mi => mi != null)
            .Cast<MenuItem>()
            .ToArray();

        return new MenuItem(
            Id: menuEntity.Id,
            Nombre: menuEntity.Nombremenu,
            Ruta: menuEntity.Rutamenu ?? "",
            Icono: menuEntity.Icono,
            Orden: menuEntity.Ordenvisualizacion,
            Items: subItems.Length > 0 ? subItems : null
        );
    }
}
