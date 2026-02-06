using PointlessWaymarks.CmsData.BracketCodes;
using PointlessWaymarks.CmsData.Database.Models;
using PointlessWaymarks.CmsWpfControls.ContentList;
using PointlessWaymarks.WpfCommon.Status;

namespace PointlessWaymarks.CmsWpfControls.TrailList;

public static class TrailActions
{
    public static async Task BracketCodesToClipboard(List<TrailContent> contents,
        StatusControlContext statusContext)
    {
        var codeList = contents.Select(BracketCodeTrails.Create).ToList();
        var finalString = string.Join(Environment.NewLine, codeList);

        await TextAndContentRepresentationToClipboard(contents, finalString, statusContext);
    }

    public static string DefaultBracketCode(TrailContent? content)
    {
        return content is null ? string.Empty : $"{BracketCodeTrails.Create(content)}";
    }

    public static async Task DefaultBracketCodesToClipboard(List<TrailContent> contents,
        StatusControlContext statusContext)
    {
        var codeList = contents.Select(BracketCodeTrails.Create).ToList();
        var finalString = string.Join(Environment.NewLine, codeList);

        await TextAndContentRepresentationToClipboard(contents, finalString, statusContext);
    }

    public static async Task ImageBracketCodesToClipboard(List<TrailContent> contents,
        StatusControlContext statusContext)
    {
        var codeList = contents.Select(BracketCodeTrailImageLink.Create).ToList();
        var finalString = string.Join(Environment.NewLine, codeList);

        await TextAndContentRepresentationToClipboard(contents, finalString, statusContext);
    }

    private static async Task TextAndContentRepresentationToClipboard(List<TrailContent> contents, string finalString,
        StatusControlContext statusContext)
    {
        await ContentClipboardRepresentation.TextAndContentRepresentationToClipboard(
            contents.Cast<IContentCommon>().ToList(), finalString, statusContext);
    }

    public static async Task TextStatsBracketCodesToClipboard(List<TrailContent> contents,
        StatusControlContext statusContext)
    {
        var codeList = contents.Select(BracketCodeTrailTextStats.Create).ToList();
        var finalString = string.Join(Environment.NewLine, codeList);

        await TextAndContentRepresentationToClipboard(contents, finalString, statusContext);
    }


    public static async Task TextStatsExtendedBracketCodesToClipboard(List<TrailContent> contents,
        StatusControlContext statusContext)
    {
        var codeList = contents.Select(BracketCodeTrailTextStats.CreateExtended).ToList();
        var finalString = string.Join(Environment.NewLine, codeList);

        await TextAndContentRepresentationToClipboard(contents, finalString, statusContext);
    }
}