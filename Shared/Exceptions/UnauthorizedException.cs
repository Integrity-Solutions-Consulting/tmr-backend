namespace tmr_backend.Shared.Exceptions;

public sealed class UnauthorizedException(string message) : Exception(message);
