using PointlessWaymarks.LlamaAspects;

namespace PointlessWaymarks.CloudBackupData.Reports;

[NotifyPropertyChanged]
public partial class JobDailyActivity
{
    public long ActivityCount { get; set; }
    public DateTime ActivityDate { get; set; }
    public double ActivitySize { get; set; }
    public long ErrorCount { get; set; }
}