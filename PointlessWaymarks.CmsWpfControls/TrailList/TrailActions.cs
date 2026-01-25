using System.Text.Json;
using System.Windows;
using Microsoft.Office.Interop.Excel;
using PointlessWaymarks.CmsData;
using PointlessWaymarks.CmsData.BracketCodes;
using PointlessWaymarks.CmsData.Database;
using PointlessWaymarks.CmsData.Database.Models;
using PointlessWaymarks.CmsData.Server;
using PointlessWaymarks.CmsWpfControls.ContentList;
using PointlessWaymarks.CommonTools;
using PointlessWaymarks.WpfCommon;
using PointlessWaymarks.WpfCommon.Status;

namespace PointlessWaymarks.CmsWpfControls.TrailList;

public static class TrailActions
{
    public static async Task BracketCodesToClipboard(List<TrailContent> contents,
        StatusControlContext statusContext)
    {
        var finalString = contents.Aggregate(string.Empty,
            (current, loopSelected) =>
                current + $"{BracketCodeTrails.Create(loopSelected)}{Environment.NewLine}");

        await TextAndContentRepresentationToClipboard(contents, finalString, statusContext);
    }

    public static string DefaultBracketCode(TrailContent? content)
    {
        return content is null ? string.Empty : $"{BracketCodeTrails.Create(content)}";
    }

    public static async Task DefaultBracketCodesToClipboard(List<TrailContent> contents,
        StatusControlContext statusContext)
    {
        var finalString = contents.Aggregate(string.Empty,
            (current, loopSelected) =>
                current + $"{BracketCodeTrails.Create(loopSelected)}{Environment.NewLine}");

        await TextAndContentRepresentationToClipboard(contents, finalString, statusContext);
    }

    public static async Task ImageBracketCodesToClipboard(List<TrailContent> contents,
        StatusControlContext statusContext)
    {
        var finalString = contents.Aggregate(string.Empty,
            (current, loopSelected) =>
                current + $"{BracketCodeTrailImageLink.Create(loopSelected)}{Environment.NewLine}");

        await TextAndContentRepresentationToClipboard(contents, finalString, statusContext);
    }

    public static async Task TextAndContentRepresentationToClipboard(List<TrailContent> contents,
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
            var clipboardRepresentation = ContentClipboardRepresentation.ClipboardObject(contents.Cast<IContentCommon>().ToList());

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


    public static async Task TextStatsBracketCodesToClipboard(List<TrailContent> contents,
        StatusControlContext statusContext)
    {
        var finalString = contents.Aggregate(string.Empty,
            (current, loopSelected) =>
                current + $"{BracketCodeTrailTextStats.Create(loopSelected)}{Environment.NewLine}");

        await TextAndContentRepresentationToClipboard(contents, finalString, statusContext);
    }


    public static async Task TextStatsExtendedBracketCodesToClipboard(List<TrailContent> contents,
        StatusControlContext statusContext)
    {
        var finalString = contents.Aggregate(string.Empty,
            (current, loopSelected) =>
                current + $"{BracketCodeTrailTextStats.CreateExtended(loopSelected)}{Environment.NewLine}");

        await TextAndContentRepresentationToClipboard(contents, finalString, statusContext);
    }
}