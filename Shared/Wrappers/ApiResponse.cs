namespace tmr_backend.Shared.Wrappers;

public sealed class ApiResponse<T>
{
    public bool Success { get; init; }
    public int StatusCode { get; init; }
    public string Message { get; init; } = string.Empty;
    public T? Data { get; init; }
    public IEnumerable<ApiError>? Errors { get; init; }
    public PaginationMeta? Meta { get; init; }

    public static ApiResponse<T> Ok(T data, string message = "OK", PaginationMeta? meta = null) =>
        new() { Success = true, StatusCode = 200, Message = message, Data = data, Meta = meta };

    public static ApiResponse<T> Fail(int statusCode, string message, IEnumerable<ApiError> errors) =>
        new() { Success = false, StatusCode = statusCode, Message = message, Errors = errors };
}