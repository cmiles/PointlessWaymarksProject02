using Amazon.Runtime.Internal.Util;
using Microsoft.EntityFrameworkCore;
using PointlessWaymarks.CloudBackupData;
using PointlessWaymarks.CloudBackupData.Batch;
using PointlessWaymarks.CloudBackupData.Models;
using PointlessWaymarks.CloudBackupData.Reports;
using PointlessWaymarks.CommonTools;
using PointlessWaymarks.CommonTools.S3;
using PointlessWaymarks.WindowsTools;
using Serilog;

namespace PointlessWaymarks.CloudBackupRunner;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        LogTools.StandardStaticLoggerForDefaultLogDirectory("CloudBackupRunner");

        AppDomain.CurrentDomain.UnhandledException += delegate(object _, UnhandledExceptionEventArgs eventArgs)
        {
            var exceptionMessage = "Unhandled Fatal Exception";
            if (eventArgs.ExceptionObject is Exception castException) exceptionMessage = castException.Message;

            Log.ForContext("exceptionObject", eventArgs.ExceptionObject.SafeObjectDump())
                .Error(exceptionMessage, eventArgs.ExceptionObject);

            try
            {
                WindowsNotificationBuilders.NewNotifier("Cloud Backup Runner").Result
                    .SetAutomationLogoNotificationIconUrl().SetErrorReportAdditionalInformationMarkdown(
                        EmbeddedResourceTools.GetEmbeddedResourceText("README.md"))
                    .Error("Unhandled Exception...", exceptionMessage)
                    .RunSynchronously();
            }
            catch (Exception)
            {
                // ignored
            }

            Log.CloseAndFlush();
        };

        var startTime = DateTime.Now;

        if (args.Length is < 1 or > 3)
        {
            Console.WriteLine("""
                              The PointlessWaymarks CloudBackup Runner uses Backup Jobs created with the
                              Pointless Waymarks Cloud Backup Editor to perform Copies, Uploads and Deletions on
                              Amazon S3 to create a backup of your local files. Backup Jobs are stored in
                              a Database File that is specified as the first argument to the program.
                              """);
            Console.WriteLine();
            Console.WriteLine("""
                              To list Backup Jobs and recent Transfer Batches specify the Database File
                              as the only argument to the program.
                              """);
            Console.WriteLine();
            Console.WriteLine("""
                              To run a Backup Job provide the Database File and Job Id as arguments.
                              """);
            Console.WriteLine();
            Console.WriteLine("""
                              By default when you run a Backup Job the program will scan every local and
                              S3 file for changes to create a 'Transfer Batch' in the database. The Transfer
                              Batch holds a record of all the Copies, Uploads and Deletions needed to
                              make S3 match your local files. With larger numbers of files this can take
                              a long time... If you have a large number of files, or a backup of files that
                              only change infrequently, it may make sense to resume a previous Batch.
                              """);
            Console.WriteLine();
            Console.WriteLine("""
                              Resuming a previous Transfer Batch will mean that the backup will NOT account
                              for any file changes since the Batch was created!! It will also mean the program
                              will spend more time copying, uploading and deleting files and less time scanning for
                              changes... Use with caution!
                              """);
            Console.WriteLine();
            Console.WriteLine("""
                              To try to resume a batch specify the Database File, Job Id and:
                              """);
            Console.WriteLine();
            Console.WriteLine("""
                                Batch Id - To resume a specific Batch specify the Batch Id. If the Batch Id
                                is not found a new Batch will be created.
                              """);
            Console.WriteLine();
            Console.WriteLine("""
                                last - To resume the last Batch specify 'last'. If there is no last batch a
                                new Batch will be created.
                              """);
            Console.WriteLine();
            Console.WriteLine("""
                                new - A new batch will be created including a full rescan of the cloud files.
                              """);
            Console.WriteLine();
            Console.WriteLine("""
                                auto - To have the program guess whether there is a batch worth resuming specify
                                'auto'. This will look for a recent batch that has a low error rate and still
                                needs a large number of actions to complete. If a 'best guess' Batch is not found
                                a new batch will be created.
                              """);

            return -1;
        }

        var consoleId = Guid.NewGuid();

        var progress = new ConsoleAndDataNotificationProgress(consoleId);

        var errorNotifier = (await WindowsNotificationBuilders.NewNotifier("Cloud Backup Runner"))
            .SetAutomationLogoNotificationIconUrl().SetErrorReportAdditionalInformationMarkdown(
                EmbeddedResourceTools.GetEmbeddedResourceText("README.md"));

        Log.ForContext("args", args, true).Information(
            "PointlessWaymarks.CloudBackupRunner Starting");

        var db = await CloudBackupContext.TryCreateInstance(args[0]);

        if (!db.success || db.context is null)
        {
            await errorNotifier.Error($"Failed to Connect to the Db {args[0]}");

            Log.ForContext(nameof(db), db, true).Error("Failed to Connect to the Db {dbFile}", args[0]);
            Log.Information("Cloud Backup Runner - Finished Run");
            await Log.CloseAndFlushAsync();
            return -1;
        }

        if (args.Length == 1)
        {
            var jobs = await db.context.BackupJobs.Include(backupJob => backupJob.Batches).ToListAsync();

            Log.Verbose("Found {jobCount} Jobs", jobs.Count);

            foreach (var loopJob in jobs)
            {
                Console.WriteLine(
                    $"{loopJob.Id}  {loopJob.Name}: {loopJob.LocalDirectory} to {loopJob.CloudBucket}:{loopJob.CloudDirectory}");

                var batches = loopJob.Batches.OrderByDescending(x => x.CreatedOn).Take(5).ToList();

                foreach (var loopBatch in batches)
                    Console.WriteLine(
                        $"  Batch Id {loopBatch.Id} - {loopBatch.CreatedOn} - Copies {loopBatch.CloudCopies.Count(x => x.CopyCompletedSuccessfully)} of {loopBatch.CloudCopies.Count}, Complete Uploads {loopBatch.CloudUploads.Count(x => x.UploadCompletedSuccessfully)} of {loopBatch.CloudUploads.Count} Complete, Deletions {loopBatch.CloudDeletions.Count(x => x.DeletionCompletedSuccessfully)} of {loopBatch.CloudDeletions.Count} Complete, {loopBatch.CloudCopies.Count(x => !string.IsNullOrWhiteSpace(x.ErrorMessage)) + loopBatch.CloudUploads.Count(x => !string.IsNullOrWhiteSpace(x.ErrorMessage)) + loopBatch.CloudDeletions.Count(x => !string.IsNullOrWhiteSpace(x.ErrorMessage))} Errors");
            }

            Log.Information("Cloud Backup Runner - Finished Run");

            await Log.CloseAndFlushAsync();
            return 0;
        }

        if (!int.TryParse(args[1], out var jobId))
        {
            await errorNotifier.Error($"Failed to Parse Job Id {args[1]}");

            Log.Error("Failed to Parse Job Id {jobId}", args[1]);
            Log.Information("Cloud Backup Runner - Finished Run");
            await Log.CloseAndFlushAsync();
            return -1;
        }

        var backupJob = await db.context.BackupJobs.Include(backupJob => backupJob.Batches)
            .SingleOrDefaultAsync(x => x.Id == jobId);

        if (backupJob == null)
        {
            await errorNotifier.Error($"Failed to find a Backup Job with Id {jobId} in {args[0]}");

            Log.Error("Failed to find a Backup Job with Id {jobId} in {dbFile}", jobId, args[0]);
            Log.Information("Cloud Backup Runner - Finished Run");
            await Log.CloseAndFlushAsync();
            return -1;
        }

        DataNotifications.PublishProgressNotification(consoleId.ToString(), Environment.ProcessId, "Starting...",
            backupJob.PersistentId, null);

        progress.PersistentId = backupJob.PersistentId;

        Log.Logger = new LoggerConfiguration().StandardEnrichers().LogToConsole()
            .LogToFileInProgramDirectory("CloudBackupRunner").WriteTo.DelegatingTextSink(
                x => DataNotifications.PublishProgressNotification(consoleId.ToString(), Environment.ProcessId, x,
                    backupJob.PersistentId, null),
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        var cloudCredentials = PasswordVaultTools.GetCredentials(backupJob.VaultS3CredentialsIdentifier);

        if (string.IsNullOrWhiteSpace(cloudCredentials.username) ||
            string.IsNullOrWhiteSpace(cloudCredentials.password))
        {
            await errorNotifier.Error("Cloud Credentials are not Valid?");

            Log.Error(
                $"Cloud Credentials are not Valid? Access Key is blank {string.IsNullOrWhiteSpace(cloudCredentials.username)}, Password is blank {string.IsNullOrWhiteSpace(cloudCredentials.password)}");
            Log.Information("Cloud Backup Runner - Finished Run");
            DataNotifications.PublishRunFinishedNotification("CloudBackupRunner", Environment.ProcessId, "Run Finished", backupJob.PersistentId, null);
            await Log.CloseAndFlushAsync();
            return -1;
        }

        var frozenNow = DateTime.Now;

        if (!Enum.TryParse(backupJob.CloudProvider, out S3Providers provider)) provider = S3Providers.Amazon;

        string serviceUrl;

        if (provider != S3Providers.Amazon)
        {
            serviceUrl =
                PasswordVaultTools.GetCredentials(backupJob.VaultServiceUrlIdentifier).password;

            if (string.IsNullOrWhiteSpace(serviceUrl))
            {
                await errorNotifier.Error("Service URL is not Valid?");

                Log.Error(
                    "Service URL is not Valid? Service URL is Blank.");
                Log.Information("Cloud Backup Runner - Finished Run");
                DataNotifications.PublishRunFinishedNotification("CloudBackupRunner", Environment.ProcessId, "Run Finished", backupJob.PersistentId, null);
                await Log.CloseAndFlushAsync();
                return -1;
            }
        }
        else
        {
            serviceUrl = S3Tools.AmazonServiceUrlFromBucketRegion(backupJob.CloudRegion);
        }

        var amazonCredentials = new S3AccountInformation
        {
            AccessKey = () => cloudCredentials.username,
            Secret = () => cloudCredentials.password,
            ServiceUrl = () => serviceUrl,
            BucketName = () => backupJob.CloudBucket,
            S3Provider = () => provider,
            FullFileNameForJsonUploadInformation = () =>
                Path.Combine(FileLocationHelpers.DefaultStorageDirectory().FullName,
                    $"{frozenNow:yyyy-MM-dd-HH-mm}-{args[0]}.json"),
            FullFileNameForToExcel = () => Path.Combine(FileLocationHelpers.DefaultStorageDirectory().FullName,
                $"{frozenNow:yyyy-MM-dd-HH-mm}-{args[0]}.xlsx")
        };
        CloudTransferBatch? batch = null;

        //3 Args mean that a batch has been specified in one of 3 ways: auto, last, or id. On all of these options
        //a 'bad' option (last when there is no last batch, id that doesn't match anything in the db...) will fall
        //thru to a new batch being generated. 
        if (args.Length == 3)
        {
            //Auto: Very simple - if there is a batch in the last two weeks that is < 95% done and < 10% errors use it
            if (args[2].Equals("auto", StringComparison.OrdinalIgnoreCase) &&
                backupJob.Batches.Any(x => x.CreatedOn > DateTime.Now.AddDays(-14)))
            {
                var mostRecentBatch = backupJob.Batches.MaxBy(x => x.CreatedOn)!;
                var batchStatistics = await BatchStatistics.CreateInstance(mostRecentBatch.Id);
                var totalActions = batchStatistics.CopiesCount + batchStatistics.UploadsCount +
                                   batchStatistics.DeletesCount;

                var successfulActions = batchStatistics.CopiesCompleteCount + batchStatistics.UploadsCompleteCount +
                                        batchStatistics.DeletesCompleteCount;
                var errorActions = batchStatistics.CopiesWithErrorNoteCount +
                                   batchStatistics.UploadsWithErrorNoteCount +
                                   batchStatistics.DeletesWithErrorNoteCount;
                var percentSuccess = totalActions == 0 ? 1 : successfulActions / (decimal)totalActions;
                var percentError = totalActions == 0 ? 0 : errorActions / (decimal)totalActions;
                var highPercentSuccess = percentSuccess > .95M;
                var highPercentErrors = percentError > .10M;

                Console.WriteLine("Auto Batch Selection");
                Console.WriteLine($"  Last Batch: Id {mostRecentBatch.Id} Created On: {mostRecentBatch.CreatedOn}");
                Console.WriteLine($"  Total Actions: {totalActions}");
                Console.WriteLine(
                    $"  Successful Actions: {successfulActions} - {(totalActions == 0 ? 0 : successfulActions / (decimal)totalActions):P0)}");
                Console.WriteLine(
                    $"  Error Actions: {errorActions} - {(totalActions == 0 ? 0 : errorActions / (decimal)totalActions):P0}");
                Console.WriteLine($"    High Percent Success: {highPercentSuccess}");
                Console.WriteLine($"    High Percent Errors: {highPercentErrors}");

                //Case: We have a batch that seems likely to be productive to resume
                if (!highPercentSuccess && !highPercentErrors)
                {
                    batch = mostRecentBatch;
                    Log.ForContext(nameof(batch), batch.SafeObjectDumpNoEnumerables())
                        .Information(
                            "Batch Set to Batch Id {batchId} Based on auto argument and High Percent Success {highPercentSuccess} and High Percent Errors {highPercentErrors}",
                            batch.Id, highPercentSuccess, highPercentErrors);
                }
                //Other cases fall thru to the 'null batch' logic below
            }
            //Last
            else if (args[2].Equals("last", StringComparison.OrdinalIgnoreCase))
            {
                batch = backupJob.Batches.MaxBy(x => x.CreatedOn);
                if (batch != null)
                    Log.ForContext(nameof(batch), batch.SafeObjectDumpNoEnumerables())
                        .Information("Batch Set to Batch Id {batchId} Based on last argument", batch.Id);
                else
                    Log.ForContext(nameof(batch), batch.SafeObjectDumpNoEnumerables())
                        .Information("Setting Batch based on last argument failed");
            }
            //Id
            else if (int.TryParse(args[2], out var parsedBatchId))
            {
                batch = backupJob.Batches.SingleOrDefault(x => x.Id == parsedBatchId);
                if (batch != null)
                    Log.ForContext(nameof(batch), batch.SafeObjectDumpNoEnumerables())
                        .Information("Batch Set to Batch Id {batchId} Based on Id", batch.Id);
                else
                    Log.ForContext(nameof(batch), batch.SafeObjectDumpNoEnumerables())
                        .Information("Setting Batch based on argument Id Argument {batchArgument} Failed", args[2]);
            }
            //New Batch - this will be a new batch and a full rescan
            else if (args[2].Equals("new", StringComparison.OrdinalIgnoreCase))
            {
                var newBatch = await CloudTransfer.CreateBatchInDatabaseFromCloudAndLocalScan(amazonCredentials,
                    backupJob,
                    progress);
                if (newBatch is not null)
                {
                    batch = await db.context.CloudTransferBatches.SingleAsync(x => x.Id == newBatch.Batch.Id);
                }
                else
                {
                    Log.ForContext(nameof(backupJob), backupJob.Dump())
                        .Information(
                            "Comparing Local Files to the Cloud File Cache Files produced no backup actions - Nothing To Do, Stopping");
                    Log.Information("Cloud Backup Runner - Finished Run");
                    DataNotifications.PublishRunFinishedNotification("CloudBackupRunner", Environment.ProcessId, "Run Finished", backupJob.PersistentId, batch.Id);
                    await Log.CloseAndFlushAsync();
                    return 0;
                }
            }
        }

        CloudTransferBatchInformation? batchInformation;

        //Batch equals null here means either that no batch was specified or that the batch specification
        //didn't return anything - either way a new batch is created, the source of the batch changes depends
        //on the age of the last Cloud File Scan.
        if (batch == null)
        {
            if (backupJob.LastCloudFileScan != null && backupJob.LastCloudFileScan > DateTime.Now.AddDays(-180))
                batchInformation = await CloudTransfer.CreateBatchInDatabaseFromCloudCacheFilesAndLocalScan(
                    amazonCredentials,
                    backupJob,
                    progress);
            else
                batchInformation = await CloudTransfer.CreateBatchInDatabaseFromCloudAndLocalScan(amazonCredentials,
                    backupJob,
                    progress);
        }
        else
        {
            batchInformation = await CloudTransferBatchInformation.CreateInstance(batch.Id);
        }


        //Batch is null here means that the local scan and the cloud cache files match - nothing to do

        if (batchInformation == null)
        {
            Log.ForContext(nameof(backupJob), backupJob.Dump())
                .Information(
                    "Comparing Local Files to the Cloud File Cache Files produced no backup actions - Nothing To Do, Stopping");
            Log.Information("Cloud Backup Runner - Finished Run");
            await Log.CloseAndFlushAsync();
            return 0;
        }

        Log.ForContext(nameof(batchInformation), batchInformation.SafeObjectDumpNoEnumerables()).Information(
            "Using Batch Id {batchId} with {copyCount} Copies, {uploadCount} Uploads and {deleteCount} Deletes",
            batchInformation.Batch.Id,
            batchInformation.CloudCopies.Count, batchInformation.CloudUploads.Count,
            batchInformation.CloudDeletions.Count);

        if (batchInformation.CloudCopies.Count < 1 && batchInformation.CloudUploads.Count < 1 &&
            batchInformation.CloudDeletions.Count < 1)
        {
            Log.Information("Cloud Backup Ending - No Copies, Uploads or Deletions for Job Id {jobId} batch {batchId}",
                backupJob.Id,
                batchInformation.Batch.Id);
            Log.Information("Cloud Backup Runner - Finished Run");
            DataNotifications.PublishRunFinishedNotification("CloudBackupRunner", Environment.ProcessId, "Run Finished", backupJob.PersistentId, batchInformation.Batch.Id);
            await Log.CloseAndFlushAsync();
            return 0;
        }

        progress.BatchId = batchInformation.Batch.Id;

        var returnValue = 0;

        try
        {
            var runInformation =
                await CloudTransfer.CloudCopyUploadAndDelete(amazonCredentials, batchInformation.Batch.Id, startTime,
                    progress);
            Log.ForContext(nameof(runInformation), runInformation, true).Information("Cloud Backup Ending");

            var batchReport = await BatchReportToExcel.Run(batchInformation.Batch.Id, progress);

            (await WindowsNotificationBuilders.NewNotifier("Cloud Backup Runner"))
                .SetAutomationLogoNotificationIconUrl().MessageWithFile(
                    $"Uploaded {FileAndFolderTools.GetBytesReadable(runInformation.UploadedSize)} in {(runInformation.Ended - runInformation.Started).TotalHours:N2} Hours{(runInformation.CopyErrorCount + runInformation.DeleteErrorCount + runInformation.UploadErrorCount > 0 ? $" - {runInformation.CopyErrorCount + runInformation.DeleteErrorCount + runInformation.UploadErrorCount}  Errors" : string.Empty)} - Click for Report",
                    batchReport);
        }
        catch (Exception e)
        {
            Log.Error(e, "Error Running Program...");
            Console.WriteLine(e);

            await (await WindowsNotificationBuilders.NewNotifier("Cloud Backup Runner"))
                .SetAutomationLogoNotificationIconUrl().SetErrorReportAdditionalInformationMarkdown(
                    EmbeddedResourceTools.GetEmbeddedResourceText("README.md")).Error(e);

            returnValue = -1;
        }
        finally
        {
            Log.Information("Cloud Backup Runner - Finished Run");
            DataNotifications.PublishRunFinishedNotification("CloudBackupRunner", Environment.ProcessId, "Run Finished", backupJob.PersistentId, batchInformation.Batch.Id);
            await Log.CloseAndFlushAsync();
        }

        return returnValue;
    }
}