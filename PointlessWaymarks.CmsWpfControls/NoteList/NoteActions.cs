using PointlessWaymarks.CmsData.BracketCodes;
using PointlessWaymarks.CmsData.Database.Models;
using PointlessWaymarks.CmsWpfControls.ContentList;
using PointlessWaymarks.WpfCommon.Status;

namespace PointlessWaymarks.CmsWpfControls.NoteList;

public static class NoteActions
{
    public static string DefaultBracketCode(NoteContent? content)
    {
        return content is null ? string.Empty : $"{BracketCodeNotes.Create(content)}";
    }

    public static async Task DefaultBracketCodesToClipboard(List<NoteContent> contents,
        StatusControlContext statusContext)
    {
        var codeList = contents.Select(BracketCodeNotes.Create).ToList();
        var finalString = string.Join(Environment.NewLine, codeList);

        await TextAndContentRepresentationToClipboard(contents, finalString, statusContext);
    }

    private static async Task TextAndContentRepresentationToClipboard(List<NoteContent> contents,
        string clipboardString, StatusControlContext statusContext)
    {
        await ContentClipboardRepresentation.TextAndContentRepresentationToClipboard(
            contents.Cast<IContentCommon>().ToList(), clipboardString, statusContext);
    }
}