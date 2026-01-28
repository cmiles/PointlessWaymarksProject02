using PointlessWaymarks.CmsData.Database.Models;
using PointlessWaymarks.CmsWpfControls.ContentList;
using PointlessWaymarks.WpfCommon;
using PointlessWaymarks.WpfCommon.Status;
using PointlessWaymarks.WpfCommon.Utility;

namespace PointlessWaymarks.CmsWpfControls.PointList;

public static class PointActions
{
    public static async Task ShowInGoogleMapsWeb(PointContentDto contents,
        StatusControlContext statusContext)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var mapUrl =
            $"https://www.google.com/maps/search/?api=1&query={contents.Latitude:F5},{contents.Longitude:F5}";

        await ThreadSwitcher.ResumeForegroundAsync();
        ProcessHelpers.OpenUrlInExternalBrowser(mapUrl);
    }

    public static async Task ShowInOsmCycleMap(PointContentDto contents,
        StatusControlContext statusContext)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var mapUrl =
            $"http://www.openstreetmap.org/?mlat={contents.Latitude:F5}&mlon={contents.Longitude:F5}&zoom=13&layers=C";

        await ThreadSwitcher.ResumeForegroundAsync();
        ProcessHelpers.OpenUrlInExternalBrowser(mapUrl);
    }

    private static async Task TextAndContentRepresentationToClipboard(List<PointContentDto> contents,
        string finalString, StatusControlContext statusContext)
    {
        await ContentClipboardRepresentation.TextAndContentRepresentationToClipboard(
            contents.Cast<IContentCommon>().ToList(), finalString, statusContext);
    }
}