namespace tmr_backend.Shared.Wrappers;

public sealed class ApiRequest<T>
{
    public T Data { get; init; } = default!;
}