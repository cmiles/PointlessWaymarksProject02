using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using PointlessWaymarks.CmsData.Database;
using PointlessWaymarks.CmsData.Database.Models;
using PointlessWaymarks.CommonTools.S3;

namespace PointlessWaymarks.CmsData.Server;

public class PreviewServer
{
    private readonly Dictionary<Guid, string> _previewPages = new();
    public int ServerPort { get; } = FreeTcpPort();

    public static int FreeTcpPort()
    {
        //https://stackoverflow.com/questions/138043/find-the-next-tcp-port-in-net
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public async Task StartServer(string siteDomainName, string previewFileRootDirectory)
    {
        var builder = WebApplication.CreateBuilder();

        builder.WebHost.ConfigureKestrel(x => x.ListenLocalhost(ServerPort));

        var app = builder.Build();

        app.UseDeveloperExceptionPage();

        app.Use(async (context, next) =>
        {
            var possiblePath = context.Request.Path;

            if (string.IsNullOrWhiteSpace(possiblePath.Value) ||
                possiblePath.Value == "/")
            {
                var moddedFile = (await File.ReadAllTextAsync(Path.Join(previewFileRootDirectory, "index.html")))
                    .Replace(
                        $"https://{siteDomainName}",
                        $"http://{siteDomainName}", StringComparison.OrdinalIgnoreCase)
                    .Replace($"//{siteDomainName}", $"//localhost:{ServerPort}",
                        StringComparison.OrdinalIgnoreCase);
                await context.Response.WriteAsync(moddedFile);
            }
            else if (possiblePath.Value.EndsWith(".html", StringComparison.OrdinalIgnoreCase) ||
                     possiblePath.Value.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                var rawFile = new StringBuilder();

                using (var sr = File.OpenText(Path.Join(previewFileRootDirectory, possiblePath)))
                {
                    while (await sr.ReadLineAsync() is { } streamLine)
                        rawFile.AppendLine(streamLine.Replace(
                            $"https://{siteDomainName}", $"http://{siteDomainName}",
                            StringComparison.OrdinalIgnoreCase).Replace($"//{siteDomainName}",
                            $"//localhost:{ServerPort}", StringComparison.OrdinalIgnoreCase));
                }

                await context.Response.WriteAsync(rawFile.ToString());
            }
            else
            {
                await next.Invoke();
            }
        });

        var provider = new FileExtensionContentTypeProvider
        {
            Mappings =
            {
                // Add new mappings
                [".flac"] = "audio/flac",
                [".gpx"] = "application/gpx+xml"
            }
        };

        app.UseFileServer(new FileServerOptions
        {
            FileProvider = new PhysicalFileProvider(previewFileRootDirectory),
            RequestPath = "",
            EnableDirectoryBrowsing = true,
            StaticFileOptions = { ContentTypeProvider = provider }
        });

        app.MapPost("/localapi/loadpreviewpage", (ServerLoadPreviewPage data) =>
        {
            _previewPages[data.RequesterId] = data.ToPreview;

            // Redirect to another action
            return Task.FromResult(Results.Redirect($"/localapi/showpreviewpage/{data.RequesterId}"));
        });

        app.MapGet("/localapi/showpreviewpage/{requester}", (Guid requester) =>
        {
            if (_previewPages.TryGetValue(requester, out var page))
            {
                var cleanedHtml = page.Replace(
                    $"https://{siteDomainName}", $"http://{siteDomainName}",
                    StringComparison.OrdinalIgnoreCase).Replace($"//{siteDomainName}",
                    $"//localhost:{ServerPort}", StringComparison.OrdinalIgnoreCase);
                return Results.Content(cleanedHtml, "text/html");
            }

            return Results.NotFound();
        });

        app.MapGet("/localapi/contentjson/{contentId}", async (Guid contentId) =>
        {
            try
            {
                var db = await Db.Context();
                var content = await db.ContentAndSnippetsFromContentId(contentId);

                if (content == null) return Results.NotFound($"Content with ID {contentId} not found");

                var json = JsonSerializer.Serialize(content, JsonTools.WriteIndentedOptions);
                return Results.Content(json, "application/json");
            }
            catch (Exception ex)
            {
                return Results.Problem(
                    ex.Message,
                    title: "Error retrieving content",
                    statusCode: 500
                );
            }
        });

        app.MapGet("/localapi/linksnapshots/{contentId}", async (Guid contentId) =>
        {
            try
            {
                var imageFiles = UserSettingsSingleton.CurrentSettings()
                    .LinkSnapshotImages(contentId);

                if (!imageFiles.Any())
                    return Results.NotFound($"No link snapshot images found for content ID {contentId}");

                // Create a memory stream for the zip archive
                using var zipStream = new MemoryStream();
                using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
                {
                    foreach (var file in imageFiles)
                        if (file.Exists)
                        {
                            var entry = archive.CreateEntry(file.Name, CompressionLevel.Fastest);
                            await using var entryStream = entry.Open();
                            await using var fileStream = file.OpenRead();
                            await fileStream.CopyToAsync(entryStream);
                        }
                }

                zipStream.Seek(0, SeekOrigin.Begin);

                // Return the zip file
                return Results.File(zipStream, "application/zip", $"LinkSnapshots_{contentId}.zip");
            }
            catch (Exception ex)
            {
                return Results.Problem(
                    ex.Message,
                    title: "Error retrieving content",
                    statusCode: 500
                );
            }
        });

        app.MapGet("/localapi/mediafile/{contentId}", async (Guid contentId) =>
        {
            try
            {
                var db = await Db.Context();
                var content = await db.ContentFromContentId(contentId);
                var settings = UserSettingsSingleton.CurrentSettings();

                if (content == null) return Results.NotFound($"Content with ID {contentId} not found");

                FileInfo? mediaFile = null;
                var contentType = "";

                // Check content type and retrieve the appropriate media file
                switch (content)
                {
                    case PhotoContent photoContent:
                        mediaFile = settings.LocalMediaArchivePhotoContentFile(photoContent);
                        contentType = "Photo";
                        break;
                    case ImageContent imageContent:
                        mediaFile = settings.LocalMediaArchiveImageContentFile(imageContent);
                        contentType = "Image";
                        break;
                    case FileContent fileContent:
                        mediaFile = settings.LocalMediaArchiveFileContentFile(fileContent);
                        contentType = "File";
                        break;
                    case VideoContent videoContent:
                        mediaFile = settings.LocalMediaArchiveVideoContentFile(videoContent);
                        contentType = "Video";
                        break;
                }

                if (mediaFile == null)
                    return Results.NotFound($"No media file associated with {contentType} content ID {contentId}");

                var response = new ContentMediaFileResponse
                {
                    ContentType = contentType,
                    FileName = mediaFile.Name,
                    FullPath = mediaFile.FullName,
                    Exists = mediaFile.Exists,
                    FileSize = mediaFile.Exists ? mediaFile.Length : 0
                };

                return Results.Json(response, JsonTools.WriteIndentedOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem(
                    ex.Message,
                    title: "Error retrieving media file information",
                    statusCode: 500
                );
            }
        });

        await app.RunAsync();
    }
}