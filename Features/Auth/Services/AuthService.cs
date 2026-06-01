using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
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
    IPasswordHasher passwordHasher,
    IValidator<RegisterRequest> registerValidator,
    IValidator<LoginRequest> loginValidator,
    IOptions<JwtSettings> jwtOptions) : IAuthService
{
    private const int MaxFailedAttempts = 5;
    private const int LockoutMinutes    = 15;
    private readonly JwtSettings _jwt   = jwtOptions.Value;

    // ─────────────────────────────────────────────────────────────────────────
    // REGISTER — crea persona + usuario + rol base. Sin tokens.
    // ─────────────────────────────────────────────────────────────────────────
    public async Task<RegisterResponse> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var validation = await registerValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            throw new ValidationException(validation.Errors);

        var normalizedEmail = request.Email.ToLowerInvariant();
        var contrseñaDefecto = "Int3gr1ty123!"; // Contraseña por defecto (debe cambiar en el primer login)

        var existe = await db.TblAutenticacionUsuarios
            .AnyAsync(u => u.Email == normalizedEmail, ct);
        if (existe)
            throw new ConflictException("El email ya está registrado.");

        var hash = passwordHasher.Hash(contrseñaDefecto);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
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
                //Campos de autoria
                Usuariocreacion      = request.Usuario,
                Fechacreacion        = DateTime.UtcNow,
                Ipcreacion           = request.IP
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
                Fechacreacion        = DateTime.UtcNow,
                Ipcreacion           = request.IP
            };
            db.TblAutenticacionUsuarios.Add(usuario);
            await db.SaveChangesAsync(ct);

            var usuarioRol = new TblAutenticacionUsuarioRol
            {
                Idusuario       = usuario.Id,
                Idrol           = 4,
                Asignadoen      = DateTime.UtcNow,
                Activo          = true,
                Usuariocreacion = request.Usuario,
                Fechacreacion   = DateTime.UtcNow,
                Ipcreacion      = request.IP
            };
            db.TblAutenticacionUsuarioRols.Add(usuarioRol);
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
            .FirstOrDefaultAsync(u => u.Email == normalizedUser && u.Activo, ct)
            ?? throw new UnauthorizedException("Credenciales inválidas.");

        // Cuenta bloqueada
        if (usuario.Bloqueadohasta.HasValue && usuario.Bloqueadohasta > DateTime.UtcNow)
            throw new UnauthorizedException(
                $"Cuenta bloqueada temporalmente. Intente después de las {usuario.Bloqueadohasta:HH:mm} UTC.");

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
            throw new UnauthorizedException("Credenciales inválidas.");
        }

        // Credenciales OK — resetear contadores
        usuario.Intentosfallidos = 0;
        usuario.Bloqueadohasta   = null;
        usuario.Ultimologin      = DateTime.UtcNow;

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
            masAntigua.Revocadofecha       = DateTime.UtcNow;
            masAntigua.Usuariomodificacion = usuarioModificacion;
            masAntigua.Fechamodificacion   = DateTime.UtcNow;

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
            Ultimaactividad  = DateTime.UtcNow,
            Fechaexpiracion  = DateTime.UtcNow.AddDays(_jwt.AbsoluteTimeoutDays),
            Activo           = true,
            Usuariocreacion  = usuarioModificacion,
            Fechacreacion    = DateTime.UtcNow,
            Ipcreacion       = clientIp
        };
        db.TblAutenticacionSesions.Add(sesion);
        await db.SaveChangesAsync(ct);

        // Generar AT + RT
        var (at, _)                    = tokenService.GenerateAccessToken(usuario, roles);
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
            Fechacreacion   = DateTime.UtcNow
        };
        db.TblAutenticacionRefreshTokens.Add(refreshToken);
        await db.SaveChangesAsync(ct);

        return new AuthResponse(at, rawRt, rtExpiry, familyId, usuario.ToUserResponse());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // REFRESH TOKEN — rotation con detección de reúso (reuse attack)
    // ─────────────────────────────────────────────────────────────────────────
    public async Task<AuthResponse> RefreshTokenAsync(
        RefreshTokenRequest request,
        string clientIp,
        CancellationToken ct)
    {
        var hash = tokenService.HashToken(request.RefreshToken);

        var rt = await db.TblAutenticacionRefreshTokens
            .FirstOrDefaultAsync(r => r.Tokenhash == hash, ct)
            ?? throw new UnauthorizedException("Refresh token inválido.");

        // REUSE ATTACK — si ya fue usado, revocar toda la familia
        if (rt.Estausado)
        {
            await RevokeTokenFamilyInternalAsync(rt.Familiatoken, ct);
            throw new UnauthorizedException("Refresh token reutilizado. Sesión revocada por seguridad.");
        }

        if (rt.Estarevocado)
            throw new UnauthorizedException("Refresh token revocado.");

        if (rt.Fechaexpiracion < DateTime.UtcNow)
            throw new UnauthorizedException("Refresh token expirado.");

        // Idle + Absolute timeout — verificar antes de marcar RT como usado para evitar falso reuse attack
        var sesion = await db.TblAutenticacionSesions.FindAsync([rt.Idsesion], ct);
        if (sesion is not null)
        {
            var idleLimit = DateTime.UtcNow.AddDays(-_jwt.IdleTimeoutDays);
            if (sesion.Ultimaactividad < idleLimit)
            {
                await RevokeTokenFamilyInternalAsync(rt.Familiatoken, ct);
                throw new UnauthorizedException("Sesión expirada por inactividad.");
            }

            if (sesion.Fechaexpiracion.HasValue && sesion.Fechaexpiracion.Value < DateTime.UtcNow)
            {
                await RevokeTokenFamilyInternalAsync(rt.Familiatoken, ct);
                throw new UnauthorizedException("Sesión expirada. Inicia sesión nuevamente.");
            }
        }

        // Marcar como usado ANTES de emitir el nuevo (atomicidad)
        rt.Estausado   = true;
        rt.Fechausado  = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var usuario = await db.TblAutenticacionUsuarios
            .FirstOrDefaultAsync(u => u.Id == rt.Idusuario && u.Activo, ct)
            ?? throw new UnauthorizedException("Usuario no encontrado o inactivo.");

        var roles = await LoadUserRolesAsync(usuario.Id, ct);

        // Actualizar actividad de sesión
        if (sesion is not null)
        {
            sesion.Ultimaactividad     = DateTime.UtcNow;
            sesion.Usuariomodificacion = usuario.Email;
            sesion.Fechamodificacion   = DateTime.UtcNow;
        }

        // Generar nuevo par AT + RT con el MISMO familyId
        var (at, _)                   = tokenService.GenerateAccessToken(usuario, roles);
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
            Usuariocreacion = usuario.Email,
            Fechacreacion   = DateTime.UtcNow
        };
        db.TblAutenticacionRefreshTokens.Add(newRt);
        await db.SaveChangesAsync(ct);

        return new AuthResponse(at, rawRt, rtExpiry, rt.Familiatoken, usuario.ToUserResponse());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // LOGOUT — blacklist JTI del AT, revoca RT actual, cierra sesión
    // ─────────────────────────────────────────────────────────────────────────
    public async Task LogoutAsync(
        string jti,
        int idUsuario,
        DateTime atExpiry,
        string rawRefreshToken,
        CancellationToken ct)
    {
        // 1. Blacklist del JTI (solo si no existe ya)
        var jtiExiste = await db.TblAutenticacionTokenBlacklists
            .AnyAsync(b => b.Jti == jti, ct);

        if (!jtiExiste)
        {
            db.TblAutenticacionTokenBlacklists.Add(new TblAutenticacionTokenBlacklist
            {
                Jti             = jti,
                Idusuario       = idUsuario,
                Fecharevocado   = DateTime.UtcNow,
                Fechaexpiracion = atExpiry,
                Activo          = true,
                Usuariocreacion = idUsuario.ToString(),
                Fechacreacion   = DateTime.UtcNow
            });
        }

        // 2. Revocar RT actual y cerrar sesión
        var hash = tokenService.HashToken(rawRefreshToken);
        var rt = await db.TblAutenticacionRefreshTokens
            .FirstOrDefaultAsync(r => r.Tokenhash == hash, ct);

        if (rt is not null)
        {
            rt.Estarevocado = true;

            var sesion = await db.TblAutenticacionSesions.FindAsync([rt.Idsesion], ct);
            if (sesion is not null)
            {
                sesion.Estaactiva          = false;
                sesion.Revocadofecha       = DateTime.UtcNow;
                sesion.Usuariomodificacion = idUsuario.ToString();
                sesion.Fechamodificacion   = DateTime.UtcNow;
            }
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

        await RevokeTokenFamilyInternalAsync(request.TokenFamilyId, ct);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers privados
    // ─────────────────────────────────────────────────────────────────────────
    private async Task RevokeTokenFamilyInternalAsync(Guid familyId, CancellationToken ct) =>
        await db.TblAutenticacionRefreshTokens
            .Where(r => r.Familiatoken == familyId && !r.Estarevocado)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Estarevocado, true), ct);

    private async Task<List<string>> LoadUserRolesAsync(int idUsuario, CancellationToken ct) =>
        await db.TblAutenticacionUsuarioRols
            .Where(ur => ur.Idusuario == idUsuario && ur.Activo)
            .Include(ur => ur.IdrolNavigation)
            .Select(ur => ur.IdrolNavigation.Nombre)
            .ToListAsync(ct);
}
