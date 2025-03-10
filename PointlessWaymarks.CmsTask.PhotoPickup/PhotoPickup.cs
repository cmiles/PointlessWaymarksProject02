using System.Diagnostics;
using System.Text;
using System.Text.Encodings.Web;
using PointlessWaymarks.CmsData;
using PointlessWaymarks.CmsData.CommonHtml;
using PointlessWaymarks.CmsData.ContentGeneration;
using PointlessWaymarks.CmsData.Database;
using PointlessWaymarks.CommonTools;
using PointlessWaymarks.WindowsTools;
using Serilog;

namespace PointlessWaymarks.CmsTask.PhotoPickup;

public class PhotoPickup
{
    public async Task PickupPhotos(PhotoPickupSettings settings)
    {
        var notifier = (await WindowsNotificationBuilders.NewNotifier(PhotoPickupSettings.ProgramShortName()))
            .SetAutomationLogoNotificationIconUrl().SetErrorReportAdditionalInformationMarkdown(
                EmbeddedResourceTools.GetEmbeddedResourceText("README.md"));

        var pickupDirectory = new DirectoryInfo(settings.PhotoPickupDirectory);

        if (!pickupDirectory.Exists)
        {
            Log.Error("The specified Photo Pick Up Directory {pickupDirectoryFullName} does not exist?",
                pickupDirectory.FullName);
            await notifier.Error($"Error: Photo Pick Up Directory {pickupDirectory.FullName} does not exist?");
            return;
        }

        var jpgFiles = pickupDirectory.EnumerateFiles("*").Where(FileAndFolderTools.PictureFileTypeIsSupported)
            .OrderBy(x => x.Name).ToList();

        if (!jpgFiles.Any())
        {
            Log.Information($"No jpg/jpeg files found in {pickupDirectory.FullName}");
            return;
        }

        var siteSettingsFileInfo = new FileInfo(settings.PointlessWaymarksSiteSettingsFileFullName);

        if (!siteSettingsFileInfo.Exists)
        {
            Log.Error(
                "The site settings file {settingsPointlessWaymarksSiteSettingsFileFullName} was specified but not found?",
                settings.PointlessWaymarksSiteSettingsFileFullName
            );
            await notifier.Error(
                $"Site settings file {settings.PointlessWaymarksSiteSettingsFileFullName} was not found");
            return;
        }

        var archiveDirectory = new DirectoryInfo(settings.PhotoPickupArchiveDirectory);

        if (!archiveDirectory.Exists)
            try
            {
                archiveDirectory.Create();
            }
            catch (Exception e)
            {
                Log.Error(e,
                    "The specified Photo Archive Directory {settingsPhotoPickupArchiveDirectory} does not exist and could not be created.",
                    settings.PhotoPickupArchiveDirectory);
                await notifier.Error(e,
                    "The specified Photo Archive Directory {settings.PhotoPickupArchiveDirectory} does not exist and could not be created. In addition to checking that the directory exists and there are no typos you may also need to check that the program has permissions to access, read from and write to the directory.");
                return;
            }

        var consoleProgress = new ConsoleProgress();

        UserSettingsUtilities.SettingsFileFullName = siteSettingsFileInfo.FullName;
        var siteSettings = await UserSettingsUtilities.ReadFromCurrentSettingsFile(consoleProgress);
        siteSettings.VerifyOrCreateAllTopLevelFolders();

        await UserSettingsUtilities.EnsureDbIsPresent(consoleProgress);

        Log.Information($"Starting processing of the {jpgFiles.Count} jpg Photo Files ");

        foreach (var loopFile in jpgFiles)
        {
            var (metaGenerationReturn, metaContent) = await
                PhotoGenerator.PhotoMetadataToNewPhotoContent(loopFile, consoleProgress);

            if (metaGenerationReturn.HasError || metaContent == null)
            {
                Log.ForContext("metaGenerationReturn", metaGenerationReturn.SafeObjectDump()).Error(
                    $"Error With Metadata for Photo {loopFile.FullName} - {metaGenerationReturn.GenerationNote} - {metaGenerationReturn.Exception?.Message}");
                continue;
            }

            if (string.IsNullOrWhiteSpace(metaContent.Tags)) metaContent.Tags = "photo-pickup-automated-import";

            FileInfo? renamedFile;

            if (settings.RenameFileToTitle)
                renamedFile =
                    await FileAndFolderTools.TryAutoRenameFileForProgramConventions(loopFile, metaContent.Title!);
            else
                renamedFile = await FileAndFolderTools.TryAutoCleanRenameFileForProgramConventions(loopFile);

            if (renamedFile is null)
            {
                Log.Information($"Error with Filename - skipping {loopFile.Name}");
                continue;
            }

            var uniqueRenamedFile = await ToPhotoFileNameNotInDb(renamedFile);

            metaContent.OriginalFileName = uniqueRenamedFile.Name;

            var (saveGenerationReturn, savedContent) = await PhotoGenerator.SaveAndGenerateHtml(metaContent,
                uniqueRenamedFile,
                true,
                null, consoleProgress);

            //Clean up renamed files as needed
            if (uniqueRenamedFile.FullName != renamedFile.FullName && uniqueRenamedFile.FullName != loopFile.FullName)
                uniqueRenamedFile.MoveToWithUniqueName(Path.Combine(archiveDirectory.FullName, uniqueRenamedFile.Name));
            if (renamedFile.FullName != loopFile.FullName)
                renamedFile.MoveToWithUniqueName(Path.Combine(archiveDirectory.FullName, renamedFile.Name));
            loopFile.MoveToWithUniqueName(Path.Combine(archiveDirectory.FullName, loopFile.Name));

            if (saveGenerationReturn.HasError || savedContent == null)
            {
                Log.ForContext("saveGenerationReturn", saveGenerationReturn.SafeObjectDump()).Error(
                    $"Error Saving Photo {uniqueRenamedFile.FullName} - {saveGenerationReturn.GenerationNote} - {saveGenerationReturn.Exception?.Message}");

                var htmlBuilder = new StringBuilder();

                htmlBuilder.AppendLine($"<p>{HtmlEncoder.Default.Encode(saveGenerationReturn.GenerationNote)}</p>");
                if (saveGenerationReturn.Exception != null)
                {
                    htmlBuilder.AppendLine(
                        $"<p>{HtmlEncoder.Default.Encode(saveGenerationReturn.Exception.Message)}</p>");
                    htmlBuilder.AppendLine(
                        $"<p>{HtmlEncoder.Default.Encode(saveGenerationReturn.Exception.ToString())}</p>");
                }

                await notifier.Error($"Error Saving Photo {uniqueRenamedFile.FullName}", htmlBuilder.ToString(), true);
            }
            else
            {
                renamedFile.MoveToWithUniqueName(Path.Combine(archiveDirectory.FullName, loopFile.Name));

                var generatedPhotoInformation = PictureAssetProcessing.ProcessPhotoDirectory(savedContent);

                Debug.Assert(generatedPhotoInformation != null, nameof(generatedPhotoInformation) + " != null");

                var closestSize = generatedPhotoInformation.SrcsetImages.MinBy(x => Math.Abs(384 - x.Width));

                if (closestSize is { File: not null, SiteUrl: not null })
                    notifier.Message(
                        $"{UserSettingsSingleton.CurrentSettings().SiteName} - Photo Added '{metaContent.Title}'",
                        closestSize.SiteUrl);
                else
                    notifier.Message(
                        $"{UserSettingsSingleton.CurrentSettings().SiteName} - Photo Added '{metaContent.Title}'");
            }
        }
    }


    /// <summary>
    ///     This routine tries to return a FileInfo for a filename that is not in use by Photo Content in the Db, matches
    ///     program conventions and is unique in the current directory.
    /// </summary>
    /// <param name="baseFile"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    private static async Task<FileInfo> ToPhotoFileNameNotInDb(FileInfo baseFile)
    {
        var searchContext = await Db.Context();

        var fileExistsInDatabase = await searchContext.PhotoFilenameExistsInDatabase(baseFile.Name, null);
        if (!fileExistsInDatabase) return baseFile;

        var file = new FileInfo(baseFile.FullName);

        var numberLimit = 999;
        var filePostfix = 0;

        while ((file.Exists || fileExistsInDatabase) && filePostfix <= numberLimit)
        {
            numberLimit++;
            filePostfix++;

            var newFileName =
                SlugTools.CreateSlug(false,
                    $"{Path.GetFileNameWithoutExtension(baseFile.Name)}-{filePostfix:000}{baseFile.Extension}");

            fileExistsInDatabase = await searchContext.PhotoFilenameExistsInDatabase(newFileName, null);

            file = new FileInfo(Path.Combine(baseFile.DirectoryName ?? string.Empty,
                newFileName));
        }

        if (!file.Exists)
        {
            baseFile.CopyTo(file.FullName);
            file.Refresh();
            return file;
        }


        var randomPostfixLimit = 50;
        var randomPostfixCounter = 0;
        while ((file.Exists || fileExistsInDatabase) && randomPostfixCounter <= randomPostfixLimit)
        {
            randomPostfixLimit++;
            randomPostfixCounter++;

            var postFix = SlugTools.RandomLowerCaseString(6);

            var newFileName = SlugTools.CreateSlug(false,
                $"{Path.GetFileNameWithoutExtension(baseFile.Name)}-{postFix}{baseFile.Extension}");

            fileExistsInDatabase = await searchContext.PhotoFilenameExistsInDatabase(newFileName, null);

            file = new FileInfo(Path.Combine(baseFile.DirectoryName ?? string.Empty,
                newFileName));
        }

        if (!file.Exists)
        {
            baseFile.CopyTo(file.FullName);
            file.Refresh();
            return file;
        }

        throw new Exception("Can not create a Unique Directory for {fullName}");
    }
}