namespace tmr_backend.Shared.Wrappers;

public sealed record ApiError(string Field, string Message, string? Code = null);