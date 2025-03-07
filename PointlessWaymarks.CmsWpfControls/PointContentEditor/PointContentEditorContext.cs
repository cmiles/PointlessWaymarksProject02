using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using NetTopologySuite.Features;
using Omu.ValueInjecter;
using PointlessWaymarks.CmsData;
using PointlessWaymarks.CmsData.BracketCodes;
using PointlessWaymarks.CmsData.ContentGeneration;
using PointlessWaymarks.CmsData.Database;
using PointlessWaymarks.CmsData.Database.Models;
using PointlessWaymarks.CmsData.Spatial;
using PointlessWaymarks.CmsWpfControls.BodyContentEditor;
using PointlessWaymarks.CmsWpfControls.ContentIdViewer;
using PointlessWaymarks.CmsWpfControls.ContentSiteFeedAndIsDraft;
using PointlessWaymarks.CmsWpfControls.CreatedAndUpdatedByAndOnDisplay;
using PointlessWaymarks.CmsWpfControls.DataEntry;
using PointlessWaymarks.CmsWpfControls.DropdownDataEntry;
using PointlessWaymarks.CmsWpfControls.GeoSearch;
using PointlessWaymarks.CmsWpfControls.HelpDisplay;
using PointlessWaymarks.CmsWpfControls.PointDetailEditor;
using PointlessWaymarks.CmsWpfControls.TagsEditor;
using PointlessWaymarks.CmsWpfControls.TitleSummarySlugFolderEditor;
using PointlessWaymarks.CmsWpfControls.UpdateNotesEditor;
using PointlessWaymarks.CmsWpfControls.Utility;
using PointlessWaymarks.CmsWpfControls.WpfCmsHtml;
using PointlessWaymarks.CommonTools;
using PointlessWaymarks.FeatureIntersectionTags;
using PointlessWaymarks.LlamaAspects;
using PointlessWaymarks.SpatialTools;
using PointlessWaymarks.WpfCommon;
using PointlessWaymarks.WpfCommon.BoolDataEntry;
using PointlessWaymarks.WpfCommon.ChangesAndValidation;
using PointlessWaymarks.WpfCommon.ConversionDataEntry;
using PointlessWaymarks.WpfCommon.MarkdownDisplay;
using PointlessWaymarks.WpfCommon.Status;
using PointlessWaymarks.WpfCommon.StringDataEntry;
using PointlessWaymarks.WpfCommon.WebViewVirtualDomain;
using PointlessWaymarks.WpfCommon.WpfHtml;
using Point = NetTopologySuite.Geometries.Point;

namespace PointlessWaymarks.CmsWpfControls.PointContentEditor;

[NotifyPropertyChanged]
[GenerateStatusCommands]
public partial class PointContentEditorContext : IHasChanges, ICheckForChangesAndValidation,
    IHasValidationIssues, IWebViewMessenger
{
    public EventHandler? RequestContentEditorWindowClose;

    private PointContentEditorContext(StatusControlContext statusContext, PointContent pointContent,
        string serializedMapIcons, GeoSearchContext factoryLocationSearchContext)
    {
        StatusContext = statusContext;

        BuildCommands();

        DbEntry = pointContent;

        FromWebView = new WorkQueue<FromWebViewMessage>
        {
            Processor = ProcessFromWebView
        };

        ToWebView = new WorkQueue<ToWebViewRequest>(true);

        MapPreviewNavigationManager = MapCmsJson.LocalActionNavigation(StatusContext);

        this.SetupCmsLeafletPointChooserMapHtmlAndJs("Map", UserSettingsSingleton.CurrentSettings().LatitudeDefault,
            UserSettingsSingleton.CurrentSettings().LongitudeDefault, serializedMapIcons,
            UserSettingsSingleton.CurrentSettings().CalTopoApiKey, UserSettingsSingleton.CurrentSettings().BingApiKey);

        PropertyChanged += OnPropertyChanged;

        LocationSearchContext = factoryLocationSearchContext;

        LocationSearchContext.LocationSelected += (sender, args) =>
        {
            var centerData = new MapJsonCoordinateDto(args.Latitude, args.Longitude, "CenterCoordinateRequest");

            var serializedData = JsonSerializer.Serialize(centerData);

            ToWebView.Enqueue(new JsonData { Json = serializedData });
        };
    }

    public BodyContentEditorContext? BodyContent { get; set; }
    public bool BroadcastLatLongChange { get; set; } = true;
    public ContentIdViewerControlContext? ContentId { get; set; }
    public CreatedAndUpdatedByAndOnDisplayContext? CreatedUpdatedDisplay { get; set; }
    public PointContent DbEntry { get; set; }
    public List<Guid> DisplayedContentGuids { get; set; } = [];
    public ConversionDataEntryContext<double?>? ElevationEntry { get; set; }
    public HelpDisplayContext? HelpContext { get; set; }
    public ConversionDataEntryContext<double>? LatitudeEntry { get; set; }
    public GeoSearchContext LocationSearchContext { get; set; }
    public ConversionDataEntryContext<double>? LongitudeEntry { get; set; }
    public ContentSiteFeedAndIsDraftContext? MainSiteFeed { get; set; }
    public SpatialBounds? MapBounds { get; set; } = null;
    public ContentMapIconContext MapIconEntry { get; set; }
    public string? MapIconSvg { get; set; }
    public StringDataEntryContext? MapLabelContentEntry { get; set; }
    public ContentMapMarkerColorContext MapMarkerColorEntry { get; set; }
    public Action<Uri, string> MapPreviewNavigationManager { get; set; }
    public PointDetailListContext? PointDetails { get; set; }
    public BoolDataEntryContext ShowInSearch { get; set; }
    public StatusControlContext StatusContext { get; set; }
    public TagsEditorContext? TagEdit { get; set; }
    public TitleSummarySlugEditorContext? TitleSummarySlugFolder { get; set; }
    public UpdateNotesEditorContext? UpdateNotes { get; set; }

    public void CheckForChangesAndValidationIssues()
    {
        HasChanges = PropertyScanners.ChildPropertiesHaveChanges(this);
        HasValidationIssues = PropertyScanners.ChildPropertiesHaveValidationIssues(this);
    }

    public bool HasChanges { get; set; }
    public bool HasValidationIssues { get; set; }
    public WorkQueue<FromWebViewMessage> FromWebView { get; set; }
    public WorkQueue<ToWebViewRequest> ToWebView { get; set; }

    [BlockingCommand]
    private async Task AddFeatureIntersectTags()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var featureToCheck = await FeatureFromPoint();
        if (featureToCheck == null)
        {
            await StatusContext.ToastError("No valid Lat/Long to check?");
            return;
        }

        if (string.IsNullOrWhiteSpace(UserSettingsSingleton.CurrentSettings().FeatureIntersectionTagSettingsFile))
        {
            await StatusContext.ToastError(
                "To use this feature the Feature Intersect Settings file must be set in the Site Settings...");
            return;
        }

        var possibleTags = featureToCheck.IntersectionTags(
            UserSettingsSingleton.CurrentSettings().FeatureIntersectionTagSettingsFile, CancellationToken.None,
            StatusContext.ProgressTracker());

        if (!possibleTags.Any())
        {
            await StatusContext.ToastWarning("No tags found...");
            return;
        }

        TagEdit!.Tags =
            $"{TagEdit.Tags}{(string.IsNullOrWhiteSpace(TagEdit.Tags) ? "" : ",")}{string.Join(",", possibleTags)}";
    }

    [NonBlockingCommand]
    public async Task CenterMapOnSelectedLocation()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var centerData =
            new MapJsonCoordinateDto(LatitudeEntry.UserValue, LongitudeEntry.UserValue, "CenterCoordinateRequest");

        var serializedData = JsonSerializer.Serialize(centerData);

        ToWebView.Enqueue(JsonData.CreateRequest(serializedData));
    }

    [NonBlockingCommand]
    public async Task ClearSearchInBounds()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (MapBounds == null)
        {
            await StatusContext.ToastError("No Map Bounds?");
            return;
        }

        var searchResultIds = (await (await Db.Context()).ContentFromBoundingBox(MapBounds)).Select(x => x.ContentId)
            .Cast<Guid>().ToList();

        if (!searchResultIds.Any())
        {
            await StatusContext.ToastWarning("No Items Found in Bounds?");
            return;
        }

        DisplayedContentGuids = DisplayedContentGuids.Where(x => !searchResultIds.Contains(x)).ToList();

        ToWebView.Enqueue(
            JsonData.CreateRequest(
                JsonSerializer.Serialize(new MapJsonFeatureListDto(searchResultIds, "RemoveFeatures"))));
    }

    public static async Task<PointContentEditorContext> CreateInstance(StatusControlContext? statusContext,
        PointContent? pointContent)
    {
        var factoryStatusContext = await StatusControlContext.CreateInstance(statusContext);

        ThreadSwitcher.ResumeBackgroundAsync();

        var factoryMapIcons = await MapIconGenerator.SerializedMapIcons();
        var factoryLocationSearchContext = await GeoSearchContext.CreateInstance(factoryStatusContext);

        var newControl = new PointContentEditorContext(factoryStatusContext,
            NewContentModels.InitializePointContent(pointContent), factoryMapIcons, factoryLocationSearchContext);
        await newControl.LoadData(pointContent);
        return newControl;
    }

    private PointContent CurrentStateToPointContent()
    {
        var newEntry = PointContent.CreateInstance();

        if (DbEntry.Id > 0)
        {
            newEntry.ContentId = DbEntry.ContentId;
            newEntry.CreatedOn = DbEntry.CreatedOn;
            newEntry.LastUpdatedOn = DateTime.Now;
            newEntry.LastUpdatedBy = CreatedUpdatedDisplay!.UpdatedByEntry.UserValue.TrimNullToEmpty();
        }

        newEntry.Folder = TitleSummarySlugFolder!.FolderEntry.UserValue.TrimNullToEmpty();
        newEntry.Slug = TitleSummarySlugFolder.SlugEntry.UserValue.TrimNullToEmpty();
        newEntry.Summary = TitleSummarySlugFolder.SummaryEntry.UserValue.TrimNullToEmpty();
        newEntry.ShowInMainSiteFeed = MainSiteFeed!.ShowInMainSiteFeedEntry.UserValue;
        newEntry.FeedOn = MainSiteFeed.FeedOnEntry.UserValue;
        newEntry.IsDraft = MainSiteFeed.IsDraftEntry.UserValue;
        newEntry.ShowInSearch = ShowInSearch.UserValue;
        newEntry.Tags = TagEdit!.TagListString();
        newEntry.Title = TitleSummarySlugFolder.TitleEntry.UserValue.TrimNullToEmpty();
        newEntry.CreatedBy = CreatedUpdatedDisplay!.CreatedByEntry.UserValue.TrimNullToEmpty();
        newEntry.UpdateNotes = UpdateNotes!.UserValue.TrimNullToEmpty();
        newEntry.UpdateNotesFormat = UpdateNotes.UpdateNotesFormat.SelectedContentFormatAsString;
        newEntry.BodyContent = BodyContent!.UserValue.TrimNullToEmpty();
        newEntry.BodyContentFormat = BodyContent.BodyContentFormat.SelectedContentFormatAsString;
        newEntry.MapLabel = MapLabelContentEntry!.UserValue.TrimNullToEmpty();
        newEntry.MapIconName = MapIconEntry!.UserValue.TrimNullToEmpty();
        newEntry.MapMarkerColor = MapMarkerColorEntry!.UserValue.TrimNullToEmpty();

        newEntry.Latitude = LatitudeEntry!.UserValue;
        newEntry.Longitude = LongitudeEntry!.UserValue;
        newEntry.Elevation = ElevationEntry!.UserValue;

        return newEntry;
    }

    private PointContentDto CurrentStateToPointContentDto()
    {
        var toReturn = new PointContentDto();
        var currentPoint = CurrentStateToPointContent();
        toReturn.InjectFrom(currentPoint);
        toReturn.PointDetails = PointDetails!.CurrentStateToPointDetailsList();
        toReturn.PointDetails.ForEach(x => x.PointContentId = toReturn.ContentId);
        return toReturn;
    }


    [BlockingCommand]
    public async Task ExtractNewLinks()
    {
        await LinkExtraction.ExtractNewAndShowLinkContentEditors(
            $"{BodyContent!.UserValue} {UpdateNotes!.UserValue}",
            StatusContext.ProgressTracker());
    }

    public Task<IFeature?> FeatureFromPoint()
    {
        if (LatitudeEntry!.HasValidationIssues || LongitudeEntry!.HasValidationIssues)
            return Task.FromResult((IFeature?)null);

        if (ElevationEntry!.UserValue is null)
            return Task.FromResult((IFeature?)new Feature(
                new Point(LongitudeEntry.UserValue, LatitudeEntry.UserValue),
                new AttributesTable()));
        return Task.FromResult((IFeature?)new Feature(
            new Point(LongitudeEntry.UserValue, LatitudeEntry.UserValue, ElevationEntry.UserValue.Value),
            new AttributesTable()));
    }

    [BlockingCommand]
    public async Task GetElevation()
    {
        if (LatitudeEntry!.HasValidationIssues || LongitudeEntry!.HasValidationIssues)
        {
            await StatusContext.ToastError("Lat Long is not valid");
            return;
        }

        var possibleElevation =
            await ElevationGuiHelper.GetElevation(LatitudeEntry.UserValue, LongitudeEntry.UserValue, StatusContext);

        if (possibleElevation != null) ElevationEntry!.UserText = possibleElevation.Value.MetersToFeet().ToString("N0");
    }

    private void LatitudeLongitudeChangeBroadcast()
    {
        if (BroadcastLatLongChange && !LatitudeEntry!.HasValidationIssues && !LongitudeEntry!.HasValidationIssues)
        {
            var pointLocationData = new MapJsonCoordinateDto(LatitudeEntry.UserValue, LongitudeEntry.UserValue,
                "MoveUserLocationSelection");

            var serializedData = JsonSerializer.Serialize(pointLocationData);

            ToWebView.Enqueue(JsonData.CreateRequest(serializedData));
        }
    }

    [BlockingCommand]
    private async Task LinkToClipboard()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (DbEntry.Id < 1)
        {
            await StatusContext.ToastError("Sorry - please save before getting link...");
            return;
        }

        var linkString = BracketCodePointLinks.Create(DbEntry);

        await ThreadSwitcher.ResumeForegroundAsync();

        Clipboard.SetText(linkString);

        await StatusContext.ToastSuccess($"To Clipboard: {linkString}");
    }

    public async Task LoadData(PointContent? toLoad)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        DbEntry = NewContentModels.InitializePointContent(toLoad);

        TitleSummarySlugFolder =
            await TitleSummarySlugEditorContext.CreateInstance(StatusContext, DbEntry, null, null, null);
        CreatedUpdatedDisplay = await CreatedAndUpdatedByAndOnDisplayContext.CreateInstance(StatusContext, DbEntry);
        MainSiteFeed = await ContentSiteFeedAndIsDraftContext.CreateInstance(StatusContext, DbEntry);
        ShowInSearch = await BoolDataEntryTypes.CreateInstanceForShowInSearch(DbEntry, true);
        ContentId = await ContentIdViewerControlContext.CreateInstance(StatusContext, DbEntry);
        UpdateNotes = await UpdateNotesEditorContext.CreateInstance(StatusContext, DbEntry);
        TagEdit = await TagsEditorContext.CreateInstance(StatusContext, DbEntry);
        BodyContent = await BodyContentEditorContext.CreateInstance(StatusContext, DbEntry);

        MapLabelContentEntry = StringDataEntryContext.CreateInstance();
        MapLabelContentEntry.Title = "Map Label";
        MapLabelContentEntry.HelpText =
            "This text will be used to identify the point on a map. A very short string is likely best - this will not scale with the map...";
        MapLabelContentEntry.ReferenceValue = DbEntry.MapLabel ?? string.Empty;
        MapLabelContentEntry.UserValue = StringTools.NullToEmptyTrim(DbEntry.MapLabel);

        MapIconEntry = await ContentMapIconContext.CreateInstance(StatusContext, DbEntry);
        MapIconEntry.ReferenceValue = DbEntry.MapIconName ?? string.Empty;
        MapIconEntry.UserValue = StringTools.NullToEmptyTrim(DbEntry.MapIconName);
        MapIconEntry.PropertyChanged += (_, _) =>
        {
            if (MapIconEntry.HasValidationIssues || string.IsNullOrWhiteSpace(MapIconEntry.UserValue))
                MapIconSvg = string.Empty;
            StatusContext.RunFireAndForgetNonBlockingTask(async () =>
            {
                MapIconSvg = await Db.MapIconSvgFromMapIconName(MapIconEntry.UserValue);
            });
        };

        MapMarkerColorEntry = await ContentMapMarkerColorContext.CreateInstance(StatusContext, DbEntry);
        MapMarkerColorEntry.ReferenceValue = DbEntry.MapMarkerColor ?? string.Empty;
        MapMarkerColorEntry.UserValue = StringTools.NullToEmptyTrim(DbEntry.MapMarkerColor);

        ElevationEntry =
            await ConversionDataEntryContext<double?>.CreateInstance(
                ConversionDataEntryHelpers.DoubleNullableConversion);
        ElevationEntry.ValidationFunctions = [CommonContentValidation.ElevationValidation];
        ElevationEntry.ComparisonFunction = (o, u) => (o == null && u == null) || o.IsApproximatelyEqualTo(u, .001);
        ElevationEntry.Title = "Elevation (feet)";
        ElevationEntry.HelpText = "Elevation in Feet";
        ElevationEntry.ReferenceValue = DbEntry.Elevation;
        ElevationEntry.UserText = DbEntry.Elevation?.ToString("N0") ?? string.Empty;

        LatitudeEntry =
            await ConversionDataEntryContext<double>.CreateInstance(ConversionDataEntryHelpers.DoubleConversion);
        LatitudeEntry.ValidationFunctions = [CommonContentValidation.LatitudeValidation];
        LatitudeEntry.ComparisonFunction = (o, u) => o.IsApproximatelyEqualTo(u, .000001);
        LatitudeEntry.Title = "Latitude";
        LatitudeEntry.HelpText = "In DDD.DDDDDD°";
        LatitudeEntry.ReferenceValue = DbEntry.Latitude;
        LatitudeEntry.UserText = DbEntry.Latitude.ToString("F6");
        LatitudeEntry.PropertyChanged += (_, args) =>
        {
            if (string.IsNullOrWhiteSpace(args.PropertyName)) return;
            if (args.PropertyName == nameof(LatitudeEntry.UserValue)) LatitudeLongitudeChangeBroadcast();
        };

        LongitudeEntry =
            await ConversionDataEntryContext<double>.CreateInstance(ConversionDataEntryHelpers.DoubleConversion);
        LongitudeEntry.ValidationFunctions = [CommonContentValidation.LongitudeValidation];
        LongitudeEntry.ComparisonFunction = (o, u) => o.IsApproximatelyEqualTo(u, .000001);
        LongitudeEntry.Title = "Longitude";
        LongitudeEntry.HelpText = "In DDD.DDDDDD°";
        LongitudeEntry.ReferenceValue = DbEntry.Longitude;
        LongitudeEntry.UserText = DbEntry.Longitude.ToString("F6");
        LongitudeEntry.PropertyChanged += (_, args) =>
        {
            if (string.IsNullOrWhiteSpace(args.PropertyName)) return;
            if (args.PropertyName == nameof(LongitudeEntry.UserValue)) LatitudeLongitudeChangeBroadcast();
        };

        PointDetails = await PointDetailListContext.CreateInstance(StatusContext, DbEntry);

        HelpContext = new HelpDisplayContext([CommonFields.TitleSlugFolderSummary, BracketCodeHelpMarkdown.HelpBlock]);

        LatitudeLongitudeChangeBroadcast();

        var db = await Db.Context();
        var searchBounds = SpatialBounds.FromCoordinates(LatitudeEntry.UserValue, LongitudeEntry.UserValue, 5000);

        var closeByFeatures = (await db.ContentFromBoundingBox(searchBounds, [Db.ContentTypeDisplayStringForPoint]))
            .Where(x => x.ContentId != DbEntry.ContentId && !DisplayedContentGuids.Contains(x.ContentId)).ToList();
        var mapInformation = await MapCmsJson.ProcessContentToMapInformation(closeByFeatures.Cast<object>().ToList(), false);
        DisplayedContentGuids =
            DisplayedContentGuids.Union(closeByFeatures.Select(x => x.ContentId).Cast<Guid>()).ToList();

        ToWebView.Enqueue(
            FileBuilder.CreateRequest(mapInformation.fileCopyList.Select(x => new FileBuilderCopy(x, false)).ToList(),
                []));
        ToWebView.Enqueue(JsonData.CreateRequest(await MapCmsJson.NewMapFeatureCollectionDtoSerialized(
            mapInformation.featureList,
            mapInformation.bounds.ExpandToMinimumMeters(1000), "NewFeatureCollection")));

        PropertyScanners.SubscribeToChildHasChangesAndHasValidationIssues(this, CheckForChangesAndValidationIssues);
    }

    public async Task MapMessageReceived(string json)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var parsedJson = JsonNode.Parse(json);

        if (parsedJson == null) return;

        var messageType = parsedJson["messageType"]?.ToString() ?? string.Empty;

        if (messageType.Equals("userSelectedLatitudeLongitudeChanged",
                StringComparison.InvariantCultureIgnoreCase))
        {
            var latitude = parsedJson["latitude"]?.GetValue<double>();
            var longitude = parsedJson["longitude"]?.GetValue<double>();

            if (latitude == null || longitude == null) return;

            BroadcastLatLongChange = false;

            LatitudeEntry!.UserText = latitude.Value.ToString("F6");
            LongitudeEntry!.UserText = longitude.Value.ToString("F6");

            BroadcastLatLongChange = true;
        }

        if (messageType == "mapBoundsChange")
        {
            MapBounds = new SpatialBounds(parsedJson["bounds"]["_northEast"]["lat"].GetValue<double>(),
                parsedJson["bounds"]["_northEast"]["lng"].GetValue<double>(),
                parsedJson["bounds"]["_southWest"]["lat"].GetValue<double>(),
                parsedJson["bounds"]["_southWest"]["lng"].GetValue<double>());
            return;
        }
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.PropertyName)) return;

        if (!e.PropertyName.Contains("HasChanges") && !e.PropertyName.Contains("Validation"))
            CheckForChangesAndValidationIssues();
    }

    public Task ProcessFromWebView(FromWebViewMessage args)
    {
        if (!string.IsNullOrWhiteSpace(args.Message))
            StatusContext.RunFireAndForgetNonBlockingTask(async () => await MapMessageReceived(args.Message));
        return Task.CompletedTask;
    }

    [BlockingCommand]
    public async Task Save()
    {
        await SaveAndGenerateHtml(false);
    }

    [BlockingCommand]
    public async Task SaveAndClose()
    {
        await SaveAndGenerateHtml(true);
    }

    public async Task SaveAndGenerateHtml(bool closeAfterSave)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var (generationReturn, newContent) = await PointGenerator.SaveAndGenerateHtml(CurrentStateToPointContentDto(),
            null, StatusContext.ProgressTracker());

        if (generationReturn.HasError || newContent == null)
        {
            await StatusContext.ShowMessageWithOkButton("Problem Saving and Generating Html",
                generationReturn.GenerationNote);
            return;
        }

        await LoadData(Db.PointContentDtoToPointContentAndDetails(newContent).content);

        if (closeAfterSave)
        {
            await ThreadSwitcher.ResumeForegroundAsync();
            RequestContentEditorWindowClose?.Invoke(this, EventArgs.Empty);
        }
    }

    [NonBlockingCommand]
    public async Task SearchGeoJsonInBounds()
    {
        await SearchInBounds([Db.ContentTypeDisplayStringForGeoJson]);
    }

    public async Task SearchInBounds(List<string> searchContentTypes)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (MapBounds == null)
        {
            await StatusContext.ToastError("No Map Bounds?");
            return;
        }

        var searchResult = (await (await Db.Context()).ContentFromBoundingBox(MapBounds, searchContentTypes)).Where(x =>
            x.ContentId != DbEntry.ContentId && !DisplayedContentGuids.Contains(x.ContentId)).ToList();

        if (!searchResult.Any())
        {
            await StatusContext.ToastWarning("No New Items Found");
            return;
        }

        await StatusContext.ToastSuccess(
            $"Added {searchResult.Count} Item{(searchResult.Count > 1 ? "s" : string.Empty)}");

        var mapInformation = await MapCmsJson.ProcessContentToMapInformation(searchResult.Cast<object>().ToList(), false);
        DisplayedContentGuids =
            DisplayedContentGuids.Union(searchResult.Select(x => x.ContentId).Cast<Guid>()).ToList();

        ToWebView.Enqueue(
            FileBuilder.CreateRequest(mapInformation.fileCopyList.Select(x => new FileBuilderCopy(x, false)).ToList(),
                []));
        ToWebView.Enqueue(JsonData.CreateRequest(await MapCmsJson.NewMapFeatureCollectionDtoSerialized(
            mapInformation.featureList,
            mapInformation.bounds.ExpandToMinimumMeters(1000), "AddFeatureCollection")));
    }

    [NonBlockingCommand]
    public async Task SearchLinesInBounds()
    {
        await SearchInBounds([Db.ContentTypeDisplayStringForLine]);
    }

    [NonBlockingCommand]
    public async Task SearchPhotosInBounds()
    {
        await SearchInBounds([Db.ContentTypeDisplayStringForPhoto]);
    }

    [NonBlockingCommand]
    public async Task SearchPointsInBounds()
    {
        await SearchInBounds([Db.ContentTypeDisplayStringForPoint]);
    }

    [BlockingCommand]
    private async Task ViewOnSite()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (DbEntry.Id < 1)
        {
            await StatusContext.ToastError("Please save the content first...");
            return;
        }

        var settings = UserSettingsSingleton.CurrentSettings();

        var url = $"{settings.PointPageUrl(DbEntry)}";

        var ps = new ProcessStartInfo(url) { UseShellExecute = true, Verb = "open" };
        Process.Start(ps);
    }
}