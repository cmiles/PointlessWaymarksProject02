using PointlessWaymarks.CmsData.BracketCodes;
using PointlessWaymarks.CmsData.Database.Models;
using PointlessWaymarks.CmsWpfControls.ContentList;
using PointlessWaymarks.WpfCommon.Status;
using System.Linq;

namespace PointlessWaymarks.CmsWpfControls.PostList;

public static class PostActions
{
    public static async Task BracketCodesToClipboard(List<PostContent> contents,
        StatusControlContext statusContext)
    {
        var codeList = contents.Select(BracketCodePosts.Create).ToList();
        var finalString = string.Join(Environment.NewLine, codeList);

        await TextAndContentRepresentationToClipboard(contents, finalString, statusContext);
    }

    public static string DefaultBracketCode(PostContent? content)
    {
        return content is null ? string.Empty : $"{BracketCodePosts.Create(content)}";
    }

    public static async Task DefaultBracketCodesToClipboard(List<PostContent> contents,
        StatusControlContext statusContext)
    {
        var codeList = contents.Select(BracketCodePosts.Create).ToList();
        var finalString = string.Join(Environment.NewLine, codeList);

        await TextAndContentRepresentationToClipboard(contents, finalString, statusContext);
    }

    public static async Task ImageBracketCodesToClipboard(List<PostContent> contents,
        StatusControlContext statusContext)
    {
        var codeList = contents.Select(BracketCodePostImageLink.Create).ToList();
        var finalString = string.Join(Environment.NewLine, codeList);

        await TextAndContentRepresentationToClipboard(contents, finalString, statusContext);
    }

    public static async Task TextAndContentRepresentationToClipboard(List<PostContent> contents,
        string clipboardString, StatusControlContext statusContext)
    {
        await ContentClipboardRepresentation.TextAndContentRepresentationToClipboard(
            contents.Cast<IContentCommon>().ToList(), clipboardString, statusContext);
    }
}