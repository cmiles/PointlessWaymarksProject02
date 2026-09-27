using System.Collections.Generic;

namespace WaterDaze.Models;

public class GaugeSite
{
    public string Description { get; set; } = string.Empty;
    public string DisplayTitle { get; set; } = string.Empty;
    public string SiteCode { get; set; } = string.Empty;
    public string SiteName { get; set; } = string.Empty;

    public static List<GaugeSite> TucsonAreaSites { get; } =
    [
        new()
        {
            SiteCode = "09485000",
            SiteName = "RINCON CREEK NEAR TUCSON, AZ.",
            DisplayTitle = "Rincon Creek -- 09485000",
            Description = "East of the Arizona Trail Crossing"
        },

        new()
        {
            SiteCode = "09484000",
            SiteName = "SABINO CREEK NEAR TUCSON, AZ",
            DisplayTitle = "Sabino Creek Near Tucson -- 09484000",
            Description = "Sabino Dam"
        },

        new()
        {
            SiteCode = "09484201",
            SiteName = "BEAR CREEK ABOVE BEAR CANYON ROAD, NEAR TUCSON, AZ",
            DisplayTitle = "Bear Creek Above Bear Canyon Road -- 09484201",
            Description = "South of the Bear Canyon Trail"
        },

        new()
        {
            SiteCode = "09484600",
            SiteName = "PANTANO WASH NEAR VAIL, AZ.",
            DisplayTitle = "Pantano Wash Near Vail -- 09484600",
            Description = "East of Colossal Cave Road, West of Agua Verde Creek"
        },

        new()
        {
            SiteCode = "09484550",
            SiteName = "CIENEGA CREEK NEAR SONOITA, AZ.",
            DisplayTitle = "Cienega Creek Near Sonoita -- 09484550",
            Description = "Hilton Ranch Road/Wood Canyon Area"
        },

        new()
        {
            SiteCode = "09484500",
            SiteName = "TANQUE VERDE CREEK AT TUCSON, AZ.",
            DisplayTitle = "Tanque Verde Creek at Tucson -- 09484500",
            Description = "Sabino Canyon Road and Tanque Verde Creek"
        },

        new()
        {
            SiteCode = "09485700",
            SiteName = "RILLITO CREEK AT DODGE BOULEVARD, AT TUCSON, AZ.",
            DisplayTitle = "Rillito Creek at Dodge Boulevard at Tucson -- 09485700",
            Description = "Dodge Boulevard Crossing"
        },

        new()
        {
            SiteCode = "09472050",
            SiteName = "SAN PEDRO R AT REDINGTON BRIDGE NR REDINGTON, AZ",
            DisplayTitle = "San Pedro R at Redington Bridge nr Redington -- 09472050",
            Description = "Redington Bridge"
        },

        new()
        {
            SiteCode = "09484580",
            SiteName = "BARREL CANYON NEAR SONOITA, AZ",
            DisplayTitle = "Barrel Canyon Near Sonoita -- 09484580",
            Description = "83 and Barrel Canyon"
        },

        new()
        {
            SiteCode = "09486055",
            SiteName = "RILLITO CREEK AT LA CHOLLA BLVD NEAR TUCSON, AZ.",
            DisplayTitle = "Rillito Creek at La Cholla Blvd Near Tucson -- 09486055",
            Description = "La Cholla Boulevard Crossing"
        },

        new()
        {
            SiteCode = "09486350",
            SiteName = "CANADA DEL ORO BLW INA ROAD, NEAR TUCSON, AZ.",
            DisplayTitle = "Canada Del Oro Blw Ina Road, Near Tucson -- 09486350",
            Description = "Below Ina Road"
        },

        new()
        {
            SiteCode = "09482500",
            SiteName = "SANTA CRUZ RIVER AT TUCSON, AZ",
            DisplayTitle = "Santa Cruz River at Tucson -- 09482500",
            Description = "Congress Street / Tucson"
        },

        new()
        {
            SiteCode = "09482495",
            SiteName = "SANTA CRUZ RIVER AT MISSION LANE GCS AT TUCSON, AZ",
            DisplayTitle = "Santa Cruz River at Mission Lane GCS at Tucson -- 09482495",
            Description = "Mission Lane Grade Control Structure"
        },

        new()
        {
            SiteCode = "09482490",
            SiteName = "SANTA CRUZ RIVER AT STARR PASS GCS AT TUCSON AZ",
            DisplayTitle = "Santa Cruz River at Starr Pass GCS at Tucson AZ -- 09482490",
            Description = "Starr Pass Grade Control Structure"
        },

        new()
        {
            SiteCode = "09482440",
            SiteName = "SANTA CRUZ RIVER AT SILVERLAKE RD, AT TUCSON, AZ",
            DisplayTitle = "Santa Cruz River at Silverlake Rd, at Tucson -- 09482440",
            Description = "Silverlake Road Crossing"
        }
    ];

    public string UsgsInventoryUrl =>
        $"https://waterdata.usgs.gov/nwis/inventory?agency_code=USGS&site_no={SiteCode}";

    public override string ToString()
    {
        return DisplayTitle;
    }
}