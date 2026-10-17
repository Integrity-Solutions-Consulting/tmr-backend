namespace tmr_backend.Infrastructure.Security;

public sealed class JwtSettings
{
    public string SecretKey          { get; init; } = string.Empty;
    public string Issuer             { get; init; } = string.Empty;
    public string Audience           { get; init; } = string.Empty;
    public int    AccessTokenMinutes  { get; init; } = 15;
    public int    RefreshTokenDays    { get; init; } = 7;
    public int    IdleTimeoutDays     { get; init; } = 2;
    public int    AbsoluteTimeoutDays { get; init; } = 7;
    public int    MaxActiveSessions   { get; init; } = 3;
}