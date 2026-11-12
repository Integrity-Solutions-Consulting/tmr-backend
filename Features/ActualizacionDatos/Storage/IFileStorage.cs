namespace tmr_backend.Features.ActualizacionDatos.Storage;

public sealed record StoredFile(string Key, string Provider, long Size, string Sha256, string ContentType);
public interface IFileStorage
{
    Task<StoredFile> SaveAsync(Stream content, string extension, string contentType, CancellationToken ct);
    Task<Stream?> OpenReadAsync(string key, CancellationToken ct, string? provider = null);
    Task DeleteAsync(string key, CancellationToken ct, string? provider = null);
}
