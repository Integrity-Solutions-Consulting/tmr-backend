using Microsoft.EntityFrameworkCore;
using tmr_backend.Features.Auth.GetCurrentUser.DTOs;
using tmr_backend.Infrastructure.Database;
using tmr_backend.Infrastructure.Shared;

namespace tmr_backend.Features.Auth.GetCurrentUser;

/// <summary>
/// Handler para obtener datos del usuario autenticado.
/// Consulta usuario por ID (LINQ con Include/ThenInclude).
/// </summary>
public class GetCurrentUserHandler
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;

    public GetCurrentUserHandler(
        ApplicationDbContext db,
        ICurrentUserService currentUserService)
    {
        _db = db;
        _currentUserService = currentUserService;
    }

    public async Task<CurrentUserResponse> Handle(CancellationToken ct)
    {
        // TODO: Implementar lógica de obtener usuario actual
        throw new NotImplementedException("GetCurrentUser handler is not implemented yet.");
    }
}
