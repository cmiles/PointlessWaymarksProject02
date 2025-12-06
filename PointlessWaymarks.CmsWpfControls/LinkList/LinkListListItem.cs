using System.IO;
using PointlessWaymarks.CmsData;
using PointlessWaymarks.CmsData.Database.Models;
using PointlessWaymarks.CmsWpfControls.ContentList;
using PointlessWaymarks.LlamaAspects;
using PointlessWaymarks.WpfCommon;
using PointlessWaymarks.WpfCommon.Utility;

namespace PointlessWaymarks.CmsWpfControls.LinkList;

[NotifyPropertyChanged]
public partial class LinkListListItem : IContentListItem
{
    private LinkListListItem(LinkContentActions itemActions, LinkContent dbEntry)
    {
        DbEntry = dbEntry;
        ItemActions = itemActions;

        ItemActions.StatusContext.RunFireAndForgetNonBlockingTask(LoadSnapshotImages);
    }

    public LinkContent DbEntry { get; set; }
    public LinkContentActions ItemActions { get; set; }
    public string LinkContentString { get; set; } = string.Empty;
    public bool ShowType { get; set; }
    public List<LinkSnapshotImageItem> SnapshotImages { get; set; } = [];

    public ContentClipboardRepresentation ClipboardObject()
    {
        return ItemActions.ClipboardObject(DbEntry);
    }

    public IContentCommon Content()
    {
        return new ContentCommonShell
        {
            Summary =
                string.Join(Environment.NewLine,
                    new List<string?>
                    {
                        DbEntry.Title,
                        DbEntry.Site,
                        DbEntry.Url,
                        DbEntry.Author,
                        DbEntry.Description,
                        DbEntry.Comments
                    }.Where(x => !string.IsNullOrWhiteSpace(x))),
            Title = DbEntry.Title,
            ContentId = DbEntry.ContentId,
            ContentVersion = DbEntry.ContentVersion,
            Id = DbEntry.Id,
            CreatedBy = DbEntry.CreatedBy,
            CreatedOn = DbEntry.CreatedOn,
            LastUpdatedBy = DbEntry.LastUpdatedBy,
            LastUpdatedOn = DbEntry.LastUpdatedOn,
            Tags = DbEntry.Tags
        };
    }

    public Guid? ContentId()
    {
        return DbEntry.ContentId;
    }

    public string DefaultBracketCode()
    {
        return ItemActions.DefaultBracketCode(DbEntry);
    }

    public async Task DefaultBracketCodeToClipboard()
    {
        await ItemActions.DefaultBracketCodeToClipboard(DbEntry);
    }

    public async Task Delete()
    {
        await ItemActions.Delete(DbEntry);
    }

    public async Task Edit()
    {
        await ItemActions.Edit(DbEntry);
    }

    public async Task ExtractNewLinks()
    {
        await ItemActions.ExtractNewLinks(DbEntry);
    }

    public async Task GenerateHtml()
    {
        await ItemActions.GenerateHtml(DbEntry);
    }

    public async Task ViewHistory()
    {
        await ItemActions.ViewHistory(DbEntry);
    }

    public async Task ViewOnSite()
    {
        await ItemActions.ViewOnSite(DbEntry);
    }

    public CurrentSelectedTextTracker? SelectedTextTracker { get; set; } = new();

    public static Task<LinkListListItem> CreateInstance(LinkContentActions itemActions)
    {
        return Task.FromResult(new LinkListListItem(itemActions, LinkContent.CreateInstance()));
    }

    public async Task LoadSnapshotImages()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var images = UserSettingsSingleton.CurrentSettings().LinkSnapshotImages(DbEntry.ContentId);

        if (!images.Any())
        {
            if (SnapshotImages.Any()) SnapshotImages = [];
            return;
        }

        var fileList = new List<LinkSnapshotImageItem>();

        foreach (var loopImageFile in images)
        {
            var parts = Path.GetFileNameWithoutExtension(loopImageFile.Name).Split("--");
            if (parts.Length < 2) continue;
            var newEntry = new LinkSnapshotImageItem { FileName = loopImageFile.FullName, Description = parts[1] };
            fileList.Add(newEntry);
        }

        SnapshotImages = fileList.OrderByDescending(x => x.Description).ToList();
    }
}