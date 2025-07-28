using System.Collections.ObjectModel;
using PointlessWaymarks.FeatureIntersectionTags.Models;
using PointlessWaymarks.GeoToolsGui.Models;
using PointlessWaymarks.LlamaAspects;

namespace PointlessWaymarks.GeoToolsGui.Settings;

[NotifyPropertyChanged]
public partial class FeatureIntersectTaggerSettings
{
    public int? BufferPointsAndLinesByFeet { get; set; } = null;
    public bool CreateBackups { get; set; }
    public bool CreateBackupsInDefaultStorage { get; set; }
    public string ExifToolFullName { get; set; } = string.Empty;
    public ObservableCollection<FeatureFileContext> FeatureIntersectFiles { get; set; } = [];
    public string FilesToTagLastDirectoryFullName { get; set; } = string.Empty;
    public string? OsmOverpassUrl { get; set; } = "https://overpass-api.de/api/interpreter";
    public ObservableCollection<string> PadUsAttributes { get; set; } = [];
    public string PadUsDirectory { get; set; } = string.Empty;
    public bool RateLimitOsmOverpass { get; set; } = true;
    public bool SanitizeTags { get; set; } = true;
    public bool TagSpacesToHyphens { get; set; }
    public bool TagsToLowerCase { get; set; } = true;
    public bool UseOsmOverpass { get; set; }
    public ObservableCollection<string> OsmTagFilters { get; set; } = [];
    public bool OsmInTagging { get; set; }

    public IntersectSettings ToIntersectSettings()
    {
        return new IntersectSettings
        {
            BufferPointsAndLinesByFeet = BufferPointsAndLinesByFeet,
            CreateBackups = CreateBackups,
            CreateBackupsInDefaultStorage = CreateBackupsInDefaultStorage,
            ExifToolFullName = ExifToolFullName,
            FeatureIntersectFiles = FeatureIntersectFiles
                .Select(x => new IntersectFile
                {
                    AttributesForTags = x.AttributesForTags,
                    ContentId = x.ContentId,
                    Downloaded = x.Downloaded,
                    FileName = x.FileName,
                    Name = x.Name,
                    Note = x.Note,
                    Source = x.Source,
                    TagAll = x.TagAll
                }).ToList(),
            FilesToTagLastDirectoryFullName = FilesToTagLastDirectoryFullName,
            OsmOverpassUrl = OsmOverpassUrl,
            PadUsAttributes = PadUsAttributes.ToList(),
            PadUsDirectory = PadUsDirectory,
            RateLimitOsmOverpass = RateLimitOsmOverpass,
            SanitizeTags = SanitizeTags,
            TagSpacesToHyphens = TagSpacesToHyphens,
            TagsToLowerCase = TagsToLowerCase,
            UseOsmOverpass = UseOsmOverpass,
            OsmInTagging = OsmInTagging,
            OsmTagFilters = OsmTagFilters.ToList()
        };
    }
}