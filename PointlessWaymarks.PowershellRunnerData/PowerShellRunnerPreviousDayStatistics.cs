using PointlessWaymarks.LlamaAspects;

namespace PointlessWaymarks.PowerShellRunnerData;

[NotifyPropertyChanged]
public partial class PowerShellRunnerPreviousDayStatistics
{
    public DateTime Day { get; set; }
    public int Error { get; set; }
    public int Success { get; set; }
}