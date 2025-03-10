using System.Text.Json;
using HtmlTags;
using PointlessWaymarks.CmsData.BracketCodes;
using PointlessWaymarks.CmsData.CommonHtml;
using PointlessWaymarks.CmsData.Database.Models;
using PointlessWaymarks.CmsData.Database.PointDetailDataModels;
using PointlessWaymarks.CommonTools;

namespace PointlessWaymarks.CmsData.ContentHtml.PointHtml;

public static class PointParts
{
    public static string GoogleMapsDirectionsToLatLongUrl(PointContent point)
    {
        return $"https://www.google.com/maps/dir/?api=1&destination={point.Latitude},{point.Longitude}";
    }

    public static string GoogleMapsDirectionsToLatLongUrl(PointContentDto point)
    {
        return $"https://www.google.com/maps/dir/?api=1&destination={point.Latitude},{point.Longitude}";
    }

    public static HtmlTag GoogleMapsLatLongLink(PointContentDto point)
    {
        return new LinkTag("Google Maps", GoogleMapsLatLongUrl(point), "point-map-external-link");
    }

    public static string GoogleMapsLatLongUrl(PointContentDto point)
    {
        return $"https://www.google.com/maps/search/?api=1&query={point.Latitude},{point.Longitude}";
    }

    public static string GoogleMapsLatLongUrl(PointContent point)
    {
        return $"https://www.google.com/maps/search/?api=1&query={point.Latitude},{point.Longitude}";
    }

    public static HtmlTag OsmCycleMapLatLongLink(PointContentDto point)
    {
        return new LinkTag("OSM Cycle Maps", OsmCycleMapsLatLongUrl(point), "point-map-external-link");
    }

    public static string OsmCycleMapsLatLongUrl(PointContentDto point)
    {
        return OsmCycleMapsLatLongUrl(point.Latitude, point.Longitude);
    }

    public static string OsmCycleMapsLatLongUrl(double latitude, double longitude)
    {
        return $"http://www.openstreetmap.org/?mlat={latitude}&mlon={longitude}&zoom=13&layers=C";
    }

    public static async Task<HtmlTag> PointDetailsCompact(PointContentDto? dbEntry)
    {
        if (dbEntry?.PointDetails == null || !dbEntry.PointDetails.Any()) return HtmlTag.Empty();

        var detailList = new HtmlTag("ul");

        foreach (var loopDetail in dbEntry.PointDetails)
        {
            if (string.IsNullOrWhiteSpace(loopDetail.DataType)) continue;
            if (string.IsNullOrWhiteSpace(loopDetail.StructuredDataAsJson)) continue;

            var typeLine = new HtmlTag("li", detailList).AddClass("point-detail-type");

            switch (loopDetail.DataType)
            {
                case "Campground":
                {
                    var pointDetails = JsonSerializer.Deserialize<Campground>(loopDetail.StructuredDataAsJson);

                    if (pointDetails == null) break;

                    if (!string.IsNullOrEmpty(pointDetails.Notes))
                    {
                        var noteText = ContentProcessing.ProcessContent(
                            await BracketCodeCommon.ProcessCodesForSite(pointDetails.Notes).ConfigureAwait(false),
                            pointDetails.NotesContentFormat);

                        typeLine.Text($"{loopDetail.DataType}: {noteText.RemoveOuterPTags()}").Encoded(false);
                    }
                    else
                    {
                        typeLine.Text($"{loopDetail.DataType}");
                    }

                    break;
                }
                case "Parking":
                {
                    var pointDetails = JsonSerializer.Deserialize<Parking>(loopDetail.StructuredDataAsJson);

                    if (pointDetails == null) break;

                    if (!string.IsNullOrEmpty(pointDetails.Notes))
                    {
                        var noteText = ContentProcessing.ProcessContent(
                            await BracketCodeCommon.ProcessCodesForSite(pointDetails.Notes).ConfigureAwait(false),
                            pointDetails.NotesContentFormat);

                        typeLine.Text($"{loopDetail.DataType}:  {noteText.RemoveOuterPTags()}").Encoded(false);
                    }
                    else
                    {
                        typeLine.Text($"{loopDetail.DataType}");
                    }

                    break;
                }
                case "Vehicle Access":
                {
                    var pointDetails = JsonSerializer.Deserialize<VehicleAccess>(loopDetail.StructuredDataAsJson);

                    if (pointDetails == null) break;

                    var returnText = $"{loopDetail.DataType}: ";
                    if (pointDetails.RecommendedForPassengerCar) returnText += "Passenger Car Accessible.";
                    if (pointDetails.RecommendedTwoWheelDriveModerateClearance)
                        returnText += "Recommended for Two Wheel Drive Moderate Clearance Vehicles.";
                    if (pointDetails.RecommendedFourWheelDriveHighClearance)
                        returnText += "Recommended only for Four Wheel Drive High Clearance Vehicles.";

                    if (!string.IsNullOrEmpty(pointDetails.Notes))
                    {
                        var noteText = ContentProcessing.ProcessContent(
                            await BracketCodeCommon.ProcessCodesForSite(pointDetails.Notes).ConfigureAwait(false),
                            pointDetails.NotesContentFormat);

                        returnText += $" {noteText.RemoveOuterPTags()}";
                    }

                    typeLine.Text(returnText).Encoded(false);

                    break;
                }
                case "Fee":
                {
                    var pointDetails = JsonSerializer.Deserialize<Fee>(loopDetail.StructuredDataAsJson);

                    if (pointDetails == null) break;

                    if (!string.IsNullOrEmpty(pointDetails.Notes))
                    {
                        var noteText = ContentProcessing.ProcessContent(
                            await BracketCodeCommon.ProcessCodesForSite(pointDetails.Notes).ConfigureAwait(false),
                            pointDetails.NotesContentFormat);

                        typeLine.Text($"{loopDetail.DataType.HtmlEncode()}:  {noteText.RemoveOuterPTags()}")
                            .Encoded(false);
                    }
                    else
                    {
                        typeLine.Text($"{loopDetail.DataType}");
                    }

                    break;
                }
                case "Driving Directions":
                {
                    var pointDetails =
                        JsonSerializer.Deserialize<DrivingDirections>(loopDetail.StructuredDataAsJson);

                    if (pointDetails == null) break;

                    if (!string.IsNullOrEmpty(pointDetails.Notes))
                    {
                        var noteText = ContentProcessing.ProcessContent(
                            await BracketCodeCommon.ProcessCodesForSite(pointDetails.Notes).ConfigureAwait(false),
                            pointDetails.NotesContentFormat);

                        typeLine.Text($"{loopDetail.DataType}:  {noteText.RemoveOuterPTags()}").Encoded(false);
                    }
                    else
                    {
                        typeLine.Text($"{loopDetail.DataType}");
                    }

                    break;
                }
                case "Feature":
                {
                    var pointDetails = JsonSerializer.Deserialize<Feature>(loopDetail.StructuredDataAsJson);

                    if (pointDetails == null) break;

                    if (!string.IsNullOrEmpty(pointDetails.Notes))
                    {
                        var noteText = ContentProcessing.ProcessContent(
                            await BracketCodeCommon.ProcessCodesForSite(pointDetails.Notes).ConfigureAwait(false),
                            pointDetails.NotesContentFormat);

                        typeLine.Text($"{pointDetails.Type}:  {noteText.RemoveOuterPTags()}").Encoded(false);
                    }
                    else
                    {
                        typeLine.Text($"{pointDetails.Type}");
                    }

                    break;
                }
                case "Peak":
                {
                    var pointDetails = JsonSerializer.Deserialize<Peak>(loopDetail.StructuredDataAsJson);

                    if (pointDetails == null) break;

                    if (!string.IsNullOrEmpty(pointDetails.Notes))
                    {
                        var noteText = ContentProcessing.ProcessContent(
                            await BracketCodeCommon.ProcessCodesForSite(pointDetails.Notes).ConfigureAwait(false),
                            pointDetails.NotesContentFormat);

                        typeLine.Text($"{loopDetail.DataType}:  {noteText.RemoveOuterPTags()}").Encoded(false);
                    }
                    else
                    {
                        typeLine.Text($"{loopDetail.DataType}");
                    }

                    break;
                }
                case "Restroom":
                {
                    var pointDetails = JsonSerializer.Deserialize<Restroom>(loopDetail.StructuredDataAsJson);

                    if (pointDetails == null) break;

                    if (!string.IsNullOrEmpty(pointDetails.Notes))
                    {
                        var noteText = ContentProcessing.ProcessContent(
                            await BracketCodeCommon.ProcessCodesForSite(pointDetails.Notes).ConfigureAwait(false),
                            pointDetails.NotesContentFormat);

                        typeLine.Text($"{loopDetail.DataType}:  {noteText.RemoveOuterPTags()}").Encoded(false);
                    }
                    else
                    {
                        typeLine.Text($"{loopDetail.DataType}");
                    }

                    break;
                }
                case "Trail Junction":
                {
                    var pointDetails = JsonSerializer.Deserialize<TrailJunction>(loopDetail.StructuredDataAsJson);

                    if (pointDetails == null) break;

                    if (!string.IsNullOrEmpty(pointDetails.Notes))
                    {
                        var noteText = ContentProcessing.ProcessContent(
                            await BracketCodeCommon.ProcessCodesForSite(pointDetails.Notes).ConfigureAwait(false),
                            pointDetails.NotesContentFormat);

                        typeLine.Text($"{loopDetail.DataType}:  {noteText.RemoveOuterPTags()}").Encoded(false);
                    }
                    else
                    {
                        typeLine.Text($"{loopDetail.DataType}");
                    }

                    break;
                }
            }
        }

        return detailList;
    }

    public static async Task<HtmlTag> PointDetailsDiv(PointContentDto? dbEntry)
    {
        if (dbEntry?.PointDetails == null || !dbEntry.PointDetails.Any()) return HtmlTag.Empty();

        var containerDiv = new DivTag().AddClass("point-detail-list-container");

        foreach (var loopDetail in dbEntry.PointDetails)
        {
            if (string.IsNullOrWhiteSpace(loopDetail.DataType)) continue;
            if (string.IsNullOrWhiteSpace(loopDetail.StructuredDataAsJson)) continue;

            var outerDiv = new DivTag().AddClass("point-detail-container");
            var typeLine = new HtmlTag("p").Text(loopDetail.DataType).AddClass("point-detail-type");
            outerDiv.Children.Add(typeLine);

            switch (loopDetail.DataType)
            {
                case "Campground":
                {
                    var pointDetails = JsonSerializer.Deserialize<Campground>(loopDetail.StructuredDataAsJson);

                    if (pointDetails == null) break;

                    var infoList = new HtmlTag("ul").AddClass("point-detail-info-list");

                    if (pointDetails.Fee)
                        infoList.Children.Add(new HtmlTag("li").Text("There is a Fee to Camp at this location"));

                    if (!string.IsNullOrEmpty(pointDetails.Notes))
                    {
                        var noteText = ContentProcessing.ProcessContent(
                            await BracketCodeCommon.ProcessCodesForSite(pointDetails.Notes).ConfigureAwait(false),
                            pointDetails.NotesContentFormat);

                        infoList.Children.Add(new HtmlTag("li").Encoded(false).Text(noteText));
                    }

                    outerDiv.Children.Add(infoList);

                    break;
                }
                case "Parking":
                {
                    var pointDetails = JsonSerializer.Deserialize<Parking>(loopDetail.StructuredDataAsJson);

                    if (pointDetails == null) break;

                    var infoList = new HtmlTag("ul").AddClass("point-detail-info-list");

                    if (pointDetails.Fee)
                        infoList.Children.Add(new HtmlTag("li").Text("Fee Area"));

                    if (!string.IsNullOrEmpty(pointDetails.Notes))
                    {
                        var noteText = ContentProcessing.ProcessContent(
                            await BracketCodeCommon.ProcessCodesForSite(pointDetails.Notes).ConfigureAwait(false),
                            pointDetails.NotesContentFormat);

                        infoList.Children.Add(new HtmlTag("li").Encoded(false).Text(noteText));
                    }

                    outerDiv.Children.Add(infoList);

                    break;
                }
                case "Vehicle Access":
                {
                    var pointDetails = JsonSerializer.Deserialize<VehicleAccess>(loopDetail.StructuredDataAsJson);

                    if (pointDetails == null) break;

                    var infoList = new HtmlTag("ul").AddClass("point-detail-info-list");

                    if (pointDetails.RecommendedForPassengerCar)
                        infoList.Children.Add(new HtmlTag("li").Text("Passenger Car Accessible"));
                    if (pointDetails.RecommendedTwoWheelDriveModerateClearance)
                        infoList.Children.Add(
                            new HtmlTag("li").Text("Recommended for Two Wheel Drive Moderate Clearance Vehicles"));
                    if (pointDetails.RecommendedFourWheelDriveHighClearance)
                        infoList.Children.Add(
                            new HtmlTag("li").Text("Recommended only for Four Wheel Drive High Clearance Vehicles"));

                    if (!string.IsNullOrEmpty(pointDetails.Notes))
                    {
                        var noteText = ContentProcessing.ProcessContent(
                            await BracketCodeCommon.ProcessCodesForSite(pointDetails.Notes).ConfigureAwait(false),
                            pointDetails.NotesContentFormat);

                        infoList.Children.Add(new HtmlTag("li").Encoded(false).Text(noteText));
                    }

                    outerDiv.Children.Add(infoList);

                    break;
                }
                case "Fee":
                {
                    var pointDetails = JsonSerializer.Deserialize<Fee>(loopDetail.StructuredDataAsJson);

                    if (pointDetails == null) return outerDiv;

                    var infoList = new HtmlTag("ul").AddClass("point-detail-info-list");

                    if (!string.IsNullOrEmpty(pointDetails.Notes))
                    {
                        var noteText = ContentProcessing.ProcessContent(
                            await BracketCodeCommon.ProcessCodesForSite(pointDetails.Notes).ConfigureAwait(false),
                            pointDetails.NotesContentFormat);

                        infoList.Children.Add(new HtmlTag("li").Encoded(false).Text(noteText));
                    }

                    outerDiv.Children.Add(infoList);

                    break;
                }
                case "Driving Directions":
                {
                    var pointDetails =
                        JsonSerializer.Deserialize<DrivingDirections>(loopDetail.StructuredDataAsJson);

                    if (pointDetails == null) return outerDiv;

                    var infoList = new HtmlTag("ul").AddClass("point-detail-info-list");

                    if (!string.IsNullOrEmpty(pointDetails.Notes))
                    {
                        var noteText = ContentProcessing.ProcessContent(
                            await BracketCodeCommon.ProcessCodesForSite(pointDetails.Notes).ConfigureAwait(false),
                            pointDetails.NotesContentFormat);

                        infoList.Children.Add(new HtmlTag("li").Encoded(false).Text(noteText));
                    }

                    outerDiv.Children.Add(infoList);

                    break;
                }
                case "Feature":
                {
                    var pointDetails = JsonSerializer.Deserialize<Feature>(loopDetail.StructuredDataAsJson);

                    if (pointDetails == null) return outerDiv;

                    typeLine.Text($"Point Detail: {pointDetails.Type}");

                    var infoList = new HtmlTag("ul").AddClass("point-detail-info-list");

                    if (!string.IsNullOrEmpty(pointDetails.Notes))
                    {
                        var noteText = ContentProcessing.ProcessContent(
                            await BracketCodeCommon.ProcessCodesForSite(pointDetails.Notes).ConfigureAwait(false),
                            pointDetails.NotesContentFormat);

                        infoList.Children.Add(new HtmlTag("li").Encoded(false).Text(noteText));
                    }

                    outerDiv.Children.Add(infoList);

                    break;
                }
                case "Peak":
                {
                    var pointDetails = JsonSerializer.Deserialize<Peak>(loopDetail.StructuredDataAsJson);

                    if (pointDetails == null) return outerDiv;

                    var infoList = new HtmlTag("ul").AddClass("point-detail-info-list");

                    if (!string.IsNullOrEmpty(pointDetails.Notes))
                    {
                        var noteText = ContentProcessing.ProcessContent(
                            await BracketCodeCommon.ProcessCodesForSite(pointDetails.Notes).ConfigureAwait(false),
                            pointDetails.NotesContentFormat);

                        infoList.Children.Add(new HtmlTag("li").Encoded(false).Text(noteText));
                    }

                    outerDiv.Children.Add(infoList);

                    break;
                }
                case "Restroom":
                {
                    var pointDetails = JsonSerializer.Deserialize<Restroom>(loopDetail.StructuredDataAsJson);

                    if (pointDetails == null) return outerDiv;

                    var infoList = new HtmlTag("ul").AddClass("point-detail-info-list");

                    if (!string.IsNullOrEmpty(pointDetails.Notes))
                    {
                        var noteText = ContentProcessing.ProcessContent(
                            await BracketCodeCommon.ProcessCodesForSite(pointDetails.Notes).ConfigureAwait(false),
                            pointDetails.NotesContentFormat);

                        infoList.Children.Add(new HtmlTag("li").Encoded(false).Text(noteText));
                    }

                    outerDiv.Children.Add(infoList);

                    break;
                }
                case "Trail Junction":
                {
                    var pointDetails = JsonSerializer.Deserialize<TrailJunction>(loopDetail.StructuredDataAsJson);

                    if (pointDetails == null) return outerDiv;

                    var infoList = new HtmlTag("ul").AddClass("point-detail-info-list");

                    if (pointDetails.Sign)
                        infoList.Children.Add(
                            new HtmlTag("li").Text(pointDetails.Sign ? "Signed" : "No Sign"));

                    if (!string.IsNullOrEmpty(pointDetails.Notes))
                    {
                        var noteText = ContentProcessing.ProcessContent(
                            await BracketCodeCommon.ProcessCodesForSite(pointDetails.Notes).ConfigureAwait(false),
                            pointDetails.NotesContentFormat);

                        infoList.Children.Add(new HtmlTag("li").Encoded(false).Text(noteText));
                    }

                    outerDiv.Children.Add(infoList);

                    break;
                }
            }

            containerDiv.Children.Add(outerDiv);
        }

        return containerDiv;
    }

    public static string PointDivAndScript(Guid pointContentId)
    {
        var divScriptGuidConnector = Guid.NewGuid();

        var tag =
            $"""
             <div id="Point-{divScriptGuidConnector}" class="leaflet-container leaflet-retina leaflet-fade-anim leaflet-grab leaflet-touch-drag point-content-map"></div>
             """;

        var script =
            $"""
             <script>
                lazyInit(document.querySelector("#Point-{divScriptGuidConnector}"), () => singlePointMapInit(document.querySelector("#Point-{divScriptGuidConnector}"), "{pointContentId}"))
             </script>
             """;

        return tag + script;
    }

    public static HtmlTag PointTextInfoDiv(PointContentDto point)
    {
        var container = new DivTag().AddClass("point-text-info-container");
        var pTag = new HtmlTag("p")
            .Text(
                $"Lat: {Math.Round(point.Latitude, 5)}, Long: {Math.Round(point.Longitude, 5)}{(point.Elevation == null ? string.Empty : $", Elevation: {point.Elevation:N0}', {point.Elevation.FeetToMeters():F2}m'")}, {OsmCycleMapLatLongLink(point)}, {GoogleMapsLatLongLink(point)}")
            .AddClass("point-location-text").Encoded(false);

        container.Children.Add(pTag);

        return container;
    }

    public static async Task<HtmlTag> StandAlonePointDetailsDiv(PointContentDto? dbEntry)
    {
        if (dbEntry?.PointDetails == null || !dbEntry.PointDetails.Any()) return HtmlTag.Empty();

        var borderDiv = new DivTag().AddClass("point-detail-info-border").AddClass("info-block");
        var containerDiv = new DivTag().AddClass("point-detail-info-container");
        borderDiv.Children.Add(containerDiv);

        var titleLine = new HtmlTag("p")
            .Text(
                $"{new LinkTag(dbEntry.Title, UserSettingsSingleton.CurrentSettings().PointPageUrl(dbEntry))} - {GoogleMapsLatLongLink(dbEntry)}")
            .Encoded(false).AddClass("point-detail-info-title");
        containerDiv.Children.Add(titleLine);

        containerDiv.Children.Add(await PointDetailsCompact(dbEntry).ConfigureAwait(false));

        return borderDiv;
    }

    public static async Task<HtmlTag> StandAlonePointDetailsDiv(PointContentDto? dbEntry, string prefix)
    {
        if (dbEntry?.PointDetails == null || !dbEntry.PointDetails.Any()) return HtmlTag.Empty();

        var containerDiv = new DivTag().AddClass("point-detail-info-container");

        var titleLine = new HtmlTag("p")
            .Text(
                $"<b>{prefix}</b> {new LinkTag(dbEntry.Title, UserSettingsSingleton.CurrentSettings().PointPageUrl(dbEntry))} - {GoogleMapsLatLongLink(dbEntry)}")
            .Encoded(false).AddClass("point-detail-info-title");
        containerDiv.Children.Add(titleLine);

        containerDiv.Children.Add(await PointDetailsCompact(dbEntry).ConfigureAwait(false));

        return containerDiv;
    }
}