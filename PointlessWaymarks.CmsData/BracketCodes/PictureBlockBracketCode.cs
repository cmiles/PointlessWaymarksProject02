using HtmlTags;
using PointlessWaymarks.CmsData.CommonHtml;
using PointlessWaymarks.CmsData.Database;
using PointlessWaymarks.CmsData.Database.Models;
using PointlessWaymarks.CommonTools;

namespace PointlessWaymarks.CmsData.BracketCodes;

public static class PictureBlockBracketCode
{
    public const string BracketCodeToken = "pictureblock";

    public static string Create(string innerBracketCodes)
    {
        return $"""
                
                [[{BracketCodeToken}
                {innerBracketCodes}
                ]]
                """;
    }

    private static string CaptionText(dynamic content)
    {
        return content switch
        {
            PhotoContent p => Tags.PhotoCaptionText(p),
            ImageContent i => Tags.ImageCaptionText(i),
            _ => string.Empty
        };
    }

    private static async Task<HtmlTag> PictureItem(dynamic content, int itemNumber, string classPrefix, string sizes)
    {
        if (content.ContentId is not Guid contentId) return HtmlTag.Empty();
        if (content.MainPicture is not Guid mainPictureId) return HtmlTag.Empty();

        var pageUrl = await UserSettingsSingleton.CurrentSettings().ContentUrl(contentId).ConfigureAwait(false);
        var pictureAsset = PictureAssetProcessing.ProcessPictureDirectory(mainPictureId);
        if (pictureAsset == null) return HtmlTag.Empty();

        var itemDiv = new DivTag().AddClasses($"{classPrefix}-item", $"{classPrefix}-item-{itemNumber}");
        var linkTag = new LinkTag(string.Empty, pageUrl);
        linkTag.Children.Add(Tags.PictureImgTag(pictureAsset, sizes, true).AddClass($"{classPrefix}-image"));

        itemDiv.Children.Add(linkTag);

        return itemDiv;
    }

    private static HtmlTag PictureCaption(dynamic content, int itemNumber, string classPrefix)
    {
        var captionText = CaptionText(content) as string;

        if (string.IsNullOrWhiteSpace(captionText)) return HtmlTag.Empty();

        return new HtmlTag("figcaption").AddClasses($"{classPrefix}-caption", $"{classPrefix}-caption-{itemNumber}")
            .Text(captionText.TrimNullToEmpty());
    }

    private static async Task<HtmlTag> TwoPhotoFigure(IReadOnlyList<dynamic> orderedContent)
    {
        var figureTag = new HtmlTag("figure").AddClass("two-pictures-container");

        figureTag.Children.Add(await PictureItem(orderedContent[0], 1, "two-pictures", "(min-width: 640px) 50vw, 100vw")
            .ConfigureAwait(false));
        figureTag.Children.Add(await PictureItem(orderedContent[1], 2, "two-pictures", "(min-width: 640px) 50vw, 100vw")
            .ConfigureAwait(false));
        figureTag.Children.Add(PictureCaption(orderedContent[0], 1, "two-pictures"));
        figureTag.Children.Add(PictureCaption(orderedContent[1], 2, "two-pictures"));

        return figureTag;
    }

    private static async Task<HtmlTag> PictureBlockCell(dynamic content, int itemNumber, string sizes)
    {
        var cellTag = new DivTag().AddClasses("picture-block-cell", $"picture-block-cell-{itemNumber}");
        cellTag.Children.Add(await PictureItem(content, itemNumber, "picture-block", sizes).ConfigureAwait(false));
        cellTag.Children.Add(PictureCaption(content, itemNumber, "picture-block"));

        return cellTag;
    }

    private static async Task<HtmlTag> PictureBlockFigure(IReadOnlyList<dynamic> orderedContent)
    {
        var figureTag = new HtmlTag("figure").AddClass("picture-block-container");

        var firstRow = new DivTag().AddClasses("picture-block-row", "picture-block-row-feature");
        firstRow.Children.Add(await PictureBlockCell(orderedContent[0], 1, "(min-width: 1200px) 100vw, 100vw")
            .ConfigureAwait(false));
        figureTag.Children.Add(firstRow);

        for (var rowStart = 1; rowStart < orderedContent.Count; rowStart += 2)
        {
            var rowNumber = (rowStart + 1) / 2 + 1;
            var rowItemCount = Math.Min(2, orderedContent.Count - rowStart);
            var rowTag = new DivTag().AddClasses("picture-block-row", $"picture-block-row-{rowNumber}");
            if (rowItemCount == 1) rowTag.AddClass("picture-block-row-single");

            for (var i = rowStart; i < rowStart + rowItemCount; i++)
                rowTag.Children.Add(await PictureBlockCell(orderedContent[i], i + 1,
                    "(min-width: 1200px) 50vw, 100vw")
                    .ConfigureAwait(false));

            figureTag.Children.Add(rowTag);
        }

        return figureTag;
    }

    public static async Task<string?> Process(string? toProcess, IProgress<string>? progress = null)
    {
        if (string.IsNullOrWhiteSpace(toProcess)) return string.Empty;

        progress?.Report("Searching for Picture Block Codes");

        var resultList = BracketCodeCommon.ContentGalleryBracketCodeMatches(toProcess, BracketCodeToken);

        if (!resultList.Any()) return toProcess;

        var db = await Db.Context().ConfigureAwait(false);

        foreach (var loopMatch in resultList)
        {
            var content = (await db.ContentFromContentIds(loopMatch.contentGuid, false).ConfigureAwait(false))
                .Where(x => DynamicTypeTools.PropertyExists(x, "MainPicture"))
                .Where(x => x.MainPicture is Guid)
                .ToList();

            var contentById = content.Where(x => DynamicTypeTools.PropertyExists(x, "ContentId"))
                .Where(x => x.ContentId is Guid)
                .ToDictionary(x => (Guid)x.ContentId);

            var orderedContent = loopMatch.contentGuid.Where(contentById.ContainsKey).Select(x => contentById[x])
                .ToList();

            if (orderedContent.Count < 2) continue;

            var figureTag = orderedContent.Count == 2
                ? await TwoPhotoFigure(orderedContent).ConfigureAwait(false)
                : await PictureBlockFigure(orderedContent).ConfigureAwait(false);

            toProcess = toProcess.Replace(loopMatch.bracketCodeText, figureTag.ToString());

            progress?.Report("Picture Block Code processed");
        }

        return toProcess;
    }
}
