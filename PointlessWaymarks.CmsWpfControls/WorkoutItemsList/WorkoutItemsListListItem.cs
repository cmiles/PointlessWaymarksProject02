using PointlessWaymarks.CmsData.Database.Models;
using PointlessWaymarks.LlamaAspects;
using PointlessWaymarks.WpfCommon.Utility;

namespace PointlessWaymarks.CmsWpfControls.WorkoutItemsList;

[NotifyPropertyChanged]
public partial class WorkoutItemsListListItem : ISelectedTextTracker
{
    public required WorkoutItem DbEntry { get; set; }
    public CurrentSelectedTextTracker? SelectedTextTracker { get; set; } = new();

    public static WorkoutItemsListListItem CreateInstance(WorkoutItem dbItem)
    {
        return new WorkoutItemsListListItem { DbEntry = dbItem };
    }
}
