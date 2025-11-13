using AngleSharp;
using Microsoft.Playwright;
using pinboard.net;
using pinboard.net.Models;
using PointlessWaymarks.CmsData.ContentHtml.LinkListHtml;
using PointlessWaymarks.CmsData.Database;
using PointlessWaymarks.CmsData.Database.Models;

namespace PointlessWaymarks.CmsData.ContentGeneration;

public static class LinkGenerator
{
    public static async Task GenerateHtmlAndJson(DateTime? generationVersion, IProgress<string>? progress = null)
    {
        progress?.Report("Link Content - Generate HTML");

        var htmlContext = new LinkListPage { GenerationVersion = generationVersion };

        await htmlContext.WriteLocalHtmlRssAndJson().ConfigureAwait(false);
    }

    // Tries AngleSharp first (with timeout), then falls back to Playwright (with timeout).
    // Defaults: AngleSharp 10s, Playwright 30s.
    public static async Task<(GenerationReturn generationReturn, LinkMetadata? metadata)> LinkMetadataFromUrlBestEffort(
        string url,
        IProgress<string>? progress = null,
        TimeSpan? angleSharpTimeout = null,
        TimeSpan? playwrightTimeout = null)
    {
        if (string.IsNullOrWhiteSpace(url)) return (GenerationReturn.Error("No URL?"), null);

        angleSharpTimeout ??= TimeSpan.FromSeconds(10);
        playwrightTimeout ??= TimeSpan.FromSeconds(30);

        // Try AngleSharp first
        try
        {
            progress?.Report($"Metadata: Trying AngleSharp (timeout {angleSharpTimeout.Value.TotalSeconds:N0}s)...");
            using (var cts = new CancellationTokenSource(angleSharpTimeout.Value))
            {
                var angleTask = Task.Run(() => LinkMetadataFromUrlWithAngleSharp(url, progress), cts.Token);

                try
                {
                    var angleResult = await angleTask.ConfigureAwait(false);
                    if (angleResult is { metadata: not null, generationReturn.HasError: false })
                    {
                        progress?.Report("Metadata: AngleSharp succeeded.");
                        return angleResult;
                    }

                    progress?.Report(
                        "Metadata: AngleSharp completed without usable metadata, falling back to Playwright...");
                }
                catch (OperationCanceledException)
                {
                    progress?.Report("Metadata: AngleSharp timed out, falling back to Playwright...");
                }
            }
        }
        catch (Exception ex)
        {
            progress?.Report($"Metadata: AngleSharp threw an exception: {ex.Message}. Falling back to Playwright...");
        }

        // Fallback to Playwright
        try
        {
            progress?.Report($"Metadata: Trying Playwright (timeout {playwrightTimeout.Value.TotalSeconds:N0}s)...");
            using (var cts = new CancellationTokenSource(playwrightTimeout.Value))
            {
                var pwTask = Task.Run(() => LinkMetadataFromUrlWithPlaywright(url, progress), cts.Token);

                try
                {
                    var pwResult = await pwTask.ConfigureAwait(false);
                    if (pwResult is { metadata: not null, generationReturn.HasError: false })
                    {
                        progress?.Report("Metadata: Playwright succeeded.");
                        return pwResult;
                    }

                    progress?.Report("Metadata: Playwright completed but did not return usable metadata.");
                    return pwResult; // Return whatever Playwright reported (likely an error)
                }
                catch (OperationCanceledException)
                {
                    progress?.Report("Metadata: Playwright timed out.");
                    return (
                        GenerationReturn.Error(
                            $"Timed out obtaining metadata for {url} (AngleSharp {angleSharpTimeout}, Playwright {playwrightTimeout})."),
                        null);
                }
            }
        }
        catch (Exception ex)
        {
            return (GenerationReturn.Error($"Playwright metadata parse failed for {url}: {ex.Message}"), null);
        }
    }

    public static async Task<(GenerationReturn generationReturn, LinkMetadata? metadata)>
        LinkMetadataFromUrlWithAngleSharp(
            string url, IProgress<string>? progress = null)
    {
        if (string.IsNullOrWhiteSpace(url)) return (GenerationReturn.Error("No URL?"), null);

        progress?.Report("Setting up and Downloading Site");

        var toReturn = new LinkMetadata();

        var config = Configuration.Default.WithDefaultLoader().WithJs();
        var context = BrowsingContext.New(config);
        var document = await context.OpenAsync(url).ConfigureAwait(false);

        progress?.Report("Looking for Title");

        var titleString = document.Head?.Children.FirstOrDefault(x => x.TagName == "TITLE")?.TextContent;

        if (string.IsNullOrWhiteSpace(titleString))
            titleString = document.QuerySelector("meta[property='og:title']")?.Attributes
                .FirstOrDefault(x => x.LocalName == "content")?.Value;

        if (string.IsNullOrWhiteSpace(titleString))
            titleString = document.QuerySelector("meta[name='DC.title']")?.Attributes
                .FirstOrDefault(x => x.LocalName == "content")?.Value;

        if (string.IsNullOrWhiteSpace(titleString))
            titleString = document.QuerySelector("meta[name='twitter:title']")?.Attributes
                .FirstOrDefault(x => x.LocalName == "value")?.Value;

        if (!string.IsNullOrWhiteSpace(titleString)) toReturn.Title = titleString;

        progress?.Report("Looking for Author");

        var authorString = document.QuerySelector("meta[property='og:author']")?.Attributes
            .FirstOrDefault(x => x.LocalName == "content")?.Value;

        if (string.IsNullOrWhiteSpace(authorString))
            authorString = document.QuerySelector("meta[name='DC.contributor']")?.Attributes
                .FirstOrDefault(x => x.LocalName == "content")?.Value;

        if (string.IsNullOrWhiteSpace(authorString))
            authorString = document.QuerySelector("meta[property='article:author']")?.Attributes
                .FirstOrDefault(x => x.LocalName == "content")?.Value;

        if (string.IsNullOrWhiteSpace(authorString))
            authorString = document.QuerySelector("meta[name='author']")?.Attributes
                .FirstOrDefault(x => x.LocalName == "content")?.Value;

        if (string.IsNullOrWhiteSpace(authorString))
            authorString = document.QuerySelector("a[rel~=\"author\"]")?.TextContent;

        if (string.IsNullOrWhiteSpace(authorString))
            authorString = document.QuerySelector(".author__name")?.TextContent;

        if (string.IsNullOrWhiteSpace(authorString))
            authorString = document.QuerySelector(".author_name")?.TextContent;

        if (!string.IsNullOrWhiteSpace(authorString)) toReturn.Author = authorString;

        progress?.Report($"Looking for Author - Found {toReturn.Author}");


        progress?.Report("Looking for Date Time");

        var linkDateString = document.QuerySelector("meta[property='article:modified_time']")?.Attributes
            .FirstOrDefault(x => x.LocalName == "content")?.Value;

        if (string.IsNullOrWhiteSpace(linkDateString))
            linkDateString = document.QuerySelector("meta[property='og:updated_time']")?.Attributes
                .FirstOrDefault(x => x.LocalName == "content")?.Value;

        if (string.IsNullOrWhiteSpace(linkDateString))
            linkDateString = document.QuerySelector("meta[property='article:published_time']")?.Attributes
                .FirstOrDefault(x => x.LocalName == "content")?.Value;

        if (string.IsNullOrWhiteSpace(linkDateString))
            linkDateString = document.QuerySelector("meta[property='article:published_time']")?.Attributes
                .FirstOrDefault(x => x.LocalName == "content")?.Value;

        if (string.IsNullOrWhiteSpace(linkDateString))
            linkDateString = document.QuerySelector("meta[name='DC.date.created']")?.Attributes
                .FirstOrDefault(x => x.LocalName == "content")?.Value;

        progress?.Report($"Looking for Date Time - Found {linkDateString}");

        if (!string.IsNullOrWhiteSpace(linkDateString))
        {
            if (DateTime.TryParse(linkDateString, out var parsedDateTime))
            {
                toReturn.LinkDate = parsedDateTime;
                progress?.Report($"Looking for Date Time - Parsed to {parsedDateTime}");
            }
            else
            {
                progress?.Report("Did not parse Date Time");
            }
        }

        progress?.Report("Looking for Site Name");

        var siteString = document.QuerySelector("meta[property='og:site_name']")?.Attributes
            .FirstOrDefault(x => x.LocalName == "content")?.Value;

        if (string.IsNullOrWhiteSpace(siteString))
            siteString = document.QuerySelector("meta[name='DC.publisher']")?.Attributes
                .FirstOrDefault(x => x.LocalName == "content")?.Value;

        if (string.IsNullOrWhiteSpace(siteString))
            siteString = document.QuerySelector("meta[name='twitter:site']")?.Attributes
                .FirstOrDefault(x => x.LocalName == "value")?.Value.Replace("@", "");

        if (!string.IsNullOrWhiteSpace(siteString)) toReturn.Site = siteString;

        progress?.Report($"Looking for Site Name - Found {toReturn.Site}");

        progress?.Report("Looking for Description");

        var descriptionString = document.QuerySelector("meta[name='description']")?.Attributes
            .FirstOrDefault(x => x.LocalName == "content")?.Value;

        if (string.IsNullOrWhiteSpace(descriptionString))
            descriptionString = document.QuerySelector("meta[property='og:description']")?.Attributes
                .FirstOrDefault(x => x.LocalName == "content")?.Value;

        if (string.IsNullOrWhiteSpace(descriptionString))
            descriptionString = document.QuerySelector("meta[name='twitter:description']")?.Attributes
                .FirstOrDefault(x => x.LocalName == "content")?.Value;

        if (!string.IsNullOrWhiteSpace(descriptionString)) toReturn.Description = descriptionString;

        progress?.Report($"Looking for Description - Found {toReturn.Description}");

        return (GenerationReturn.Success($"Parsed URL Metadata for {url} without error"), toReturn);
    }

    // New: Playwright-based version (handles JS-heavy sites)
    public static async Task<(GenerationReturn generationReturn, LinkMetadata? metadata)>
        LinkMetadataFromUrlWithPlaywright(
            string url, IProgress<string>? progress = null)
    {
        if (string.IsNullOrWhiteSpace(url)) return (GenerationReturn.Error("No URL?"), null);

        try
        {
            progress?.Report("Playwright: Launching browser");

            using var playwright = await Playwright.CreateAsync().ConfigureAwait(false);
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true
            }).ConfigureAwait(false);

            var page = await browser.NewPageAsync().ConfigureAwait(false);

            progress?.Report($"Playwright: Navigating to {url}");
            try
            {
                await page.GotoAsync(url,
                        new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 60000 })
                    .ConfigureAwait(false);

                // Try to wait for network to be quiet, but don't fail the whole operation if it never settles.
                try
                {
                    await page.WaitForLoadStateAsync(LoadState.NetworkIdle,
                            new PageWaitForLoadStateOptions { Timeout = 15000 })
                        .ConfigureAwait(false);
                }
                catch
                {
                    progress?.Report("Playwright: Network idle not reached within timeout; continuing");
                }
            }
            catch (TimeoutException tex)
            {
                progress?.Report($"Playwright: Timeout navigating to {url} - {tex.Message}");
            }

            var result = new LinkMetadata();

            // Helpers
            static async Task<string?> FirstAttr(IPage p, string selector, string attr)
            {
                try
                {
                    var v = await p.Locator(selector).First.GetAttributeAsync(attr).ConfigureAwait(false);
                    return string.IsNullOrWhiteSpace(v) ? null : v.Trim();
                }
                catch
                {
                    return null;
                }
            }

            static async Task<string?> FirstText(IPage p, string selector)
            {
                try
                {
                    var loc = p.Locator(selector);
                    if (await loc.CountAsync().ConfigureAwait(false) == 0) return null;
                    var txt = await loc.First.InnerTextAsync().ConfigureAwait(false);
                    return string.IsNullOrWhiteSpace(txt) ? null : txt.Trim();
                }
                catch
                {
                    return null;
                }
            }

            // Title
            progress?.Report("Playwright: Looking for Title");
            var title = (await page.TitleAsync().ConfigureAwait(false)).Trim();

            if (string.IsNullOrWhiteSpace(title))
                title = await FirstAttr(page, "meta[property='og:title']", "content").ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(title))
                title = await FirstAttr(page, "meta[name='DC.title']", "content").ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(title))
                title = await FirstAttr(page, "meta[name='twitter:title']", "value").ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(title)) result.Title = title;

            // Author
            progress?.Report("Playwright: Looking for Author");
            var author = await FirstAttr(page, "meta[property='og:author']", "content").ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(author))
                author = await FirstAttr(page, "meta[name='DC.contributor']", "content").ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(author))
                author = await FirstAttr(page, "meta[property='article:author']", "content").ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(author))
                author = await FirstAttr(page, "meta[name='author']", "content").ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(author))
                author = await FirstText(page, "a[rel~=\"author\"]").ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(author))
                author = await FirstText(page, ".author__name").ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(author))
                author = await FirstText(page, ".author_name").ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(author)) result.Author = author;
            progress?.Report($"Playwright: Author - Found {result.Author}");

            // Date
            progress?.Report("Playwright: Looking for Date Time");
            var dateString = await FirstAttr(page, "meta[property='article:modified_time']", "content")
                .ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(dateString))
                dateString = await FirstAttr(page, "meta[property='og:updated_time']", "content").ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(dateString))
                dateString = await FirstAttr(page, "meta[property='article:published_time']", "content")
                    .ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(dateString))
                dateString = await FirstAttr(page, "meta[name='DC.date.created']", "content").ConfigureAwait(false);

            progress?.Report($"Playwright: Date - Found {dateString}");

            if (!string.IsNullOrWhiteSpace(dateString))
            {
                if (DateTime.TryParse(dateString, out var parsed))
                {
                    result.LinkDate = parsed;
                    progress?.Report($"Playwright: Date - Parsed to {parsed}");
                }
                else
                {
                    progress?.Report("Playwright: Date - Could not parse");
                }
            }

            // Site
            progress?.Report("Playwright: Looking for Site Name");
            var site = await FirstAttr(page, "meta[property='og:site_name']", "content").ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(site))
                site = await FirstAttr(page, "meta[name='DC.publisher']", "content").ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(site))
            {
                var twitterSite = await FirstAttr(page, "meta[name='twitter:site']", "value").ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(twitterSite)) site = twitterSite.Replace("@", "");
            }

            if (!string.IsNullOrWhiteSpace(site)) result.Site = site;
            progress?.Report($"Playwright: Site - Found {result.Site}");

            // Description
            progress?.Report("Playwright: Looking for Description");
            var description = await FirstAttr(page, "meta[name='description']", "content").ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(description))
                description = await FirstAttr(page, "meta[property='og:description']", "content").ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(description))
                description = await FirstAttr(page, "meta[name='twitter:description']", "content")
                    .ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(description)) result.Description = description;
            progress?.Report($"Playwright: Description - Found {result.Description}");

            return (GenerationReturn.Success($"Parsed URL Metadata for {url} with Playwright"), result);
        }
        catch (Exception ex)
        {
            return (GenerationReturn.Error($"Playwright metadata parse failed for {url}: {ex.Message}"), null);
        }
    }

    /// <summary>
    ///     Callers must check the generationReturn for success or failure!
    /// </summary>
    /// <param name="toSave"></param>
    /// <param name="generationVersion"></param>
    /// <param name="progress"></param>
    /// <returns></returns>
    public static async Task<(GenerationReturn generationReturn, LinkContent? linkContent)> SaveAndGenerateHtml(
        LinkContent toSave, DateTime? generationVersion, IProgress<string>? progress = null)
    {
        var validationReturn = await Validate(toSave).ConfigureAwait(false);

        if (validationReturn.HasError) return (validationReturn, null);

        try
        {
            Db.DefaultPropertyCleanup(toSave);
            toSave.Tags = Db.TagListCleanup(toSave.Tags);
            await Db.SaveLinkContent(toSave).ConfigureAwait(false);
            await SaveLinkToPinboard(toSave, progress).ConfigureAwait(false);
            await GenerateHtmlAndJson(generationVersion, progress).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            return (
                GenerationReturn.Error(
                    $"Error with Map Content {toSave.Title}", toSave.ContentId,
                    e), toSave);
        }

        DataNotifications.PublishDataNotification("Link Generator", DataNotificationContentType.Link,
            DataNotificationUpdateType.LocalContent, [toSave.ContentId]);

        return (GenerationReturn.Success($"Saved and Generated Content And Html for Links to Add {toSave.Title}"),
            toSave);
    }

    public static async Task<GenerationReturn> SaveLinkToPinboard(LinkContent toSave,
        IProgress<string>? progress = null)
    {
        if (string.IsNullOrWhiteSpace(UserSettingsSingleton.CurrentSettings().PinboardApiToken))
            return GenerationReturn.Success("No PinboardApiToken - skipping save to Pinboard", toSave.ContentId);

        var descriptionFragments = new List<string>();
        if (!string.IsNullOrWhiteSpace(toSave.Site)) descriptionFragments.Add($"Site: {toSave.Site}");
        if (toSave.LinkDate != null) descriptionFragments.Add($"Date: {toSave.LinkDate.Value:g}");
        if (!string.IsNullOrWhiteSpace(toSave.Description))
            descriptionFragments.Add($"Description: {toSave.Description}");
        if (!string.IsNullOrWhiteSpace(toSave.Comments)) descriptionFragments.Add($"Comments: {toSave.Comments}");
        if (!string.IsNullOrWhiteSpace(toSave.Author)) descriptionFragments.Add($"Author: {toSave.Author}");

        var tagList = Db.TagListParse(toSave.Tags);
        tagList.Add(UserSettingsSingleton.CurrentSettings().SiteName);
        tagList = tagList.Select(x => x.Replace(" ", "-")).ToList();

        progress?.Report("Setting up Pinboard");

        var bookmark = new Bookmark
        {
            Url = toSave.Url,
            Description = toSave.Title,
            Extended = string.Join(" ;; ", descriptionFragments),
            Tags = tagList,
            CreatedDate = DateTime.Now,
            Shared = true,
            ToRead = false,
            Replace = true
        };

        try
        {
            using var pb = new PinboardAPI(UserSettingsSingleton.CurrentSettings().PinboardApiToken);
            progress?.Report("Adding Pinboard Bookmark");
            await pb.Posts.Add(bookmark).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            return GenerationReturn.Error("Trouble Saving to Pinboard", toSave.ContentId, e);
        }

        progress?.Report("Pinboard Bookmark Complete");

        return GenerationReturn.Success("Saved to Pinboard", toSave.ContentId);
    }

    public static async Task<GenerationReturn> Validate(LinkContent? linkContent)
    {
        if (linkContent == null) return GenerationReturn.Error("Link Content is Null?");

        var rootDirectoryCheck = UserSettingsUtilities.ValidateLocalSiteRootDirectory();

        if (!rootDirectoryCheck.Valid)
            return GenerationReturn.Error($"Problem with Root Directory: {rootDirectoryCheck.Explanation}",
                linkContent.ContentId);

        var (createdUpdatedValid, createdUpdatedValidationMessage) =
            await CommonContentValidation.ValidateCreatedAndUpdatedBy(linkContent, linkContent.Id < 1);

        if (!createdUpdatedValid)
            return GenerationReturn.Error(createdUpdatedValidationMessage, linkContent.ContentId);

        var urlValidation =
            await CommonContentValidation.ValidateLinkContentLinkUrl(linkContent.Url, linkContent.ContentId)
                .ConfigureAwait(false);

        if (!urlValidation.Valid)
            return GenerationReturn.Error(urlValidation.Explanation, linkContent.ContentId);

        return GenerationReturn.Success("Link Content Validation Successful");
    }

    public class LinkMetadata
    {
        public string? Author { get; set; }
        public string? Description { get; set; }
        public DateTime? LinkDate { get; set; }
        public string? Site { get; set; }
        public string? Title { get; set; }
    }
}