using System.Security.Cryptography;
using System.Text;

namespace tmr_backend.Features.Permisos.Services;

/// <summary>
/// Token del enlace de revisión externa (magic link) del jefe inmediato externo.
/// El token en claro solo viaja en el correo; en base de datos se guarda su hash SHA-256,
/// así una lectura de la tabla, de la auditoría o de un respaldo no permite usar el enlace.
/// </summary>
public static class TokenRevisionExterna
{
    /// <summary>Genera un token aleatorio de 32 caracteres hexadecimales (128 bits).</summary>
    public static string Generar() => RandomNumberGenerator.GetHexString(32, lowercase: true);

    /// <summary>Hash que se persiste en token_externo (64 caracteres hexadecimales).</summary>
    public static string Hash(string tokenPlano) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(tokenPlano.Trim().ToLowerInvariant())));

    /// <summary>Formato del token en claro que llega por URL: 32 caracteres hexadecimales.</summary>
    public static bool FormatoValido(string? token) =>
        !string.IsNullOrWhiteSpace(token)
        && token.Length == 32
        && Guid.TryParseExact(token, "N", out _);

    /// <summary>Indica si el token en claro corresponde al hash guardado.</summary>
    public static bool Coincide(string? tokenPlano, string? hashGuardado)
    {
        if (!FormatoValido(tokenPlano) || string.IsNullOrWhiteSpace(hashGuardado))
            return false;
        var calculado = Encoding.ASCII.GetBytes(Hash(tokenPlano!));
        var guardado = Encoding.ASCII.GetBytes(hashGuardado);
        return CryptographicOperations.FixedTimeEquals(calculado, guardado);
    }
}
