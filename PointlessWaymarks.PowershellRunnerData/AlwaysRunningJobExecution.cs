using System.Collections.Concurrent;
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
    private static readonly ConcurrentDictionary<(string databaseFile, Guid jobId), AlwaysRunningJobExecution>
        ActiveExecutions = new();

    private readonly CancellationTokenSource _stopCts = new();
    private Guid _dbId;
    private string _obfuscationKey = string.Empty;
    private Pipeline? _pipeline;
    private Guid? _runId;
    private bool _restartRequested;
    public Func<ScriptJobRun, Task>? CallbackAfterRunFirstSave;
    public required string DatabaseFile;
    public required Guid JobId;
    public required string RunType;

    public AlwaysRunningJobExecution()
    {
        DataNotifications.NewDataNotificationChannel().MessageReceived += OnDataNotificationReceived;
    }

    private static string NormalizeDatabasePath(string dbPath) => Path.GetFullPath(dbPath).Trim().ToLowerInvariant();

    public async Task Execute()
    {
        var key = (NormalizeDatabasePath(DatabaseFile), JobId);
        ActiveExecutions[key] = this;

        try
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
        catch (Exception e)
        {
            Console.WriteLine(e);
            Log.Error(e, "Error Running Always-Running Script Execution");
        }
        finally
        {
            ActiveExecutions.TryRemove(KeyValuePair.Create(key, this));
        }
    }

    public static AlwaysRunningJobExecution StartAlwaysRunningJob(Guid jobId, string databaseFile,
        string runType,
        Func<ScriptJobRun, Task>? callbackAfterRunFirstSave = null)
    {
        var key = (NormalizeDatabasePath(databaseFile), jobId);

        if (ActiveExecutions.TryGetValue(key, out var existing) && !existing._stopCts.IsCancellationRequested)
        {
            return existing;
        }

        var runner = new AlwaysRunningJobExecution
        {
            CallbackAfterRunFirstSave = callbackAfterRunFirstSave,
            DatabaseFile = databaseFile,
            JobId = jobId,
            RunType = runType
        };

        _ = Task.Run(runner.Execute);

        return runner;
    }

    public static AlwaysRunningJobExecution RestartAlwaysRunningJob(Guid jobId, string databaseFile,
        string runType = "Main Program Timer",
        Func<ScriptJobRun, Task>? callbackAfterRunFirstSave = null)
    {
        var key = (NormalizeDatabasePath(databaseFile), jobId);

        if (ActiveExecutions.TryGetValue(key, out var existing) && !existing._stopCts.IsCancellationRequested)
        {
            existing.RunType = runType;
            if (callbackAfterRunFirstSave != null)
                existing.CallbackAfterRunFirstSave = callbackAfterRunFirstSave;
            existing.RequestRestart();
            return existing;
        }

        return StartAlwaysRunningJob(jobId, databaseFile, runType, callbackAfterRunFirstSave);
    }

    public static bool RequestRestart(Guid jobId, string databaseFile)
    {
        var key = (NormalizeDatabasePath(databaseFile), jobId);

        if (ActiveExecutions.TryGetValue(key, out var existing) && !existing._stopCts.IsCancellationRequested)
        {
            existing.RequestRestart();
            return true;
        }

        return false;
    }

    public static bool RequestRestart(Guid jobId)
    {
        var matched = ActiveExecutions.Values.Where(x => x.JobId == jobId && !x._stopCts.IsCancellationRequested).ToList();
        if (!matched.Any()) return false;

        foreach (var execution in matched)
        {
            execution.RequestRestart();
        }

        return true;
    }

    public static bool RequestRestartByDatabaseId(Guid dbId, Guid jobId)
    {
        var matched = ActiveExecutions.Values.Where(x => x._dbId == dbId && x.JobId == jobId && !x._stopCts.IsCancellationRequested).ToList();
        if (!matched.Any()) return false;

        foreach (var execution in matched)
        {
            execution.RequestRestart();
        }

        return true;
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

    public void RequestRestart()
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

    public void RequestStop()
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

        using var runSpace = RunspaceFactory.CreateRunspace(initialSessionState);
        runSpace.Open();

        _pipeline = runSpace.CreatePipeline();
        DirectoryInfo? tempRunDirectory = null;

        try
        {
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

            if (_restartRequested || _stopCts.IsCancellationRequested)
            {
                _pipeline.StopAsync();
            }
            else
            {
                _pipeline.InvokeAsync();
            }

            await Task.Delay(200);

            while (_pipeline.PipelineStateInfo.State is PipelineState.Running or PipelineState.Stopping)
                await Task.Delay(250);

            if (tempRunDirectory is not null && tempRunDirectory.Exists) tempRunDirectory.Delete(true);

            Collection<object> remainingErrors = _pipeline.Error.NonBlockingRead();
            if (remainingErrors.Count > 0)
            {
                runLog.SetErrored();
                foreach (var errorObject in remainingErrors)
                {
                    var errorString = errorObject.ToString();
                    runLog.Add($"{DateTime.Now:G}>> Error: {errorString}");
                    if (!string.IsNullOrWhiteSpace(errorString))
                        DataNotifications.PublishPowershellProgressNotification(identifier, databaseId, jobId, runId,
                            errorString);
                }
            }

            if (_pipeline.PipelineStateInfo.State == PipelineState.Failed ||
                (_pipeline.PipelineStateInfo.Reason is not null && _pipeline.PipelineStateInfo.Reason is not PipelineStoppedException) ||
                (_pipeline.PipelineStateInfo.State != PipelineState.Stopped && _pipeline.HadErrors))
            {
                runLog.SetErrored();
            }
        }
        finally
        {
            try
            {
                _pipeline.Dispose();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            _pipeline = null;
        }
    }

    private void OnDataNotificationReceived(object? sender, TinyMessageReceivedEventArgs e)
    {
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
            return;
        }
    }
}
