using PointlessWaymarks.LlamaAspects;

namespace PointlessWaymarks.PowerShellRunnerData;

[NotifyPropertyChanged]
public partial class PowerShellRunnerDayStatistics
{
    public DateTime Day { get; set; }
    public int Error { get; set; }
    public int Running { get; set; }
    public int Scheduled { get; set; }
    public int Success { get; set; }
}