using PointlessWaymarks.CmsData.Database;
using PointlessWaymarks.CmsWpfControls.ContentList;

namespace PointlessWaymarks.CmsWpfControls.ContentMap;

public class ContentMapListLoader : ContentListLoaderBase
{
    public ContentMapListLoader(string headerName, List<Guid> contentIdsToLoad) : base(headerName, null)
    {
        ShowType = true;
        ContentIdsToLoad = contentIdsToLoad;
        AddNewItemsFromDataNotifications = false;
    }

    public List<Guid> ContentIdsToLoad { get; set; }
    
    public override async Task<List<object>> LoadItems(IProgress<string>? progress = null)
    {
        var db = await Db.Context();

        return await db.ContentFromContentIds(ContentIdsToLoad, false);
    }
}