using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using Ookii.Dialogs.Wpf;
using PointlessWaymarks.CmsData;
using PointlessWaymarks.CmsData.BracketCodes;
using PointlessWaymarks.CmsData.CommonHtml;
using PointlessWaymarks.CmsData.ContentGeneration;
using PointlessWaymarks.CmsData.Database;
using PointlessWaymarks.CmsData.Database.Models;
using PointlessWaymarks.CmsWpfControls.BodyContentEditor;
using PointlessWaymarks.CmsWpfControls.ContentIdViewer;
using PointlessWaymarks.CmsWpfControls.ContentSiteFeedAndIsDraft;
using PointlessWaymarks.CmsWpfControls.CreatedAndUpdatedByAndOnDisplay;
using PointlessWaymarks.CmsWpfControls.DataEntry;
using PointlessWaymarks.CmsWpfControls.HelpDisplay;
using PointlessWaymarks.CmsWpfControls.ImageContentEditor;
using PointlessWaymarks.CmsWpfControls.OptionalLocationEntry;
using PointlessWaymarks.CmsWpfControls.PhotoContentEditor;
using PointlessWaymarks.WpfCommon.Elevation;
using PointlessWaymarks.CmsWpfControls.TagsEditor;
using PointlessWaymarks.CmsWpfControls.TitleSummarySlugFolderEditor;
using PointlessWaymarks.CmsWpfControls.UpdateNotesEditor;
using PointlessWaymarks.CmsWpfControls.Utility;
using PointlessWaymarks.CommonTools;
using PointlessWaymarks.LlamaAspects;
using PointlessWaymarks.SpatialTools;
using PointlessWaymarks.WpfCommon;
using PointlessWaymarks.WpfCommon.BoolDataEntry;
using PointlessWaymarks.WpfCommon.ChangesAndValidation;
using PointlessWaymarks.WpfCommon.ConversionDataEntry;
using PointlessWaymarks.WpfCommon.FileMetadataDisplay;
using PointlessWaymarks.WpfCommon.MarkdownDisplay;
using PointlessWaymarks.WpfCommon.SimpleMediaPlayer;
using PointlessWaymarks.WpfCommon.Status;
using PointlessWaymarks.WpfCommon.StringDataEntry;
using PointlessWaymarks.WpfCommon.Utility;
using Serilog;

namespace PointlessWaymarks.CmsWpfControls.VideoContentEditor;

[NotifyPropertyChanged]
[GenerateStatusCommands]
public partial class VideoContentEditorContext : IHasChanges, IHasValidationIssues,
    ICheckForChangesAndValidation
{
    public EventHandler? RequestContentEditorWindowClose;

    private VideoContentEditorContext(StatusControlContext statusContext, VideoContent dbEntry)
    {
        StatusContext = statusContext;

        BuildCommands();

        VideoContext = new SimpleMediaPlayerContext();

        DbEntry = dbEntry;

        PropertyChanged += OnPropertyChanged;
    }

    public BodyContentEditorContext? BodyContent { get; set; }
    public ContentIdViewerControlContext? ContentId { get; set; }
    public CreatedAndUpdatedByAndOnDisplayContext? CreatedUpdatedDisplay { get; set; }
    public VideoContent DbEntry { get; set; }
    public HelpDisplayContext? HelpContext { get; set; }
    public FileInfo? InitialVideo { get; set; }
    public StringDataEntryContext? LicenseEntry { get; set; }
    public FileInfo? LoadedFile { get; set; }
    public ImageContentEditorWindow? MainImageExternalEditorWindow { get; set; }
    public ContentSiteFeedAndIsDraftContext? MainSiteFeed { get; set; }
    public OptionalLocationEntryContext? OptionalLocationEntry { get; set; }
    public FileInfo? SelectedFile { get; set; }
    public bool SelectedFileHasPathOrNameChanges { get; set; }
    public bool SelectedFileHasValidationIssues { get; set; }
    public bool SelectedFileNameHasInvalidCharacters { get; set; }
    public string SelectedFileValidationMessage { get; set; } = string.Empty;
    public BoolDataEntryContext ShowInSearch { get; set; }
    public StatusControlContext StatusContext { get; set; }
    public TagsEditorContext? TagEdit { get; set; }
    public TitleSummarySlugEditorContext? TitleSummarySlugFolder { get; set; }
    public UpdateNotesEditorContext? UpdateNotes { get; set; }
    public ConversionDataEntryContext<Guid?>? UserMainPictureEntry { get; set; }
    public IContentCommon? UserMainPictureEntryContent { get; set; }
    public string? UserMainPictureEntrySmallImageUrl { get; set; }
    public bool VideoCanBeReEncodedWithFfmpeg { get; set; }
    public SimpleMediaPlayerContext? VideoContext { get; set; }
    public StringDataEntryContext? VideoCreatedByEntry { get; set; }
    public ConversionDataEntryContext<DateTime>? VideoCreatedOnEntry { get; set; }
    public ConversionDataEntryContext<DateTime?>? VideoCreatedOnUtcEntry { get; set; }


    public string VideoEditorHelpText =>
        @"
### Video Content

Interesting books, dissertations, academic papers, maps, meeting notes, articles, memos, reports, etc. are available on a wide variety of subjects - but over years, decades, of time resources can easily 'disappear' from the internet... Websites are no longer available, agencies delete documents they are no longer legally required to retain, older versions of a document are not kept when a newer version comes out, departments shut down, funding runs out...

Video Content is intended to allow the creation of a 'library' of Videos that you can tag, search, share and retain. The Video you choose for Video Content will be copied to the site just like an image or photo would be.

With any file you have on your site it is your responsibility to know if it is legally acceptable to have the file on the site - like any content in this CMS you should only enter it into the CMS if you want it 'publicly' available on your site, there are options that allow some content to be more discrete - but NO options that allow you to fully hide content.

Notes:
 - No Video Previews are automatically generated - you will need to add any images/previews/etc. manually to the Body Content
 - To help when working with PDFs the program can extract pages of a PDF as Image Content for quick/easy use in the Body Content - details:
   - To use this functionality pdftocairo must be available on your computer and the location of pdftocairo must be set in the Settings
   - On windows the easiest way to install pdftocairo is to install MiKTeX - [Getting MiKTeX - MiKTeX.org](https://miktex.org/download)
   - The page you specify to generate an image is the page that the PDF Viewer you are using is showing (rather than the 'content page number' printed at the bottom of a page) - for example with a book in PDF format to get an image of the 'cover' the page number is '1'
 - The Video Content page can contain a link to download the file - but it is not appropriate to offer all content for download, use the 'Show Public Download Link' to turn on/off the download link. This setting will impact the behaviour of the 'filedownloadlink' bracket code - if 'Show Public Download Link' is unchecked a filedownloadlink bracket code will become a link to the Video Content Page (rather than a download link for the content).
 - Regardless of the 'Show Public Download Link' the file will be copied to the site - if you have a sensitive document that should not be copied beyond your computer consider just creating Post Content for it - the Video Content type is only useful for content where you want the Video to be 'with' the site.
 - If appropriate consider including links to the original source in the Body Content
 - If what you are writing about is a 'file' but you don't want/need to store the file itself on your site you should probably just create a Post (or other content type like and Image) - use Video Content when you want to store the file. 
";

    public bool VideoIsNotHtmlVideoEmbedFriendly { get; set; }


    public void CheckForChangesAndValidationIssues()
    {
        HasChanges = PropertyScanners.ChildPropertiesHaveChanges(this) || SelectedFileHasPathOrNameChanges ||
                     DbEntry.MainPicture != CurrentMainPicture();
        HasValidationIssues = PropertyScanners.ChildPropertiesHaveValidationIssues(this) ||
                              SelectedFileHasValidationIssues;
    }

    public bool HasChanges { get; set; }
    public bool HasValidationIssues { get; set; }

    [BlockingCommand]
    private async Task AddFeatureIntersectTags()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var possibleTags = await OptionalLocationEntry!.GetFeatureIntersectTagsWithUiAlerts();

        if (possibleTags.Any())
            TagEdit!.Tags =
                $"{TagEdit.Tags}{(string.IsNullOrWhiteSpace(TagEdit.Tags) ? "" : ",")}{string.Join(",", possibleTags)}";
    }

    [BlockingCommand]
    public async Task AutoCleanRenameSelectedFile()
    {
        await FileHelpers.TryAutoCleanRenameSelectedFile(SelectedFile, StatusContext, x => SelectedFile = x);
    }

    [BlockingCommand]
    public async Task AutoRenameSelectedFileBasedOnTitle()
    {
        await FileHelpers.TryAutoRenameSelectedFile(SelectedFile, TitleSummarySlugFolder!.TitleEntry.UserValue,
            StatusContext, x => SelectedFile = x);
    }

    public async Task ChooseFile(bool loadMetadata)
    {
        await ThreadSwitcher.ResumeForegroundAsync();

        StatusContext.Progress("Starting image load.");

        var dialog = new VistaOpenFileDialog { Filter = "supported formats (*.mp4;*.webm,*.ogg)|*.mp4;*.webm;*.ogg" };

        if (!(dialog.ShowDialog() ?? false)) return;

        var newFile = new FileInfo(dialog.FileName);

        if (!newFile.Exists)
        {
            await StatusContext.ToastError("Video doesn't exist?");
            return;
        }

        if (!VideoGenerator.VideoFileTypeIsSupported(newFile))
        {
            await StatusContext.ToastError("Only JPEGs are supported...");
            return;
        }

        await ThreadSwitcher.ResumeBackgroundAsync();

        SelectedFile = newFile;

        StatusContext.Progress($"Video load - {SelectedFile.FullName} ");

        if (!loadMetadata) return;

        var (generationReturn, metadata) =
            await PhotoGenerator.PhotoMetadataFromFile(SelectedFile, false, StatusContext.ProgressTracker());

        if (generationReturn.HasError)
        {
            await StatusContext.ShowMessageWithOkButton("Video Metadata Load Issue", generationReturn.GenerationNote);
            return;
        }

        if (metadata == null)
        {
            await StatusContext.ShowMessageWithOkButton("Video Metadata in Null?", generationReturn.GenerationNote);
            return;
        }

        VideoMetadataToCurrentContent(metadata);
    }

    [BlockingCommand]
    public async Task ChooseFileAndFillMetadata()
    {
        await ChooseFile(true);
    }

    [BlockingCommand]
    public async Task ChooseFileWithoutMetadataLoad()
    {
        await ChooseFile(false);
    }

    public static async Task<VideoContentEditorContext> CreateInstance(StatusControlContext? statusContext,
        FileInfo? initialVideo = null)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var factoryStatusContext = await StatusControlContext.CreateInstance(statusContext);

        var toLoad = VideoContent.CreateInstance();

        var newContext = new VideoContentEditorContext(factoryStatusContext, toLoad)
            { StatusContext = { BlockUi = true } };

        if (initialVideo is { Exists: true }) newContext.InitialVideo = initialVideo;

        await newContext.LoadData(toLoad);

        newContext.StatusContext.BlockUi = false;

        return newContext;
    }

    public static async Task<VideoContentEditorContext> CreateInstance(StatusControlContext? statusContext,
        VideoContent? initialContent, bool skipMetadataLoadFromVideo = false)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var factoryStatusContext = await StatusControlContext.CreateInstance(statusContext);

        var newControl =
            new VideoContentEditorContext(factoryStatusContext,
                NewContentModels.InitializeVideoContent(initialContent));
        await newControl.LoadData(initialContent, skipMetadataLoadFromVideo: skipMetadataLoadFromVideo);
        return newControl;
    }

    public Guid? CurrentMainPicture()
    {
        if (UserMainPictureEntry is { HasValidationIssues: false, UserValue: not null })
            return UserMainPictureEntry.UserValue;

        return BracketCodeCommon.PhotoOrImageCodeFirstIdInContent(BodyContent?.UserValue, null).Result;
    }

    public VideoContent CurrentStateToVideoContent()
    {
        var newEntry = VideoContent.CreateInstance();

        newEntry.ContentId = DbEntry.ContentId;
        newEntry.CreatedOn = DbEntry.CreatedOn;

        if (DbEntry.LastUpdatedOn is not null) newEntry.LastUpdatedOn = DbEntry.LastUpdatedOn;
        if (DbEntry.LastUpdatedBy is not null) newEntry.LastUpdatedBy = DbEntry.LastUpdatedBy;

        if (DbEntry.Id > 0)
        {
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
        newEntry.OriginalFileName = SelectedFile!.Name;
        newEntry.UserMainPicture = UserMainPictureEntry!.UserValue;
        newEntry.License = LicenseEntry!.UserValue.TrimNullToEmpty();
        newEntry.VideoCreatedBy = VideoCreatedByEntry!.UserValue.TrimNullToEmpty();
        newEntry.VideoCreatedOn = VideoCreatedOnEntry!.UserValue;
        newEntry.VideoCreatedOnUtc = VideoCreatedOnUtcEntry!.UserValue;
        newEntry.Latitude = OptionalLocationEntry!.LatitudeEntry!.UserValue;
        newEntry.Longitude = OptionalLocationEntry.LongitudeEntry!.UserValue;
        newEntry.Elevation = OptionalLocationEntry.ElevationEntry!.UserValue;
        newEntry.ShowLocation = OptionalLocationEntry.ShowLocationEntry!.UserValue;

        return newEntry;
    }

    [NonBlockingCommand]
    public async Task EditUserMainPicture()
    {
        if (UserMainPictureEntryContent == null)
        {
            await StatusContext.ToastWarning("No Picture to Edit?");
            return;
        }

        await SetUserMainPicture();

        if (UserMainPictureEntryContent is PhotoContent photoToEdit)
        {
            var window =
                await PhotoContentEditorWindow.CreateInstance(photoToEdit);
            await window.PositionWindowAndShowOnUiThread();
            return;
        }

        if (UserMainPictureEntryContent is ImageContent imageToEdit)
        {
            var window =
                await ImageContentEditorWindow.CreateInstance(imageToEdit);
            await window.PositionWindowAndShowOnUiThread();
            return;
        }

        await StatusContext.ToastWarning("Didn't find the expected Photo/Image to edit?");
    }

    [BlockingCommand]
    public async Task ExtractNewLinks()
    {
        await LinkExtraction.ExtractNewAndShowLinkContentEditors(
            $"{BodyContent!.UserValue} {UpdateNotes!.UserValue}",
            StatusContext.ProgressTracker());
    }

    [NonBlockingCommand]
    private async Task LinkToClipboard()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (DbEntry.Id < 1)
        {
            await StatusContext.ToastError("Sorry - please save before getting link...");
            return;
        }

        var linkString = BracketCodeVideoEmbed.Create(DbEntry);

        await ThreadSwitcher.ResumeForegroundAsync();

        Clipboard.SetText(linkString);

        await StatusContext.ToastSuccess($"To Clipboard: {linkString}");
    }

    private async Task LoadData(VideoContent? toLoad, bool skipMediaDirectoryCheck = false,
        bool skipMetadataLoadFromVideo = false)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        StatusContext.Progress("Loading Data...");

        DbEntry = NewContentModels.InitializeVideoContent(toLoad);

        TitleSummarySlugFolder = await TitleSummarySlugEditorContext.CreateInstance(StatusContext, DbEntry,
            "To File Name",
            AutoRenameSelectedFileBasedOnTitleCommand,
            x => SelectedFile != null && !Path.GetFileNameWithoutExtension(SelectedFile.Name)
                .Equals(SlugTagTools.CreateSlug(false, x.TitleEntry.UserValue), StringComparison.OrdinalIgnoreCase));
        MainSiteFeed = await ContentSiteFeedAndIsDraftContext.CreateInstance(StatusContext, DbEntry);
        ShowInSearch = await BoolDataEntryTypes.CreateInstanceForShowInSearch(DbEntry, true);
        CreatedUpdatedDisplay = await CreatedAndUpdatedByAndOnDisplayContext.CreateInstance(StatusContext, DbEntry);

        LicenseEntry = StringDataEntryContext.CreateInstance();
        LicenseEntry.Title = "License";
        LicenseEntry.HelpText = "The Video's License";
        LicenseEntry.ReferenceValue = DbEntry.License ?? string.Empty;
        LicenseEntry.UserValue = DbEntry.License.TrimNullToEmpty();

        VideoCreatedByEntry = StringDataEntryContext.CreateInstance();
        VideoCreatedByEntry.Title = "Video Created By";
        VideoCreatedByEntry.HelpText = "Who created the video";
        VideoCreatedByEntry.ReferenceValue = DbEntry.VideoCreatedBy ?? string.Empty;
        VideoCreatedByEntry.UserValue = DbEntry.VideoCreatedBy.TrimNullToEmpty();

        VideoCreatedOnEntry =
            await ConversionDataEntryContext<DateTime>.CreateInstance(ConversionDataEntryHelpers.DateTimeConversion);
        VideoCreatedOnEntry.Title = "Video Created On";
        VideoCreatedOnEntry.HelpText = "Date and, optionally, Time the Video was Created";
        VideoCreatedOnEntry.ReferenceValue = DbEntry.VideoCreatedOn;
        VideoCreatedOnEntry.UserText = DbEntry.VideoCreatedOn.ToString("MM/dd/yyyy h:mm:ss tt");

        VideoCreatedOnUtcEntry =
            await ConversionDataEntryContext<DateTime?>.CreateInstance(ConversionDataEntryHelpers
                .DateTimeNullableConversion);
        VideoCreatedOnUtcEntry.Title = "Video Created On UTC Date/Time";
        VideoCreatedOnUtcEntry.HelpText =
            "UTC Date and Time the Video was Created - the UTC Date Time is not displayed but is used to compare the Video's Date Time to data like GPX Files/Lines.";
        VideoCreatedOnUtcEntry.ReferenceValue = DbEntry.VideoCreatedOnUtc;
        VideoCreatedOnUtcEntry.UserText = DbEntry.VideoCreatedOnUtc?.ToString("MM/dd/yyyy h:mm:ss tt") ?? string.Empty;

        ContentId = await ContentIdViewerControlContext.CreateInstance(StatusContext, DbEntry);
        UpdateNotes = await UpdateNotesEditorContext.CreateInstance(StatusContext, DbEntry);
        TagEdit = await TagsEditorContext.CreateInstance(StatusContext, DbEntry);
        BodyContent = await BodyContentEditorContext.CreateInstance(StatusContext, DbEntry);
        UserMainPictureEntry =
            await ConversionDataEntryContext<Guid?>.CreateInstance(ConversionDataEntryTypes
                .PhotoOrImageGuidNullableAndBracketCodeConversion);
        UserMainPictureEntry.ValidationFunctions = [CommonContentValidation.ValidateUserMainPicture];
        UserMainPictureEntry.ReferenceValue = DbEntry.UserMainPicture;
        UserMainPictureEntry.UserText = DbEntry.UserMainPicture.ToString() ?? string.Empty;
        UserMainPictureEntry.Title = "Link Image";
        UserMainPictureEntry.HelpText =
            "Putting a Photo or Image ContentId here will cause that image to be used as the 'link' image for the file - very useful when the content is embedded and you don't have a photo or image in the Body Content.";
        UserMainPictureEntry.PropertyChanged += UserMainPictureEntryOnPropertyChanged;
        await SetUserMainPicture();

        OptionalLocationEntry = await OptionalLocationEntryContext.CreateInstance(StatusContext, DbEntry);

        HelpContext = new HelpDisplayContext([
            VideoEditorHelpText, CommonFields.TitleSlugFolderSummary, BracketCodeHelpMarkdown.HelpBlock
        ]);

        if (!skipMediaDirectoryCheck && !string.IsNullOrWhiteSpace(DbEntry.OriginalFileName) && DbEntry.Id > 0)
        {
            await FileManagement.CheckVideoOriginalFileIsInMediaAndContentDirectories(DbEntry);

            var archiveVideo = new FileInfo(Path.Combine(
                UserSettingsSingleton.CurrentSettings().LocalMediaArchiveVideoDirectory().FullName,
                DbEntry.OriginalFileName));

            var fileContentDirectory = UserSettingsSingleton.CurrentSettings().LocalSiteVideoContentDirectory(DbEntry);

            var contentVideo = new FileInfo(Path.Combine(fileContentDirectory.FullName, DbEntry.OriginalFileName));

            if (!archiveVideo.Exists && contentVideo.Exists)
            {
                await FileManagement.WriteSelectedVideoContentFileToMediaArchive(contentVideo);
                archiveVideo.Refresh();
            }

            if (archiveVideo.Exists)
            {
                LoadedFile = archiveVideo;
                SelectedFile = archiveVideo;
            }
            else
            {
                await StatusContext.ShowMessageWithOkButton("Missing Video",
                    $"There is an original file listed for this entry - {DbEntry.OriginalFileName} -" +
                    $" but it was not found in the expected locations of {archiveVideo.FullName} or {contentVideo.FullName} - " +
                    "this will cause an error and prevent you from saving. You can re-load the file or " +
                    "maybe your media directory moved unexpectedly and you could close this editor " +
                    "and restore it (or change it in settings) before continuing?");
            }
        }

        if (DbEntry.Id < 1 && InitialVideo is { Exists: true } && VideoGenerator.VideoFileTypeIsSupported(InitialVideo))
        {
            SelectedFile = InitialVideo;
            InitialVideo = null;

            if (!skipMetadataLoadFromVideo)
            {
                var (generationReturn, metadataReturn) =
                    await PhotoGenerator.PhotoMetadataFromFile(SelectedFile, false, StatusContext.ProgressTracker());
                if (!generationReturn.HasError && metadataReturn != null) VideoMetadataToCurrentContent(metadataReturn);
            }
        }

        if (string.IsNullOrWhiteSpace(TitleSummarySlugFolder.SummaryEntry.UserValue) && SelectedFile != null)
            TitleSummarySlugFolder.TitleEntry.UserValue = Regex.Replace(
                Path.GetFileNameWithoutExtension(SelectedFile.Name).Replace("-", " ").Replace("_", " ")
                    .CamelCaseToSpacedString(), @"\s+", " ");

        await SelectedFileChanged();
    }

    private void MainImageExternalContextSaved(object? sender, EventArgs e)
    {
        if (sender is ImageContentEditorContext imageContext)
        {
            StatusContext.RunNonBlockingTask(async () =>
                await TryAddUserMainPicture(imageContext.DbEntry.ContentId));

            if (MainImageExternalEditorWindow?.ImageEditor != null)
                MainImageExternalEditorWindow.ImageEditor.Saved -= MainImageExternalContextSaved;

            MainImageExternalEditorWindowCleanup();
        }
    }

    public void MainImageExternalEditorWindowCleanup()
    {
        if (MainImageExternalEditorWindow?.ImageEditor == null) return;

        try
        {
            MainImageExternalEditorWindow.Closed -= OnMainImageExternalEditorWindowOnClosed;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        try
        {
            MainImageExternalEditorWindow.ImageEditor.Saved -= MainImageExternalContextSaved;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    private void OnMainImageExternalEditorWindowOnClosed(object? sender, EventArgs args)
    {
        MainImageExternalEditorWindowCleanup();
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.PropertyName)) return;

        if (!e.PropertyName.Contains("HasChanges") && !e.PropertyName.Contains("Validation"))
            CheckForChangesAndValidationIssues();

        if (e.PropertyName == nameof(SelectedFile)) StatusContext.RunFireAndForgetNonBlockingTask(SelectedFileChanged);
    }

    [BlockingCommand]
    private async Task OpenSelectedFile()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (SelectedFile is not { Exists: true, Directory.Exists: true })
        {
            await StatusContext.ToastError("No Selected Video or Selected Video no longer exists?");
            return;
        }

        await ThreadSwitcher.ResumeForegroundAsync();

        var ps = new ProcessStartInfo(SelectedFile.FullName) { UseShellExecute = true, Verb = "open" };
        Process.Start(ps);
    }

    [BlockingCommand]
    private async Task OpenSelectedFileDirectory()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (SelectedFile is not { Exists: true, Directory.Exists: true })
        {
            await StatusContext.ToastWarning("No Selected Video or Selected Video no longer exists?");
            return;
        }

        await ProcessHelpers.OpenExplorerWindowForFile(SelectedFile.FullName);
    }

    [BlockingCommand]
    private async Task PointFromLocation()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (DbEntry.Id < 1)
        {
            await StatusContext.ToastError("The Photo must be saved before creating a Point.");
            return;
        }

        if (OptionalLocationEntry!.LatitudeEntry!.UserValue == null ||
            OptionalLocationEntry.LongitudeEntry!.UserValue == null)
        {
            await StatusContext.ToastError("Latitude or Longitude is missing?");
            return;
        }

        var latitudeValidation =
            await SpatialValueValidations.LatitudeValidation(OptionalLocationEntry.LatitudeEntry.UserValue.Value);
        var longitudeValidation =
            await SpatialValueValidations.LongitudeValidation(OptionalLocationEntry.LongitudeEntry.UserValue.Value);

        if (!latitudeValidation.Valid || !longitudeValidation.Valid)
        {
            await StatusContext.ToastError("Latitude/Longitude is not valid?");
            return;
        }

        var frozenNow = DateTime.Now;

        var newPartialPoint = PointContent.CreateInstance();

        newPartialPoint.CreatedOn = frozenNow;
        newPartialPoint.FeedOn = frozenNow;
        newPartialPoint.BodyContent = BracketCodeVideoEmbed.Create(DbEntry);
        newPartialPoint.Title = $"Point From {TitleSummarySlugFolder!.TitleEntry.UserValue}";
        newPartialPoint.Tags = TagEdit!.TagListString();
        newPartialPoint.Slug = SlugTagTools.CreateSlug(true, newPartialPoint.Title);
        newPartialPoint.Latitude = OptionalLocationEntry.LatitudeEntry.UserValue.Value;
        newPartialPoint.Longitude = OptionalLocationEntry.LongitudeEntry.UserValue.Value;
        newPartialPoint.Elevation = OptionalLocationEntry.ElevationEntry!.UserValue;

        var pointWindow = await PointContentEditorWindow.CreateInstance(newPartialPoint);

        await pointWindow.PositionWindowAndShowOnUiThread();
    }

    [BlockingCommand]
    public async Task ReEncodeWithFfmpeg()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (SelectedFile is not { Exists: true })
        {
            await StatusContext.ToastError("No video file selected to re-encode.");
            return;
        }

        if (!VideoCanBeReEncodedWithFfmpeg)
        {
            await StatusContext.ToastError("This video cannot be automatically re-encoded with ffmpeg.");
            return;
        }

        var settings = UserSettingsSingleton.CurrentSettings();

        if (!settings.FfmpegAndFfprobeExist())
        {
            await StatusContext.ToastError("ffmpeg and ffprobe must be configured in settings to re-encode videos.");
            return;
        }

        var outputExtension = SelectedFile.Extension.ToLowerInvariant();

        var outputFile = new FileInfo(Path.Combine(
            SelectedFile.Directory!.FullName,
            $"{Path.GetFileNameWithoutExtension(SelectedFile.Name)}-reencoding-temp{outputExtension}"));

        outputFile = UniqueFileTools.UniqueFile(outputFile.Directory!, outputFile.Name)!;

        var ffmpegExe = settings.FfmpegExe();

        // Build ffmpeg arguments for HTML5-compatible encoding
        var ffmpegArgs = outputExtension switch
        {
            ".webm" =>
                $"-i \"{SelectedFile.FullName}\" -c:v libvpx-vp9 -lossless 1 -c:a libopus -b:a 256k -y \"{outputFile.FullName}\"",
            ".ogg" => $"-i \"{SelectedFile.FullName}\" -c:v libtheora -q:v 10 -c:a flac -y \"{outputFile.FullName}\"",
            _ =>
                $"-i \"{SelectedFile.FullName}\" -c:v libx264 -preset slow -crf 18 -profile:v high -level 4.2 -pix_fmt yuv420p -c:a aac -b:a 192k -movflags +faststart -y \"{outputFile.FullName}\""
        };

        try
        {
            StatusContext.Progress("Re-encoding video with ffmpeg...");

            var processStartInfo = new ProcessStartInfo
            {
                FileName = ffmpegExe,
                Arguments = ffmpegArgs,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process();

            process.StartInfo = processStartInfo;
            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            process.OutputDataReceived += (_, args) =>
            {
                if (!string.IsNullOrEmpty(args.Data)) outputBuilder.AppendLine(args.Data);
            };

            process.ErrorDataReceived += (_, args) =>
            {
                if (!string.IsNullOrEmpty(args.Data))
                {
                    errorBuilder.AppendLine(args.Data);
                    // ffmpeg writes progress to stderr, update status
                    if (args.Data.Contains("frame=") || args.Data.Contains("time="))
                        StatusContext.Progress($"Re-encoding: {args.Data.Trim()}");
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                Log.ForContext("SelectedFile", SelectedFile.FullName)
                    .ForContext("OutputFile", outputFile.FullName)
                    .ForContext("ExitCode", process.ExitCode)
                    .ForContext("StdOut", outputBuilder.ToString())
                    .ForContext("StdError", errorBuilder.ToString())
                    .Error("ffmpeg re-encoding failed");

                await StatusContext.ShowMessageWithOkButton("Re-encoding Failed",
                    $"ffmpeg failed to re-encode the video. Exit code: {process.ExitCode}\n\nCheck logs for details.");
                return;
            }

            outputFile.Refresh();

            if (!outputFile.Exists)
            {
                await StatusContext.ToastError("Re-encoding appeared to succeed but output file was not found.");
                return;
            }

            // Create backup filename for the original
            var originalFileName = SelectedFile.Name;
            var backupFileName =
                $"{Path.GetFileNameWithoutExtension(originalFileName)}_reencoding_backup{Path.GetExtension(originalFileName)}";
            var backupFile = new FileInfo(Path.Combine(SelectedFile.Directory!.FullName, backupFileName));

            // Ensure backup filename is unique
            backupFile = UniqueFileTools.UniqueFile(backupFile.Directory!, backupFile.Name)!;

            // Rename original file to back up
            File.Move(SelectedFile.FullName, backupFile.FullName);

            Log.ForContext("OriginalFile", SelectedFile.FullName)
                .ForContext("BackupFile", backupFile.FullName)
                .Information("Renamed original video file to backup");

            // Rename the re-encoded file to the original filename
            var finalFile = new FileInfo(Path.Combine(SelectedFile.Directory!.FullName, originalFileName));
            File.Move(outputFile.FullName, finalFile.FullName);

            Log.ForContext("TempFile", outputFile.FullName)
                .ForContext("FinalFile", finalFile.FullName)
                .Information("Renamed re-encoded video to original filename");

            await StatusContext.ToastSuccess($"Video re-encoded successfully. Original saved as {backupFile.Name}");

            // Update the selected file to point to the newly encoded file with original name
            SelectedFile = finalFile;
        }
        catch (Exception ex)
        {
            Log.ForContext("SelectedFile", SelectedFile.FullName)
                .ForContext("OutputFile", outputFile.FullName)
                .Error(ex, "Exception while re-encoding video with ffmpeg");

            await StatusContext.ShowMessageWithOkButton("Re-encoding Error",
                $"An error occurred while re-encoding: {ex.Message}");
        }
    }

    [BlockingCommand]
    public async Task RenameSelectedFile()
    {
        await FileHelpers.RenameSelectedFile(SelectedFile, StatusContext, x => SelectedFile = x);
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

    [BlockingCommand]
    private async Task SaveAndExtractImageFromMp4()
    {
        if (SelectedFile is not { Exists: true } || !SelectedFile.Extension.ToUpperInvariant().Contains("MP4"))
        {
            await StatusContext.ToastError("Please selected a valid mp4 file");
            return;
        }

        var (generationReturn, fileContent) = await VideoGenerator.SaveAndGenerateHtml(CurrentStateToVideoContent(),
            SelectedFile, null, StatusContext.ProgressTracker());

        if (generationReturn.HasError)
        {
            await StatusContext.ShowMessageWithOkButton("Trouble Saving",
                $"Trouble saving - you must be able to save before extracting a frame - {generationReturn.GenerationNote}");
            return;
        }

        await LoadData(fileContent);

        var autoSaveResult =
            await ImageExtractionHelpers.VideoFrameToImageAutoSave(StatusContext, DbEntry,
                VideoContext!.VideoPositionInMilliseconds);

        if (autoSaveResult == null) return;

        UserMainPictureEntry!.UserText = autoSaveResult.Value.ToString();
    }

    public async Task SaveAndGenerateHtml(bool closeAfterSave)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (SelectedFile == null)
        {
            await StatusContext.ToastError("No File Selected? There must be a video to Save...");
            return;
        }

        var (generationReturn, newContent) = await VideoGenerator.SaveAndGenerateHtml(CurrentStateToVideoContent(),
            SelectedFile, null, StatusContext.ProgressTracker());

        if (generationReturn.HasError || newContent == null)
        {
            await StatusContext.ShowMessageWithOkButton("Problem Saving and Generating Html",
                generationReturn.GenerationNote);
            return;
        }

        await LoadData(newContent);

        if (closeAfterSave)
        {
            await ThreadSwitcher.ResumeForegroundAsync();
            RequestContentEditorWindowClose?.Invoke(this, EventArgs.Empty);
        }
    }

    private async Task SelectedFileChanged()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        SelectedFileHasPathOrNameChanges =
            (SelectedFile?.FullName ?? string.Empty) != (LoadedFile?.FullName ?? string.Empty);

        var (isValid, explanation) =
            await CommonContentValidation.FileContentFileValidation(SelectedFile, DbEntry.ContentId);

        SelectedFileHasValidationIssues = !isValid;

        SelectedFileValidationMessage = explanation;

        SelectedFileNameHasInvalidCharacters =
            await CommonContentValidation.FileContentFileFileNameHasInvalidCharacters(SelectedFile, DbEntry.ContentId);

        VideoContext!.VideoSource = SelectedFile is { Exists: true }
            ? VideoContext.VideoSource = SelectedFile.FullName
            : VideoContext.VideoSource = string.Empty;

        var settings = UserSettingsSingleton.CurrentSettings();


        var ffprobe = settings.FfprobeExe();
        if (SelectedFile is { Exists: true } && settings.FfmpegAndFfprobeExist())
        {
            try
            {
                var ffprobeExe = settings.FfprobeExe();

                var processStartInfo = new ProcessStartInfo
                {
                    FileName = ffprobeExe,
                    Arguments =
                        $"-v error -select_streams v:0 -show_entries stream=codec_name -of default=noprint_wrappers=1:nokey=1 \"{SelectedFile.FullName}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = processStartInfo };
                process.Start();

                var codecOutput = await process.StandardOutput.ReadToEndAsync();
                var errorOutput = await process.StandardError.ReadToEndAsync();

                await process.WaitForExitAsync();

                if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(codecOutput))
                {
                    var codecName = codecOutput.Trim().ToLowerInvariant();

                    // HTML5 video tag friendly codecs
                    var htmlFriendlyCodecs = new[] { "h264", "vp8", "vp9", "av1" };

                    // Codecs that ffmpeg can convert
                    var convertibleCodecs = new[]
                    {
                        "hevc", "h265", "mpeg4", "mpeg2video", "mpeg1video", "wmv", "wmv3", "vc1",
                        "vp6", "theora", "mjpeg", "msmpeg4v3", "msmpeg4v2", "msmpeg4", "h263",
                        "flv1", "cinepak", "indeo3", "svq3", "rv40", "rv30"
                    };

                    VideoIsNotHtmlVideoEmbedFriendly = !htmlFriendlyCodecs.Contains(codecName);
                    VideoCanBeReEncodedWithFfmpeg =
                        convertibleCodecs.Contains(codecName) || htmlFriendlyCodecs.Contains(codecName);
                }
                else
                {
                    // If ffprobe fails, assume compatible and don't show warnings
                    VideoIsNotHtmlVideoEmbedFriendly = false;
                    VideoCanBeReEncodedWithFfmpeg = false;

                    Log.ForContext("SelectedFile", SelectedFile.FullName)
                        .ForContext("ExitCode", process.ExitCode)
                        .ForContext("StdError", errorOutput)
                        .Debug("ffprobe check failed for video file");
                }
            }
            catch (Exception ex)
            {
                // On any error, assume compatible
                VideoIsNotHtmlVideoEmbedFriendly = false;
                VideoCanBeReEncodedWithFfmpeg = false;

                Log.ForContext("SelectedFile", SelectedFile.FullName)
                    .Error(ex, "Exception while checking video codec compatibility with ffprobe");
            }
        }
        else
        {
            // No ffprobe available or no file selected
            VideoIsNotHtmlVideoEmbedFriendly = false;
            VideoCanBeReEncodedWithFfmpeg = false;
        }

        TitleSummarySlugFolder?.CheckForChangesToTitleToFunctionStates();
    }

    public async Task SetUserMainPicture()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (UserMainPictureEntry == null || UserMainPictureEntry.HasValidationIssues ||
            UserMainPictureEntry.UserValue == null)
        {
            UserMainPictureEntrySmallImageUrl = null;
            UserMainPictureEntryContent = null;
            return;
        }

        try
        {
            var db = await Db.Context();
            UserMainPictureEntryContent = await db.ContentFromContentId(UserMainPictureEntry.UserValue.Value);

            UserMainPictureEntrySmallImageUrl = PictureAssetProcessing
                .ProcessPictureDirectory(UserMainPictureEntry.UserValue.Value)?.SmallPicture
                ?.File?.FullName;
        }
        catch (Exception e)
        {
            UserMainPictureEntrySmallImageUrl = null;
            UserMainPictureEntryContent = null;
            Log.Error(e, "Caught exception in VideoContentEditorContext while trying to setup the User Main Picture.");
        }
    }

    public async Task TryAddUserMainPicture(Guid? contentId)
    {
        if (contentId == null || contentId == Guid.Empty) return;
        var context = await Db.Context();
        if (context.ImageContents.Any(x => x.ContentId == contentId))
            UserMainPictureEntry!.UserText = contentId.Value.ToString();
    }

    private void UserMainPictureEntryOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        StatusContext.RunFireAndForgetNonBlockingTask(SetUserMainPicture);
    }


    public void VideoMetadataToCurrentContent(PhotoMetadata metadata)
    {
        LicenseEntry!.UserValue = metadata.License ?? string.Empty;
        VideoCreatedByEntry!.UserValue = metadata.PhotoCreatedBy ?? string.Empty;
        VideoCreatedOnEntry!.UserText = metadata.PhotoCreatedOn.ToString("MM/dd/yyyy h:mm:ss tt");
        VideoCreatedOnUtcEntry!.UserText =
            metadata.PhotoCreatedOnUtc?.ToString("MM/dd/yyyy h:mm:ss tt") ?? string.Empty;
        TitleSummarySlugFolder!.SummaryEntry.UserValue = metadata.Summary ?? string.Empty;
        TagEdit!.Tags = metadata.Tags ?? string.Empty;
        TitleSummarySlugFolder.TitleEntry.UserValue = metadata.Title ?? string.Empty;
        TitleSummarySlugFolder.TitleToSlug();
        TitleSummarySlugFolder.FolderEntry.UserValue = metadata.PhotoCreatedOn.Year.ToString("F0");
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

        var url = $"{settings.VideoPageUrl(DbEntry)}";

        var ps = new ProcessStartInfo(url) { UseShellExecute = true, Verb = "open" };
        Process.Start(ps);
    }

    [NonBlockingCommand]
    public async Task ViewUserMainPicture()
    {
        if (UserMainPictureEntryContent == null)
        {
            await StatusContext.ToastWarning("No Picture to View?");
            return;
        }

        await SetUserMainPicture();

        if (UserMainPictureEntryContent is PhotoContent photoToEdit)
        {
            var possibleVideo = UserSettingsSingleton.CurrentSettings().LocalMediaArchivePhotoContentFile(photoToEdit);

            if (possibleVideo is not { Exists: true })
            {
                await StatusContext.ToastWarning("No Media Video Found?");
                return;
            }

            await ThreadSwitcher.ResumeForegroundAsync();

            var ps = new ProcessStartInfo(possibleVideo.FullName) { UseShellExecute = true, Verb = "open" };
            Process.Start(ps);
            return;
        }

        if (UserMainPictureEntryContent is ImageContent imageToEdit)
        {
            var possibleVideo = UserSettingsSingleton.CurrentSettings().LocalMediaArchiveImageContentFile(imageToEdit);

            if (possibleVideo is not { Exists: true })
            {
                await StatusContext.ToastWarning("No Media Video Found?");
                return;
            }

            await ThreadSwitcher.ResumeForegroundAsync();

            var ps = new ProcessStartInfo(possibleVideo.FullName) { UseShellExecute = true, Verb = "open" };
            Process.Start(ps);
            return;
        }

        await StatusContext.ToastWarning("Didn't find the expected Photo/Image to view?");
    }

    [BlockingCommand]
    public async Task ViewVideoMetadata()
    {
        await FileMetadataReport.AllFileMetadataToHtmlDocumentAndOpen(SelectedFile, UserSettingsSingleton.CurrentSettings().FfprobeExe(), StatusContext);
    }
}