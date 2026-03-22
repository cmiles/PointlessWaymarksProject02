using System.Collections.ObjectModel;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using Microsoft.EntityFrameworkCore;
using PointlessWaymarks.CommonTools;
using PointlessWaymarks.PowerShellRunnerData.Models;
using Serilog;
using TinyIpc.Messaging;

namespace PointlessWaymarks.PowerShellRunnerData;

public class AlwaysRunningJobExecution
{
    private readonly CancellationTokenSource _stopCts = new();
    private Guid _dbId;
    private string _obfuscationKey = string.Empty;
    private Pipeline? _pipeline;
    private Guid? _runId;
    private bool _restartRequested;
    internal Func<ScriptJobRun, Task>? CallbackAfterRunFirstSave;
    public required string DatabaseFile;
    public required Guid JobId;
    public required string RunType;

    internal AlwaysRunningJobExecution()
    {
        DataNotifications.NewDataNotificationChannel().MessageReceived += OnDataNotificationReceived;
    }

    internal async Task Execute()
    {
        _obfuscationKey = await ObfuscationKeyHelpers.GetObfuscationKey(DatabaseFile);
        _dbId = await PowerShellRunnerDbQuery.DbId(DatabaseFile);

        while (!_stopCts.IsCancellationRequested)
        {
            await RunOnce();

            if (!_restartRequested) break;

            _restartRequested = false;
        }
    }

    private async Task RunOnce()
    {
        var db = await PowerShellRunnerDbContext.CreateInstance(DatabaseFile);
        var job = await db.ScriptJobs.FirstOrDefaultAsync(x => x.PersistentId == JobId);

        if (job == null) return;

        var run = new ScriptJobRun
        {
            ScriptJobPersistentId = job.PersistentId,
            PersistentId = Guid.NewGuid(),
            StartedOnUtc = DateTime.UtcNow,
            Script = job.Script,
            RunType = RunType
        };

        db.ScriptJobRuns.Add(run);
        await db.SaveChangesAsync();

        _runId = run.PersistentId;

        DataNotifications.PublishRunDataNotification(nameof(AlwaysRunningJobExecution),
            DataNotifications.DataNotificationUpdateType.New, _dbId, run.ScriptJobPersistentId, run.PersistentId);

        if (CallbackAfterRunFirstSave != null) await CallbackAfterRunFirstSave(run);

        await using var runLog = new RunLog(DatabaseFile, _obfuscationKey, run.PersistentId);
        var exitReason = string.Empty;

        try
        {
            var decryptedScript = job.Script.Decrypt(_obfuscationKey);

            runLog.Add($"{DateTime.Now:G}>> Always-running task started");

            await ExecuteScript(decryptedScript, _dbId, job.PersistentId, run.PersistentId,
                job.Name, job.ScriptType, runLog);

            if (_restartRequested)
                exitReason = "ScheduledRestart";
            else if (_stopCts.IsCancellationRequested)
                exitReason = "UserCancelled";
            else
                exitReason = "ScriptExited";
        }
        catch (Exception e)
        {
            runLog.SetErrored();
            runLog.Add($"{DateTime.Now:G}>> Exception: {e.Message}");
            exitReason = "Error";

            Console.WriteLine(e);
            Log.Error(e, "Error Running Always-Running Script");
        }
        finally
        {
            runLog.Add($"{DateTime.Now:G}>> Always-running task ended - {exitReason}");

            await runLog.FlushAsync();

            run.CompletedOnUtc = DateTime.UtcNow;
            run.LengthInSeconds = (int)(run.CompletedOnUtc!.Value - run.StartedOnUtc).TotalSeconds;
            if (run.LengthInSeconds == 0) run.LengthInSeconds = 1;
            run.Errors = runLog.HasErrors;
            run.ExitReason = exitReason;

            await db.SaveChangesAsync();

            _runId = null;

            DataNotifications.PublishRunDataNotification(nameof(AlwaysRunningJobExecution),
                DataNotifications.DataNotificationUpdateType.Update, _dbId, run.ScriptJobPersistentId,
                run.PersistentId);
        }
    }

    internal void RequestRestart()
    {
        _restartRequested = true;

        try
        {
            _pipeline?.StopAsync();
        }
        catch (Exception exception)
        {
            Console.WriteLine(exception);
        }
    }

    internal void RequestStop()
    {
        _restartRequested = false;
        _stopCts.Cancel();

        try
        {
            _pipeline?.StopAsync();
        }
        catch (Exception exception)
        {
            Console.WriteLine(exception);
        }
    }

    private async Task ExecuteScript(string toInvoke, Guid databaseId,
        Guid jobId, Guid runId,
        string identifier, string scriptType, RunLog runLog)
    {
        var initialSessionState = InitialSessionState.CreateDefault();

        var runSpace = RunspaceFactory.CreateRunspace(initialSessionState);
        runSpace.Open();

        _pipeline = runSpace.CreatePipeline();
        DirectoryInfo? tempRunDirectory = null;

        if (scriptType == nameof(ScriptKind.DotNetSingleFile))
        {
            tempRunDirectory = FileLocationHelpers.RunCodeTempDirectory(runId);

            runLog.Add($"Program directory: {tempRunDirectory.FullName}");

            var tempCsFile = Path.Combine(tempRunDirectory.FullName,
                $"pw-dnr--{JobId.ToString().Replace("-", string.Empty)}.cs");
            await File.WriteAllTextAsync(tempCsFile, toInvoke);
            _pipeline.Commands.AddScript(
                $"& dotnet run {tempCsFile} --artifacts-path {tempRunDirectory.FullName}");
        }
        else
        {
            _pipeline.Commands.AddScript(toInvoke);
        }

        _pipeline.Input.Close();

        _pipeline.Output.DataReady += (_, _) =>
        {
            Collection<PSObject> psObjects = _pipeline.Output.NonBlockingRead();
            foreach (var psObject in psObjects)
            {
                runLog.Add($"{DateTime.Now:G}>> {psObject.ToString()}");
                DataNotifications.PublishPowershellProgressNotification(identifier, databaseId, jobId, runId,
                    psObject.ToString());
            }
        };

        _pipeline.StateChanged += (_, eventArgs) =>
        {
            runLog.Add(
                $"{DateTime.Now:G}>> State: {eventArgs.PipelineStateInfo.State} {eventArgs.PipelineStateInfo.Reason?.ToString() ?? string.Empty}");

            DataNotifications.PublishPowershellStateNotification(identifier, databaseId, jobId, runId,
                eventArgs.PipelineStateInfo.State,
                eventArgs.PipelineStateInfo.Reason?.ToString() ?? string.Empty);
        };

        _pipeline.Error.DataReady += (_, _) =>
        {
            Collection<object> errorObjects = _pipeline.Error.NonBlockingRead();
            if (errorObjects.Count == 0) return;

            runLog.SetErrored();
            foreach (var errorObject in errorObjects)
            {
                var errorString = errorObject.ToString();
                runLog.Add($"{DateTime.Now:G}>> Error: {errorString}");
                if (!string.IsNullOrWhiteSpace(errorString))
                    DataNotifications.PublishPowershellProgressNotification(identifier, databaseId, jobId, runId,
                        errorString);
            }
        };

        _pipeline.InvokeAsync();

        await Task.Delay(200);

        while (_pipeline.PipelineStateInfo.State == PipelineState.Running) await Task.Delay(250);

        if (tempRunDirectory is not null && tempRunDirectory.Exists) tempRunDirectory.Delete(true);

        if (_pipeline.HadErrors) runLog.SetErrored();
    }

    private void OnDataNotificationReceived(object? sender, TinyMessageReceivedEventArgs e)
    {
        if (_pipeline == null || _runId == null) return;

        var translatedMessage = DataNotifications.TranslateDataNotification(e.Message.ToString());

        if (translatedMessage.IsT6 && _runId.HasValue)
        {
            var openRequest = translatedMessage.AsT6;
            if (openRequest.DatabaseId != _dbId) return;

            DataNotifications.PublishOpenJobsResponse("Always Running Task", _dbId, _runId.Value);
            return;
        }

        if (translatedMessage.IsT5)
        {
            var cancelRequest = translatedMessage.AsT5;

            if (cancelRequest.DatabaseId != _dbId) return;
            if (cancelRequest.RunPersistentId != _runId) return;

            RequestStop();
        }
    }
}
