using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using tmr_backend.Features.Auth.DTOs.Request;
using tmr_backend.Features.Auth.DTOs.Response;
using tmr_backend.Features.Auth.Mappings;
using tmr_backend.Infrastructure.Database;
using tmr_backend.Infrastructure.Database.Entities;
using tmr_backend.Infrastructure.Security;
using tmr_backend.Shared.Exceptions;

namespace tmr_backend.Features.Auth.Services;

public sealed class AuthService(
    ApplicationDbContext db,
    ITokenService tokenService,
    IHttpContextAccessor httpContextAccessor,
    IPasswordHasher passwordHasher,
    IValidator<RegisterRequest> registerValidator,
    IValidator<LoginRequest> loginValidator,
    IOptions<JwtSettings> jwtOptions) : IAuthService
{
    private const int MaxFailedAttempts = 5;
    private const int LockoutMinutes    = 15;
    private const int PASSWORD_HISTORY_LIMIT = 5;
    private readonly JwtSettings _jwt   = jwtOptions.Value;

    // ─────────────────────────────────────────────────────────────────────────
    // REGISTER — crea persona + usuario + rol base. Sin tokens.
    // ─────────────────────────────────────────────────────────────────────────
    public async Task<RegisterResponse> RegisterAsync(RegisterRequest request, HttpContext context, CancellationToken ct)
    {
        var clientIp   = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var validation = await registerValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            throw new ValidationException(validation.Errors);

        var normalizedEmail = request.Email.ToLowerInvariant();
        var contraseniaDefecto = "Int3gr1ty123!"; // Contraseña por defecto (debe cambiar en el primer login)

        var existe = await db.TblAutenticacionUsuarios
            .AnyAsync(u => u.Email == normalizedEmail, ct);
        if (existe)
            throw new ConflictException("El email ya está registrado.");

        var rolExiste = await db.TblAutenticacionRols
            .AnyAsync(r => r.Id == request.IdRol && r.Activo, ct);
        if (!rolExiste)
            throw new ValidationException("El rol enviado no existe o no esta activo.");

        var hash = passwordHasher.Hash(contraseniaDefecto);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var fecha = DateTime.UtcNow;
            var persona = new TblAdministracionPersona
            {
                Idgenero             = request.IdGenero,
                Idnacionalidad       = request.IdNacionalidad,
                Idtipoidentificacion = request.IdTipoIdentificacion,
                Numeroidentificacion = request.Numeroidentificacion ?? "0000000000",
                Tipopersona          = request.TipoPersona ?? "NATURAL",
                Nombres              = request.Nombres,
                Apellidos            = request.Apellidos,
                Fechanacimiento      = DateOnly.Parse(request.FechaNacimiento),
                Email                = normalizedEmail,
                Telefono             = request.Telefono ?? "",
                Direccion            = request.Direccion ?? "",
                Activo               = true,
                Usuariocreacion      = request.Usuario,
                Fechacreacion        = fecha,
                Ipcreacion           = clientIp
            };
            db.TblAdministracionPersonas.Add(persona);
            await db.SaveChangesAsync(ct);

            var usuario = new TblAutenticacionUsuario
            {
                Idpersona            = persona.Id,
                Email                = normalizedEmail,
                Hashpassword         = hash,
                Emailverificado      = false,
                Intentosfallidos     = 0,
                Bloqueadohasta       = null,
                Debecambiarpassword  = true,
                Activo               = true,
                Usuariocreacion      = request.Usuario,
                Fechacreacion        = fecha,
                Ipcreacion           = clientIp
            };
            db.TblAutenticacionUsuarios.Add(usuario);
            await db.SaveChangesAsync(ct);

            var usuarioRol = new TblAutenticacionUsuarioRol
            {
                Idusuario       = usuario.Id,
                Idrol           = request.IdRol,
                Asignadoen      = fecha,
                Activo          = true,
                Usuariocreacion = request.Usuario,
                Fechacreacion   = fecha,
                Ipcreacion      = clientIp
            };
            db.TblAutenticacionUsuarioRols.Add(usuarioRol);
            
            var historialEntry = new TblAutenticacionPasswordHistorial
            {
                Idusuario       = usuario.Id,
                Hashpassword    = hash,
                Fechacambio     = fecha,
                Activo          = true,
                Usuariocreacion = request.Usuario,
                Fechacreacion   = fecha,
                Ipcreacion      = clientIp 
            };
            db.TblAutenticacionPasswordHistorials.Add(historialEntry);

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return new RegisterResponse(usuario.Id, usuario.Email, usuario.Fechacreacion);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // LOGIN — valida credenciales, crea sesión, emite AT + RT + FamilyId
    // ─────────────────────────────────────────────────────────────────────────
    public async Task<AuthResponse> LoginAsync(
        LoginRequest request,
        string clientIp,
        string? userAgent,
        string? deviceInfo,
        CancellationToken ct)
    {
        var validation = await loginValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            throw new ValidationException(validation.Errors);

        var normalizedUser = request.User.ToLowerInvariant();

        var usuario = await db.TblAutenticacionUsuarios
            .Include(u => u.IdpersonaNavigation)
            .FirstOrDefaultAsync(u => u.Email == normalizedUser && u.Activo, ct)
            ?? throw new UnauthorizedException("Credenciales inválidas.", "INVALID_CREDENTIALS");
        
        var fecha = DateTime.UtcNow;

        // Cuenta bloqueada
        if (usuario.Bloqueadohasta.HasValue && usuario.Bloqueadohasta > fecha)
            throw new UnauthorizedException(
                $"Cuenta bloqueada temporalmente. Intente después de las {usuario.Bloqueadohasta:HH:mm} UTC.",
                "ACCOUNT_LOCKED");

        // Verificar password
        if (!passwordHasher.Verify(request.Password, usuario.Hashpassword))
        {
            usuario.Intentosfallidos++;
            if (usuario.Intentosfallidos >= MaxFailedAttempts)
            {
                usuario.Bloqueadohasta   = DateTime.UtcNow.AddMinutes(LockoutMinutes);
                usuario.Intentosfallidos = 0;
            }
            await db.SaveChangesAsync(ct);
            throw new UnauthorizedException("Credenciales inválidas.", "INVALID_CREDENTIALS");
        }

        // Credenciales OK — resetear contadores
        usuario.Intentosfallidos = 0;
        usuario.Bloqueadohasta   = null;
        usuario.Ultimologin      = fecha;

        var roles = await LoadUserRolesAsync(usuario.Id, ct);

        // Límite de sesiones activas — política FIFO: revoca la más antigua si se supera el límite
        var sesionesActivas = await db.TblAutenticacionSesions
            .Where(s => s.Idusuario == usuario.Id && s.Estaactiva && s.Activo)
            .OrderBy(s => s.Fechacreacion)
            .ToListAsync(ct);

        var usuarioModificacion = usuario.Email.Contains('@') ? usuario.Email.Split('@')[0] : usuario.Email;

        if (sesionesActivas.Count >= _jwt.MaxActiveSessions)
        {
            var masAntigua = sesionesActivas.First();
            masAntigua.Estaactiva          = false;
            masAntigua.Revocadofecha       = fecha;
            masAntigua.Revocadorazon       = RevocacionRazonEnum.SESSION_LIMIT;
            masAntigua.Usuariomodificacion = usuarioModificacion;
            masAntigua.Fechamodificacion   = fecha;

            await db.TblAutenticacionRefreshTokens
                .Where(r => r.Idsesion == masAntigua.Id && !r.Estarevocado)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.Estarevocado, true), ct);

            await db.SaveChangesAsync(ct);
        }

        // Crear sesión
        var sesion = new TblAutenticacionSesion
        {
            Idusuario        = usuario.Id,
            Dispositivoinfo  = deviceInfo,
            Direccionip      = clientIp,
            Agenteusuario    = userAgent,
            Estaactiva       = true,
            Ultimaactividad  = fecha,
            Fechaexpiracion  = DateTime.UtcNow.AddDays(_jwt.AbsoluteTimeoutDays),
            Activo           = true,
            Usuariocreacion  = usuarioModificacion,
            Fechacreacion    = fecha,
            Ipcreacion       = clientIp
        };
        db.TblAutenticacionSesions.Add(sesion);
        await db.SaveChangesAsync(ct);

        // Generar AT + RT
        var (at, _, atExpiry)          = tokenService.GenerateAccessToken(usuario, roles);
        var familyId                   = Guid.NewGuid();
        var (rawRt, rtHash, rtExpiry)  = tokenService.GenerateRefreshTokenRaw();

        var refreshToken = new TblAutenticacionRefreshToken
        {
            Idusuario       = usuario.Id,
            Idsesion        = sesion.Id,
            Tokenhash       = rtHash,
            Familiatoken    = familyId,
            Estausado       = false,
            Estarevocado    = false,
            Fechaexpiracion = rtExpiry,
            Activo          = true,
            Usuariocreacion = usuarioModificacion,
            Fechacreacion   = fecha
        };
        db.TblAutenticacionRefreshTokens.Add(refreshToken);
        await db.SaveChangesAsync(ct);

        var idEmpleado = await db.TblAdministracionEmpleados
            .Where(e => e.Idpersona == usuario.Idpersona && e.Activo)
            .Select(e => (int?)e.Id)
            .FirstOrDefaultAsync(ct);

        return new AuthResponse(at, rawRt, atExpiry, familyId, usuario.ToUserResponse(idEmpleado));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // REFRESH TOKEN — rotation con detección de reúso (reuse attack)
    // ─────────────────────────────────────────────────────────────────────────
    public async Task<AuthResponse> RefreshTokenAsync(
        RefreshTokenRequest request,
        string clientIp,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            throw new UnauthorizedException("Refresh token inválido.");

        var hash = tokenService.HashToken(request.RefreshToken);

        var rt = await db.TblAutenticacionRefreshTokens
            .FirstOrDefaultAsync(r => r.Tokenhash == hash, ct)
            ?? throw new UnauthorizedException("Refresh token inválido.", "TOKEN_INVALID");

        // REUSE ATTACK — si ya fue usado, revocar toda la familia
        if (rt.Estausado)
        {
            await RevokeTokenFamilyInternalAsync(rt.Familiatoken, rt.Idusuario, RevocacionRazonEnum.TOKEN_REUSED, ct);
            throw new UnauthorizedException("Refresh token reutilizado. Sesión revocada por seguridad.", "TOKEN_REUSED");
        }

        if (rt.Estarevocado)
            throw new UnauthorizedException("Refresh token revocado.", "TOKEN_REVOKED");

        if (rt.Fechaexpiracion < DateTime.UtcNow)
            throw new UnauthorizedException("Refresh token expirado.", "TOKEN_EXPIRED");

        // Idle + Absolute timeout — verificar antes de marcar RT como usado para evitar falso reuse attack
        var sesion = await db.TblAutenticacionSesions.FirstOrDefaultAsync(s => s.Id == rt.Idsesion, ct);
        if (sesion is not null)
        {
            var currentUserIp = httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
            if (!string.IsNullOrEmpty(sesion.Direccionip) && sesion.Direccionip != currentUserIp)
            {
                await RevokeTokenFamilyInternalAsync(rt.Familiatoken, rt.Idusuario, RevocacionRazonEnum.IP_MISMATCH, ct);
                throw new UnauthorizedException("Acceso desde dirección IP diferente. Inicia sesión nuevamente.", "IP_MISMATCH");
            }

            var currentUserAgent = httpContextAccessor.HttpContext?.Request.Headers["User-Agent"].ToString();
            if (!string.IsNullOrEmpty(sesion.Agenteusuario) && sesion.Agenteusuario != currentUserAgent)
            {
                await RevokeTokenFamilyInternalAsync(rt.Familiatoken, rt.Idusuario, RevocacionRazonEnum.USER_AGENT_MISMATCH, ct);
                throw new UnauthorizedException("Acceso desde dispositivo diferente. Inicia sesión nuevamente.", "USER_AGENT_MISMATCH");
            }

            var recentRefreshCount = await db.TblAutenticacionSesions
                .Where(s => s.Idusuario == sesion.Idusuario && 
                       s.Fechamodificacion > DateTime.UtcNow.AddMinutes(-5) &&
                       s.Estaactiva == true)
                .CountAsync(ct);

            if (recentRefreshCount > 10)
            {
                await RevokeTokenFamilyInternalAsync(rt.Familiatoken, rt.Idusuario, RevocacionRazonEnum.ADMIN_REVOKE_ALL, ct);
                throw new UnauthorizedException("Demasiados intentos de refresh. Inicia sesión nuevamente.", "ADMIN_REVOKE_ALL");
            }

            var idleLimit = DateTime.UtcNow.AddDays(-_jwt.IdleTimeoutDays);
            if (sesion.Ultimaactividad < idleLimit)
            {
                await RevokeTokenFamilyInternalAsync(rt.Familiatoken, rt.Idusuario, RevocacionRazonEnum.SESSION_IDLE_TIMEOUT, ct);
                throw new UnauthorizedException("Sesión expirada por inactividad.", "SESSION_IDLE_TIMEOUT");
            }

            if (sesion.Fechaexpiracion.HasValue && sesion.Fechaexpiracion.Value < DateTime.UtcNow)
            {
                await RevokeTokenFamilyInternalAsync(rt.Familiatoken, rt.Idusuario, RevocacionRazonEnum.SESSION_EXPIRED, ct);
                throw new UnauthorizedException("Sesión expirada. Inicia sesión nuevamente.", "SESSION_EXPIRED");
            }
        }

        var fecha = DateTime.UtcNow;

        // Marcar como usado ANTES de emitir el nuevo (atomicidad)
        rt.Estausado   = true;
        rt.Activo      = false;
        rt.Fechausado  = fecha;
        await db.SaveChangesAsync(ct);

        var usuario = await db.TblAutenticacionUsuarios
            .Include(u => u.IdpersonaNavigation)
            .FirstOrDefaultAsync(u => u.Id == rt.Idusuario && u.Activo, ct)
            ?? throw new UnauthorizedException("Usuario no encontrado o inactivo.");

        var roles = await LoadUserRolesAsync(usuario.Id, ct);

        var usuarioEmail = usuario.Email.Contains('@') ? usuario.Email.Split('@')[0] : usuario.Email;

        // Actualizar actividad de sesión
        if (sesion is not null)
        {
            sesion.Ultimaactividad     = fecha;
            sesion.Usuariomodificacion = usuarioEmail;
            sesion.Fechamodificacion   = fecha;
            sesion.Ipmodificacion      = clientIp;
        }

        // Generar nuevo par AT + RT con el MISMO familyId
        var (at, _, atExpiry)         = tokenService.GenerateAccessToken(usuario, roles);
        var (rawRt, rtHash, rtExpiry) = tokenService.GenerateRefreshTokenRaw();

        var newRt = new TblAutenticacionRefreshToken
        {
            Idusuario       = usuario.Id,
            Idsesion        = rt.Idsesion,
            Tokenhash       = rtHash,
            Familiatoken    = rt.Familiatoken,   // misma familia
            Estausado       = false,
            Estarevocado    = false,
            Fechaexpiracion = rtExpiry,
            Activo          = true,
            Usuariocreacion = usuarioEmail,
            Fechacreacion   = fecha
        };
        db.TblAutenticacionRefreshTokens.Add(newRt);
        await db.SaveChangesAsync(ct);

        var idEmpleado = await db.TblAdministracionEmpleados
            .Where(e => e.Idpersona == usuario.Idpersona && e.Activo)
            .Select(e => (int?)e.Id)
            .FirstOrDefaultAsync(ct);

        return new AuthResponse(at, rawRt, atExpiry, rt.Familiatoken, usuario.ToUserResponse(idEmpleado));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // LOGOUT — blacklist JTI del AT, revoca RT actual, cierra sesión
    // ─────────────────────────────────────────────────────────────────────────
    public async Task LogoutAsync(
        string jti,
        int idUser,
        DateTime atExpiry,
        string? rawRefreshToken,
        CancellationToken ct)
    {
        var usuario = await db.TblAutenticacionUsuarios
            .FirstOrDefaultAsync(u => u.Id == idUser && u.Activo, ct)
            ?? throw new UnauthorizedException("Usuario no encontrado o inactivo.");
        
        var usuarioEmail = usuario.Email.Contains('@') ? usuario.Email.Split('@')[0] : usuario.Email;
        var fecha = DateTime.UtcNow;

        // 1. Blacklist del JTI (siempre se ejecuta para revocar el AT usado en logout)
        var jtiExiste = await db.TblAutenticacionTokenBlacklists
            .AnyAsync(b => b.Jti == jti, ct);

        if (!jtiExiste)
        {
            db.TblAutenticacionTokenBlacklists.Add(new TblAutenticacionTokenBlacklist
            {
                Jti             = jti,
                Idusuario       = idUser,
                Fecharevocado   = fecha,
                Fechaexpiracion = atExpiry,
                Activo          = true,
                Usuariocreacion = usuarioEmail,
                Fechacreacion   = fecha,
            });
        }

        // 2. Cerrar la sesión — buscar por RT o por la sesión más reciente activa
        TblAutenticacionSesion? sesion = null;

        if (!string.IsNullOrWhiteSpace(rawRefreshToken))
        {
            var hash = tokenService.HashToken(rawRefreshToken);
            var rt = await db.TblAutenticacionRefreshTokens
                .FirstOrDefaultAsync(r => r.Tokenhash == hash, ct);

            if (rt is not null)
            {
                rt.Estarevocado = true;
                rt.Activo       = false;
                sesion = await db.TblAutenticacionSesions.FirstOrDefaultAsync(s => s.Id == rt.Idsesion, ct);
            }
        }
        else
        {
            // Si no se proporciona RT, buscar la sesión más reciente activa del usuario
            sesion = await db.TblAutenticacionSesions
                .Where(s => s.Idusuario == idUser && s.Estaactiva && s.Activo)
                .OrderByDescending(s => s.Ultimaactividad)
                .FirstOrDefaultAsync(ct);
        }

        // Marcar la sesión como inactiva
        if (sesion is not null)
        {
            sesion.Estaactiva          = false;
            sesion.Activo              = false;
            sesion.Revocadofecha       = fecha;
            sesion.Revocadorazon       = RevocacionRazonEnum.LOGOUT;
            sesion.Usuariomodificacion = usuarioEmail;
            sesion.Fechamodificacion   = fecha;
            sesion.Ipmodificacion      = httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }

        await db.SaveChangesAsync(ct);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // REVOKE TOKEN — revoca toda la familia (todos los dispositivos / sesiones)
    // ─────────────────────────────────────────────────────────────────────────
    public async Task RevokeTokenAsync(RevokeTokenRequest request, int idUsuario, CancellationToken ct)
    {
        var belongs = await db.TblAutenticacionRefreshTokens
            .AnyAsync(r => r.Familiatoken == request.TokenFamilyId && r.Idusuario == idUsuario, ct);

        if (!belongs)
            throw new UnauthorizedException("No tienes permisos para revocar esta familia de tokens.");

        await RevokeTokenFamilyInternalAsync(request.TokenFamilyId, idUsuario, RevocacionRazonEnum.REVOKE, ct);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // CHANGE PASSWORD — cambia contraseña validando historial de últimas 5
    // ─────────────────────────────────────────────────────────────────────────
    public async Task ChangePasswordAsync(ChangePasswordRequest request, HttpContext context, CancellationToken ct)
    {
        var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var claim    = httpContextAccessor.HttpContext?.User.FindFirst(JwtRegisteredClaimNames.Sub);

        var userId = claim != null && int.TryParse(claim.Value, out var idUser) 
                ? idUser 
                : 0;
        
        var user = await db.TblAutenticacionUsuarios
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken: ct);

        if (user is null)
            throw new UnauthorizedException("Usuario no encontrado.");

        if (!passwordHasher.Verify(request.OldPassword, user.Hashpassword))
            throw new ArgumentException("La contraseña actual es incorrecta.");

        var passwordHistory = await db.TblAutenticacionPasswordHistorials
            .Where(ph => ph.Idusuario == userId)
            .OrderByDescending(ph => ph.Fechacambio)
            .Take(PASSWORD_HISTORY_LIMIT)
            .ToListAsync(cancellationToken: ct);

        foreach (var historicalHash in passwordHistory)
        {
            if (passwordHasher.Verify(request.NewPassword, historicalHash.Hashpassword))
                throw new InvalidOperationException(
                    $"No puede reutilizar una de las últimas {PASSWORD_HISTORY_LIMIT} contraseñas.");
        }

        string newHash = passwordHasher.Hash(request.NewPassword);
        var fecha = DateTime.UtcNow;
        var email = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.Email)?.Value ?? user.Email;
        var usuario = email.Contains('@') ? email.Split('@')[0] : email;

        user.Hashpassword        = newHash;
        user.Fechamodificacion   = fecha;
        user.Usuariomodificacion = usuario;
        user.Debecambiarpassword = false; // Contraseña cambiada con éxito

        var historialEntry = new TblAutenticacionPasswordHistorial
        {
            Idusuario       = userId,
            Hashpassword    = newHash,
            Fechacambio     = fecha,
            Activo          = true,
            Usuariocreacion = usuario,
            Fechacreacion   = fecha,
            Ipcreacion      = clientIp
        };

        db.TblAutenticacionPasswordHistorials.Add(historialEntry);
        await db.SaveChangesAsync(cancellationToken: ct);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers privados
    // ─────────────────────────────────────────────────────────────────────────
    private async Task RevokeTokenFamilyInternalAsync(Guid familyId, int idUser, RevocacionRazonEnum razon, CancellationToken ct)
    {
        var usuario = await db.TblAutenticacionUsuarios
            .FirstOrDefaultAsync(u => u.Id == idUser && u.Activo, ct)
            ?? throw new UnauthorizedException("Usuario no encontrado o inactivo.");
        
        var usuarioEmail = usuario.Email.Contains('@') ? usuario.Email.Split('@')[0] : usuario.Email;

        var tokensToRevoke = await db.TblAutenticacionRefreshTokens
            .Where(r => r.Familiatoken == familyId && !r.Estarevocado)
            .ToListAsync(ct);

        if (tokensToRevoke.Count == 0)
            return;

        var sessionIds = tokensToRevoke.Select(t => t.Idsesion).Distinct().ToList();
        var sessionsToRevoke = await db.TblAutenticacionSesions
            .Where(s => sessionIds.Contains(s.Id) && s.Estaactiva)
            .ToListAsync(ct);

        foreach (var token in tokensToRevoke)
        {
            token.Estarevocado = true;
            token.Activo       = false;
        }

        var fecha = DateTime.UtcNow;
        foreach (var sesion in sessionsToRevoke)
        {
            sesion.Estaactiva          = false;
            sesion.Activo              = false;
            sesion.Revocadofecha       = fecha;
            sesion.Revocadorazon       = razon;
            sesion.Usuariomodificacion = usuarioEmail;
            sesion.Fechamodificacion   = fecha;
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task<List<string>> LoadUserRolesAsync(int idUsuario, CancellationToken ct) =>
        await db.TblAutenticacionUsuarioRols
            .Where(ur => ur.Idusuario == idUsuario && ur.Activo)
            .Include(ur => ur.IdrolNavigation)
            .Select(ur => ur.IdrolNavigation.Nombre)
            .ToListAsync(ct);

    public async Task<string[]> GetUserModulesAsync(int idUsuario, CancellationToken ct)
    {
        var roleIds = await db.TblAutenticacionUsuarioRols
            .Where(ur => ur.Idusuario == idUsuario && ur.Activo)
            .Select(ur => ur.Idrol)
            .ToListAsync(ct);

        var query = from rp in db.TblAutenticacionRolPermisos
                    join p in db.TblAutenticacionPermisos on rp.Idpermiso equals p.Id
                    join m in db.TblAutenticacionModulos on p.Idmodulo equals m.Id
                    where roleIds.Contains(rp.Idrol)
                       && rp.Activo
                       && p.Activo
                       && m.Activo
                    select m.Nombremodulo;

        return await query.Distinct().ToArrayAsync(ct);
    }
}
