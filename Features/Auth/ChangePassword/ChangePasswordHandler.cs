using tmr_backend.Features.Auth.ChangePassword.DTOs;
using tmr_backend.Infrastructure.Database;
using tmr_backend.Infrastructure.Security;
using tmr_backend.Infrastructure.Shared;

namespace tmr_backend.Features.Auth.ChangePassword;

/// <summary>
/// Handler para el caso de uso de ChangePassword.
/// Verifica old password, actualiza hash, guarda historial.
/// </summary>
public class ChangePasswordHandler
{
    private readonly ApplicationDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ICurrentUserService _currentUserService;

    public ChangePasswordHandler(
        ApplicationDbContext db,
        IPasswordHasher passwordHasher,
        ICurrentUserService currentUserService)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _currentUserService = currentUserService;
    }

    public async Task Handle(ChangePasswordRequest request, CancellationToken ct)
    {
        // TODO: Implementar lógica de cambio de contraseña
        throw new NotImplementedException("ChangePassword handler is not implemented yet.");
    }
}
