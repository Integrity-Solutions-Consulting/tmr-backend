using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using tmr_backend.Infrastructure.Database;

namespace tmr_backend.Infrastructure.Security;

public sealed class PermissionService(ApplicationDbContext db, IMemoryCache cache) : IPermissionService
{
    // Generación global: al cambiar roles o la matriz rol-permiso se incrementa y todas las
    // entradas anteriores quedan inalcanzables (expiran solas). Ver PermisosCacheInterceptor.
    private static long _generacion;

    private static string CacheKey(int userId) => $"permissions:{Interlocked.Read(ref _generacion)}:{userId}";

    /// <summary>Invalida la caché de permisos de todos los usuarios de este proceso.</summary>
    public static void InvalidarTodos() => Interlocked.Increment(ref _generacion);

    // TTL alineado con el lifetime del Access Token (15 min)
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(15);

    public async Task<IReadOnlyList<string>> GetUserPermissionsAsync(int userId, CancellationToken ct = default)
    {
        // La clave se fija antes de consultar: si los roles cambian durante la consulta, el resultado
        // viejo queda guardado con la generación anterior y nadie lo vuelve a leer.
        var cacheKey = CacheKey(userId);
        if (cache.TryGetValue(cacheKey, out IReadOnlyList<string>? cached))
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
        cache.Set(cacheKey, result, CacheDuration);
        return result;
    }

    public void InvalidateUserPermissions(int userId) =>
        cache.Remove(CacheKey(userId));
}
