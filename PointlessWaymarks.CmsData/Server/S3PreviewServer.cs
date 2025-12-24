using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using PointlessWaymarks.CommonTools;
using PointlessWaymarks.CommonTools.S3;
using Serilog;

namespace PointlessWaymarks.CmsData.Server;

public class S3PreviewServer : IDisposable
{
    private readonly string _cacheDirectory;
    private readonly SemaphoreSlim _cacheLock = new(1, 1);
    private readonly byte[] _encryptionIv;
    private readonly byte[] _encryptionKey;
    private readonly IS3AccountInformation _s3AccountInfo;
    private readonly AmazonS3Client _s3Client;
    private bool _disposed;

    public S3PreviewServer(IS3AccountInformation s3AccountInfo)
    {
        _s3AccountInfo = s3AccountInfo;
        _s3Client = s3AccountInfo.S3Client();

        // Create encrypted cache directory in temp storage
        _cacheDirectory = Path.Combine(
            FileLocationTools.TempStorageDirectory().FullName,
            "S3PreviewCache",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(_cacheDirectory);

        // Generate encryption key and IV for this session
        using var aes = Aes.Create();
        aes.GenerateKey();
        aes.GenerateIV();
        _encryptionKey = aes.Key;
        _encryptionIv = aes.IV;

        Log.ForContext("CacheDirectory", _cacheDirectory)
            .Information("S3PreviewServer cache directory created");
    }

    public int ServerPort { get; } = FreeTcpPort();

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Decrypts and reads cached file content
    /// </summary>
    private async Task<string?> DecryptCachedFile(string originalPath)
    {
        await _cacheLock.WaitAsync();
        try
        {
            var obfuscatedFileName = GetObfuscatedFileName(originalPath);
            var cachedFilePath = Path.Combine(_cacheDirectory, obfuscatedFileName);

            if (!File.Exists(cachedFilePath))
                return null;

            using var aes = Aes.Create();
            aes.Key = _encryptionKey;
            aes.IV = _encryptionIv;

            await using var fileStream = File.OpenRead(cachedFilePath);
            await using var cryptoStream = new CryptoStream(fileStream, aes.CreateDecryptor(), CryptoStreamMode.Read);
            using var reader = new StreamReader(cryptoStream);
            return await reader.ReadToEndAsync();
        }
        catch (Exception ex)
        {
            Log.ForContext("OriginalPath", originalPath)
                .Warning(ex, "Failed to decrypt cached file");
            return null;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            _cacheLock.Dispose();
            _s3Client?.Dispose();

            // Clean up encrypted cache directory
            try
            {
                if (Directory.Exists(_cacheDirectory))
                {
                    Directory.Delete(_cacheDirectory, true);
                    Log.ForContext("CacheDirectory", _cacheDirectory)
                        .Information("S3PreviewServer cache directory cleaned up");
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to clean up S3PreviewServer cache directory");
            }
        }

        _disposed = true;
    }

    /// <summary>
    ///     Downloads file from S3, processes it, and caches it encrypted
    /// </summary>
    private async Task<string?> DownloadFromS3AndCache(string s3Key, string siteDomainName)
    {
        if (_s3Client == null || _s3AccountInfo == null)
        {
            Log.Warning("S3 client not configured");
            return null;
        }

        try
        {
            var request = new GetObjectRequest
            {
                BucketName = _s3AccountInfo.BucketName(),
                Key = s3Key
            };

            using var response = await _s3Client.GetObjectAsync(request);
            await using var responseStream = response.ResponseStream;
            using var reader = new StreamReader(responseStream);
            var content = await reader.ReadToEndAsync();

            // Cache the original content encrypted
            await EncryptAndCacheFile(content, s3Key);

            Log.ForContext("S3Key", s3Key)
                .Debug("File downloaded from S3 and cached");

            return content;
        }
        catch (Exception ex)
        {
            Log.ForContext("S3Key", s3Key)
                .Error(ex, "Failed to download file from S3");
            return null;
        }
    }

    /// <summary>
    ///     Encrypts file content and saves to cache with obfuscated filename
    /// </summary>
    private async Task<string> EncryptAndCacheFile(string content, string originalPath)
    {
        await _cacheLock.WaitAsync();
        try
        {
            var obfuscatedFileName = GetObfuscatedFileName(originalPath);
            var cachedFilePath = Path.Combine(_cacheDirectory, obfuscatedFileName);

            using var aes = Aes.Create();
            aes.Key = _encryptionKey;
            aes.IV = _encryptionIv;

            await using var fileStream = File.Create(cachedFilePath);
            await using var cryptoStream = new CryptoStream(fileStream, aes.CreateEncryptor(), CryptoStreamMode.Write);
            await using var writer = new StreamWriter(cryptoStream);
            await writer.WriteAsync(content);

            Log.ForContext("OriginalPath", originalPath)
                .ForContext("CachedPath", cachedFilePath)
                .Debug("File encrypted and cached");

            return cachedFilePath;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    ~S3PreviewServer()
    {
        Dispose(false);
    }

    public static int FreeTcpPort()
    {
        //https://stackoverflow.com/questions/138043/find-the-next-tcp-port-in-net
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    /// <summary>
    ///     Gets file content from cache or downloads from S3
    /// </summary>
    private async Task<string?> GetFileContent(string path, string siteDomainName)
    {
        // Try cache first
        var cachedContent = await DecryptCachedFile(path);
        if (cachedContent != null)
        {
            Log.ForContext("Path", path).Debug("Serving from cache");
            return cachedContent;
        }

        // Download from S3
        Log.ForContext("Path", path).Debug("Cache miss, downloading from S3");
        return await DownloadFromS3AndCache(path, siteDomainName);
    }

    /// <summary>
    ///     Generates an obfuscated filename from the original path using SHA256 hashing
    /// </summary>
    private string GetObfuscatedFileName(string originalPath)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(originalPath));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    /// <summary>
    ///     Processes HTML/JSON content by replacing domain references
    /// </summary>
    private string ProcessTextContent(string content, string siteDomainName)
    {
        return content
            .Replace($"https://{siteDomainName}", $"http://{siteDomainName}",
                StringComparison.OrdinalIgnoreCase)
            .Replace($"//{siteDomainName}", $"//localhost:{ServerPort}",
                StringComparison.OrdinalIgnoreCase);
    }

    public async Task StartServer(string siteDomainName, string? s3BucketPrefix = null)
    {
        if (_s3Client == null || _s3AccountInfo == null)
            throw new InvalidOperationException(
                "S3PreviewServer requires S3 account information to be configured");

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(x => x.ListenLocalhost(ServerPort));

        var app = builder.Build();
        app.UseDeveloperExceptionPage();

        app.Use(async (context, next) =>
        {
            var requestPath = context.Request.Path.Value ?? "/";

            // Normalize path for S3
            if (requestPath == "/" || string.IsNullOrWhiteSpace(requestPath))
                requestPath = "/index.html";

            // Remove leading slash for S3 key
            var s3Key = requestPath.TrimStart('/');

            // Add prefix if specified
            if (!string.IsNullOrWhiteSpace(s3BucketPrefix))
                s3Key = $"{s3BucketPrefix.TrimEnd('/')}/{s3Key}";

            try
            {
                // Check if this is a text file that needs processing
                var needsProcessing = requestPath.EndsWith(".html", StringComparison.OrdinalIgnoreCase) ||
                                      requestPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
                                      requestPath.EndsWith(".css", StringComparison.OrdinalIgnoreCase) ||
                                      requestPath.EndsWith(".js", StringComparison.OrdinalIgnoreCase);

                if (needsProcessing)
                {
                    var content = await GetFileContent(s3Key, siteDomainName);

                    if (content == null)
                    {
                        context.Response.StatusCode = 404;
                        await context.Response.WriteAsync($"File not found: {requestPath}");
                        return;
                    }

                    var processedContent = ProcessTextContent(content, siteDomainName);

                    // Set appropriate content type
                    var contentType = requestPath.EndsWith(".html", StringComparison.OrdinalIgnoreCase)
                        ? "text/html"
                        : requestPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                            ? "application/json"
                            : requestPath.EndsWith(".css", StringComparison.OrdinalIgnoreCase)
                                ? "text/css"
                                : "application/javascript";

                    context.Response.ContentType = contentType;
                    await context.Response.WriteAsync(processedContent);
                }
                else
                {
                    // For binary files (images, videos, etc.), stream directly from S3
                    var request = new GetObjectRequest
                    {
                        BucketName = _s3AccountInfo.BucketName(),
                        Key = s3Key
                    };

                    using var response = await _s3Client.GetObjectAsync(request);

                    // Set content type
                    var provider = new FileExtensionContentTypeProvider();
                    if (provider.TryGetContentType(requestPath, out var contentType))
                        context.Response.ContentType = contentType;
                    else
                        // Add custom mappings
                        context.Response.ContentType = requestPath.ToLowerInvariant() switch
                        {
                            var p when p.EndsWith(".flac") => "audio/flac",
                            var p when p.EndsWith(".gpx") => "application/gpx+xml",
                            var p when p.EndsWith(".webm") => "video/webm",
                            var p when p.EndsWith(".ogg") => "video/ogg",
                            _ => "application/octet-stream"
                        };

                    await response.ResponseStream.CopyToAsync(context.Response.Body);
                }
            }
            catch (AmazonS3Exception s3Ex) when (s3Ex.StatusCode == HttpStatusCode.NotFound)
            {
                context.Response.StatusCode = 404;
                await context.Response.WriteAsync($"File not found: {requestPath}");
            }
            catch (Exception ex)
            {
                Log.ForContext("RequestPath", requestPath)
                    .Error(ex, "Error serving file from S3");
                context.Response.StatusCode = 500;
                await context.Response.WriteAsync($"Error loading file: {ex.Message}");
            }

            await next(context);
        });

        await app.RunAsync();
    }
}