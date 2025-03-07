using HtmlTags;
using Microsoft.EntityFrameworkCore;
using PointlessWaymarks.CmsData.ContentHtml.PointHtml;
using PointlessWaymarks.CmsData.Database;
using PointlessWaymarks.CmsData.Database.Models;

namespace PointlessWaymarks.CmsData.BracketCodes;

public static class BracketCodePointExternalDirectionLinks
{
    public const string BracketCodeToken = "pointexternaldirectionslink";
    
    public static string Create(PointContent content)
    {
        return $"{{{{{BracketCodeToken} {content.ContentId}; {content.Title}}}}}";
    }
    
    public static async Task<List<PointContent>> DbContentFromBracketCodes(string? toProcess,
        IProgress<string>? progress = null)
    {
        if (string.IsNullOrWhiteSpace(toProcess)) return [];
        
        progress?.Report("Searching for Point External Directions Link Codes...");
        
        var guidList = BracketCodeCommon.ContentBracketCodeMatches(toProcess, BracketCodeToken)
            .Select(x => x.contentGuid).Distinct().ToList();
        
        var returnList = new List<PointContent>();
        
        if (!guidList.Any()) return returnList;
        
        var context = await Db.Context().ConfigureAwait(false);
        
        foreach (var loopMatch in guidList)
        {
            var dbContent = await context.PointContents.FirstOrDefaultAsync(x => x.ContentId == loopMatch)
                .ConfigureAwait(false);
            if (dbContent == null) continue;
            
            progress?.Report($"Point External Directions Link Code - Adding DbContent For {dbContent.Title}");
            
            returnList.Add(dbContent);
        }
        
        return returnList;
    }
    
    public static async Task<string?> Process(string? toProcess, IProgress<string>? progress = null)
    {
        if (string.IsNullOrWhiteSpace(toProcess)) return string.Empty;
        
        progress?.Report("Searching for Point External Directions Link Codes...");
        
        var resultList = BracketCodeCommon.ContentBracketCodeMatches(toProcess, BracketCodeToken);
        
        if (!resultList.Any()) return toProcess;
        
        var context = await Db.Context().ConfigureAwait(false);
        
        foreach (var loopMatch in resultList)
        {
            var dbContent =
                await context.PointContents.FirstOrDefaultAsync(x => x.ContentId == loopMatch.contentGuid)
                    .ConfigureAwait(false);
            if (dbContent == null) continue;
            
            progress?.Report($"Adding Point External Directions Link {dbContent.Title} from Code");
            
            var linkTag =
                new LinkTag(
                    string.IsNullOrWhiteSpace(loopMatch.displayText)
                        ? $"{dbContent.Title} - Google Maps Directions"
                        : loopMatch.displayText.Trim(),
                    PointParts.GoogleMapsDirectionsToLatLongUrl(dbContent), "point-external-directions-link");
            
            toProcess = toProcess.Replace(loopMatch.bracketCodeText, linkTag.ToString());
        }
        
        return toProcess;
    }
}