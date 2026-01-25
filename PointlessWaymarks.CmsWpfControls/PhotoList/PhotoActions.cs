using PointlessWaymarks.CmsData;
using PointlessWaymarks.CmsData.BracketCodes;
using PointlessWaymarks.CmsData.Database.Models;
using PointlessWaymarks.CmsWpfControls.ContentList;
using PointlessWaymarks.CommonTools;
using PointlessWaymarks.LlamaAspects;
using PointlessWaymarks.WpfCommon;
using PointlessWaymarks.WpfCommon.FileMetadataDisplay;
using PointlessWaymarks.WpfCommon.Status;
using PointlessWaymarks.WpfCommon.Utility;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;

namespace PointlessWaymarks.CmsWpfControls.PhotoList
{
    public static class PhotoActions
    {
        public static async Task BracketCodesToClipboard(List<PhotoContent> contents,
        StatusControlContext statusContext)
        {
            var finalString = contents.Aggregate(string.Empty,
                (current, loopSelected) =>
                    current + $"{BracketCodePhotos.Create(loopSelected)}{Environment.NewLine}");

            await TextAndContentRepresentationToClipboard(contents, finalString, statusContext);
        }

        public static string DefaultBracketCode(PhotoContent? content)
        {
            return content is null ? string.Empty : $"{BracketCodePhotos.Create(content)}";
        }

        public static async Task DefaultBracketCodesToClipboard(List<PhotoContent> contents,
            StatusControlContext statusContext)
        {
            var finalString = contents.Aggregate(string.Empty,
                (current, loopSelected) =>
                    current + $"{BracketCodePhotos.Create(loopSelected)}{Environment.NewLine}");

            await TextAndContentRepresentationToClipboard(contents, finalString, statusContext);
        }

        public static async Task TextBracketCodesToClipboard(List<PhotoContent> contents,
            StatusControlContext statusContext)
        {
            var finalString = contents.Aggregate(string.Empty,
                (current, loopSelected) =>
                    current + $"{BracketCodePhotoLinks.Create(loopSelected)}{Environment.NewLine}");

            await TextAndContentRepresentationToClipboard(contents, finalString, statusContext);
        }

        public static async Task ImageWithDetailsBracketCodesToClipboard(List<PhotoContent> contents,
            StatusControlContext statusContext)
        {
            var finalString = contents.Aggregate(string.Empty,
                (current, loopSelected) =>
                    current + $"{BracketCodePhotosWithDetails.Create(loopSelected)}{Environment.NewLine}");

            await TextAndContentRepresentationToClipboard(contents, finalString, statusContext);
        }

        public static async Task DailyPhotoPageBracketCodesToClipboard(List<PhotoContent> contents,
            StatusControlContext statusContext)
        {
            var finalString = contents.Aggregate(string.Empty,
                (current, loopSelected) =>
                    current + $"{BracketCodeDailyPhotoPage.Create(loopSelected).Distinct()}{Environment.NewLine}");

            await TextAndContentRepresentationToClipboard(contents, finalString, statusContext);
        }

        public static async Task ShowInPeakFinderWeb(PhotoContent contents,
            StatusControlContext statusContext)
        {
            await ThreadSwitcher.ResumeBackgroundAsync();

            var validLocation = await contents.HasValidLocation();

            if (!validLocation)
            {
                await statusContext.ToastError("No Valid Location Data?");
                return;
            }

            var bearing = contents.PhotoDirection;

            await ThreadSwitcher.ResumeForegroundAsync();

            var peakFinderUrl =
                $"https://www.peakfinder.com/?lat={contents.Latitude:F5}&lng={contents.Longitude:F5}{(bearing is null ? "" : $"&azi={bearing.Value:F0}")}";

            ProcessHelpers.OpenUrlInExternalBrowser(peakFinderUrl);
        }

        public static async Task ShowInGoogleMapsWeb(PhotoContent contents,
            StatusControlContext statusContext)
        {
            await ThreadSwitcher.ResumeBackgroundAsync();
            var validLocation = await contents.HasValidLocation();
            if (!validLocation)
            {
                await statusContext.ToastError("No Valid Location Data?");
                return;
            }

            var mapUrl =
                $"https://www.google.com/maps/search/?api=1&query={contents.Latitude:F5},{contents.Longitude:F5}";

            await ThreadSwitcher.ResumeForegroundAsync();
            ProcessHelpers.OpenUrlInExternalBrowser(mapUrl);
        }

        public static async Task ShowInOsmCycleMap(PhotoContent contents,
            StatusControlContext statusContext)
        {
            await ThreadSwitcher.ResumeBackgroundAsync();
            var validLocation = await contents.HasValidLocation();
            if (!validLocation)
            {
                await statusContext.ToastError("No Valid Location Data?");
                return;
            }

            var mapUrl =
                $"http://www.openstreetmap.org/?mlat={contents.Latitude:F5}&mlon={contents.Longitude:F5}&zoom=13&layers=C";

            await ThreadSwitcher.ResumeForegroundAsync();
            ProcessHelpers.OpenUrlInExternalBrowser(mapUrl);
        }

        public static async Task ReportPhotoMetadata(List<PhotoContent> contents,
            StatusControlContext statusContext)
        {
            await ThreadSwitcher.ResumeBackgroundAsync();

            if (contents.Count < 1)
            {
                await statusContext.ToastError("Nothing Selected?");
                return;
            }

            var settings = UserSettingsSingleton.CurrentSettings();

            var errorCount = 0;

            await ThreadSwitcher.ResumeForegroundAsync();

            foreach (var loopContents in contents)
            {
                try
                {
                    var archiveFile = settings.LocalMediaArchivePhotoContentFile(loopContents);

                    if (archiveFile is null || !archiveFile.Exists)
                    {
                        Log.ForContext("loopContent", loopContents.SafeObjectDump()).ForContext("archiveFile", archiveFile?.SafeObjectDump() ?? "null").Error("Photo Content with Invalid Original File Detected");
                        errorCount++;
                        continue;
                    }

                    var metadataWindow = await FileMetadataDisplayWindow.CreateInstance(archiveFile.FullName,
                        UserSettingsSingleton.CurrentSettings().FfprobeExe());
                    await metadataWindow.PositionWindowAndShowOnUiThread();
                }
                catch (Exception e)
                {
                    Log.ForContext("loopContent", loopContents.SafeObjectDump()).Error(e, "Photo Content Metadata Report Error");
                    errorCount++;
                }
            }

            await ThreadSwitcher.ResumeBackgroundAsync();

            if(errorCount > 0)
            {
                await statusContext.ToastWarning($"Photo Metadata Report Completed with {errorCount} errors.");
            }
            else
            {
                await statusContext.ToastSuccess("Photo Metadata Report Completed.");
            }
        }

        public static async Task TextAndContentRepresentationToClipboard(List<PhotoContent> contents,
            string clipboardString, StatusControlContext statusContext)
        {
            await ThreadSwitcher.ResumeBackgroundAsync();

            if (contents.Count < 1)
            {
                await statusContext.ToastError("Nothing Selected?");
                return;
            }

            try
            {
                // Get the ContentClipboardRepresentation from ClipboardObject
                var clipboardRepresentation =
                    ContentClipboardRepresentation.ClipboardObject(contents.Cast<IContentCommon>().ToList());

                // Create a DataObject for multiple clipboard formats
                var dataObject = new DataObject();

                // Add the plain text format for compatibility
                dataObject.SetText(clipboardString);

                // Add the ContentClipboardRepresentation as an alternate format
                // Using the ContentClipboardFormat constant as the format name
                var clipboardJson = JsonSerializer.Serialize(clipboardRepresentation);
                dataObject.SetData(ContentClipboardRepresentation.ContentClipboardFormat, clipboardJson);

                await ThreadSwitcher.ResumeForegroundAsync();

                // Set the clipboard with multiple formats
                Clipboard.SetDataObject(dataObject, true);

                await statusContext.ToastSuccess($"To Clipboard {clipboardString.TruncateWithEllipses(100)}");
            }
            catch (Exception ex)
            {
                // Fallback to simple text if the rich format fails
                await ThreadSwitcher.ResumeForegroundAsync();
                Clipboard.SetText(clipboardString);
                await statusContext.ToastWarning($"Simple text copied - rich format failed: {ex.Message}");
            }
        }
    }
}
