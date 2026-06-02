using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using tmr_backend.Infrastructure.Database;

namespace tmr_backend.Infrastructure.Security;

public sealed class PermissionService(ApplicationDbContext db, IMemoryCache cache) : IPermissionService
{
    private static string CacheKey(int userId) => $"permissions:{userId}";

    // TTL alineado con el lifetime del Access Token (15 min)
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(15);

    public async Task<IReadOnlyList<string>> GetUserPermissionsAsync(int userId, CancellationToken ct = default)
    {
        if (cache.TryGetValue(CacheKey(userId), out IReadOnlyList<string>? cached))
            return cached!;

        // Consulta BD: permisos activos de todos los roles activos del usuario
        var permissions = await db.TblAutenticacionRolPermisos
            .Where(rp =>
                rp.Activo &&
                rp.IdpermisoNavigation.Activo &&
                db.TblAutenticacionUsuarioRols.Any(ur =>
                    ur.Idusuario == userId &&
                    ur.Idrol    == rp.Idrol  &&
                    ur.Activo))
            .Select(rp => rp.IdpermisoNavigation.Codigo)
            .Distinct()
            .ToListAsync(ct);

        var result = (IReadOnlyList<string>)permissions.AsReadOnly();
        cache.Set(CacheKey(userId), result, CacheDuration);
        return result;
    }

    public void InvalidateUserPermissions(int userId) =>
        cache.Remove(CacheKey(userId));
}
