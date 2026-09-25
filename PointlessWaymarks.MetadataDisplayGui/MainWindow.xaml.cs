using System.ComponentModel;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json.Nodes;
using System.Windows;
using GongSolutions.Wpf.DragDrop;
using MetadataExtractor;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using PointlessWaymarks.CommonTools;
using PointlessWaymarks.FeatureIntersectionTags.Models;
using PointlessWaymarks.LlamaAspects;
using PointlessWaymarks.SpatialTools;
using PointlessWaymarks.WpfCommon;
using PointlessWaymarks.WpfCommon.FileMetadataDisplay;
using PointlessWaymarks.WpfCommon.ProgramUpdateMessage;
using PointlessWaymarks.WpfCommon.Status;
using PointlessWaymarks.WpfCommon.Utility;
using PointlessWaymarks.WpfCommon.WebViewVirtualDomain;
using PointlessWaymarks.WpfCommon.WpfHtml;
using PointlessWaymarks.WpfCommon.WpfHtmlResources;
using Serilog;
using XmpCore;

namespace PointlessWaymarks.MetadataDisplayGui;

/// <summary>
///     Interaction logic for MainWindow.xaml
/// </summary>
[NotifyPropertyChanged]
[GenerateStatusCommands]
public partial class MainWindow : IWebViewMessenger, IDropTarget
{
    private readonly string _currentDateVersion;

    public MainWindow()
    {
        InitializeComponent();

        JotServices.Tracker.Track(this);

        if (Width < 900) Width = 900;
        if (Height < 650) Height = 650;

        WindowInitialPositionHelpers.EnsureWindowIsVisible(this);

        var versionInfo =
            ProgramInfoTools.StandardAppInformationString(AppContext.BaseDirectory,
                "Pointless Waymarks Metadata Display Beta");

        InfoTitle = versionInfo.humanTitleString;
        WindowTitle = InfoTitle;

        _currentDateVersion = versionInfo.dateVersion;

        DataContext = this;

        StatusContext = new StatusControlContext();

        WindowStatus = new WindowIconStatus();

        UpdateMessageContext = new ProgramUpdateMessageContext(StatusContext);

        BuildCommands();

        FromWebView = new WorkQueue<FromWebViewMessage>
        {
            Processor = ProcessFromWebView
        };

        ToWebView = new WorkQueue<ToWebViewRequest>(true);
    }

    public string FfprobeExe { get; set; } = string.Empty;
    public string? FileDirectory { get; set; }
    public string? FileName { get; set; }
    public string? FilePathAndName { get; set; }
    public WorkQueue<FromWebViewMessage> FromWebView { get; set; }
    public string InfoTitle { get; set; }
    public SpatialBounds? MapBounds { get; set; }
    public StatusControlContext StatusContext { get; set; }
    public WorkQueue<ToWebViewRequest> ToWebView { get; set; }
    public ProgramUpdateMessageContext UpdateMessageContext { get; set; }
    public WindowIconStatus WindowStatus { get; set; }
    public string WindowTitle { get; set; }

    public async Task CheckForProgramUpdate(string currentDateVersion)
    {
        Log.Information(
            $"Program Update Check - Current Version {currentDateVersion}, Installer Directory {MetadataDisplayGuiSettingTools.ReadSettings().ProgramUpdateDirectory}");

        if (string.IsNullOrEmpty(currentDateVersion)) return;

        var (dateString, setupFile) = await ProgramInfoTools.LatestInstaller(
            MetadataDisplayGuiSettingTools.ReadSettings().ProgramUpdateDirectory,
            "PointlessWaymarks-MetadataDisplayGui-Setup");

        Log.Information(
            $"Program Update Check - Current Version {currentDateVersion}, Installer Directory {MetadataDisplayGuiSettingTools.ReadSettings().ProgramUpdateDirectory}, Installer Date Found {dateString ?? string.Empty}, Setup File Found {setupFile ?? string.Empty}");

        await UpdateMessageContext.LoadData(currentDateVersion, dateString, setupFile);
    }

    [NonBlockingCommand]
    public async Task CopyFilenameToClipboard()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (string.IsNullOrWhiteSpace(FilePathAndName))
        {
            await StatusContext.ToastWarning("No File?");
            return;
        }

        await ThreadSwitcher.ResumeForegroundAsync();

        Clipboard.SetText(FilePathAndName);

        await StatusContext.ToastSuccess($"To Clipboard {FilePathAndName}");
    }

    public static async Task<MainWindow> CreateInstance(string? fileName = null, string? ffProbeExe = null)
    {
        await ThreadSwitcher.ResumeForegroundAsync();

        var metadataWindow = new MainWindow();

        if (!string.IsNullOrWhiteSpace(ffProbeExe))
            metadataWindow.FfprobeExe = ffProbeExe;

        metadataWindow.StatusContext.RunBlockingTask(async () =>
        {
            await metadataWindow.CheckForProgramUpdate(metadataWindow._currentDateVersion);
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                await metadataWindow.LoadData(fileName);
            }
            else
            {
                await metadataWindow.SetupDocumentWithMinimalCss(
                    """<h3 style="text-align:center;">Drop a file on the window or application icon to view its metadata.</h3>""",
                    metadataWindow.InfoTitle ?? "File Metadata Report");
            }
        });

        return metadataWindow;
    }

    public static async Task<FileBuilder> FileMetadataMapDocument(string title, MetadataLocation location,
        string styleBlock = "",
        string javascript = "",
        string serializedMapIcons = "", string bodyContent = "")
    {
        var htmlString = $$$"""
                            <!doctype html>
                            <html lang=en>
                            <head>
                              <meta http-equiv="X-UA-Compatible" content="IE=edge" />
                              <meta charset="utf-8">
                              <meta name="viewport" content="width=device-width, initial-scale=1.0">
                              <title>{{{HtmlEncoder.Default.Encode(title)}}}</title>
                              <link rel="stylesheet" href="https://[[VirtualDomain]]/pure.css" />
                              <link rel="stylesheet" href="https://[[VirtualDomain]]/leaflet.css" />
                              <link rel="stylesheet" href="https://[[VirtualDomain]]/leaflet.awesome-svg-markers.css">
                              
                            <script src="https://[[VirtualDomain]]/leaflet.js"></script>
                            <script src="https://[[VirtualDomain]]/esri-leaflet.js"></script>
                            <script src="https://[[VirtualDomain]]/chart.umd.min.js"></script>

                            <script src="https://[[VirtualDomain]]/leaflet.awesome-svg-markers.js"></script>
                            <script src="https://[[VirtualDomain]]/localMapCommon.js"></script>
                                {{{(string.IsNullOrWhiteSpace(styleBlock) ? string.Empty : """<link rel="stylesheet" href="https://[[VirtualDomain]]/customStyle.css" />""")}}}
                                {{{(string.IsNullOrWhiteSpace(javascript) ? string.Empty : """<script src="https://[[VirtualDomain]]/customScript.js"></script>""")}}}
                            </head>
                            <body onload="initialDocumentLoad();">
                                 <h3 style="text-align: center;">{{{HtmlEncoder.Default.Encode(title)}}}</h3>
                                 <div id="mainMap" class="leaflet-container leaflet-retina leaflet-fade-anim leaflet-grab leaflet-touch-drag"
                                    style="height: 70vh;"></div>
                                 <div style="text-align: center; margin-top: 20px;">
                                    <a href="https://www.peakfinder.org/?lat={{{location.Latitude}}}&lng={{{location.Longitude}}}{{{(location.PhotoDirection is null ? "" : $"&azi={location.PhotoDirection.Value:F0}")}}}" target="_blank" class="pure-button pure-button-primary">Open in Peakfinder Web</a>
                                    <a href="https://www.google.com/maps/search/?api=1&query={{{location.Latitude}}},{{{location.Longitude}}}" target="_blank" class="pure-button pure-button-primary">Google Maps</a>
                                    <a href="http://www.openstreetmap.org/?mlat={{{location.Latitude}}}&mlon={{{location.Longitude}}}&zoom=13&layers=C" target="_blank" class="pure-button pure-button-primary">OpenStreetMap</a>
                                </div>
                                 {{{bodyContent}}}
                            </body>
                            </html>
                            """;

        var initialWebFilesMessage = new FileBuilder();

        if (!string.IsNullOrWhiteSpace(styleBlock))
            initialWebFilesMessage.Create.Add(new FileBuilderCreate("customStyle.css", styleBlock));
        if (!string.IsNullOrWhiteSpace(javascript))
            initialWebFilesMessage.Create.Add(new FileBuilderCreate("customScript.js", javascript));

        initialWebFilesMessage.Create.Add(new FileBuilderCreate("pure.css", await HtmlTools.PureCssAsString()));
        initialWebFilesMessage.Create.Add(new FileBuilderCreate("leaflet.css",
            WpfHtmlResourcesHelper.LeafletCss()));
        initialWebFilesMessage.Create.Add(new FileBuilderCreate("leaflet.js",
            WpfHtmlResourcesHelper.LeafletJs()));
        initialWebFilesMessage.Create.Add(new FileBuilderCreate("esri-leaflet.js",
            WpfHtmlResourcesHelper.ErsiLeafletJs()));
        initialWebFilesMessage.Create.Add(new FileBuilderCreate("chart.umd.min.js",
            WpfHtmlResourcesHelper.ChartJs()));

        initialWebFilesMessage.Create.Add(new FileBuilderCreate("localMapCommon.js",
            WpfHtmlResourcesHelper.LocalMapCommonJs()));
        initialWebFilesMessage.Create.AddRange(WpfHtmlResourcesHelper.AwesomeMapSvgMarkers());
        initialWebFilesMessage.Create.AddRange(WpfHtmlResourcesHelper.LeafletImages());

        initialWebFilesMessage.Create.Add(new FileBuilderCreate("pwMapSvgIcons.json", serializedMapIcons));

        initialWebFilesMessage.Create.Add(new FileBuilderCreate("Index.html", htmlString, true));

        return initialWebFilesMessage;
    }

    void IDropTarget.DragOver(IDropInfo dropInfo)
    {
        dropInfo.Effects = DragDropEffects.Copy;
    }

    void IDropTarget.Drop(IDropInfo dropInfo)
    {
        var files = DragAndDropFilesHelper.DroppedFiles(dropInfo, FileLocationTools.TempStorageDirectory(), true);
        var supportedFile = files.FirstOrDefault(x =>
            FileMetadataTools.ExifToolWriteSupportedExtensions.Contains(Path.GetExtension(x),
                StringComparer.OrdinalIgnoreCase)) ?? files.FirstOrDefault(File.Exists);

        if (!string.IsNullOrWhiteSpace(supportedFile))
        {
            StatusContext.RunBlockingTask(async () => await LoadData(supportedFile));
            return;
        }

        string textToProcess;
        var data = dropInfo.Data;
        textToProcess = data switch
        {
            string stringData => stringData,
            IDataObject dataObject when dataObject.GetDataPresent(DataFormats.UnicodeText) =>
                dataObject.GetData(DataFormats.UnicodeText) as string ?? string.Empty,
            IDataObject dataObject when dataObject.GetDataPresent(DataFormats.Text) =>
                dataObject.GetData(DataFormats.Text) as string ?? string.Empty,
            IDataObject dataObject when dataObject.GetDataPresent(DataFormats.StringFormat) =>
                dataObject.GetData(DataFormats.StringFormat) as string ?? string.Empty,
            _ => data?.ToString() ?? string.Empty
        };

        if (string.IsNullOrWhiteSpace(textToProcess)) return;

        var possiblePath = textToProcess.Trim().Trim('"', '\'');
        if (File.Exists(possiblePath))
        {
            StatusContext.RunBlockingTask(async () => await LoadData(possiblePath));
            return;
        }

        var lines = textToProcess.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var loopLine in lines)
        {
            var lineCandidate = loopLine.Trim().Trim('"', '\'');
            if (File.Exists(lineCandidate))
            {
                StatusContext.RunBlockingTask(async () => await LoadData(lineCandidate));
                return;
            }
        }
    }

    public static async Task ImageFileMetadataReports(List<FileInfo?> files, string? ffProbeExe,
        StatusControlContext statusContext)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (files.Count < 1)
        {
            await statusContext.ToastError("Nothing Selected?");
            return;
        }

        var errorCount = 0;

        await ThreadSwitcher.ResumeForegroundAsync();

        foreach (var loopFile in files)
            try
            {
                if (loopFile is null || !loopFile.Exists)
                {
                    Log.ForContext("loopFile", loopFile.SafeObjectDump())
                        .Error("Invalid File Detected");
                    errorCount++;
                    continue;
                }

                var metadataWindow = await CreateInstance(loopFile.FullName,
                    ffProbeExe);
                await metadataWindow.PositionWindowAndShowOnUiThread();
            }
            catch (Exception e)
            {
                Log.ForContext("loopFile", loopFile.SafeObjectDump())
                    .Error(e, "Metadata Report Error");
                errorCount++;
            }

        await ThreadSwitcher.ResumeBackgroundAsync();

        if (errorCount > 0)
            await statusContext.ToastWarning($"Metadata Report Completed with {errorCount} errors.");
        else
            await statusContext.ToastSuccess("Metadata Report Completed.");
    }

    public async Task LoadData(string fileName)
    {
        WindowTitle = string.IsNullOrWhiteSpace(fileName)
            ? (InfoTitle ?? "File Metadata Report")
            : $"Metadata - {fileName}";

        if (string.IsNullOrWhiteSpace(fileName))
        {
            await this.SetupDocumentWithMinimalCss(
                """<h3 style="text-align:center;">Drop a file on the window or application icon to view its metadata.</h3>""",
                "File Metadata Report");
            return;
        }

        if (!File.Exists(fileName))
        {
            await this.SetupDocumentWithMinimalCss(
                $"""<h3 style="text-align:center;">Error - File '{fileName}' does not exist or could not be accessed?</h3>""",
                "File Metadata Report");
            return;
        }

        var file = new FileInfo(fileName);

        FilePathAndName = file.FullName;
        FileName = file.Name;
        FileDirectory = file.DirectoryName;

        var fileMetadataHtml = await FileMetadataReport.AllFileMetadataToHtml(file, FfprobeExe);

        MetadataLocation location;

        if (file.Extension.Equals(".xmp", StringComparison.OrdinalIgnoreCase))
        {
            // For standalone .xmp files, parse with XmpMetaFactory and extract location
            IXmpMeta xmp;
            await using (var stream = File.OpenRead(file.FullName))
            {
                xmp = XmpMetaFactory.Parse(stream);
            }

            location = await FileMetadataXmpSidecarTools.LocationFromXmpSidecar(xmp, true,
                StatusContext.ProgressTracker());
        }
        else
        {
            // For image/video files, use ImageMetadataReader
            var metadataDirectories = ImageMetadataReader.ReadMetadata(fileName);
            var createdOn = await FileMetadataEmbeddedTools.CreatedOnLocalAndUtc(metadataDirectories);
            location = await FileMetadataEmbeddedTools.LocationFromExif(metadataDirectories, true,
                createdOn.createdOnUtc ?? createdOn.createdOnLocal ?? DateTime.Now, StatusContext.ProgressTracker());

            // If the main file has no location, check for a sidecar .xmp
            if (!location.HasValidLocation())
            {
                var sidecar = FileMetadataReport.FindXmpSidecar(file);
                if (sidecar != null)
                {
                    IXmpMeta sidecarXmp;
                    await using (var stream = File.OpenRead(sidecar.FullName))
                    {
                        sidecarXmp = XmpMetaFactory.Parse(stream);
                    }

                    var sidecarLocation = await FileMetadataXmpSidecarTools.LocationFromXmpSidecar(sidecarXmp, true,
                        StatusContext.ProgressTracker());
                    if (sidecarLocation.HasValidLocation()) location = sidecarLocation;
                }
            }
        }

        if (location.HasValidLocation())
        {
            var initialWebFilesMessage =
                await FileMetadataMapDocument($"Metadata: {fileName}", location, bodyContent: fileMetadataHtml);

            var startingPoint = PointTools.Wgs84Point(location.Longitude!.Value, location.Latitude!.Value, 0);

            var featureCollection = new FeatureCollection
            {
                new Feature(startingPoint,
                    new AttributesTable(new Dictionary<string, object>()))
            };

            if (location.PhotoDirection is not null)
            {
                var endingPoint = PointTools.ProjectCoordinate(startingPoint, location.PhotoDirection.Value, 100000);
                var line = new LineString([startingPoint.Coordinate, endingPoint.Coordinate]);

                featureCollection.Add(new Feature(line, new AttributesTable(new Dictionary<string, object>())));
            }

            var bounds = SpatialBounds.FromCoordinates(location.Latitude.Value, location.Longitude.Value, 0)
                .ExpandToMinimumMeters(3000);
            var mapJson = await MapJson.NewMapFeatureCollectionDtoSerialized([featureCollection], bounds);

            ToWebView.Enqueue(initialWebFilesMessage);

            ToWebView.Enqueue(NavigateTo.CreateRequest("Index.html", true));

            var calTopoApiKey = (await IntersectSettingTools.ReadSettings(null)).CalTopoApiKey;

            ToWebView.Enqueue(ExecuteJavaScript.CreateRequest(
                $"initialMapLoad({location.Latitude.Value}, {location.Longitude.Value}, '{calTopoApiKey}')",
                true));

            ToWebView.Enqueue(new JsonData
            {
                Json = mapJson
            });
        }
        else
        {
            await this.SetupDocumentWithMinimalCss(fileMetadataHtml, "File Metadata Report");
        }
    }

    private void MainWindow_OnClosing(object? sender, CancelEventArgs e)
    {
        Log.CloseAndFlush();
    }

    private async Task MapMessageReceived(string mapMessage)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        try
        {
            var parsedJson = JsonNode.Parse(mapMessage);
            if (parsedJson == null) return;

            var messageType = parsedJson["messageType"]?.ToString() ?? string.Empty;

            if (messageType == "mapBoundsChange")
            {
                var boundsNode = parsedJson["bounds"];
                if (boundsNode == null) return;

                var northEastNode = boundsNode["_northEast"];
                var southWestNode = boundsNode["_southWest"];

                if (northEastNode == null || southWestNode == null) return;

                var northEastLat = northEastNode["lat"]?.GetValue<double>();
                var northEastLng = northEastNode["lng"]?.GetValue<double>();
                var southWestLat = southWestNode["lat"]?.GetValue<double>();
                var southWestLng = southWestNode["lng"]?.GetValue<double>();

                if (northEastLat.HasValue && northEastLng.HasValue && southWestLat.HasValue && southWestLng.HasValue)
                    MapBounds = new SpatialBounds(
                        northEastLat.Value,
                        northEastLng.Value,
                        southWestLat.Value,
                        southWestLng.Value);
            }
        }
        catch (Exception e)
        {
            await StatusContext.ToastError($"Error parsing map message: {e.Message}");
        }
    }

    [NonBlockingCommand]
    public async Task OpenDirectory()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (string.IsNullOrWhiteSpace(FilePathAndName))
        {
            await StatusContext.ToastWarning("No File?");
            return;
        }

        if (!File.Exists(FilePathAndName))
        {
            await StatusContext.ToastError($"File '{FilePathAndName}' does not exist?");
            return;
        }

        await ProcessHelpers.OpenExplorerWindowForFile(FilePathAndName);
    }

    [NonBlockingCommand]
    public async Task OpenFile()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (string.IsNullOrWhiteSpace(FilePathAndName))
        {
            await StatusContext.ToastWarning("No File?");
            return;
        }

        if (!File.Exists(FilePathAndName))
        {
            await StatusContext.ToastError($"File '{FilePathAndName}' does not exist?");
            return;
        }

        await ThreadSwitcher.ResumeForegroundAsync();

        ProcessTools.Open(FilePathAndName);
    }

    public Task ProcessFromWebView(FromWebViewMessage args)
    {
        if (!string.IsNullOrWhiteSpace(args.Message))
            StatusContext.RunFireAndForgetNonBlockingTask(async () => await MapMessageReceived(args.Message));
        return Task.CompletedTask;
    }

    public async Task ShowMarker(double markerLatitude, double markerLongitude)
    {
        var featureCollection = new FeatureCollection
        {
            new Feature(
                PointTools.Wgs84Point(markerLongitude, markerLatitude, 0),
                new AttributesTable(new Dictionary<string, object>()))
        };

        var bounds = SpatialBounds.FromCoordinates(markerLatitude, markerLongitude, 0).ExpandToMinimumMeters(1000);
        var mapJson = await MapJson.NewMapFeatureCollectionDtoSerialized([featureCollection], bounds);

        ToWebView.Enqueue(new JsonData
        {
            Json = mapJson
        });
    }

    public async Task ShowMarkerAndBearing(double markerLatitude, double markerLongitude, double bearing,
        double distanceInMeters)
    {
        var startingPoint = PointTools.Wgs84Point(markerLongitude, markerLatitude, 0);
        var endingPoint = PointTools.ProjectCoordinate(startingPoint, bearing, distanceInMeters);
        var line = new LineString([startingPoint.Coordinate, endingPoint.Coordinate]);

        var featureCollection = new FeatureCollection
        {
            new Feature(startingPoint,
                new AttributesTable(new Dictionary<string, object>())),
            new Feature(line, new AttributesTable(new Dictionary<string, object>()))
        };

        var bounds = SpatialBounds.FromCoordinates(markerLatitude, markerLongitude, 0)
            .ExpandToMinimumMeters(Math.Min(3000, distanceInMeters));
        var mapJson = await MapJson.NewMapFeatureCollectionDtoSerialized([featureCollection], bounds);

        ToWebView.Enqueue(new JsonData
        {
            Json = mapJson
        });
    }
}