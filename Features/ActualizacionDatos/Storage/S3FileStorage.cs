using System.Security.Cryptography;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace tmr_backend.Features.ActualizacionDatos.Storage;

public sealed class FileStorageOptions
{
    public string Provider { get; set; } = "Local";
}

public sealed class S3StorageOptions
{
    public string Bucket { get; set; } = "amazon-tmr-inv-integrity";
    public string Region { get; set; } = "us-east-2";
    public string Endpoint { get; set; } = "https://s3.us-east-2.amazonaws.com";
    public string Prefix { get; set; } = "tmr/desarrollo/colaboradores/";
}

public sealed class S3FileStorage(
    IAmazonS3 client,
    IOptions<S3StorageOptions> options,
    IOptions<LocalStorageOptions> limits) : IFileStorage
{
    private const string ProviderName = "s3";
    private readonly S3StorageOptions _options = options.Value;
    private readonly long _maxBytes = limits.Value.MaxBytes;

    public async Task<StoredFile> SaveAsync(Stream content, string extension, string contentType, CancellationToken ct)
    {
        ValidateConfiguration();
        var key = BuildKey(extension);
        await using var buffer = new MemoryStream();
        using var sha = SHA256.Create();
        var bytes = 0L;
        var chunk = new byte[81920];
        int read;
        while ((read = await content.ReadAsync(chunk, ct)) > 0)
        {
            bytes += read;
            if (bytes > _maxBytes)
                throw new InvalidOperationException($"El archivo supera el límite permitido de {_maxBytes} bytes.");
            await buffer.WriteAsync(chunk.AsMemory(0, read), ct);
            sha.TransformBlock(chunk, 0, read, null, 0);
        }
        sha.TransformFinalBlock([], 0, 0);
        buffer.Position = 0;

        try
        {
            await client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = _options.Bucket,
                Key = key,
                InputStream = buffer,
                ContentType = contentType,
                AutoCloseStream = false,
                CannedACL = S3CannedACL.Private,
                ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256
            }, ct);
            return new StoredFile(key, ProviderName, bytes, Convert.ToHexString(sha.Hash!).ToLowerInvariant(), contentType);
        }
        catch
        {
            // No se registra metadata si PutObject falla. También se intenta
            // limpiar una escritura ambigua para no dejar objetos huérfanos.
            try { await client.DeleteObjectAsync(new DeleteObjectRequest { BucketName = _options.Bucket, Key = key }, CancellationToken.None); }
            catch { /* conservar el error original; el objeto queda para limpieza operativa */ }
            throw;
        }
    }

    public async Task<Stream?> OpenReadAsync(string key, CancellationToken ct, string? provider = null)
    {
        ValidateConfiguration();
        ValidateKey(key);
        try
        {
            var response = await client.GetObjectAsync(new GetObjectRequest { BucketName = _options.Bucket, Key = key }, ct);
            return response.ResponseStream;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound || ex.ErrorCode == "NoSuchKey")
        {
            return null;
        }
    }

    public async Task DeleteAsync(string key, CancellationToken ct, string? provider = null)
    {
        ValidateConfiguration();
        ValidateKey(key);
        await client.DeleteObjectAsync(new DeleteObjectRequest { BucketName = _options.Bucket, Key = key }, ct);
    }

    private string BuildKey(string extension)
    {
        var prefix = (_options.Prefix ?? string.Empty).Trim('/');
        return $"{prefix}/{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
    }

    private void ValidateConfiguration()
    {
        if (string.IsNullOrWhiteSpace(_options.Bucket) || string.IsNullOrWhiteSpace(_options.Region) || string.IsNullOrWhiteSpace(_options.Endpoint))
            throw new InvalidOperationException("La configuración de almacenamiento S3 está incompleta.");
    }

    private void ValidateKey(string key)
    {
        var prefix = (_options.Prefix ?? string.Empty).Trim('/');
        if (string.IsNullOrWhiteSpace(key) || key.Contains("..", StringComparison.Ordinal) || !key.StartsWith(prefix + "/", StringComparison.Ordinal))
            throw new InvalidOperationException("La clave S3 no pertenece al prefijo configurado.");
    }
}

public sealed class ConfigurableFileStorage(
    IOptions<FileStorageOptions> providerOptions,
    LocalFileStorage local,
    S3FileStorage s3) : IFileStorage
{
    private readonly string _provider = providerOptions.Value.Provider.Trim().ToLowerInvariant();

    public Task<StoredFile> SaveAsync(Stream content, string extension, string contentType, CancellationToken ct) =>
        _provider switch
        {
            "local" => local.SaveAsync(content, extension, contentType, ct),
            "s3" => s3.SaveAsync(content, extension, contentType, ct),
            _ => throw new InvalidOperationException("FileStorage:Provider debe ser Local o S3.")
        };

    public Task<Stream?> OpenReadAsync(string key, CancellationToken ct, string? provider = null) =>
        (provider ?? _provider).Trim().ToLowerInvariant() switch
        {
            "local" => local.OpenReadAsync(key, ct, provider),
            "s3" => s3.OpenReadAsync(key, ct, provider),
            _ => throw new InvalidOperationException("Proveedor de archivo desconocido.")
        };

    public Task DeleteAsync(string key, CancellationToken ct, string? provider = null) =>
        (provider ?? _provider).Trim().ToLowerInvariant() switch
        {
            "local" => local.DeleteAsync(key, ct, provider),
            "s3" => s3.DeleteAsync(key, ct, provider),
            _ => throw new InvalidOperationException("Proveedor de archivo desconocido.")
        };
}
