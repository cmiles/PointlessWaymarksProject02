using System.Net;
using System.Text;
using Amazon.S3;
using Amazon.S3.Model;
using PointlessWaymarks.SiteViewerMaui.S3;

namespace PointlessWaymarks.SiteViewerMaui.Services;

/// <summary>The bytes and content type resolved for a single request.</summary>
public readonly record struct S3Content(byte[] Bytes, string ContentType);

/// <summary>
///     Fetches site content from an S3 (or S3-compatible) bucket and prepares it for display in the
///     in-app WebView. This is the self-contained, no-Kestrel adaptation of the desktop
///     <c>S3PreviewServer</c>: it maps a request path to an S3 key, downloads the object, rewrites
///     site-domain URLs in text content so navigation stays inside the app, and resolves a content
///     type (including the desktop's custom <c>.flac/.gpx/.webm/.ogg</c> mappings).
///     The static helpers are pure so they can be unit tested without network access.
/// </summary>
public class S3ContentService : IDisposable
{
    private readonly IS3AccountInformation _account;
    private readonly AmazonS3Client _client;
    private readonly string? _prefix;
    private readonly string _siteDomain;
    private readonly string _virtualHost;

    public S3ContentService(IS3AccountInformation account, string siteDomain, string virtualHost, string? prefix = null)
    {
        _account = account;
        _client = account.S3Client();
        _siteDomain = siteDomain;
        _virtualHost = virtualHost;
        _prefix = prefix;
    }

    public void Dispose()
    {
        _client.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Normalizes a request path to an S3 key: "/" (or empty) becomes "index.html", the leading
    ///     slash is stripped and any configured bucket prefix is prepended. Mirrors the desktop
    ///     <c>S3PreviewServer</c> path handling.
    /// </summary>
    public static string MapPathToS3Key(string? requestPath, string? prefix = null)
    {
        var path = requestPath;

        if (string.IsNullOrWhiteSpace(path) || path == "/")
            path = "/index.html";

        var key = path.TrimStart('/');

        if (!string.IsNullOrWhiteSpace(prefix))
            key = $"{prefix.TrimEnd('/')}/{key}";

        return key;
    }

    /// <summary>Text types whose domain references are rewritten (html/json/css/js).</summary>
    public static bool NeedsTextProcessing(string path)
    {
        return path.EndsWith(".html", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".htm", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".css", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".js", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Rewrites references to the live site domain so they point at the in-app virtual host,
    ///     keeping navigation inside the WebView. The protocol-relative form handles http, https and
    ///     "//" references in one pass (equivalent to the desktop <c>ProcessTextContent</c>).
    /// </summary>
    public static string ProcessTextContent(string content, string siteDomain, string virtualHost)
    {
        if (string.IsNullOrEmpty(siteDomain)) return content;

        return content.Replace($"//{siteDomain}", $"//{virtualHost}", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Resolves a content type from the path's extension, including the desktop's custom media mappings.</summary>
    public static string ResolveContentType(string path)
    {
        var lower = path.ToLowerInvariant();

        return lower switch
        {
            _ when lower.EndsWith(".html") || lower.EndsWith(".htm") => "text/html",
            _ when lower.EndsWith(".css") => "text/css",
            _ when lower.EndsWith(".js") || lower.EndsWith(".mjs") => "application/javascript",
            _ when lower.EndsWith(".json") => "application/json",
            _ when lower.EndsWith(".xml") => "application/xml",
            _ when lower.EndsWith(".txt") => "text/plain",
            _ when lower.EndsWith(".svg") => "image/svg+xml",
            _ when lower.EndsWith(".png") => "image/png",
            _ when lower.EndsWith(".jpg") || lower.EndsWith(".jpeg") => "image/jpeg",
            _ when lower.EndsWith(".gif") => "image/gif",
            _ when lower.EndsWith(".webp") => "image/webp",
            _ when lower.EndsWith(".ico") => "image/x-icon",
            _ when lower.EndsWith(".woff2") => "font/woff2",
            _ when lower.EndsWith(".woff") => "font/woff",
            _ when lower.EndsWith(".ttf") => "font/ttf",
            _ when lower.EndsWith(".otf") => "font/otf",
            _ when lower.EndsWith(".pdf") => "application/pdf",
            _ when lower.EndsWith(".mp4") => "video/mp4",
            _ when lower.EndsWith(".mp3") => "audio/mpeg",
            _ when lower.EndsWith(".wav") => "audio/wav",
            // Custom mappings copied from the desktop S3PreviewServer.
            _ when lower.EndsWith(".flac") => "audio/flac",
            _ when lower.EndsWith(".gpx") => "application/gpx+xml",
            _ when lower.EndsWith(".webm") => "video/webm",
            _ when lower.EndsWith(".ogg") => "video/ogg",
            _ => "application/octet-stream"
        };
    }

    /// <summary>
    ///     Fetches the content for the given request path from S3. Returns null when the object does
    ///     not exist (clean not-found rather than throwing into the WebView).
    /// </summary>
    public async Task<S3Content?> GetContentAsync(string requestPath)
    {
        var key = MapPathToS3Key(requestPath, _prefix);

        try
        {
            using var response = await _client.GetObjectAsync(new GetObjectRequest
            {
                BucketName = _account.BucketName(),
                Key = key
            });

            await using var responseStream = response.ResponseStream;
            using var memory = new MemoryStream();
            await responseStream.CopyToAsync(memory);
            var bytes = memory.ToArray();

            if (NeedsTextProcessing(key))
            {
                var text = Encoding.UTF8.GetString(bytes);
                text = ProcessTextContent(text, _siteDomain, _virtualHost);
                bytes = Encoding.UTF8.GetBytes(text);
            }

            return new S3Content(bytes, ResolveContentType(key));
        }
        catch (AmazonS3Exception s3Ex) when (s3Ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }
}
