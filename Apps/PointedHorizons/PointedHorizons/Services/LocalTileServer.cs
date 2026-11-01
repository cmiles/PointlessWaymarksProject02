using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace PointedHorizons.Services;

/// <summary>
/// Embedded local loopback HTTP server that serves PMTiles archives to MapLibre GL JS
/// via HTTP Range requests (206 Partial Content), enabling offline vector and raster PMTiles
/// on Photino desktop and .NET MAUI Android.
/// </summary>
public static class LocalTileServer
{
    private static HttpListener? _listener;
    private static CancellationTokenSource? _cts;
    private static readonly object _lock = new();

    public static int Port { get; private set; } = 18234;
    public static bool IsRunning { get; private set; }

    /// <summary>
    /// Gets the base URL for the local tile server.
    /// </summary>
    public static string GetBaseUrl() => $"http://127.0.0.1:{Port}/";

    /// <summary>
    /// Gets the HTTP URL to access a specific PMTiles file through this server.
    /// </summary>
    public static string GetPmTileUrl(string fileName)
    {
        return $"{GetBaseUrl()}pmtiles/{Uri.EscapeDataString(fileName)}";
    }

    /// <summary>
    /// Starts the local HTTP listener if not already running.
    /// </summary>
    public static void StartServer()
    {
        lock (_lock)
        {
            if (IsRunning) return;

            // In WebAssembly/Browser, TCP sockets are unsupported
            if (OperatingSystem.IsBrowser()) return;

            int[] portsToTry = [18234, 18235, 18236, 18237, 0];
            foreach (int port in portsToTry)
            {
                try
                {
                    int chosenPort = port == 0 ? GetAvailablePort() : port;
                    var listener = new HttpListener();
                    listener.Prefixes.Add($"http://127.0.0.1:{chosenPort}/");
                    listener.Prefixes.Add($"http://localhost:{chosenPort}/");
                    listener.Start();

                    _listener = listener;
                    Port = chosenPort;
                    IsRunning = true;
                    _cts = new CancellationTokenSource();

                    Task.Run(() => ListenLoopAsync(_listener, _cts.Token));
                    Debug.WriteLine($"[LocalTileServer] Started successfully on port {Port}");
                    break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[LocalTileServer] Failed to bind to port {port}: {ex.Message}");
                }
            }
        }
    }

    /// <summary>
    /// Stops the local HTTP server.
    /// </summary>
    public static void StopServer()
    {
        lock (_lock)
        {
            try
            {
                _cts?.Cancel();
                _listener?.Stop();
                _listener?.Close();
            }
            catch { }
            finally
            {
                _listener = null;
                _cts = null;
                IsRunning = false;
            }
        }
    }

    private static int GetAvailablePort()
    {
        try
        {
            using var tcp = new TcpListener(IPAddress.Loopback, 0);
            tcp.Start();
            int port = ((IPEndPoint)tcp.LocalEndpoint).Port;
            tcp.Stop();
            return port;
        }
        catch
        {
            return 18234;
        }
    }

    private static async Task ListenLoopAsync(HttpListener listener, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && listener.IsListening)
        {
            try
            {
                var context = await listener.GetContextAsync();
                _ = Task.Run(() => HandleRequestAsync(context));
            }
            catch (HttpListenerException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LocalTileServer] ListenLoop error: {ex.Message}");
            }
        }
    }

    private static async Task HandleRequestAsync(HttpListenerContext context)
    {
        var req = context.Request;
        var resp = context.Response;

        try
        {
            // Set CORS headers for MapLibre inside WebView2 / Android WebView
            resp.AddHeader("Access-Control-Allow-Origin", "*");
            resp.AddHeader("Access-Control-Allow-Methods", "GET, HEAD, OPTIONS");
            resp.AddHeader("Access-Control-Allow-Headers", "Range, Content-Type, Accept");
            resp.AddHeader("Access-Control-Expose-Headers", "Content-Range, Content-Length, Accept-Ranges, ETag");
            resp.AddHeader("Accept-Ranges", "bytes");

            if (req.HttpMethod == "OPTIONS")
            {
                resp.StatusCode = (int)HttpStatusCode.NoContent;
                resp.Close();
                return;
            }

            string path = req.Url?.AbsolutePath ?? "";

            // Handle PMTiles and Style JSON requests: /pmtiles/{fileName} or /styles/{fileName}
            if (path.StartsWith("/pmtiles/", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/styles/", StringComparison.OrdinalIgnoreCase))
            {
                string prefix = path.StartsWith("/pmtiles/", StringComparison.OrdinalIgnoreCase) ? "/pmtiles/" : "/styles/";
                string rawFile = path.Substring(prefix.Length);
                string fileName = Uri.UnescapeDataString(rawFile);

                // Reject traversal attempts
                if (fileName.Contains("..") || fileName.Contains('/') || fileName.Contains('\\'))
                {
                    resp.StatusCode = (int)HttpStatusCode.Forbidden;
                    resp.Close();
                    return;
                }

                string dir = OfflineDataService.PmTilesDirectory;
                string filePath = Path.Combine(dir, fileName);

                if (!File.Exists(filePath))
                {
                    resp.StatusCode = (int)HttpStatusCode.NotFound;
                    resp.Close();
                    return;
                }

                string contentType = fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                    ? "application/json; charset=utf-8"
                    : "application/octet-stream";

                var fileInfo = new FileInfo(filePath);
                long fileLength = fileInfo.Length;
                string rangeHeader = req.Headers["Range"] ?? "";

                if (!string.IsNullOrEmpty(rangeHeader) && rangeHeader.StartsWith("bytes=", StringComparison.OrdinalIgnoreCase))
                {
                    // Parse byte range
                    string rangeStr = rangeHeader.Substring("bytes=".Length).Trim();
                    string[] parts = rangeStr.Split('-');

                    long start = 0;
                    long end = fileLength - 1;

                    if (!string.IsNullOrEmpty(parts[0]) && long.TryParse(parts[0], out long parsedStart))
                    {
                        start = parsedStart;
                    }
                    if (parts.Length > 1 && !string.IsNullOrEmpty(parts[1]) && long.TryParse(parts[1], out long parsedEnd))
                    {
                        end = parsedEnd;
                    }

                    if (start >= fileLength || start > end)
                    {
                        resp.StatusCode = 416; // Range Not Satisfiable
                        resp.AddHeader("Content-Range", $"bytes */{fileLength}");
                        resp.Close();
                        return;
                    }

                    if (end >= fileLength) end = fileLength - 1;
                    long count = end - start + 1;

                    resp.StatusCode = (int)HttpStatusCode.PartialContent;
                    resp.ContentType = contentType;
                    resp.AddHeader("Content-Range", $"bytes {start}-{end}/{fileLength}");
                    resp.ContentLength64 = count;

                    if (req.HttpMethod != "HEAD")
                    {
                        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 64 * 1024, true);
                        fs.Seek(start, SeekOrigin.Begin);
                        byte[] buffer = new byte[Math.Min(64 * 1024, count)];
                        long remaining = count;
                        while (remaining > 0)
                        {
                            int toRead = (int)Math.Min(buffer.Length, remaining);
                            int read = await fs.ReadAsync(buffer, 0, toRead);
                            if (read <= 0) break;
                            await resp.OutputStream.WriteAsync(buffer, 0, read);
                            remaining -= read;
                        }
                    }
                }
                else
                {
                    resp.StatusCode = (int)HttpStatusCode.OK;
                    resp.ContentType = contentType;
                    resp.ContentLength64 = fileLength;

                    if (req.HttpMethod != "HEAD")
                    {
                        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 64 * 1024, true);
                        await fs.CopyToAsync(resp.OutputStream);
                    }
                }

                resp.Close();
                return;
            }

            // Unknown route
            resp.StatusCode = (int)HttpStatusCode.NotFound;
            resp.Close();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LocalTileServer] Request handler error: {ex.Message}");
            try
            {
                resp.StatusCode = (int)HttpStatusCode.InternalServerError;
                resp.Close();
            }
            catch { }
        }
    }
}

