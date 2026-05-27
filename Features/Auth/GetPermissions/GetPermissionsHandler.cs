using Microsoft.EntityFrameworkCore;
using tmr_backend.Features.Auth.GetPermissions.DTOs;
using tmr_backend.Infrastructure.Database;
using tmr_backend.Infrastructure.Shared;

namespace tmr_backend.Features.Auth.GetPermissions;

/// <summary>
/// Handler para obtener permisos del usuario autenticado.
/// Consulta roles/módulos/menú con LINQ y joins sobre DbContext.
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
        // TODO: Implementar lógica de obtener permisos
        throw new NotImplementedException("GetPermissions handler is not implemented yet.");
    }
}
