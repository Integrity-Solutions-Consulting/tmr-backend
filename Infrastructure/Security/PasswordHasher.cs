namespace tmr_backend.Infrastructure.Security;

public sealed class PasswordHasher : IPasswordHasher
{
    // BCrypt — costo 12 es el estándar recomendado
    public string Hash(string password)
        => BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);

    public bool Verify(string password, string hash)
        => BCrypt.Net.BCrypt.Verify(password, hash);
}