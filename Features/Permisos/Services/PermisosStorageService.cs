using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;

namespace tmr_backend.Features.Permisos.Services;

public sealed record ArchivoValidadoPreSubida(
    string NombreOriginal,
    string ExtensionNormalizada,
    string ContentType,
    byte[] Bytes,
    string Sha256);

public sealed record ArchivoAdjuntoSubido(
    string NombreOriginal,
    string BucketName,
    string ObjectKey,
    string ContentType,
    long TamanoBytes,
    string Sha256);

public sealed record ArchivoEvidenciaDescarga(
    byte[] Contenido,
    string NombreArchivo,
    string MimeType);

public sealed record EstadoConexionMinio(
    bool Configurado,
    bool Conectado,
    string Proveedor,
    string? Endpoint,
    string BucketName,
    string Mensaje);

public interface IPermisosStorageService
{
    Task<ArchivoValidadoPreSubida> ValidarYLeerArchivoAsync(
        IFormFile archivo,
        CancellationToken ct = default);

    Task<ArchivoAdjuntoSubido> SubirAdjuntoValidadoAsync(
        ArchivoValidadoPreSubida archivo,
        int idSolicitud,
        int anio,
        CancellationToken ct = default);

    Task<ArchivoEvidenciaDescarga?> ObtenerAdjuntoAsync(
        string bucketName,
        string objectKey,
        string nombreOriginal,
        string contentType,
        CancellationToken ct = default);

    Task EliminarAdjuntoAsync(
        string bucketName,
        string objectKey,
        CancellationToken ct = default);

    Task<EstadoConexionMinio> VerificarConexionAsync(CancellationToken ct = default);
}

/// <summary>
/// Almacenamiento de adjuntos (PDF, PNG, JPG &lt;= 5 MB) en MinIO / S3 vía API S3 nativa (AWS Signature V4) (RNF-003, RNF-004).
/// </summary>
public sealed class PermisosStorageService : IPermisosStorageService
{
    public const long TamanoMaximoBytes = 15 * 1024 * 1024; // 15 MB por archivo (certificados médicos, radiografías de alta resolución, etc.)
    private const string LocalBucketMarker = "__local__";

    private static readonly ConcurrentDictionary<string, bool> BucketsVerificados = new(StringComparer.Ordinal);

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

    public async Task<ArchivoValidadoPreSubida> ValidarYLeerArchivoAsync(
        IFormFile archivo,
        CancellationToken ct = default)
    {
        if (archivo is null || archivo.Length == 0)
            throw new InvalidOperationException("El archivo seleccionado está vacío. Por favor adjunta un documento válido.");

        if (archivo.Length > TamanoMaximoBytes)
            throw new InvalidOperationException("El archivo supera el tamaño máximo permitido de 15 MB por documento.");

        var ext = Path.GetExtension(archivo.FileName)?.Trim().ToLowerInvariant();
        if (ext is not (".pdf" or ".png" or ".jpg" or ".jpeg"))
            throw new InvalidOperationException("Formato no compatible. Puedes adjuntar archivos en PDF, PNG o JPG.");

        await using var ms = new MemoryStream((int)archivo.Length);
        await archivo.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();

        // RNF-003: Validación estricta de cabecera binaria real (magic bytes)
        var (esValido, mimeDetectado, extNormalizada) = ValidarCabeceraArchivo(bytes, ext);
        if (!esValido)
            throw new InvalidOperationException("El archivo parece estar dañado o no corresponde a un documento PDF, PNG o JPG válido.");

        var sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var nombreSeguro = string.Concat(Path.GetFileName(archivo.FileName).Where(c => !Path.GetInvalidFileNameChars().Contains(c) && c is not ('<' or '>' or '"' or '\'' or ';' or '\r' or '\n' or '\0'))).Trim();
        if (string.IsNullOrWhiteSpace(nombreSeguro))
            nombreSeguro = $"respaldo{extNormalizada}";

        return new ArchivoValidadoPreSubida(
            NombreOriginal: nombreSeguro.Length > 255 ? nombreSeguro[..255] : nombreSeguro,
            ExtensionNormalizada: extNormalizada,
            ContentType: mimeDetectado,
            Bytes: bytes,
            Sha256: sha256);
    }

    public async Task<ArchivoAdjuntoSubido> SubirAdjuntoValidadoAsync(
        ArchivoValidadoPreSubida archivo,
        int idSolicitud,
        int anio,
        CancellationToken ct = default)
    {
        var cfg = LeerConfiguracionMinio();
        var objectKey = $"solicitudes/{anio}/{idSolicitud}/{Guid.NewGuid():N}{archivo.ExtensionNormalizada}";

        if (cfg.Configurado)
        {
            await SubirAMinioS3Async(
                cfg.Endpoint!,
                cfg.UseSsl,
                cfg.BucketName,
                objectKey,
                archivo.Bytes,
                archivo.ContentType,
                cfg.AccessKey!,
                cfg.SecretKey!,
                ct);

            _logger.LogInformation(
                "Adjunto subido a MinIO S3 exitosamente: bucket={Bucket}, key={ObjectKey}, bytes={Bytes}",
                cfg.BucketName, objectKey, archivo.Bytes.Length);

            return new ArchivoAdjuntoSubido(
                archivo.NombreOriginal,
                cfg.BucketName,
                objectKey,
                archivo.ContentType,
                archivo.Bytes.Length,
                archivo.Sha256);
        }

        if (!cfg.AllowLocalFallback)
        {
            throw new InvalidOperationException(
                "MinIO no está configurado. Defina Minio:Endpoint, Minio:AccessKey y Minio:SecretKey en appsettings o variables de entorno.");
        }

        // Respaldo explícito en disco únicamente si AllowLocalFallback = true y MinIO no está configurado
        var rootPath = string.IsNullOrWhiteSpace(_env.WebRootPath)
            ? Path.Combine(AppContext.BaseDirectory, "wwwroot")
            : _env.WebRootPath;

        var fullPath = Path.Combine(rootPath, "uploads", objectKey.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllBytesAsync(fullPath, archivo.Bytes, ct);

        _logger.LogWarning("MinIO no configurado; adjunto guardado localmente en {FullPath}", fullPath);

        return new ArchivoAdjuntoSubido(
            archivo.NombreOriginal,
            LocalBucketMarker,
            objectKey,
            archivo.ContentType,
            archivo.Bytes.Length,
            archivo.Sha256);
    }

    public async Task<ArchivoEvidenciaDescarga?> ObtenerAdjuntoAsync(
        string bucketName,
        string objectKey,
        string nombreOriginal,
        string contentType,
        CancellationToken ct = default)
    {
        var nombreDescarga = string.IsNullOrWhiteSpace(nombreOriginal) ? "adjunto.pdf" : nombreOriginal;
        var mime = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType;

        if (string.Equals(bucketName, LocalBucketMarker, StringComparison.OrdinalIgnoreCase))
        {
            var rootPath = string.IsNullOrWhiteSpace(_env.WebRootPath)
                ? Path.Combine(AppContext.BaseDirectory, "wwwroot")
                : _env.WebRootPath;

            var uploadsDir = Path.GetFullPath(Path.Combine(rootPath, "uploads")); var fullPath = Path.GetFullPath(Path.Combine(uploadsDir, objectKey.Replace('/', Path.DirectorySeparatorChar))); if (!fullPath.StartsWith(uploadsDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return null;
            if (!File.Exists(fullPath)) return null;

            var localBytes = await File.ReadAllBytesAsync(fullPath, ct);
            return new ArchivoEvidenciaDescarga(localBytes, nombreDescarga, mime);
        }

        var cfg = LeerConfiguracionMinio();
        if (!cfg.Configurado)
            throw new InvalidOperationException("MinIO no está configurado para descargar este archivo.");

        var bytes = await DescargarDeMinioS3Async(
            cfg.Endpoint!,
            cfg.UseSsl,
            bucketName,
            objectKey,
            cfg.AccessKey!,
            cfg.SecretKey!,
            ct);

        return bytes is null ? null : new ArchivoEvidenciaDescarga(bytes, nombreDescarga, mime);
    }

    public async Task EliminarAdjuntoAsync(
        string bucketName,
        string objectKey,
        CancellationToken ct = default)
    {
        try
        {
            if (string.Equals(bucketName, LocalBucketMarker, StringComparison.OrdinalIgnoreCase))
            {
                var rootPath = string.IsNullOrWhiteSpace(_env.WebRootPath)
                    ? Path.Combine(AppContext.BaseDirectory, "wwwroot")
                    : _env.WebRootPath;

                var uploadsDir = Path.GetFullPath(Path.Combine(rootPath, "uploads")); var fullPath = Path.GetFullPath(Path.Combine(uploadsDir, objectKey.Replace('/', Path.DirectorySeparatorChar))); if (!fullPath.StartsWith(uploadsDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return;
                if (File.Exists(fullPath))
                    File.Delete(fullPath);
                return;
            }

            var cfg = LeerConfiguracionMinio();
            if (!cfg.Configurado) return;

            var baseUri = ConstruirBaseUri(cfg.Endpoint!, cfg.UseSsl);
            var client = _httpClientFactory.CreateClient();
            using var delReq = CrearRequestFirmadaS3(
                HttpMethod.Delete,
                baseUri,
                bucketName,
                objectKey,
                [],
                "application/octet-stream",
                cfg.AccessKey!,
                cfg.SecretKey!);
            using var resp = await client.SendAsync(delReq, ct);

            _logger.LogInformation(
                "Limpieza de objeto huérfano en MinIO S3: bucket={Bucket}, key={ObjectKey}, status={StatusCode}",
                bucketName, objectKey, (int)resp.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo eliminar el objeto huérfano {ObjectKey} del bucket {Bucket}", objectKey, bucketName);
        }
    }

    public async Task<EstadoConexionMinio> VerificarConexionAsync(CancellationToken ct = default)
    {
        var cfg = LeerConfiguracionMinio();
        if (!cfg.Configurado)
        {
            return new EstadoConexionMinio(
                Configurado: false,
                Conectado: false,
                Proveedor: cfg.AllowLocalFallback ? "LOCAL_FALLBACK" : "SIN_CONFIGURAR",
                Endpoint: cfg.Endpoint,
                BucketName: cfg.BucketName,
                Mensaje: "Faltan credenciales en la sección Minio (Endpoint, AccessKey, SecretKey).");
        }

        try
        {
            var baseUri = ConstruirBaseUri(cfg.Endpoint!, cfg.UseSsl);
            await AsegurarBucketS3Async(baseUri, cfg.BucketName, cfg.AccessKey!, cfg.SecretKey!, ct);

            return new EstadoConexionMinio(
                Configurado: true,
                Conectado: true,
                Proveedor: "MINIO_S3",
                Endpoint: baseUri.ToString(),
                BucketName: cfg.BucketName,
                Mensaje: $"Conectado a MinIO S3 correctamente y bucket '{cfg.BucketName}' disponible.");
        }
        catch (Exception ex)
        {
            return new EstadoConexionMinio(
                Configurado: true,
                Conectado: false,
                Proveedor: "MINIO_S3",
                Endpoint: cfg.Endpoint,
                BucketName: cfg.BucketName,
                Mensaje: ex.Message);
        }
    }

    private (bool Configurado, string? Endpoint, string? AccessKey, string? SecretKey, string BucketName, bool UseSsl, bool AllowLocalFallback) LeerConfiguracionMinio()
    {
        var endpoint = _configuration["Minio:Endpoint"]?.Trim();
        var accessKey = _configuration["Minio:AccessKey"]?.Trim();
        var secretKey = _configuration["Minio:SecretKey"]?.Trim();
        var bucket = _configuration["Minio:BucketName"]?.Trim();
        if (string.IsNullOrWhiteSpace(bucket)) bucket = "permisos";

        var useSsl = bool.TryParse(_configuration["Minio:UseSSL"], out var ssl) && ssl;
        var allowLocal = bool.TryParse(_configuration["Minio:AllowLocalFallback"], out var fallback) && fallback;

        var configurado = !string.IsNullOrWhiteSpace(endpoint)
                          && !string.IsNullOrWhiteSpace(accessKey)
                          && !string.IsNullOrWhiteSpace(secretKey);

        return (configurado, endpoint, accessKey, secretKey, bucket, useSsl, allowLocal);
    }

    private static (bool Valido, string Mime, string Ext) ValidarCabeceraArchivo(byte[] bytes, string ext)
    {
        // PDF: %PDF- (25 50 44 46 2D)
        if (ext == ".pdf"
            && bytes.Length >= 5
            && bytes[0] == 0x25 && bytes[1] == 0x50 && bytes[2] == 0x44 && bytes[3] == 0x46 && bytes[4] == 0x2D)
        {
            return (true, "application/pdf", ".pdf");
        }

        // PNG: 89 50 4E 47 0D 0A 1A 0A
        if (ext == ".png"
            && bytes.Length >= 8
            && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47
            && bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A)
        {
            return (true, "image/png", ".png");
        }

        // JPEG: FF D8 FF
        if (ext is ".jpg" or ".jpeg"
            && bytes.Length >= 3
            && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
        {
            return (true, "image/jpeg", ".jpg");
        }

        return (false, "", "");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // IMPLEMENTACIÓN MINIO S3 NATIVA (AWS SIGNATURE V4)
    // ═══════════════════════════════════════════════════════════════════════════

    private async Task AsegurarBucketS3Async(
        Uri baseUri,
        string bucket,
        string accessKey,
        string secretKey,
        CancellationToken ct)
    {
        var bucketCacheKey = $"{baseUri}|{bucket}";
        if (BucketsVerificados.ContainsKey(bucketCacheKey))
            return;

        var client = _httpClientFactory.CreateClient();
        using var headReq = CrearRequestFirmadaS3(HttpMethod.Head, baseUri, bucket, "", [], "application/octet-stream", accessKey, secretKey);
        using var headResp = await client.SendAsync(headReq, ct);

        VerificarNoSeaConsolaWeb(headResp, baseUri);

        if (headResp.StatusCode == HttpStatusCode.OK)
        {
            BucketsVerificados[bucketCacheKey] = true;
            return;
        }

        if (headResp.StatusCode == HttpStatusCode.NotFound)
        {
            using var putBucketReq = CrearRequestFirmadaS3(HttpMethod.Put, baseUri, bucket, "", [], "application/octet-stream", accessKey, secretKey);
            using var putBucketResp = await client.SendAsync(putBucketReq, ct);

            VerificarNoSeaConsolaWeb(putBucketResp, baseUri);

            if (!putBucketResp.IsSuccessStatusCode && putBucketResp.StatusCode != HttpStatusCode.Conflict)
            {
                var err = await putBucketResp.Content.ReadAsStringAsync(ct);
                throw new InvalidOperationException(
                    $"No se pudo crear ni acceder al bucket '{bucket}' en MinIO S3 ({(int)putBucketResp.StatusCode}): {err}");
            }

            BucketsVerificados[bucketCacheKey] = true;
            return;
        }

        if (headResp.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
        {
            throw new InvalidOperationException(
                $"MinIO rechazó las credenciales ({(int)headResp.StatusCode}) para el bucket '{bucket}'. Verifique Minio:AccessKey y Minio:SecretKey.");
        }
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
        await AsegurarBucketS3Async(baseUri, bucket, accessKey, secretKey, ct);

        var client = _httpClientFactory.CreateClient();
        using var putReq = CrearRequestFirmadaS3(HttpMethod.Put, baseUri, bucket, key, content, contentType, accessKey, secretKey);
        using var response = await client.SendAsync(putReq, ct);

        VerificarNoSeaConsolaWeb(response, baseUri);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(
                $"Error al subir archivo a MinIO S3 ({(int)response.StatusCode}): {body}");
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
        await AsegurarBucketS3Async(baseUri, bucket, accessKey, secretKey, ct);

        var client = _httpClientFactory.CreateClient();
        using var getReq = CrearRequestFirmadaS3(HttpMethod.Get, baseUri, bucket, key, [], "application/octet-stream", accessKey, secretKey);
        using var response = await client.SendAsync(getReq, ct);

        VerificarNoSeaConsolaWeb(response, baseUri);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"MinIO devolvió {(int)response.StatusCode} al descargar: {body}");
        }

        return await response.Content.ReadAsByteArrayAsync(ct);
    }

    private static void VerificarNoSeaConsolaWeb(HttpResponseMessage response, Uri baseUri)
    {
        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (string.Equals(mediaType, "text/html", StringComparison.OrdinalIgnoreCase)
            || response.StatusCode == HttpStatusCode.MethodNotAllowed)
        {
            throw new InvalidOperationException(
                $"El endpoint de MinIO '{baseUri}' respondió como Consola Web (HTML/405). Configure Minio:Endpoint apuntando al dominio o puerto de la API S3 (9000/9002).");
        }
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

        var canonicalRequest = string.Join("\n",
            method.Method,
            canonicalUri,
            "",
            canonicalHeaders,
            signedHeaders,
            payloadHash);

        var credentialScope = $"{dateStamp}/{region}/{service}/aws4_request";
        var stringToSign = string.Join("\n",
            "AWS4-HMAC-SHA256",
            amzDate,
            credentialScope,
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalRequest))).ToLowerInvariant());

        var signingKey = ObtenerClaveFirmaAwsV4(secretKey, dateStamp, region, service);
        var signature = Convert.ToHexString(HMACSHA256.HashData(signingKey, Encoding.UTF8.GetBytes(stringToSign))).ToLowerInvariant();

        var authorization = $"AWS4-HMAC-SHA256 Credential={accessKey}/{credentialScope}, SignedHeaders={signedHeaders}, Signature={signature}";

        var req = new HttpRequestMessage(method, requestUri);
        req.Headers.Host = hostHeader;
        req.Headers.TryAddWithoutValidation("x-amz-date", amzDate);
        req.Headers.TryAddWithoutValidation("x-amz-content-sha256", payloadHash);
        req.Headers.TryAddWithoutValidation("Authorization", authorization);

        if (method == HttpMethod.Put)
        {
            var byteContent = new ByteArrayContent(payload);
            byteContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            byteContent.Headers.ContentLength = payload.Length;
            req.Content = byteContent;
        }

        return req;
    }

    private static byte[] ObtenerClaveFirmaAwsV4(string secretKey, string dateStamp, string region, string service)
    {
        var kSecret = Encoding.UTF8.GetBytes("AWS4" + secretKey);
        var kDate = HMACSHA256.HashData(kSecret, Encoding.UTF8.GetBytes(dateStamp));
        var kRegion = HMACSHA256.HashData(kDate, Encoding.UTF8.GetBytes(region));
        var kService = HMACSHA256.HashData(kRegion, Encoding.UTF8.GetBytes(service));
        return HMACSHA256.HashData(kService, Encoding.UTF8.GetBytes("aws4_request"));
    }
}
