namespace tmr_backend.Infrastructure.Security;

public sealed class JwtSettings
{
    public string SecretKey          { get; init; } = string.Empty;
    public string Issuer             { get; init; } = string.Empty;
    public string Audience           { get; init; } = string.Empty;
    public int    AccessTokenMinutes { get; init; } = 15;
    public int    RefreshTokenDays   { get; init; } = 7;
}