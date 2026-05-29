using tmr_backend.Features.Auth.ChangePassword.DTOs;
using tmr_backend.Infrastructure.Database;
using tmr_backend.Infrastructure.Database.Entities;
using tmr_backend.Infrastructure.Security;
using tmr_backend.Infrastructure.Shared;
using Microsoft.EntityFrameworkCore;

namespace tmr_backend.Features.Auth.ChangePassword;

/// <summary>
/// Handler para el caso de uso de ChangePassword.
/// Verifica old password, actualiza hash, guarda historial.
/// Política: No reutilizar las últimas 5 contraseñas.
/// </summary>
public class ChangePasswordHandler
{
    private readonly ApplicationDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ICurrentUserService _currentUserService;
    private const int PASSWORD_HISTORY_LIMIT = 5;

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
        // 1. Obtener usuario autenticado
        int userId = _currentUserService.UserId;
        var user = await _db.TblAutenticacionUsuarios
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken: ct);

        if (user is null)
            throw new UnauthorizedAccessException("Usuario no encontrado.");

        // 2. Verificar que old password es correcto
        if (!_passwordHasher.Verify(request.OldPassword, user.Hashpassword))
            throw new UnauthorizedAccessException("La contraseña actual es incorrecta.");

        // 3. Consultar últimas 5 contraseñas del historial
        var passwordHistory = await _db.TblAutenticacionPasswordHistorials
            .Where(ph => ph.Idusuario == userId)
            .OrderByDescending(ph => ph.Fechacambio)
            .Take(PASSWORD_HISTORY_LIMIT)
            .ToListAsync(cancellationToken: ct);

        // 4. Validar que no reutiliza contraseña anterior
        foreach (var historicalHash in passwordHistory)
        {
            if (_passwordHasher.Verify(request.NewPassword, historicalHash.Hashpassword))
                throw new InvalidOperationException(
                    $"No puede reutilizar una de las últimas {PASSWORD_HISTORY_LIMIT} contraseñas.");
        }

        // 5. Hash la nueva contraseña
        string newHash = _passwordHasher.Hash(request.NewPassword);

        // 6. Actualizar usuario con nuevo hash
        user.Hashpassword = newHash;
        user.Fechamodificacion = DateTime.UtcNow;
        user.Usuariomodificacion = _currentUserService.Email;

        // 7. Insertar nuevo registro en historial
        var historialEntry = new TblAutenticacionPasswordHistorial
        {
            Idusuario = userId,
            Hashpassword = newHash,
            Fechacambio = DateTime.UtcNow,
            Activo = true,
            Usuariocreacion = _currentUserService.Email,
            Fechacreacion = DateTime.UtcNow,
            Ipcreacion = "0.0.0.0" // TODO: Obtener IP real de HttpContext si es necesario
        };

        _db.TblAutenticacionPasswordHistorials.Add(historialEntry);

        // 8. Guardar cambios
        await _db.SaveChangesAsync(cancellationToken: ct);
    }
}
