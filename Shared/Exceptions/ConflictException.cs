namespace tmr_backend.Shared.Exceptions;

public class ConflictException(string message, string? code = null) : Exception(message)
{
    public string? Code { get; } = code;
}