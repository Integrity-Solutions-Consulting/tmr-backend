using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace tmr_backend.Features.ActualizacionDatos.Storage;

public sealed class LocalStorageOptions { public string RootPath { get; set; } = "App_Data/colaboradores"; public long MaxBytes { get; set; } = 10 * 1024 * 1024; }

public sealed class LocalFileStorage(IOptions<LocalStorageOptions> options, IWebHostEnvironment env) : IFileStorage
{
    private readonly LocalStorageOptions _options = options.Value;
    private string Root => Path.GetFullPath(Path.IsPathRooted(_options.RootPath) ? _options.RootPath : Path.Combine(env.ContentRootPath, _options.RootPath));
    public async Task<StoredFile> SaveAsync(Stream content, string extension, string contentType, CancellationToken ct)
    {
        if (content.Length > _options.MaxBytes) throw new InvalidOperationException("El archivo supera el límite permitido de 10 MB.");
        Directory.CreateDirectory(Root);
        var key = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var path = Path.Combine(Root, key);
        try { await using var output = File.Create(path); using var sha = SHA256.Create(); var buffer = new byte[81920]; int read; long total = 0;
            while ((read = await content.ReadAsync(buffer, ct)) > 0) { total += read; if (total > _options.MaxBytes) throw new InvalidOperationException("El archivo supera el límite permitido de 10 MB."); await output.WriteAsync(buffer.AsMemory(0, read), ct); sha.TransformBlock(buffer, 0, read, null, 0); }
            sha.TransformFinalBlock([], 0, 0); return new StoredFile(key, "local", total, Convert.ToHexString(sha.Hash!).ToLowerInvariant(), contentType); }
        catch { if (File.Exists(path)) File.Delete(path); throw; }
    }
    public Task<Stream?> OpenReadAsync(string key, CancellationToken ct, string? provider = null)
    { var safe = Path.GetFileName(key); var path = Path.Combine(Root, safe); return Task.FromResult<Stream?>(File.Exists(path) ? File.OpenRead(path) : null); }
    public Task DeleteAsync(string key, CancellationToken ct, string? provider = null) { var path = Path.Combine(Root, Path.GetFileName(key)); if (File.Exists(path)) File.Delete(path); return Task.CompletedTask; }
}
