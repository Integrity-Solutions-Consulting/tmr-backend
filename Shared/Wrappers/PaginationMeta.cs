namespace tmr_backend.Shared.Wrappers;

public sealed record PaginationMeta(int Page, int PageSize, int TotalCount)
{
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}