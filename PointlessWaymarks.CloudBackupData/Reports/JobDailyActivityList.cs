using Microsoft.EntityFrameworkCore;
using PointlessWaymarks.LlamaAspects;

namespace PointlessWaymarks.CloudBackupData.Reports;

[NotifyPropertyChanged]
public partial class JobDailyActivityList
{
    public List<JobDailyActivity> Activity { get; set; } = new();
    public required int JobId { get; set; }
    public int DaysBack { get; set; } = 30;

    public async Task Update()
    {
        var context = await CloudBackupContext.CreateInstance();

        var job = await context.BackupJobs.SingleOrDefaultAsync(x => x.Id == JobId);

        if (job == null)
        {
            Activity = [];
            return;
        }

        var today = DateTime.Today;

        var batches = await context.CloudTransferBatches.Where(x => x.BackupJobId == JobId).Select(x => x.Id)
            .ToListAsync();

        if (batches.Count == 0)
        {
            Activity = [];
            return;
        }

        //Remove the most recent day - it is likely not complete
        Activity = Activity.OrderByDescending(x => x.ActivityDate).Skip(1).ToList();

        for (var i = 0; i <= DaysBack; i++)
        {
            var referenceDate = today.AddDays(-1 * i).Date;

            if (Activity.Any(x => x.ActivityDate == referenceDate)) continue;

            var toAdd = new JobDailyActivity() { ActivityDate = referenceDate };
            Activity.Add(toAdd);

            var uploads = await context.CloudUploads.Where(x =>
                batches.Contains(x.CloudTransferBatchId) && x.UploadCompletedSuccessfully &&
                x.LastUpdatedOn.Date == toAdd.ActivityDate).ToListAsync();
            var copies = await context.CloudCopies.Where(x =>
                batches.Contains(x.CloudTransferBatchId) && x.CopyCompletedSuccessfully &&
                x.LastUpdatedOn.Date == toAdd.ActivityDate).ToListAsync();
            var deletes = await context.CloudDeletions.Where(x =>
                batches.Contains(x.CloudTransferBatchId) && x.DeletionCompletedSuccessfully &&
                x.LastUpdatedOn.Date == toAdd.ActivityDate).ToListAsync();
            var uploadErrors = await context.CloudUploads.Where(x =>
                batches.Contains(x.CloudTransferBatchId) && !string.IsNullOrWhiteSpace(x.ErrorMessage) &&
                x.LastUpdatedOn.Date == toAdd.ActivityDate).CountAsync();
            var copyErrors = await context.CloudCopies.Where(x =>
                batches.Contains(x.CloudTransferBatchId) && !string.IsNullOrWhiteSpace(x.ErrorMessage) &&
                x.LastUpdatedOn.Date == toAdd.ActivityDate).CountAsync();
            var deleteErrors = await context.CloudDeletions.Where(x =>
                batches.Contains(x.CloudTransferBatchId) && !string.IsNullOrWhiteSpace(x.ErrorMessage) &&
                x.LastUpdatedOn.Date == toAdd.ActivityDate).CountAsync();

            toAdd.ActivityCount = uploads.Count + copies.Count + deletes.Count;
            toAdd.ActivitySize = uploads.Sum(x => x.FileSize) + copies.Sum(x => x.FileSize) +
                                 deletes.Sum(x => x.FileSize);
            toAdd.ErrorCount = uploadErrors + copyErrors + deleteErrors;
        }

        Activity = Activity.OrderByDescending(x => x.ActivityDate).Take(30).ToList();
    }
}