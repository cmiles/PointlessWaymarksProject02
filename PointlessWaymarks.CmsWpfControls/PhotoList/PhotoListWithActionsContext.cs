using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using KellermanSoftware.CompareNetObjects;
using KellermanSoftware.CompareNetObjects.Reports;
using Microsoft.EntityFrameworkCore;
using Omu.ValueInjecter;
using Ookii.Dialogs.Wpf;
using PointlessWaymarks.CmsData;
using PointlessWaymarks.CmsData.BracketCodes;
using PointlessWaymarks.CmsData.ContentGeneration;
using PointlessWaymarks.CmsData.ContentHtml.PhotoHtml;
using PointlessWaymarks.CmsData.Database;
using PointlessWaymarks.CmsData.Database.Models;
using PointlessWaymarks.CmsData.ImageHelpers;
using PointlessWaymarks.CmsWpfControls.ContentList;
using PointlessWaymarks.CommonTools;
using PointlessWaymarks.FeatureIntersectionTags;
using PointlessWaymarks.FeatureIntersectionTags.Models;
using PointlessWaymarks.LlamaAspects;
using PointlessWaymarks.WpfCommon;
using PointlessWaymarks.WpfCommon.Elevation;
using PointlessWaymarks.WpfCommon.FileMetadataDisplay;
using PointlessWaymarks.WpfCommon.Status;
using PointlessWaymarks.WpfCommon.Utility;
using Serilog;

namespace PointlessWaymarks.CmsWpfControls.PhotoList;

[NotifyPropertyChanged]
[GenerateStatusCommands]
public partial class PhotoListWithActionsContext
{
    private PhotoListWithActionsContext(StatusControlContext statusContext, WindowIconStatus? windowStatus,
        ContentListContext listContext, bool loadInBackground = true)
    {
        StatusContext = statusContext;
        WindowStatus = windowStatus;
        CommonCommands = new CmsCommonCommands(StatusContext, WindowStatus);

        BuildCommands();

        ListContext = listContext;

        ListContext.ContextMenuItems =
        [
            new ContextMenuItemData { ItemName = "Edit", ItemCommand = ListContext.EditSelectedCommand },
            new ContextMenuItemData
            {
                ItemName = "Image Code to Clipboard",
                ItemCommand = ListContext.BracketCodeToClipboardSelectedCommand
            },
            new ContextMenuItemData
            {
                ItemName = "Image Code with Photo Details to Clipboard",
                ItemCommand = ImageWithDetailsBracketCodesToClipboardForSelectedCommand
            },
            new ContextMenuItemData
            {
                ItemName = "Text Code to Clipboard", ItemCommand = TextBracketCodesToClipboardForSelectedCommand
            },
            new ContextMenuItemData
            {
                ItemName = "Picture Block to Clipboard",
                ItemCommand = ListContext.PictureBlockBracketCodeToClipboardSelectedCommand
            },
            new ContextMenuItemData
            {
                ItemName = "Picture Gallery to Clipboard",
                ItemCommand = ListContext.PictureGalleryBracketCodeToClipboardSelectedCommand
            },
            new ContextMenuItemData
            {
                ItemName = "Daily Photo Page Code to Clipboard",
                ItemCommand = DailyPhotoPageBracketCodesToClipboardForSelectedCommand
            },
            new ContextMenuItemData { ItemName = "View Photos - Individual", ItemCommand = ViewSelectedFilesCommand },
            new ContextMenuItemData
            {
                ItemName = "View Selected Photos - Group",
                ItemCommand = ListContext.PicturesAndVideosViewWindowSelectedCommand
            },
            new ContextMenuItemData
            {
                ItemName = "Add Intersection Tags - With OSM", ItemCommand = AddIntersectionTagsWithOsmToSelectedCommand
            },
            new ContextMenuItemData
            {
                ItemName = "Add Intersection Tags - Without OSM",
                ItemCommand = AddIntersectionTagsWithoutOsmToSelectedCommand
            },
            new ContextMenuItemData
            {
                ItemName = "View Intersection Tags",
                ItemCommand = ShowIntersectionTagsForSelectedCommand
            },
            new ContextMenuItemData { ItemName = "Export Files", ItemCommand = ExportFilesCommand },
            new ContextMenuItemData { ItemName = "Open URL", ItemCommand = ListContext.ViewOnSiteCommand },
            new ContextMenuItemData { ItemName = "Delete", ItemCommand = ListContext.DeleteSelectedCommand },
            new ContextMenuItemData
            {
                ItemName = "Map Selected Items", ItemCommand = ListContext.SpatialItemsToContentMapWindowSelectedCommand
            },

            new ContextMenuItemData { ItemName = "Refresh Data", ItemCommand = RefreshDataCommand }
        ];


        if (loadInBackground) StatusContext.RunFireAndForgetBlockingTask(RefreshData);
    }

    public CmsCommonCommands CommonCommands { get; set; }
    public ContentListContext ListContext { get; set; }
    public StatusControlContext StatusContext { get; set; }
    public WindowIconStatus? WindowStatus { get; set; }

    [BlockingCommand]
    public async Task AddIntersectionTagsToSelected(CancellationToken cancellationToken)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (!SelectedListItems().Any())
        {
            await StatusContext.ToastError("Nothing Selected?");
            return;
        }

        var frozenSelect = SelectedListItems().ToList();

        if (string.IsNullOrWhiteSpace(UserSettingsSingleton.CurrentSettings().FeatureIntersectionTagSettingsFile))
        {
            await StatusContext.ToastError("The Settings File for the Feature Intersection is blank?");
            return;
        }

        var settingsFileInfo = new FileInfo(UserSettingsSingleton.CurrentSettings().FeatureIntersectionTagSettingsFile);
        if (!settingsFileInfo.Exists)
        {
            await StatusContext.ToastError(
                $"The Settings File for the Feature Intersection {settingsFileInfo.FullName} doesn't exist?");
            return;
        }

        var settings =
            JsonSerializer.Deserialize<IntersectSettings>(await File.ReadAllTextAsync(settingsFileInfo.FullName,
                cancellationToken));
        if (settings == null)
        {
            StatusContext.Progress(
                $"The settings file {settingsFileInfo.FullName} did not deserialized to valid settings...");
            return;
        }

        var errorList = new List<string>();
        var successList = new List<string>();
        var noTagsList = new List<string>();

        var processedCount = 0;

        cancellationToken.ThrowIfCancellationRequested();

        var toProcess = new List<PhotoContent>();
        var intersectResults = new List<IntersectResult>();

        foreach (var loopSelected in frozenSelect)
        {
            var feature = settings.BufferPointsAndLinesByFeet > 0
                ? loopSelected.DbEntry.FeatureFromPointAsCircle(settings.BufferPointsAndLinesByFeet.Value)
                : loopSelected.DbEntry.FeatureFromPoint();

            if (feature == null) continue;

            var toAdd = PhotoContent.CreateInstance();

            toProcess.Add((PhotoContent)toAdd.InjectFrom(loopSelected.DbEntry));
            intersectResults.Add(new IntersectResult(feature)
            {
                ContentId = loopSelected.DbEntry.ContentId,
                Description = $"Photo Content - {loopSelected.DbEntry.Title ?? "No Title"}"
            });
        }

        await intersectResults.IntersectionTags(settings,
            cancellationToken,
            StatusContext.ProgressTracker());

        var updateTime = DateTime.Now;

        foreach (var loopSelected in toProcess)
        {
            processedCount++;

            try
            {
                var taggerResult = intersectResults.Single(x => x.ContentId == loopSelected.ContentId);

                if (!taggerResult.Tags.Any())
                {
                    noTagsList.Add($"{loopSelected.Title} - no tags found");
                    StatusContext.Progress(
                        $"Processed - {loopSelected.Title} - no tags found - Photo {processedCount} of {frozenSelect.Count}");
                    continue;
                }

                var tagListForIntersection = SlugTagTools.TagListParseToSpacedString(loopSelected.Tags);
                tagListForIntersection.AddRange(taggerResult.Tags);
                loopSelected.Tags = SlugTagTools.TagListJoinToSpacedString(tagListForIntersection);
                loopSelected.LastUpdatedBy = "Feature Intersection Tagger";
                loopSelected.LastUpdatedOn = updateTime;

                var mediaFile = UserSettingsSingleton.CurrentSettings().LocalSitePhotoContentFile(loopSelected);

                if (mediaFile == null)
                {
                    errorList.Add(
                        $"No Media File Found for Photo: {loopSelected.Title}");
                    StatusContext.Progress(
                        $"Processed - {loopSelected.Title} - no media library photo found - Photo {processedCount} of {frozenSelect.Count}");
                    continue;
                }

                var (saveGenerationReturn, _) =
                    await PhotoGenerator.SaveAndGenerateHtml(loopSelected, mediaFile, false,
                        DateTime.Now, StatusContext.ProgressTracker());

                if (saveGenerationReturn.HasError)
                    //TODO: Need alerting on this that would actually be seen...
                {
                    Log.ForContext("generationError", saveGenerationReturn.GenerationNote)
                        .ForContext("generationException", saveGenerationReturn.Exception?.ToString() ?? string.Empty)
                        .Error(
                            "Photo Save Error during Selected Photo Feature Intersection Tagging");
                    errorList.Add(
                        $"Save Failed! Photo: {loopSelected.Title}, {saveGenerationReturn.GenerationNote}");
                    continue;
                }

                successList.Add(
                    $"{loopSelected.Title} - found Tags {string.Join(", ", taggerResult.Tags)}");
                StatusContext.Progress(
                    $"Processed - {loopSelected.Title} - found Tags {string.Join(", ", taggerResult.Tags)} - Photo {processedCount} of {frozenSelect.Count}");
            }
            catch (Exception e)
            {
                Log.Error(e,
                    $"Photo Save Error during Selected Photo Feature Intersection Tagging {loopSelected.Title}, {loopSelected.ContentId}");
                errorList.Add(
                    $"Save Failed! Photo: {loopSelected.Title}, {e.Message}");
            }

            if (cancellationToken.IsCancellationRequested) break;
        }

        if (errorList.Any())
        {
            var bodyBuilder = new StringBuilder();
            bodyBuilder.AppendLine(
                $"There were errors getting Feature Intersection Tags and saving items - Errors: {errorList.Count}, Success: {successList.Count}, No Tags: {noTagsList.Count}.");
            bodyBuilder.AppendLine();
            bodyBuilder.AppendFormat("Errors:");
            bodyBuilder.AppendLine(string.Join(Environment.NewLine, errorList));
            bodyBuilder.AppendLine();
            bodyBuilder.AppendFormat("Successes:");
            bodyBuilder.AppendLine(string.Join(Environment.NewLine, successList));
            bodyBuilder.AppendLine();
            bodyBuilder.AppendFormat("No Tags Found:");
            bodyBuilder.AppendLine(string.Join(Environment.NewLine, noTagsList));

            await StatusContext.ShowMessageWithOkButton("Feature Intersection Errors", bodyBuilder.ToString());
        }
    }

    [BlockingCommand]
    [StopAndWarnIfNoSelectedListItems]
    public async Task AddIntersectionTagsWithOsmToSelected(CancellationToken cancellationToken)
    {
        await PhotoActions.AddIntersectionTags(SelectedListItemsContent(), StatusContext, true, cancellationToken);
    }

    [BlockingCommand]
    [StopAndWarnIfNoSelectedListItems]
    public async Task AddIntersectionTagsWithoutOsmToSelected(CancellationToken cancellationToken)
    {
        await PhotoActions.AddIntersectionTags(SelectedListItemsContent(), StatusContext, false, cancellationToken);
    }

    public static async Task<PhotoListWithActionsContext> CreateInstance(StatusControlContext? statusContext,
        WindowIconStatus? windowStatus = null, IContentListLoader? listLoader = null, bool loadInBackground = true)
    {
        var factoryStatusContext = await StatusControlContext.CreateInstance(statusContext);

        await ThreadSwitcher.ResumeBackgroundAsync();

        var factoryListContext =
            await ContentListContext.CreateInstance(factoryStatusContext, listLoader ?? new PhotoListLoader(100),
                [Db.ContentTypeDisplayStringForPhoto],
                windowStatus);

        return new PhotoListWithActionsContext(factoryStatusContext, windowStatus, factoryListContext,
            loadInBackground);
    }

    [BlockingCommand]
    [StopAndWarnIfNoSelectedListItems]
    public async Task DailyPhotoPageBracketCodesToClipboardForSelected()
    {
        await PhotoActions.DailyPhotoPageBracketCodesToClipboard(SelectedListItemsContent(), StatusContext);
    }

    [BlockingCommand]
    [StopAndWarnIfNotOneSelectedListItems]
    public async Task EmailHtmlToClipboard()
    {
        var frozenSelected = SelectedListItems().First();

        var emailHtml = await Email.ToHtmlEmail(frozenSelected.DbEntry, StatusContext.ProgressTracker());

        await ThreadSwitcher.ResumeForegroundAsync();

        HtmlClipboardHelpers.CopyToClipboard(emailHtml, emailHtml);

        await StatusContext.ToastSuccess("Email Html on Clipboard");
    }

    [BlockingCommand]
    [StopAndWarnIfNoSelectedListItemsAskIfOverMax(MaxSelectedItems = 10)]
    public async Task ExportFiles(CancellationToken cancellationToken)
    {
        await PhotoActions.ExportFiles(SelectedListItemsContent(), StatusContext, cancellationToken);
    }

    [NonBlockingCommand]
    public async Task FileNameAndTakenOnDoNotMatch()
    {
        await RunReport(ReportFileNameAndTakenOnDoNotMatchGenerator, "File Name and Taken Dates Don't Match");
    }

    [BlockingCommand]
    [StopAndWarnIfNoSelectedListItems]
    public async Task ForcedResize(CancellationToken cancellationToken)
    {
        var totalCount = SelectedListItems().Count;
        var currentLoop = 0;

        foreach (var loopSelected in SelectedListItems())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (++currentLoop % 10 == 0)
                StatusContext.Progress($"Cleaning Generated Images And Resizing {currentLoop} of {totalCount} - " +
                                       $"{loopSelected.DbEntry.Title}");
            var resizeResult =
                await PictureResizing.CopyCleanResizePhoto(loopSelected.DbEntry, StatusContext.ProgressTracker());

            if (!resizeResult.HasError) continue;

            PointlessWaymarksLogTools.LogGenerationReturn(resizeResult, "Photo Forced Resizing");

            if (currentLoop < totalCount)
            {
                if (await StatusContext.ShowMessage("Error Resizing",
                        $"There was an error resizing the image {loopSelected.DbEntry.OriginalFileName} in {loopSelected.DbEntry.Title}{Environment.NewLine}{Environment.NewLine}{resizeResult.GenerationNote}{Environment.NewLine}{Environment.NewLine}Continue?",
                        ["Yes", "No"]) == "No") return;
            }
            else
            {
                await StatusContext.ShowMessageWithOkButton("Error Resizing",
                    $"There was an error resizing the image {loopSelected.DbEntry.OriginalFileName} in {loopSelected.DbEntry.Title}{Environment.NewLine}{Environment.NewLine}{resizeResult.GenerationNote}");
            }
        }
    }

    [BlockingCommand]
    [StopAndWarnIfNoSelectedListItems]
    public async Task ImageWithDetailsBracketCodesToClipboardForSelected()
    {
        await PhotoActions.ImageWithDetailsBracketCodesToClipboard(SelectedListItemsContent(), StatusContext);
    }

    [BlockingCommand]
    public async Task PhotoMetadataFromPickedFile()
    {
        await ThreadSwitcher.ResumeForegroundAsync();

        var dialog = new VistaOpenFileDialog { Multiselect = false, Filter = "All files (*.*)|*.*" };

        if (!(dialog.ShowDialog() ?? false)) return;

        var selectedFile = dialog.FileName;

        if (string.IsNullOrWhiteSpace(selectedFile)) return;
        if (!File.Exists(selectedFile)) return;
        var file = new FileInfo(selectedFile);

        var metadataWindow =
            await FileMetadataDisplayWindow.CreateInstance(file.FullName,
                UserSettingsSingleton.CurrentSettings().FfprobeExe());
        await metadataWindow.PositionWindowAndShowOnUiThread();
    }


    [BlockingCommand]
    [StopAndWarnIfNoSelectedListItems]
    public async Task PhotoTitleToFilename()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var frozenSelected = SelectedListItems();

        var settings = UserSettingsSingleton.CurrentSettings();

        var errors = new List<string>();
        var successCounter = 0;
        var skipCounter = 0;

        foreach (var loopPhoto in frozenSelected)
        {
            if (string.IsNullOrWhiteSpace(loopPhoto.DbEntry.Title))
            {
                skipCounter++;
                continue;
            }

            try
            {
                var selectedFile = settings.LocalMediaArchivePhotoContentFile(loopPhoto.DbEntry);

                if (selectedFile is null)
                {
                    errors.Add($"{loopPhoto.DbEntry.Title} - No file found?");
                    continue;
                }

                if (selectedFile is not { Exists: true })
                {
                    errors.Add($"{loopPhoto.DbEntry.Title} - file {selectedFile.FullName} does not exist?");
                    continue;
                }

                var cleanedName = SlugTagTools.CreateSlug(false, loopPhoto.DbEntry.Title.TrimNullToEmpty());

                if (string.IsNullOrWhiteSpace(cleanedName))
                {
                    errors.Add($"{loopPhoto.DbEntry.Title} - Can't rename the file to an empty string...");
                    continue;
                }

                if (!FileAndFolderTools.IsNoUrlEncodingNeeded(cleanedName))
                {
                    errors.Add(
                        $"{loopPhoto.DbEntry.Title} - {cleanedName} - File Names must be limited to A - Z a - z 0 - 9 - . _");
                    continue;
                }

                if (string.Equals(loopPhoto.DbEntry.OriginalFileName,
                        $"{cleanedName}{Path.GetExtension(selectedFile.Name)}"))
                {
                    skipCounter++;
                    continue;
                }

                var moveToName = Path.Combine(selectedFile.Directory?.FullName ?? string.Empty,
                    $"{cleanedName}{Path.GetExtension(selectedFile.Name)}");

                // Check if a different file (not just case difference) already exists
                if (File.Exists(moveToName) &&
                    !string.Equals(selectedFile.FullName, moveToName, StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add($"{loopPhoto.DbEntry.Title} - {moveToName} - Suggested new Filename Already Exists");
                    continue;
                }

                try
                {
                    // For case-only renames, we need to do a two-step rename on case-insensitive filesystems
                    if (string.Equals(selectedFile.FullName, moveToName, StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(selectedFile.FullName, moveToName, StringComparison.Ordinal))
                    {
                        // Case-only rename: use temporary file
                        var tempName = Path.Combine(selectedFile.Directory?.FullName ?? string.Empty,
                            $"{Guid.NewGuid()}{Path.GetExtension(selectedFile.Name)}");
                        File.Move(selectedFile.FullName, tempName);
                        File.Move(tempName, moveToName);
                    }
                    else
                    {
                        // Normal copy
                        File.Copy(selectedFile.FullName, moveToName);
                    }
                }
                catch (Exception e)
                {
                    errors.Add($"{loopPhoto.DbEntry.Title} - {moveToName} - {e.Message}");
                    continue;
                }

                var finalFile = new FileInfo(moveToName);
                loopPhoto.DbEntry.OriginalFileName = finalFile.Name;

                if (string.IsNullOrWhiteSpace(loopPhoto.DbEntry.LastUpdatedBy))
                    loopPhoto.DbEntry.LastUpdatedBy = loopPhoto.DbEntry.CreatedBy;
                if (loopPhoto.DbEntry.LastUpdatedOn is null || loopPhoto.DbEntry.LastUpdatedOn == DateTime.MinValue)
                    loopPhoto.DbEntry.LastUpdatedOn = DateTime.Now;

                var saveResult = await PhotoGenerator.SaveAndGenerateHtml(loopPhoto.DbEntry, finalFile, false, null,
                    StatusContext.ProgressTracker());

                if (saveResult.generationReturn.HasError)
                {
                    errors.Add(
                        $"{loopPhoto.DbEntry.Title} - {moveToName} - {saveResult.generationReturn.ToErrorString()}");
                    continue;
                }

                successCounter++;
            }
            catch (Exception e)
            {
                errors.Add($"{loopPhoto.DbEntry.Title} - {e.Message}");
            }
        }

        if (errors.Any())
            await StatusContext.ShowMessageWithOkButton("Errors Renaming",
                $"{successCounter} Succeeded, {skipCounter} Already Equal, {errors.Count} Failed: {Environment.NewLine}{Environment.NewLine}{string.Join($"{Environment.NewLine}{Environment.NewLine}", errors)}");
        else
            await StatusContext.ToastSuccess($"Renamed {successCounter} files, {skipCounter} Names already match.");
    }

    [BlockingCommand]
    [StopAndWarnIfNoSelectedListItemsAskIfOverMax(MaxSelectedItems = 10, ActionVerb = "open")]
    public async Task PhotoToPointContentEditor(CancellationToken cancellationToken)
    {
        var count = 1;

        var frozenNow = DateTime.Now;

        foreach (var loopPhoto in SelectedListItems())
        {
            cancellationToken.ThrowIfCancellationRequested();

            StatusContext.Progress(
                $"Opening Point Content Editor for '{loopPhoto.DbEntry.Title}' - {count++} of {SelectedListItems().Count}");

            var newPartialPoint = PointContent.CreateInstance();

            newPartialPoint.CreatedOn = frozenNow;
            newPartialPoint.FeedOn = frozenNow;
            newPartialPoint.BodyContent = BracketCodePhotos.Create(loopPhoto.DbEntry);
            newPartialPoint.Title = $"Point From {loopPhoto.DbEntry.Title}";
            newPartialPoint.Tags = loopPhoto.DbEntry.Tags;
            newPartialPoint.Slug = SlugTagTools.CreateSlug(true, newPartialPoint.Title);

            if (loopPhoto.DbEntry.Latitude != null) newPartialPoint.Latitude = loopPhoto.DbEntry.Latitude.Value;
            if (loopPhoto.DbEntry.Longitude != null) newPartialPoint.Longitude = loopPhoto.DbEntry.Longitude.Value;
            if (loopPhoto.DbEntry.Elevation != null) newPartialPoint.Elevation = loopPhoto.DbEntry.Elevation.Value;

            var pointWindow = await PointContentEditorWindow.CreateInstance(newPartialPoint);

            await pointWindow.PositionWindowAndShowOnUiThread();
        }
    }

    [BlockingCommand]
    public async Task RefreshData()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        await ListContext.LoadData();
    }

    [BlockingCommand]
    [StopAndWarnIfNoSelectedListItems]
    public async Task RegenerateHtmlAndReprocessPhotoForSelected(CancellationToken cancellationToken)
    {
        var loopCount = 0;
        var totalCount = SelectedListItems().Count;

        var db = await Db.Context();

        var errorList = new List<string>();

        foreach (var loopSelected in SelectedListItems())
        {
            if (cancellationToken.IsCancellationRequested) break;

            loopCount++;

            var currentVersion = db.PhotoContents.SingleOrDefault(x => x.ContentId == loopSelected.DbEntry.ContentId);

            if (currentVersion == null)
            {
                StatusContext.Progress(
                    $"Re-processing Photo and Generating Html for {loopSelected.DbEntry.Title} failed - not found in DB, {loopCount} of {totalCount}");
                errorList.Add($"Photo Titled {loopSelected.DbEntry.Title} was not found in the database?");
                continue;
            }

            if (string.IsNullOrWhiteSpace(currentVersion.LastUpdatedBy))
                currentVersion.LastUpdatedBy = currentVersion.CreatedBy;
            currentVersion.LastUpdatedOn = DateTime.Now;

            StatusContext.Progress(
                $"Re-processing Photo and Generating Html for {loopSelected.DbEntry.Title}, {loopCount} of {totalCount}");

            var mediaLibraryFile = UserSettingsSingleton.CurrentSettings()
                .LocalMediaArchivePhotoContentFile(currentVersion);

            if (mediaLibraryFile == null)
            {
                errorList.Add($"The Media File Library for {loopSelected.DbEntry.Title} was not found?");
                continue;
            }

            var (generationReturn, _) = await PhotoGenerator.SaveAndGenerateHtml(currentVersion, mediaLibraryFile
                , true, null,
                StatusContext.ProgressTracker());

            if (generationReturn.HasError)
            {
                StatusContext.Progress(
                    $"Re-processing Photo and Generating Html for {loopSelected.DbEntry.Title} Error {generationReturn.GenerationNote}, {generationReturn.Exception}, {loopCount} of {totalCount}");
                errorList.Add($"Error processing Photo Titled {loopSelected.DbEntry.Title}...");
            }
        }

        if (errorList.Any())
        {
            errorList.Reverse();
            await StatusContext.ShowMessageWithOkButton("Errors Resizing and Regenerating HTML",
                string.Join($"{Environment.NewLine}{Environment.NewLine}", errorList));
        }
    }

    [NonBlockingCommand]
    public async Task ReportAllPhotos()
    {
        await RunReport(ReportAllPhotosGenerator, "All Photos");
    }


    private async Task<List<object>> ReportAllPhotosGenerator()
    {
        var db = await Db.Context();

        return (await db.PhotoContents.OrderByDescending(x => x.PhotoCreatedOn).ToListAsync()).Cast<object>().ToList();
    }

    [NonBlockingCommand]
    public async Task ReportBlankLicense()
    {
        await RunReport(ReportBlankLicenseGenerator, "Blank License");
    }

    private async Task<List<object>> ReportBlankLicenseGenerator()
    {
        var db = await Db.Context();

        var allContents = await db.PhotoContents.OrderByDescending(x => x.PhotoCreatedOn).ToListAsync();

        var returnList = new List<PhotoContent>();

        foreach (var loopContents in allContents)
            if (string.IsNullOrWhiteSpace(loopContents.License))
                returnList.Add(loopContents);

        return returnList.Cast<object>().ToList();
    }

    [NonBlockingCommand]
    public async Task ReportFileNameAndTakenOnDoNotMatch()
    {
        await RunReport(ReportFileNameAndTakenOnDoNotMatchGenerator, "File Name and Taken Date Don't Match");
    }

    private async Task<List<object>> ReportFileNameAndTakenOnDoNotMatchGenerator
        ()
    {
        var db = await Db.Context();

        var allContents = await db.PhotoContents.OrderByDescending(x => x.PhotoCreatedOn).ToListAsync();

        var returnList = new List<PhotoContent>();

        foreach (var loopContents in allContents)
        {
            if (loopContents.OriginalFileName == null) continue;

            var fileDate = DateTimeTools.DateOnlyFromTitleStringByConvention(Path
                .GetFileNameWithoutExtension(loopContents.OriginalFileName).Replace("-", " ").Replace("_", " ")
                .CamelCaseToSpacedString());

            if (fileDate == null)
            {
                returnList.Add(loopContents);
                continue;
            }

            if (fileDate.Value.titleDate.Year == loopContents.PhotoCreatedOn.Year &&
                fileDate.Value.titleDate.Month == loopContents.PhotoCreatedOn.Month) continue;

            returnList.Add(loopContents);
        }

        return returnList.Cast<object>().ToList();
    }

    [NonBlockingCommand]
    public async Task ReportLicenseAndTakenYearDoNotMatch()
    {
        await RunReport(ReportLicenseAndTakenYearDoNotMatchGenerator, "License and Taken Date Don't Match");
    }

    private async Task<List<object>> ReportLicenseAndTakenYearDoNotMatchGenerator()
    {
        var db = await Db.Context();

        var allContents = await db.PhotoContents.OrderByDescending(x => x.PhotoCreatedOn).ToListAsync();

        var returnList = new List<PhotoContent>();

        foreach (var loopContents in allContents)
        {
            if (string.IsNullOrWhiteSpace(loopContents.License))
            {
                returnList.Add(loopContents);
                continue;
            }

            var possibleYear = Regex.Match(loopContents.License, @"(?<PossibleYear>[12]\d\d\d)",
                RegexOptions.IgnoreCase).Value;

            if (string.IsNullOrWhiteSpace(possibleYear)) continue;

            if (!int.TryParse(possibleYear, out var licenseYear)) continue;

            var createdOn = loopContents.PhotoCreatedOn.Year;

            if (createdOn == licenseYear) continue;

            returnList.Add(loopContents);
        }

        return returnList.Cast<object>().ToList();
    }

    [NonBlockingCommand]
    public async Task ReportMultiSpacesInTitle()
    {
        await RunReport(ReportMultiSpacesInTitleGenerator, "Multiple Spaces in Title");
    }

    private async Task<List<object>> ReportMultiSpacesInTitleGenerator()
    {
        var db = await Db.Context();

        return (await db.PhotoContents.Where(x => x.Title != null && x.Title.Contains("  "))
            .OrderByDescending(x => x.PhotoCreatedOn)
            .ToListAsync()).Cast<object>().ToList();
    }

    [NonBlockingCommand]
    public async Task ReportNoTags()
    {
        await RunReport(ReportNoTagsGenerator, "No Tags");
    }

    private async Task<List<object>> ReportNoTagsGenerator()
    {
        var db = await Db.Context();

        return (await db.PhotoContents.Where(x => x.Tags == "").ToListAsync()).Cast<object>().ToList();
    }

    [BlockingCommand]
    [StopAndWarnIfNotOneSelectedListItems]
    public async Task ReportPhotoMetadata()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var singleSelected = SelectedListItemsContent().First();

        await PhotoActions.ReportPhotoMetadata(singleSelected.AsList(), StatusContext);
    }

    [NonBlockingCommand]
    public async Task ReportTitleAndFileNameDoNotMatch()
    {
        await RunReport(ReportTitleAndFileNameDoNotMatchGenerator, "Title and Filename Don't Match");
    }

    private async Task<List<object>> ReportTitleAndFileNameDoNotMatchGenerator()
    {
        var db = await Db.Context();

        var allContents = await db.PhotoContents.OrderByDescending(x => x.PhotoCreatedOn).ToListAsync();

        var returnList = new List<PhotoContent>();

        foreach (var loopContents in allContents)
        {
            var titleFilename = SlugTagTools.CreateSlug(false, loopContents.Title.TrimNullToEmpty());

            if (string.IsNullOrWhiteSpace(titleFilename)) returnList.Add(loopContents);

            if (string.Equals(titleFilename, Path.GetFileNameWithoutExtension(loopContents.OriginalFileName))) continue;

            returnList.Add(loopContents);
        }

        return returnList.Cast<object>().ToList();
    }

    [NonBlockingCommand]
    public async Task ReportTitleAndTakenDoNotMatch()
    {
        await RunReport(ReportTitleAndTakenDoNotMatchGenerator, "Title and Taken Dates Don't Match");
    }

    private async Task<List<object>> ReportTitleAndTakenDoNotMatchGenerator()
    {
        var db = await Db.Context();

        var allContents = await db.PhotoContents.OrderByDescending(x => x.PhotoCreatedOn).ToListAsync();

        var returnList = new List<PhotoContent>();

        foreach (var loopContents in allContents)
        {
            var titleDate = DateTimeTools.YearAndEnglishTextMonthFromStartOfString(loopContents.Title);

            if (titleDate == null) continue;

            if (titleDate.Value.Year == loopContents.PhotoCreatedOn.Year &&
                titleDate.Value.Month == loopContents.PhotoCreatedOn.Month) continue;

            returnList.Add(loopContents);
        }

        return returnList.Cast<object>().ToList();
    }


    [NonBlockingCommand]
    public async Task ReportTitleDoesNotStartWithYearMonth()
    {
        await RunReport(ReportTitleDoesNotStartWithYearMonthGenerator, "Title Does Not Start with Year and Month");
    }

    private async Task<List<object>> ReportTitleDoesNotStartWithYearMonthGenerator()
    {
        var db = await Db.Context();

        var allContents = await db.PhotoContents.OrderByDescending(x => x.PhotoCreatedOn).ToListAsync();

        var returnList = new List<PhotoContent>();

        foreach (var loopContents in allContents)
        {
            var titleDate = DateTimeTools.YearAndEnglishTextMonthFromStartOfString(loopContents.Title);

            if (titleDate == null) returnList.Add(loopContents);
        }

        return returnList.Cast<object>().ToList();
    }

    [BlockingCommand]
    [StopAndWarnIfNoSelectedListItems]
    public async Task RescanMetadataAndFillBlanks(CancellationToken cancellationToken)
    {
        var frozenSelected = SelectedListItems().ToList();

        var errorMessages = new List<string>();
        var updates = new List<(string updateMessage, PhotoContent toUpdate)>();

        StatusContext.Progress($"Processing {frozenSelected.Count} Photo Files");

        var counter = 0;
        var updateSetTime = DateTime.Now;

        foreach (var loopSelected in frozenSelected)
        {
            cancellationToken.ThrowIfCancellationRequested();

            StatusContext.Progress(
                $"Photo - {loopSelected.DbEntry.Title} - {++counter} of {frozenSelected.Count} - Checking Metadata");

            var mediaFile = UserSettingsSingleton.CurrentSettings()
                .LocalMediaArchivePhotoContentFile(loopSelected.DbEntry);

            if (mediaFile is not { Exists: true })
            {
                errorMessages.Add($"{loopSelected.DbEntry.Title} - Media File Not Found? Something has gone wrong...");
                continue;
            }

            var metadataReturn =
                await PhotoGenerator.PhotoMetadataFromFile(mediaFile, true, StatusContext.ProgressTracker());

            if (metadataReturn.generationReturn.HasError || metadataReturn.metadata == null)
            {
                errorMessages.Add(
                    $"{loopSelected.DbEntry.Title} - error with metadata? {metadataReturn.generationReturn.GenerationNote}.");
                continue;
            }

            var toModify = (PhotoContent)PhotoContent.CreateInstance().InjectFrom(loopSelected.DbEntry);

            if (toModify.PhotoCreatedOnUtc == null && metadataReturn.metadata.PhotoCreatedOnUtc != null)
                toModify.PhotoCreatedOnUtc = metadataReturn.metadata.PhotoCreatedOnUtc;
            if (string.IsNullOrWhiteSpace(toModify.PhotoCreatedBy) &&
                !string.IsNullOrWhiteSpace(metadataReturn.metadata.PhotoCreatedBy))
                toModify.PhotoCreatedBy = metadataReturn.metadata.PhotoCreatedBy;
            if (string.IsNullOrWhiteSpace(toModify.Aperture) &&
                !string.IsNullOrWhiteSpace(metadataReturn.metadata.Aperture))
                toModify.Aperture = metadataReturn.metadata.Aperture;
            if (string.IsNullOrWhiteSpace(toModify.CameraMake) &&
                !string.IsNullOrWhiteSpace(metadataReturn.metadata.CameraMake))
                toModify.CameraMake = metadataReturn.metadata.CameraMake;
            if (string.IsNullOrWhiteSpace(toModify.CameraModel) &&
                !string.IsNullOrWhiteSpace(metadataReturn.metadata.CameraModel))
                toModify.CameraModel = metadataReturn.metadata.CameraModel;
            if (string.IsNullOrWhiteSpace(toModify.FocalLength) &&
                !string.IsNullOrWhiteSpace(metadataReturn.metadata.FocalLength))
                toModify.FocalLength = metadataReturn.metadata.FocalLength;
            if (string.IsNullOrWhiteSpace(toModify.Lens) &&
                !string.IsNullOrWhiteSpace(metadataReturn.metadata.Lens))
                toModify.Lens = metadataReturn.metadata.Lens;
            if (string.IsNullOrWhiteSpace(toModify.License) &&
                !string.IsNullOrWhiteSpace(metadataReturn.metadata.License))
                toModify.License = metadataReturn.metadata.License;
            if (string.IsNullOrWhiteSpace(toModify.ShutterSpeed) &&
                !string.IsNullOrWhiteSpace(metadataReturn.metadata.ShutterSpeed))
                toModify.ShutterSpeed = metadataReturn.metadata.ShutterSpeed;
            if (toModify.Iso == null && metadataReturn.metadata.Iso != null)
                toModify.Iso = metadataReturn.metadata.Iso;
            if (toModify.Latitude == null && metadataReturn.metadata.Latitude != null)
                toModify.Latitude = metadataReturn.metadata.Latitude;
            if (toModify.Longitude == null && metadataReturn.metadata.Longitude != null)
                toModify.Longitude = metadataReturn.metadata.Longitude;
            if (toModify.Elevation == null && metadataReturn.metadata.Elevation != null)
                toModify.Elevation = metadataReturn.metadata.Elevation;
            if (toModify.PhotoDirection == null && metadataReturn.metadata.PhotoDirection != null)
                toModify.PhotoDirection = metadataReturn.metadata.PhotoDirection;

            var comparisonResult =
                new CompareLogic { Config = { MaxDifferences = 100 } }.Compare(loopSelected.DbEntry, toModify);

            if (comparisonResult.AreEqual)
            {
                StatusContext.Progress($"Photo - {loopSelected.DbEntry.Title} - No Changes");
                continue;
            }

            toModify.LastUpdatedOn = updateSetTime;
            toModify.LastUpdatedBy = UserSettingsSingleton.CurrentSettings().DefaultCreatedBy;

            var friendlyReport = new UserFriendlyReport();
            updates.Add((friendlyReport.OutputString(comparisonResult.Differences), toModify));
        }

        if (errorMessages.Any() && !updates.Any())
        {
            await StatusContext.ShowMessageWithOkButton("Errors getting Metadata",
                $"No changes were found but there were errors during processing: {Environment.NewLine}{Environment.NewLine}{string.Join(Environment.NewLine, errorMessages)}");
            return;
        }

        if (errorMessages.Any())
            if (await StatusContext.ShowMessage("Errors getting Metadata",
                    $"There were errors during processing, continue and see changes? {Environment.NewLine}{Environment.NewLine}{string.Join(Environment.NewLine, errorMessages)}",
                    ["Yes", "No"]) == "No")
                return;

        var changeMessages = string.Join(Environment.NewLine,
            updates.Select(x =>
                $"{Environment.NewLine}{x.toUpdate.Title} | {x.updateMessage} | {x.toUpdate.ContentId}"));

        if (await StatusContext.ShowMessage("Metadata Updates",
                $"Update {updates.Count} Photos where blanks were replaced based on current Metadata? {Environment.NewLine}{Environment.NewLine}{string.Join(Environment.NewLine, changeMessages)}",
                ["Yes", "No"]) == "No") return;

        var updateErrorMessages = new List<string>();
        var successCount = 0;

        foreach (var loopUpdate in updates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var mediaLibraryFile = UserSettingsSingleton.CurrentSettings()
                .LocalMediaArchivePhotoContentFile(loopUpdate.toUpdate);

            if (mediaLibraryFile == null)
            {
                updateErrorMessages.Add(
                    $"{loopUpdate.toUpdate.Title} - {loopUpdate.toUpdate.ContentId} - photo not found in Media Library?");
                continue;
            }

            var result = await PhotoGenerator.SaveAndGenerateHtml(loopUpdate.toUpdate, mediaLibraryFile
                , false,
                DateTime.Now, StatusContext.ProgressTracker());

            if (result.generationReturn.HasError)
                updateErrorMessages.Add(
                    $"{loopUpdate.toUpdate.Title} - {loopUpdate.toUpdate.ContentId} - {result.generationReturn.GenerationNote}");
            else
                successCount++;
        }

        if (updateErrorMessages.Any())
            await StatusContext.ShowMessageWithOkButton("Errors Saving Photos",
                $"Saved {successCount} Photos successfully but there were {updateErrorMessages.Count} failures: {Environment.NewLine}{string.Join(Environment.NewLine, updateErrorMessages)}");
    }

    private static async Task RunReport(Func<Task<List<object>>> toRun, string title)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var reportLoader = new ContentListLoaderReport(toRun);

        var newWindow =
            await PhotoListWindow.CreateInstance(
                await CreateInstance(null, null, reportLoader));
        newWindow.WindowTitle = title;
        await newWindow.PositionWindowAndShowOnUiThread();
    }

    public List<PhotoListListItem> SelectedListItems()
    {
        return ListContext.ListSelection.SelectedItems.Where(x => x is PhotoListListItem).Cast<PhotoListListItem>()
            .ToList();
    }

    public List<PhotoContent> SelectedListItemsContent()
    {
        return ListContext.ListSelection.SelectedItems.Where(x => x is PhotoListListItem).Cast<PhotoListListItem>()
            .Select(x => x.DbEntry).ToList();
    }

    [BlockingCommand]
    [StopAndWarnIfNoSelectedListItems]
    public async Task ShowIntersectionTagsForSelected(CancellationToken cancellationToken)
    {
        await PhotoActions.ShowIntersectionTagsForSelected(SelectedListItemsContent(), StatusContext,
            cancellationToken);
    }

    [BlockingCommand]
    [StopAndWarnIfNoSelectedListItems]
    public async Task TextBracketCodesToClipboardForSelected()
    {
        await PhotoActions.TextBracketCodesToClipboard(SelectedListItemsContent(), StatusContext);
    }

    [BlockingCommand]
    [StopAndWarnIfNoSelectedListItemsAskIfOverMax(MaxSelectedItems = 10)]
    public async Task ViewSelectedFiles(CancellationToken cancelToken)
    {
        var currentSelected = SelectedListItems();

        foreach (var loopSelected in currentSelected)
        {
            cancelToken.ThrowIfCancellationRequested();

            await loopSelected.ItemActions.ViewFile(loopSelected.DbEntry);
        }
    }
}