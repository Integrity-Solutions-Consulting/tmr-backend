namespace tmr_backend.Infrastructure.Security;

public interface IPermissionService
{
    /// <summary>
    /// Retorna los códigos de permiso del usuario (ej: "PROYECTOS_CREATE").
    /// Primera llamada: consulta BD y guarda en caché 15 min.
    /// Llamadas siguientes: lee directo de caché.
    /// </summary>
    Task<IReadOnlyList<string>> GetUserPermissionsAsync(int userId, CancellationToken ct = default);

    /// <summary>
    /// Elimina los permisos del usuario de la caché.
    /// Llamar cuando un admin modifica los permisos o roles del usuario.
    /// </summary>
    void InvalidateUserPermissions(int userId);
}
