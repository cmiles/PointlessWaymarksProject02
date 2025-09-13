using Microsoft.EntityFrameworkCore;
using PointlessWaymarks.CmsData.CommonHtml;
using PointlessWaymarks.CmsData.Database;
using PointlessWaymarks.CmsData.Database.Models;

namespace PointlessWaymarks.CmsData.BracketCodes;

public static class BracketCodeTrailImageLink
{
    public const string BracketCodeToken = "trailimagelink";

    public static string Create(TrailContent content)
    {
        return $"{{{{{BracketCodeToken} {content.ContentId}; {content.Title}}}}}";
    }

    public static async Task<List<TrailContent>> DbContentFromBracketCodes(string? toProcess,
        IProgress<string>? progress = null)
    {
        if (string.IsNullOrWhiteSpace(toProcess)) return [];

        progress?.Report("Searching for Trail Content Codes...");

        var resultList = BracketCodeCommon.ContentBracketCodeMatches(toProcess, BracketCodeToken)
            .Select(x => x.contentGuid).Distinct().ToList();

        var returnList = new List<TrailContent>();

        if (!resultList.Any()) return returnList;

        foreach (var loopGuid in resultList)
        {
            var context = await Db.Context().ConfigureAwait(false);

            var dbContent = await context.TrailContents.FirstOrDefaultAsync(x => x.ContentId == loopGuid)
                .ConfigureAwait(false);
            if (dbContent == null) continue;

            progress?.Report($"Trail Image Code - Adding DbContent For {dbContent.Title}");

            returnList.Add(dbContent);
        }

        return returnList;
    }

    /// <summary>
    ///     Processes {{trail guid;human_identifier}} with a specified function - best use may be for easily building
    ///     library code.
    /// </summary>
    /// <param name="toProcess"></param>
    /// <param name="pageConversion"></param>
    /// <param name="progress"></param>
    /// <returns></returns>
    private static async Task<string?> Process(string? toProcess,
        Func<(PictureSiteInformation pictureInfo, string linkUrl), string> pageConversion,
        IProgress<string>? progress = null)
    {
        if (string.IsNullOrWhiteSpace(toProcess)) return string.Empty;

        progress?.Report("Searching for Trail Image Link Codes");

        var resultList = BracketCodeCommon.ContentBracketCodeMatches(toProcess, BracketCodeToken);

        if (!resultList.Any()) return toProcess;

        var context = await Db.Context().ConfigureAwait(false);

        foreach (var loopMatch in resultList)
        {
            var dbTrail = await context.TrailContents.FirstOrDefaultAsync(x => x.ContentId == loopMatch.contentGuid)
                .ConfigureAwait(false);

            if (dbTrail == null) continue;
            if (dbTrail.MainPicture == null)
            {
                progress?.Report(
                    $"Trail Image Link without Main Image - converting to trail - Trail: {dbTrail.Title}");

                var newBracketCodeText = loopMatch.bracketCodeText.Replace(BracketCodeToken,
                    BracketCodeTrails.BracketCodeToken,
                    StringComparison.OrdinalIgnoreCase);

                toProcess = toProcess.Replace(loopMatch.bracketCodeText, newBracketCodeText);

                await BracketCodeTrails.Process(toProcess).ConfigureAwait(false);

                continue;
            }

            var dbPicture = new PictureSiteInformation(dbTrail.MainPicture.Value);

            if (dbPicture.Pictures == null)
            {
                progress?.Report(
                    $"Trail Image Link with Null PictureSiteInformation - converting to trail - Trail: {dbTrail.Title}");

                var newBracketCodeText = loopMatch.bracketCodeText.Replace(BracketCodeToken,
                    BracketCodeTrails.BracketCodeToken,
                    StringComparison.OrdinalIgnoreCase);

                toProcess = toProcess.Replace(loopMatch.bracketCodeText, newBracketCodeText);

                await BracketCodeTrails.Process(toProcess).ConfigureAwait(false);

                continue;
            }

            var conversion = pageConversion((dbPicture, UserSettingsSingleton.CurrentSettings().TrailPageUrl(dbTrail)));

            if (string.IsNullOrWhiteSpace(conversion))
            {
                progress?.Report(
                    $"Trail Image Link - converting to trail - Trail: {dbTrail.Title}");

                var newBracketCodeText = loopMatch.bracketCodeText.Replace(BracketCodeToken,
                    BracketCodeTrails.BracketCodeToken,
                    StringComparison.OrdinalIgnoreCase);

                toProcess = toProcess.Replace(loopMatch.bracketCodeText, newBracketCodeText);

                await BracketCodeTrails.Process(toProcess).ConfigureAwait(false);

                continue;
            }

            toProcess = toProcess.Replace(loopMatch.bracketCodeText, conversion);

            progress?.Report($"Trail Image Link {dbTrail.Title} processed");
        }

        return toProcess;
    }

    /// <summary>
    ///     This method processes a trailimagelink code for use in email.
    /// </summary>
    /// <param name="toProcess"></param>
    /// <param name="progress"></param>
    /// <returns></returns>
    public static async Task<string> ProcessForEmail(string? toProcess, IProgress<string>? progress = null)
    {
        return await Process(toProcess,
            pictureInfo => pictureInfo.pictureInfo.EmailPictureTableTag().ToString() ?? string.Empty,
            progress).ConfigureAwait(false) ?? string.Empty;
    }

    /// <summary>
    ///     Processes {{trail guid;human_identifier}} into figure html with a link to the trail page.
    /// </summary>
    /// <param name="toProcess"></param>
    /// <param name="progress"></param>
    /// <returns></returns>
    public static async Task<string?> ProcessToFigureWithLink(string? toProcess, IProgress<string>? progress = null)
    {
        return await Process(toProcess,
            pictureInfo => pictureInfo.pictureInfo.PictureFigureWithCaptionAndLinkTag("100vw", pictureInfo.linkUrl)
                .ToString() ?? string.Empty, progress).ConfigureAwait(false);
    }
}