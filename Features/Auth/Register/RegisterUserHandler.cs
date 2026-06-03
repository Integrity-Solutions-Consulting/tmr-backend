using System.Globalization;
using Microsoft.EntityFrameworkCore;
using tmr_backend.Features.Auth.DTOs.Response;
using tmr_backend.Features.Auth.Register.DTOs;
using tmr_backend.Infrastructure.Database;
using tmr_backend.Infrastructure.Database.Entities;
using tmr_backend.Infrastructure.Security;
using tmr_backend.Shared.Exceptions;

namespace tmr_backend.Features.Auth.Register;

public sealed class RegisterUserHandler(
    ApplicationDbContext db,
    IPasswordHasher passwordHasher)
{
    private const string GenericPassword = "Int3gr1ty123!";

    public async Task<RegisterResponse> Handle(RegisterUserRequest request, HttpContext context, CancellationToken ct)
    {
        Validate(request);

        var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var now = DateTime.UtcNow;
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var passwordHash = passwordHasher.Hash(GenericPassword);

        var exists = await db.TblAutenticacionUsuarios
            .AnyAsync(u => u.Email == normalizedEmail, ct);

        if (exists)
            throw new ConflictException("El email ya esta registrado.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        try
        {
            var persona = new TblAdministracionPersona
            {
                Idgenero = request.IdGenero,
                Idnacionalidad = request.IdNacionalidad,
                Idtipoidentificacion = request.IdTipoIdentificacion,
                Numeroidentificacion = request.Numeroidentificacion.Trim(),
                Tipopersona = request.TipoPersona.Trim().ToUpperInvariant(),
                Nombres = request.Nombres.Trim(),
                Apellidos = request.Apellidos.Trim(),
                Fechanacimiento = DateOnly.ParseExact(request.FechaNacimiento, "dd-MM-yyyy", CultureInfo.InvariantCulture),
                Email = string.IsNullOrWhiteSpace(request.CorreoContacto)
                    ? normalizedEmail
                    : request.CorreoContacto.Trim().ToLowerInvariant(),
                Telefono = request.Telefono.Trim(),
                Direccion = request.Direccion.Trim(),
                Activo = true,
                Usuariocreacion = request.Usuario.Trim(),
                Fechacreacion = now,
                Ipcreacion = clientIp
            };

            db.TblAdministracionPersonas.Add(persona);
            await db.SaveChangesAsync(ct);

            var usuario = new TblAutenticacionUsuario
            {
                Idpersona = persona.Id,
                Email = normalizedEmail,
                Hashpassword = passwordHash,
                Emailverificado = false,
                Intentosfallidos = 0,
                Bloqueadohasta = null,
                Debecambiarpassword = true,
                Activo = true,
                Usuariocreacion = request.Usuario.Trim(),
                Fechacreacion = now,
                Ipcreacion = clientIp
            };

            db.TblAutenticacionUsuarios.Add(usuario);
            await db.SaveChangesAsync(ct);

            var usuarioRol = new TblAutenticacionUsuarioRol
            {
                Idusuario = usuario.Id,
                Idrol = 4, // TODO: hacer dinamica la asignacion de rol.
                Asignadoen = now,
                Activo = true,
                Usuariocreacion = request.Usuario.Trim(),
                Fechacreacion = now,
                Ipcreacion = clientIp
            };

            var historialPassword = new TblAutenticacionPasswordHistorial
            {
                Idusuario = usuario.Id,
                Hashpassword = passwordHash,
                Fechacambio = now,
                Activo = true,
                Usuariocreacion = request.Usuario.Trim(),
                Fechacreacion = now,
                Ipcreacion = clientIp
            };

            db.TblAutenticacionUsuarioRols.Add(usuarioRol);
            db.TblAutenticacionPasswordHistorials.Add(historialPassword);
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

    private static void Validate(RegisterUserRequest request)
    {
        if (request.TipoIdentificacion is not ("C" or "R" or "P" or "O"))
            throw new InvalidOperationException("TipoIdentificacion debe ser C, R, P u O.");

        var tipoPersona = request.TipoPersona.Trim().ToUpperInvariant();
        if (tipoPersona is not ("NATURAL" or "JURIDICA"))
            throw new InvalidOperationException("TipoPersona debe ser NATURAL o JURIDICA.");

        if (!DateOnly.TryParseExact(request.FechaNacimiento, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            throw new InvalidOperationException("FechaNacimiento debe tener formato dd-MM-yyyy.");

        if (string.IsNullOrWhiteSpace(request.Email))
            throw new InvalidOperationException("Email es requerido.");

        if (string.IsNullOrWhiteSpace(request.Numeroidentificacion))
            throw new InvalidOperationException("Numeroidentificacion es requerido.");

        if (string.IsNullOrWhiteSpace(request.Usuario))
            throw new InvalidOperationException("Usuario es requerido.");
    }
}
