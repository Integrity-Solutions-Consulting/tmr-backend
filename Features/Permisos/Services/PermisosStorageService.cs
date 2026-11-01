using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;

namespace tmr_backend.Features.Permisos.Services;

public sealed record ArchivoEvidenciaResultado(
    string NombreOriginal,
    string UrlAlmacenamiento,
    string MimeType,
    long TamanoBytes);

public sealed record ArchivoEvidenciaDescarga(
    byte[] Contenido,
    string NombreArchivo,
    string MimeType);

public interface IPermisosStorageService
{
    Task<ArchivoEvidenciaResultado> GuardarEvidenciaAsync(
        IFormFile archivo,
        string codigoPermiso,
        CancellationToken ct = default);

    Task<ArchivoEvidenciaDescarga?> ObtenerEvidenciaAsync(
        string? evidenciaUrl,
        string? nombreOriginal,
        string? mimeType,
        CancellationToken ct = default);
}

/// <summary>
/// Servicio de almacenamiento de evidencias de permisos.
/// Soporta almacenamiento compatible con S3 / MinIO (usado en Dokploy) mediante firma AWS Signature V4
/// sin requerir dependencias externas pesadas, y cuenta con fallback automático a almacenamiento local
/// en disco (wwwroot/uploads/permisos) para entornos de desarrollo.
/// </summary>
public sealed class PermisosStorageService : IPermisosStorageService
{
    private static readonly HashSet<string> ExtensionesPermitidas = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".jpg", ".jpeg", ".png", ".webp"
    };

    private const long TamanoMaximoBytes = 10 * 1024 * 1024; // 10 MB

    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _env;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PermisosStorageService> _logger;

    public PermisosStorageService(
        IConfiguration configuration,
        IWebHostEnvironment env,
        IHttpClientFactory httpClientFactory,
        ILogger<PermisosStorageService> logger)
    {
        _configuration = configuration;
        _env = env;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<ArchivoEvidenciaResultado> GuardarEvidenciaAsync(
        IFormFile archivo,
        string codigoPermiso,
        CancellationToken ct = default)
    {
        if (archivo is null || archivo.Length == 0)
            throw new InvalidOperationException("El archivo de evidencia está vacío.");

        if (archivo.Length > TamanoMaximoBytes)
            throw new InvalidOperationException("El archivo de evidencia supera el límite máximo permitido de 10 MB.");

        var ext = Path.GetExtension(archivo.FileName);
        if (string.IsNullOrWhiteSpace(ext) || !ExtensionesPermitidas.Contains(ext))
            throw new InvalidOperationException("Formato de archivo no permitido. Adjunte un archivo PDF, JPG o PNG.");

        var nombreLimpio = SanitizarNombre(Path.GetFileNameWithoutExtension(archivo.FileName));
        var codigoLimpio = SanitizarNombre(codigoPermiso);
        var key = $"permisos/{DateTime.UtcNow:yyyy/MM}/{codigoLimpio}_{Guid.NewGuid():N}_{nombreLimpio}{ext.ToLowerInvariant()}";
        var mimeType = ResolverMimeType(ext, archivo.ContentType);

        await using var ms = new MemoryStream();
        await archivo.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();

        // 1. Intentar subir a MinIO / S3 si está configurado
        var endpoint = _configuration["Minio:Endpoint"] ?? _configuration["Storage:Minio:Endpoint"];
        var accessKey = _configuration["Minio:AccessKey"] ?? _configuration["Storage:Minio:AccessKey"];
        var secretKey = _configuration["Minio:SecretKey"] ?? _configuration["Storage:Minio:SecretKey"];
        var bucket = _configuration["Minio:BucketName"] ?? _configuration["Storage:Minio:BucketName"] ?? "tmr-permisos";
        var useSsl = bool.TryParse(_configuration["Minio:UseSSL"] ?? _configuration["Storage:Minio:UseSSL"], out var ssl) && ssl;

        if (!string.IsNullOrWhiteSpace(endpoint) &&
            !string.IsNullOrWhiteSpace(accessKey) &&
            !string.IsNullOrWhiteSpace(secretKey))
        {
            try
            {
                await SubirAMinioS3Async(endpoint, useSsl, bucket, key, bytes, mimeType, accessKey, secretKey, ct);
                var storageRef = $"minio://{bucket}/{key}";
                _logger.LogInformation("Evidencia subida a MinIO exitosamente: {StorageRef}", storageRef);
                return new ArchivoEvidenciaResultado(archivo.FileName, storageRef, mimeType, bytes.Length);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo subir evidencia a MinIO ({Endpoint}); usando almacenamiento local de respaldo.", endpoint);
            }
        }

        // 2. Fallback: Almacenamiento local en wwwroot/uploads/permisos
        var rootPath = string.IsNullOrWhiteSpace(_env.WebRootPath)
            ? Path.Combine(AppContext.BaseDirectory, "wwwroot")
            : _env.WebRootPath;

        var relativePath = key.Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.Combine(rootPath, "uploads", relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await File.WriteAllBytesAsync(fullPath, bytes, ct);
        var localRef = $"local://{key}";
        _logger.LogInformation("Evidencia almacenada localmente en: {FullPath}", fullPath);

        return new ArchivoEvidenciaResultado(archivo.FileName, localRef, mimeType, bytes.Length);
    }

    public async Task<ArchivoEvidenciaDescarga?> ObtenerEvidenciaAsync(
        string? evidenciaUrl,
        string? nombreOriginal,
        string? mimeType,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(evidenciaUrl))
            return null;

        var nombreDescarga = string.IsNullOrWhiteSpace(nombreOriginal) ? "evidencia_permiso.pdf" : nombreOriginal;
        var mime = string.IsNullOrWhiteSpace(mimeType)
            ? ResolverMimeType(Path.GetExtension(nombreDescarga), null)
            : mimeType;

        if (evidenciaUrl.StartsWith("minio://", StringComparison.OrdinalIgnoreCase))
        {
            var sinEsquema = evidenciaUrl["minio://".Length..];
            var slashIdx = sinEsquema.IndexOf('/');
            if (slashIdx > 0)
            {
                var bucket = sinEsquema[..slashIdx];
                var key = sinEsquema[(slashIdx + 1)..];
                var endpoint = _configuration["Minio:Endpoint"] ?? _configuration["Storage:Minio:Endpoint"];
                var accessKey = _configuration["Minio:AccessKey"] ?? _configuration["Storage:Minio:AccessKey"];
                var secretKey = _configuration["Minio:SecretKey"] ?? _configuration["Storage:Minio:SecretKey"];
                var useSsl = bool.TryParse(_configuration["Minio:UseSSL"] ?? _configuration["Storage:Minio:UseSSL"], out var ssl) && ssl;

                if (!string.IsNullOrWhiteSpace(endpoint) &&
                    !string.IsNullOrWhiteSpace(accessKey) &&
                    !string.IsNullOrWhiteSpace(secretKey))
                {
                    var bytes = await DescargarDeMinioS3Async(endpoint, useSsl, bucket, key, accessKey, secretKey, ct);
                    if (bytes is not null)
                        return new ArchivoEvidenciaDescarga(bytes, nombreDescarga, mime);
                }
            }
        }

        if (evidenciaUrl.StartsWith("local://", StringComparison.OrdinalIgnoreCase))
        {
            var key = evidenciaUrl["local://".Length..];
            var rootPath = string.IsNullOrWhiteSpace(_env.WebRootPath)
                ? Path.Combine(AppContext.BaseDirectory, "wwwroot")
                : _env.WebRootPath;

            var fullPath = Path.Combine(rootPath, "uploads", key.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(fullPath))
            {
                var bytes = await File.ReadAllBytesAsync(fullPath, ct);
                return new ArchivoEvidenciaDescarga(bytes, nombreDescarga, mime);
            }
        }

        return null;
    }

    private async Task SubirAMinioS3Async(
        string endpoint,
        bool useSsl,
        string bucket,
        string key,
        byte[] content,
        string contentType,
        string accessKey,
        string secretKey,
        CancellationToken ct)
    {
        var baseUri = ConstruirBaseUri(endpoint, useSsl);
        var client = _httpClientFactory.CreateClient();

        // Intentar asegurar que el bucket exista (ignorar si ya existe)
        try
        {
            using var headBucketReq = CrearRequestFirmadaS3(HttpMethod.Head, baseUri, bucket, "", [], "application/octet-stream", accessKey, secretKey);
            using var headResp = await client.SendAsync(headBucketReq, ct);
            if (headResp.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                using var putBucketReq = CrearRequestFirmadaS3(HttpMethod.Put, baseUri, bucket, "", [], "application/octet-stream", accessKey, secretKey);
                await client.SendAsync(putBucketReq, ct);
            }
        }
        catch
        {
            // Continuar con el PUT del objeto
        }

        using var putReq = CrearRequestFirmadaS3(HttpMethod.Put, baseUri, bucket, key, content, contentType, accessKey, secretKey);
        using var response = await client.SendAsync(putReq, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"MinIO devolvió {(int)response.StatusCode}: {body}");
        }
    }

    private async Task<byte[]?> DescargarDeMinioS3Async(
        string endpoint,
        bool useSsl,
        string bucket,
        string key,
        string accessKey,
        string secretKey,
        CancellationToken ct)
    {
        var baseUri = ConstruirBaseUri(endpoint, useSsl);
        var client = _httpClientFactory.CreateClient();
        using var getReq = CrearRequestFirmadaS3(HttpMethod.Get, baseUri, bucket, key, [], "application/octet-stream", accessKey, secretKey);
        using var response = await client.SendAsync(getReq, ct);
        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadAsByteArrayAsync(ct);
    }

    private static Uri ConstruirBaseUri(string endpoint, bool useSsl)
    {
        if (endpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            endpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return new Uri(endpoint.TrimEnd('/'));
        }

        var scheme = useSsl ? "https" : "http";
        return new Uri($"{scheme}://{endpoint.TrimEnd('/')}");
    }

    private static HttpRequestMessage CrearRequestFirmadaS3(
        HttpMethod method,
        Uri baseUri,
        string bucket,
        string objectKey,
        byte[] payload,
        string contentType,
        string accessKey,
        string secretKey)
    {
        const string region = "us-east-1";
        const string service = "s3";

        var now = DateTime.UtcNow;
        var amzDate = now.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        var dateStamp = now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

        var canonicalUri = string.IsNullOrEmpty(objectKey)
            ? $"/{bucket}"
            : $"/{bucket}/{string.Join("/", objectKey.Split('/').Select(Uri.EscapeDataString))}";

        var requestUri = new Uri(baseUri, canonicalUri);
        var payloadHash = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
        var hostHeader = requestUri.IsDefaultPort ? requestUri.Host : $"{requestUri.Host}:{requestUri.Port}";

        var canonicalHeaders = $"host:{hostHeader}\nx-amz-content-sha256:{payloadHash}\nx-amz-date:{amzDate}\n";
        const string signedHeaders = "host;x-amz-content-sha256;x-amz-date";

        var canonicalRequest = $"{method.Method}\n{canonicalUri}\n\n{canonicalHeaders}\n{signedHeaders}\n{payloadHash}";
        var canonicalRequestHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalRequest))).ToLowerInvariant();

        var credentialScope = $"{dateStamp}/{region}/{service}/aws4_request";
        var stringToSign = $"AWS4-HMAC-SHA256\n{amzDate}\n{credentialScope}\n{canonicalRequestHash}";

        var signingKey = ObtenerClaveFirmaAws4(secretKey, dateStamp, region, service);
        var signature = Convert.ToHexString(HMACSHA256.HashData(signingKey, Encoding.UTF8.GetBytes(stringToSign))).ToLowerInvariant();

        var authorization = $"AWS4-HMAC-SHA256 Credential={accessKey}/{credentialScope}, SignedHeaders={signedHeaders}, Signature={signature}";

        var request = new HttpRequestMessage(method, requestUri);
        request.Headers.TryAddWithoutValidation("x-amz-date", amzDate);
        request.Headers.TryAddWithoutValidation("x-amz-content-sha256", payloadHash);
        request.Headers.TryAddWithoutValidation("Authorization", authorization);

        if (method == HttpMethod.Put && payload.Length > 0)
        {
            var byteContent = new ByteArrayContent(payload);
            byteContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            request.Content = byteContent;
        }

        return request;
    }

    private static byte[] ObtenerClaveFirmaAws4(string secretKey, string dateStamp, string region, string service)
    {
        var kSecret = Encoding.UTF8.GetBytes("AWS4" + secretKey);
        var kDate = HMACSHA256.HashData(kSecret, Encoding.UTF8.GetBytes(dateStamp));
        var kRegion = HMACSHA256.HashData(kDate, Encoding.UTF8.GetBytes(region));
        var kService = HMACSHA256.HashData(kRegion, Encoding.UTF8.GetBytes(service));
        return HMACSHA256.HashData(kService, Encoding.UTF8.GetBytes("aws4_request"));
    }

    private static string SanitizarNombre(string valor)
    {
        var chars = valor
            .Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_')
            .Take(40)
            .ToArray();
        return chars.Length == 0 ? "archivo" : new string(chars);
    }

    private static string ResolverMimeType(string ext, string? contentType)
    {
        if (!string.IsNullOrWhiteSpace(contentType) && contentType != "application/octet-stream")
            return contentType;

        return ext.ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };
    }
}
