using FluentValidation;
using Microsoft.EntityFrameworkCore;
using tmr_backend.Features.Configuracion.Register_Temp.DTOs.Request;
using tmr_backend.Features.Configuracion.Register_Temp.DTOs.Response;
using tmr_backend.Features.Configuracion.Register_Temp.Mappings;
using tmr_backend.Infrastructure.Database;
using tmr_backend.Infrastructure.Database.Entities;
using tmr_backend.Infrastructure.Security; // ITokenService, IPasswordHasher

namespace tmr_backend.Features.Configuracion.Register_Temp.Services;

public sealed class AuthService(
    ApplicationDbContext db,
    ITokenService tokenService,
    IPasswordHasher passwordHasher,
    IValidator<RegisterRequest> validator) : IAuthService
{
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var normalizedEmail = request.Email.ToLowerInvariant();

        // 1. Validar
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            throw new ValidationException(validation.Errors);

        // 2. Verificar duplicado
        var existe = await db.TblAutenticacionUsuarios
            .AnyAsync(u => u.Email == request.Email.ToLower(), ct);
        if (existe)
            throw new InvalidOperationException("El email ya existe.");

        // 3. Hashear password
        var hash = passwordHasher.Hash(request.Password);

        // 4. Todo en una sola transacción atómica
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            //4.1 Crear persona
            var persona = new TblAdministracionPersona
            {
                Idgenero = 1, // Asignar según la lógica de negocio
                Idnacionalidad = 5,
                Idtipoidentificacion = 25,
                Numeroidentificacion = "0912345678",
                Tipopersona = "NATURAL",
                Nombres = "Juan",
                Apellidos = "Pérez",
                Fechanacimiento = new DateOnly(1990, 1, 1),
                Email = request.Email,
                Telefono = "1234567890",
                Direccion = "Calle 123",
                Activo = true,
                Usuariocreacion = "admin",
                Fechacreacion = DateTime.UtcNow,
                Usuariomodificacion = "admin",
                Ipcreacion = "127.0.0.1"
            };
            await db.TblAdministracionPersonas.AddAsync(persona, ct);
            await db.SaveChangesAsync(ct);

            //4.2 Crear usuario autenticación
            var usuario = new TblAutenticacionUsuario
            {
                Idpersona = persona.Id, // Asignar según la lógica de negocio
                Email = normalizedEmail,
                Hashpassword = hash,
                Fechacreacion = DateTime.UtcNow,
                Estaactivo = true,
                Debecambiarpassword = true,
                Activo = true,
                Usuariocreacion = "system",
                Ipcreacion = "127.0.0.1"
            };
            
            // 5. Persistir
            await db.TblAutenticacionUsuarios.AddAsync(usuario, ct);
            await db.SaveChangesAsync(ct);

            await transaction.CommitAsync(ct);

            // 6. Generar tokens
            var accessToken  = tokenService.GenerateAccessToken(usuario);
            var (refreshToken, expiresAt) = tokenService.GenerateRefreshToken();
            //await tokenService.SaveRefreshTokenAsync(usuario.Id, refreshToken, ct);

            return new AuthResponse(accessToken, refreshToken, expiresAt, usuario.ToUserResponse());   
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw; // relanzar para que el endpoint lo maneje
        }
    }
}
