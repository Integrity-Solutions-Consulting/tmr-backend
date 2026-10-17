namespace tmr_backend.Shared.Exceptions;

public sealed class UnauthorizedException(string message, string? code = null) : Exception(message)
{
    public string? Code { get; } = code;
}
