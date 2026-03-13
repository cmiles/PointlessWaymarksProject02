namespace PointlessWaymarks.FeatureIntersectionTags.Models;

public partial class IntersectSettings
{
    public int? BufferPointsAndLinesByFeet { get; set; } = null;
    public bool CreateBackups { get; set; }
    public bool CreateBackupsInDefaultStorage { get; set; }
    public List<IntersectFile> FeatureIntersectFiles { get; set; } = [];
    public string FilesToTagLastDirectoryFullName { get; set; } = string.Empty;
    public string? OsmOverpassUrl { get; set; } = "https://overpass-api.de/api/interpreter";
    public List<string> PadUsAttributes { get; set; } = [];
    public string PadUsDirectory { get; set; } = string.Empty;
    public bool RateLimitOsmOverpass { get; set; } = true;
    public bool SanitizeTags { get; set; } = true;
    public bool TagSpacesToHyphens { get; set; }
    public bool TagsToLowerCase { get; set; } = true;
    public bool UseOsmOverpass { get; set; }
    public List<string> OsmTagFilters { get; set; } = [];
    public bool OsmInTagging { get; set; }
}