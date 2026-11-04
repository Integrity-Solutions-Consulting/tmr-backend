using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

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

    Task<EstadoConexionMinio> VerificarConexionAsync(CancellationToken ct = default);
}

/// <summary>
/// Almacenamiento de adjuntos (PDF, PNG, JPG &lt;= 5 MB) en MinIO / S3 en Dokploy (RNF-003, RNF-004).
/// Soporta tanto la API S3 nativa (puerto 9000, AWS Signature V4) como puente automático hacia la API REST
/// de MinIO Console (/api/v1, puerto 9001) cuando el dominio público en Dokploy apunta a la consola web.
/// </summary>
public sealed class PermisosStorageService : IPermisosStorageService
{
    public const long TamanoMaximoBytes = 5 * 1024 * 1024; // 5 MB exactos (RF-005 / RNF-003)
    private const string LocalBucketMarker = "__local__";

    // HttpClient dedicado sin CookieContainer automático para enviar la cookie JWT de MinIO Console sin interferencias
    private static readonly HttpClient ConsoleHttpClient = new(new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        UseCookies = false,
        AutomaticDecompression = DecompressionMethods.All
    })
    {
        Timeout = TimeSpan.FromSeconds(60)
    };

    private static readonly ConcurrentDictionary<string, bool> ModoConsolaPorEndpoint = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, bool> UploadPrefixUsaBase64PorEndpoint = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, (string CookieHeader, DateTime ExpiraUtc)> SesionesConsola = new(StringComparer.Ordinal);
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
            throw new InvalidOperationException("El archivo adjunto está vacío.");

        if (archivo.Length > TamanoMaximoBytes)
            throw new InvalidOperationException("El archivo supera el tamaño máximo permitido de 5 MB (RF-005).");

        var ext = Path.GetExtension(archivo.FileName)?.Trim().ToLowerInvariant();
        if (ext is not (".pdf" or ".png" or ".jpg" or ".jpeg"))
            throw new InvalidOperationException("Formato no permitido. Solo se aceptan archivos PDF, PNG o JPG.");

        await using var ms = new MemoryStream((int)archivo.Length);
        await archivo.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();

        // RNF-003: Validación estricta de cabecera binaria real (magic bytes)
        var (esValido, mimeDetectado, extNormalizada) = ValidarCabeceraArchivo(bytes, ext);
        if (!esValido)
            throw new InvalidOperationException("El contenido binario del archivo no coincide con un PDF, PNG o JPG válido.");

        var sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var nombreSeguro = Path.GetFileName(archivo.FileName);
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
            await SubirAMinioAsync(
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
                "Adjunto subido a MinIO exitosamente: bucket={Bucket}, key={ObjectKey}, bytes={Bytes}",
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

            var fullPath = Path.Combine(rootPath, "uploads", objectKey.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(fullPath)) return null;

            var localBytes = await File.ReadAllBytesAsync(fullPath, ct);
            return new ArchivoEvidenciaDescarga(localBytes, nombreDescarga, mime);
        }

        var cfg = LeerConfiguracionMinio();
        if (!cfg.Configurado)
            throw new InvalidOperationException("MinIO no está configurado para descargar este archivo.");

        var bytes = await DescargarDeMinioAsync(
            cfg.Endpoint!,
            cfg.UseSsl,
            bucketName,
            objectKey,
            cfg.AccessKey!,
            cfg.SecretKey!,
            ct);

        return bytes is null ? null : new ArchivoEvidenciaDescarga(bytes, nombreDescarga, mime);
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
            var usaConsola = await ResolverModoYBucketAsync(baseUri, cfg.BucketName, cfg.AccessKey!, cfg.SecretKey!, ct);

            return new EstadoConexionMinio(
                Configurado: true,
                Conectado: true,
                Proveedor: usaConsola ? "MINIO_CONSOLE_API" : "MINIO_S3",
                Endpoint: baseUri.ToString(),
                BucketName: cfg.BucketName,
                Mensaje: usaConsola
                    ? $"Conectado a MinIO vía API de Consola (/api/v1) y bucket '{cfg.BucketName}' disponible."
                    : $"Conectado a MinIO S3 correctamente y bucket '{cfg.BucketName}' disponible.");
        }
        catch (Exception ex)
        {
            return new EstadoConexionMinio(
                Configurado: true,
                Conectado: false,
                Proveedor: "MINIO",
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
        if (string.IsNullOrWhiteSpace(bucket)) bucket = "tmr-solicitudes";

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
    // ORQUESTACIÓN DUAL: API S3 NATIVA (9000) + PUENTE MINIO CONSOLE API (9001)
    // ═══════════════════════════════════════════════════════════════════════════

    private async Task<bool> ResolverModoYBucketAsync(
        Uri baseUri,
        string bucket,
        string accessKey,
        string secretKey,
        CancellationToken ct)
    {
        var endpointKey = baseUri.ToString();
        var bucketCacheKey = $"{endpointKey}|{bucket}";

        if (ModoConsolaPorEndpoint.TryGetValue(endpointKey, out var modoConsolaConocido))
        {
            if (modoConsolaConocido)
            {
                if (!BucketsVerificados.ContainsKey(bucketCacheKey))
                {
                    await AsegurarBucketConsolaAsync(baseUri, bucket, accessKey, secretKey, ct);
                    BucketsVerificados[bucketCacheKey] = true;
                }
                return true;
            }

            if (!BucketsVerificados.ContainsKey(bucketCacheKey))
            {
                await AsegurarBucketS3Async(baseUri, bucket, accessKey, secretKey, ct);
                BucketsVerificados[bucketCacheKey] = true;
            }
            return false;
        }

        // Primera conexión al endpoint: probar S3 HEAD /{bucket}; si responde como Consola Web (HTML / 405), activar puente /api/v1
        var client = _httpClientFactory.CreateClient();
        using var headReq = CrearRequestFirmadaS3(HttpMethod.Head, baseUri, bucket, "", [], "application/octet-stream", accessKey, secretKey);
        using var headResp = await client.SendAsync(headReq, ct);

        if (EsRespuestaConsolaWeb(headResp))
        {
            _logger.LogInformation(
                "MinIO en '{Endpoint}' responde en puerto de Consola Web; activando puente automático REST /api/v1 para el bucket '{Bucket}'.",
                baseUri, bucket);

            ModoConsolaPorEndpoint[endpointKey] = true;
            await AsegurarBucketConsolaAsync(baseUri, bucket, accessKey, secretKey, ct);
            BucketsVerificados[bucketCacheKey] = true;
            return true;
        }

        // Verificar también el caso en que HEAD devuelva 200 sin HTML pero un PUT a S3 sea rechazado con 405 por un proxy de consola
        if (headResp.StatusCode == HttpStatusCode.OK)
        {
            ModoConsolaPorEndpoint[endpointKey] = false;
            BucketsVerificados[bucketCacheKey] = true;
            return false;
        }

        if (headResp.StatusCode == HttpStatusCode.NotFound)
        {
            using var putBucketReq = CrearRequestFirmadaS3(HttpMethod.Put, baseUri, bucket, "", [], "application/octet-stream", accessKey, secretKey);
            using var putBucketResp = await client.SendAsync(putBucketReq, ct);

            if (EsRespuestaConsolaWeb(putBucketResp))
            {
                ModoConsolaPorEndpoint[endpointKey] = true;
                await AsegurarBucketConsolaAsync(baseUri, bucket, accessKey, secretKey, ct);
                BucketsVerificados[bucketCacheKey] = true;
                return true;
            }

            if (!putBucketResp.IsSuccessStatusCode && putBucketResp.StatusCode != HttpStatusCode.Conflict)
            {
                var err = await putBucketResp.Content.ReadAsStringAsync(ct);
                throw new InvalidOperationException(
                    $"No se pudo crear ni acceder al bucket '{bucket}' en MinIO S3 ({(int)putBucketResp.StatusCode}): {err}");
            }

            ModoConsolaPorEndpoint[endpointKey] = false;
            BucketsVerificados[bucketCacheKey] = true;
            return false;
        }

        if (headResp.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
        {
            throw new InvalidOperationException(
                $"MinIO rechazó las credenciales ({(int)headResp.StatusCode}) para el bucket '{bucket}'. Verifique Minio:AccessKey y Minio:SecretKey.");
        }

        ModoConsolaPorEndpoint[endpointKey] = false;
        return false;
    }

    private async Task SubirAMinioAsync(
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
        var usaConsola = await ResolverModoYBucketAsync(baseUri, bucket, accessKey, secretKey, ct);

        if (usaConsola)
        {
            await SubirObjetoConsolaAsync(baseUri, bucket, key, content, contentType, accessKey, secretKey, ct);
            return;
        }

        var client = _httpClientFactory.CreateClient();
        using var putReq = CrearRequestFirmadaS3(HttpMethod.Put, baseUri, bucket, key, content, contentType, accessKey, secretKey);
        using var response = await client.SendAsync(putReq, ct);

        if (EsRespuestaConsolaWeb(response))
        {
            // El endpoint respondió 200 en HEAD pero 405/HTML en PUT: conmutar inmediatamente a MinIO Console API
            ModoConsolaPorEndpoint[baseUri.ToString()] = true;
            BucketsVerificados.TryRemove($"{baseUri}|{bucket}", out _);
            await AsegurarBucketConsolaAsync(baseUri, bucket, accessKey, secretKey, ct);
            BucketsVerificados[$"{baseUri}|{bucket}"] = true;
            await SubirObjetoConsolaAsync(baseUri, bucket, key, content, contentType, accessKey, secretKey, ct);
            return;
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(
                $"Error al subir archivo a MinIO S3 ({(int)response.StatusCode}): {body}");
        }
    }

    private async Task<byte[]?> DescargarDeMinioAsync(
        string endpoint,
        bool useSsl,
        string bucket,
        string key,
        string accessKey,
        string secretKey,
        CancellationToken ct)
    {
        var baseUri = ConstruirBaseUri(endpoint, useSsl);
        var usaConsola = await ResolverModoYBucketAsync(baseUri, bucket, accessKey, secretKey, ct);

        if (usaConsola)
        {
            return await DescargarObjetoConsolaAsync(baseUri, bucket, key, accessKey, secretKey, ct);
        }

        var client = _httpClientFactory.CreateClient();
        using var getReq = CrearRequestFirmadaS3(HttpMethod.Get, baseUri, bucket, key, [], "application/octet-stream", accessKey, secretKey);
        using var response = await client.SendAsync(getReq, ct);

        if (EsRespuestaConsolaWeb(response))
        {
            ModoConsolaPorEndpoint[baseUri.ToString()] = true;
            return await DescargarObjetoConsolaAsync(baseUri, bucket, key, accessKey, secretKey, ct);
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"MinIO devolvió {(int)response.StatusCode} al descargar: {body}");
        }

        return await response.Content.ReadAsByteArrayAsync(ct);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // IMPLEMENTACIÓN MINIO CONSOLE REST API (/api/v1 — PUERTO 9001 EN DOKPLOY)
    // ═══════════════════════════════════════════════════════════════════════════

    private async Task<string> ObtenerCookieSesionConsolaAsync(
        Uri baseUri,
        string accessKey,
        string secretKey,
        bool forzarRenovacion,
        CancellationToken ct)
    {
        var cacheKey = $"{baseUri}|{accessKey}";
        if (!forzarRenovacion
            && SesionesConsola.TryGetValue(cacheKey, out var sesion)
            && sesion.ExpiraUtc > DateTime.UtcNow)
        {
            return sesion.CookieHeader;
        }

        var loginUri = new Uri(baseUri, "/api/v1/login");
        var payloadJson = JsonSerializer.Serialize(new
        {
            accessKey,
            secretKey
        });

        using var req = new HttpRequestMessage(HttpMethod.Post, loginUri)
        {
            Content = new StringContent(payloadJson, Encoding.UTF8, "application/json")
        };

        using var resp = await ConsoleHttpClient.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var errBody = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(
                $"No se pudo autenticar en MinIO ({(int)resp.StatusCode}). Verifique Minio:AccessKey y Minio:SecretKey. Detalle: {ExtraerMensajeError(errBody)}");
        }

        var cookies = new List<string>();
        if (resp.Headers.TryGetValues("Set-Cookie", out var setCookieHeaders))
        {
            foreach (var raw in setCookieHeaders)
            {
                var par = raw.Split(';', 2)[0].Trim();
                if (!string.IsNullOrWhiteSpace(par))
                    cookies.Add(par);
            }
        }

        if (cookies.Count == 0)
        {
            throw new InvalidOperationException(
                $"MinIO Console en '{baseUri}' aceptó el login pero no devolvió cookie de sesión.");
        }

        var cookieHeader = string.Join("; ", cookies);
        SesionesConsola[cacheKey] = (cookieHeader, DateTime.UtcNow.AddMinutes(25));
        return cookieHeader;
    }

    private async Task<HttpResponseMessage> EnviarRequestConsolaAutenticadaAsync(
        Func<string, HttpRequestMessage> requestFactory,
        Uri baseUri,
        string accessKey,
        string secretKey,
        CancellationToken ct)
    {
        var cookie = await ObtenerCookieSesionConsolaAsync(baseUri, accessKey, secretKey, forzarRenovacion: false, ct);
        using var req1 = requestFactory(cookie);
        var resp1 = await ConsoleHttpClient.SendAsync(req1, ct);

        if (resp1.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            resp1.Dispose();
            var cookieNueva = await ObtenerCookieSesionConsolaAsync(baseUri, accessKey, secretKey, forzarRenovacion: true, ct);
            using var req2 = requestFactory(cookieNueva);
            return await ConsoleHttpClient.SendAsync(req2, ct);
        }

        return resp1;
    }

    private async Task AsegurarBucketConsolaAsync(
        Uri baseUri,
        string bucket,
        string accessKey,
        string secretKey,
        CancellationToken ct)
    {
        // 1. Consultar buckets existentes en /api/v1/buckets
        var listUri = new Uri(baseUri, "/api/v1/buckets");
        using (var listResp = await EnviarRequestConsolaAutenticadaAsync(cookie =>
               {
                   var r = new HttpRequestMessage(HttpMethod.Get, listUri);
                   r.Headers.TryAddWithoutValidation("Cookie", cookie);
                   return r;
               }, baseUri, accessKey, secretKey, ct))
        {
            if (listResp.IsSuccessStatusCode)
            {
                var json = await listResp.Content.ReadAsStringAsync(ct);
                if (ExisteBucketEnRespuestaConsola(json, bucket))
                    return;
            }
        }

        // 2. Crear bucket en /api/v1/buckets (intento 1: esquema moderno de MinIO Console; intento 2: esquema booleano anterior)
        var payloadsCreacion = new[]
        {
            JsonSerializer.Serialize(new
            {
                name = bucket,
                locking = false,
                versioning = new
                {
                    enabled = false,
                    excludePrefixes = Array.Empty<string>(),
                    excludeFolders = false
                }
            }),
            JsonSerializer.Serialize(new
            {
                name = bucket,
                locking = false,
                versioning = false
            })
        };

        string ultimoError = string.Empty;
        int ultimoStatus = 0;

        foreach (var bodyJson in payloadsCreacion)
        {
            using var createResp = await EnviarRequestConsolaAutenticadaAsync(cookie =>
            {
                var r = new HttpRequestMessage(HttpMethod.Post, listUri)
                {
                    Content = new StringContent(bodyJson, Encoding.UTF8, "application/json")
                };
                r.Headers.TryAddWithoutValidation("Cookie", cookie);
                return r;
            }, baseUri, accessKey, secretKey, ct);

            if (createResp.IsSuccessStatusCode || createResp.StatusCode == HttpStatusCode.Conflict)
            {
                _logger.LogInformation("Bucket '{Bucket}' verificado/creado exitosamente en MinIO.", bucket);
                return;
            }

            ultimoStatus = (int)createResp.StatusCode;
            ultimoError = await createResp.Content.ReadAsStringAsync(ct);

            if (ultimoError.Contains("already own it", StringComparison.OrdinalIgnoreCase)
                || ultimoError.Contains("already exists", StringComparison.OrdinalIgnoreCase)
                || ultimoError.Contains("BucketAlreadyOwnedByYou", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        throw new InvalidOperationException(
            $"No se pudo crear ni acceder al bucket '{bucket}' en MinIO ({ultimoStatus}): {ExtraerMensajeError(ultimoError)}");
    }

    private async Task SubirObjetoConsolaAsync(
        Uri baseUri,
        string bucket,
        string key,
        byte[] content,
        string contentType,
        string accessKey,
        string secretKey,
        CancellationToken ct)
    {
        var slashIdx = key.LastIndexOf('/');
        var folderPrefix = slashIdx >= 0 ? key[..(slashIdx + 1)] : string.Empty;
        var fileName = slashIdx >= 0 ? key[(slashIdx + 1)..] : key;
        var prefixBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(folderPrefix));
        var endpointKey = baseUri.ToString();

        var usarBase64 = !UploadPrefixUsaBase64PorEndpoint.TryGetValue(endpointKey, out var conocido) || conocido;
        var prefixQuery = usarBase64 ? prefixBase64 : folderPrefix;

        await EjecutarUploadConsolaConReintentoAsync(
            baseUri, bucket, prefixQuery, fileName, content, contentType, accessKey, secretKey, ct);

        // En la primera subida por este endpoint, confirmar que el objeto quedó en la ruta exacta 'solicitudes/{anio}/{id}/...'
        if (!UploadPrefixUsaBase64PorEndpoint.ContainsKey(endpointKey) && !string.IsNullOrEmpty(folderPrefix))
        {
            var existeEnRutaEsperada = await ExisteObjetoConsolaAsync(baseUri, bucket, key, accessKey, secretKey, ct);
            if (existeEnRutaEsperada)
            {
                UploadPrefixUsaBase64PorEndpoint[endpointKey] = usarBase64;
            }
            else
            {
                // Esta versión de MinIO Console recibe prefix sin codificar Base64 en /objects/upload
                UploadPrefixUsaBase64PorEndpoint[endpointKey] = false;
                await EjecutarUploadConsolaConReintentoAsync(
                    baseUri, bucket, folderPrefix, fileName, content, contentType, accessKey, secretKey, ct);
            }
        }
    }

    private async Task EjecutarUploadConsolaConReintentoAsync(
        Uri baseUri,
        string bucket,
        string prefixParam,
        string fileName,
        byte[] content,
        string contentType,
        string accessKey,
        string secretKey,
        CancellationToken ct)
    {
        var uploadUri = new Uri(
            baseUri,
            $"/api/v1/buckets/{Uri.EscapeDataString(bucket)}/objects/upload?prefix={Uri.EscapeDataString(prefixParam)}");

        // En MinIO Console (restapi/objects_handlers.go), el nombre del campo multipart es el tamaño en bytes del archivo
        var sizeFieldName = content.Length.ToString(CultureInfo.InvariantCulture);

        using var resp = await EnviarUploadMultipartConsolaAsync(
            uploadUri, baseUri, sizeFieldName, fileName, content, contentType, accessKey, secretKey, ct);

        if (resp.IsSuccessStatusCode)
            return;

        if (resp.StatusCode == HttpStatusCode.NotFound)
        {
            // Si el bucket fue eliminado externamente mientras el backend estaba encendido, recrearlo y reintentar una vez
            BucketsVerificados.TryRemove($"{baseUri}|{bucket}", out _);
            await AsegurarBucketConsolaAsync(baseUri, bucket, accessKey, secretKey, ct);
            BucketsVerificados[$"{baseUri}|{bucket}"] = true;

            using var retryResp = await EnviarUploadMultipartConsolaAsync(
                uploadUri, baseUri, sizeFieldName, fileName, content, contentType, accessKey, secretKey, ct);

            if (retryResp.IsSuccessStatusCode)
                return;

            var retryErr = await retryResp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(
                $"Error al subir el adjunto al bucket '{bucket}' en MinIO ({(int)retryResp.StatusCode}): {ExtraerMensajeError(retryErr)}");
        }

        // Respaldo de compatibilidad si una versión específica espera campo 'file' en lugar del tamaño
        var errBody = await resp.Content.ReadAsStringAsync(ct);
        using var fallbackResp = await EnviarUploadMultipartConsolaAsync(
            uploadUri, baseUri, "file", fileName, content, contentType, accessKey, secretKey, ct);

        if (fallbackResp.IsSuccessStatusCode)
            return;

        throw new InvalidOperationException(
            $"Error al subir el adjunto al bucket '{bucket}' en MinIO ({(int)resp.StatusCode}): {ExtraerMensajeError(errBody)}");
    }

    private async Task<bool> ExisteObjetoConsolaAsync(
        Uri baseUri,
        string bucket,
        string key,
        string accessKey,
        string secretKey,
        CancellationToken ct)
    {
        try
        {
            var keyBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(key));
            var checkUri = new Uri(
                baseUri,
                $"/api/v1/buckets/{Uri.EscapeDataString(bucket)}/objects?prefix={Uri.EscapeDataString(keyBase64)}");

            using var resp = await EnviarRequestConsolaAutenticadaAsync(cookie =>
            {
                var r = new HttpRequestMessage(HttpMethod.Get, checkUri);
                r.Headers.TryAddWithoutValidation("Cookie", cookie);
                return r;
            }, baseUri, accessKey, secretKey, ct);

            if (!resp.IsSuccessStatusCode)
                return false;

            var json = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("objects", out var objs)
                && objs.ValueKind == JsonValueKind.Array)
            {
                foreach (var obj in objs.EnumerateArray())
                {
                    if (obj.TryGetProperty("name", out var nameProp)
                        && string.Equals(nameProp.GetString(), key, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }
        }
        catch
        {
            // Si el listado falla, asumir éxito de la respuesta 2xx previa
            return true;
        }

        return false;
    }

    private async Task<HttpResponseMessage> EnviarUploadMultipartConsolaAsync(
        Uri uploadUri,
        Uri baseUri,
        string fieldName,
        string fileName,
        byte[] content,
        string contentType,
        string accessKey,
        string secretKey,
        CancellationToken ct)
    {
        return await EnviarRequestConsolaAutenticadaAsync(cookie =>
        {
            var boundary = $"----TmrMinioUpload{Guid.NewGuid():N}";
            var form = new MultipartFormDataContent(boundary);
            form.Headers.ContentType = MediaTypeHeaderValue.Parse($"multipart/form-data; boundary={boundary}");

            var filePart = new ByteArrayContent(content);
            filePart.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            filePart.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
            {
                Name = $"\"{fieldName}\"",
                FileName = $"\"{fileName}\""
            };
            form.Add(filePart);

            var req = new HttpRequestMessage(HttpMethod.Post, uploadUri)
            {
                Content = form
            };
            req.Headers.TryAddWithoutValidation("Cookie", cookie);
            return req;
        }, baseUri, accessKey, secretKey, ct);
    }

    private async Task<byte[]?> DescargarObjetoConsolaAsync(
        Uri baseUri,
        string bucket,
        string key,
        string accessKey,
        string secretKey,
        CancellationToken ct)
    {
        var keyBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(key));
        var downloadUri = new Uri(
            baseUri,
            $"/api/v1/buckets/{Uri.EscapeDataString(bucket)}/objects/download?prefix={Uri.EscapeDataString(keyBase64)}");

        using var resp = await EnviarRequestConsolaAutenticadaAsync(cookie =>
        {
            var req = new HttpRequestMessage(HttpMethod.Get, downloadUri);
            req.Headers.TryAddWithoutValidation("Cookie", cookie);
            return req;
        }, baseUri, accessKey, secretKey, ct);

        if (resp.StatusCode == HttpStatusCode.NotFound)
            return null;

        if (!resp.IsSuccessStatusCode)
        {
            var errBody = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(
                $"MinIO devolvió {(int)resp.StatusCode} al descargar '{key}': {ExtraerMensajeError(errBody)}");
        }

        return await resp.Content.ReadAsByteArrayAsync(ct);
    }

    private static bool ExisteBucketEnRespuestaConsola(string json, string targetBucket)
    {
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("buckets", out var bucketsProp)
                && bucketsProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in bucketsProp.EnumerateArray())
                {
                    if (item.TryGetProperty("name", out var nameProp)
                        && string.Equals(nameProp.GetString(), targetBucket, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }
        }
        catch
        {
            // Ignorar error de parseo y continuar con intento de creación idempotente
        }

        return false;
    }

    private static string ExtraerMensajeError(string rawBody)
    {
        if (string.IsNullOrWhiteSpace(rawBody))
            return "Sin detalle adicional del servidor.";

        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                if (doc.RootElement.TryGetProperty("detailedMessage", out var detailed)
                    && detailed.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(detailed.GetString()))
                {
                    return detailed.GetString()!;
                }

                if (doc.RootElement.TryGetProperty("message", out var msg)
                    && msg.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(msg.GetString()))
                {
                    return msg.GetString()!;
                }
            }
        }
        catch
        {
            // Devolver texto plano truncado
        }

        return rawBody.Length > 300 ? rawBody[..300] : rawBody;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // IMPLEMENTACIÓN MINIO S3 NATIVA (AWS SIGNATURE V4 — PUERTO 9000)
    // ═══════════════════════════════════════════════════════════════════════════

    private async Task AsegurarBucketS3Async(
        Uri baseUri,
        string bucket,
        string accessKey,
        string secretKey,
        CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient();
        using var headReq = CrearRequestFirmadaS3(HttpMethod.Head, baseUri, bucket, "", [], "application/octet-stream", accessKey, secretKey);
        using var headResp = await client.SendAsync(headReq, ct);

        if (headResp.StatusCode == HttpStatusCode.OK)
            return;

        if (headResp.StatusCode == HttpStatusCode.NotFound)
        {
            using var putBucketReq = CrearRequestFirmadaS3(HttpMethod.Put, baseUri, bucket, "", [], "application/octet-stream", accessKey, secretKey);
            using var putBucketResp = await client.SendAsync(putBucketReq, ct);

            if (!putBucketResp.IsSuccessStatusCode && putBucketResp.StatusCode != HttpStatusCode.Conflict)
            {
                var err = await putBucketResp.Content.ReadAsStringAsync(ct);
                throw new InvalidOperationException(
                    $"No se pudo crear ni acceder al bucket '{bucket}' en MinIO S3 ({(int)putBucketResp.StatusCode}): {err}");
            }
            return;
        }

        if (headResp.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
        {
            throw new InvalidOperationException(
                $"MinIO rechazó las credenciales ({(int)headResp.StatusCode}) para el bucket '{bucket}'. Verifique Minio:AccessKey y Minio:SecretKey.");
        }
    }

    private static bool EsRespuestaConsolaWeb(HttpResponseMessage response)
    {
        var mediaType = response.Content.Headers.ContentType?.MediaType;
        return string.Equals(mediaType, "text/html", StringComparison.OrdinalIgnoreCase)
               || response.StatusCode == HttpStatusCode.MethodNotAllowed;
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
