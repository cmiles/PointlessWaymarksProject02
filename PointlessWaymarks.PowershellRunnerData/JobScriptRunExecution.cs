using System.Collections.ObjectModel;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using Microsoft.EntityFrameworkCore;
using PointlessWaymarks.CommonTools;
using PointlessWaymarks.PowerShellRunnerData.Models;
using Serilog;
using TinyIpc.Messaging;

namespace PointlessWaymarks.PowerShellRunnerData;

internal class JobScriptRunExecution
{
    private Guid _dbId;
    private Pipeline? _pipeline;
    private Guid? _runId;
    internal required bool AllowSimultaneousRuns;
    internal Func<ScriptJobRun, Task>? CallbackAfterJobFirstSave;
    internal required string DatabaseFile;
    internal required Guid JobId;
    internal required string RunType;

    internal JobScriptRunExecution()
    {
        DataNotifications.NewDataNotificationChannel().MessageReceived += OnDataNotificationReceived;
    }

    internal async Task<ScriptJobRun?> ExecuteJob()
    {
        var db = await PowerShellRunnerDbContext.CreateInstance(DatabaseFile);
        var job = await db.ScriptJobs.FirstOrDefaultAsync(x => x.PersistentId == JobId);

        if (job == null) return null;

        var run = new ScriptJobRun
        {
            ScriptJobPersistentId = job.PersistentId, PersistentId = Guid.NewGuid(), StartedOnUtc = DateTime.UtcNow,
            Script = job.Script, RunType = RunType
        };

        if (AllowSimultaneousRuns)
        {
            run = new ScriptJobRun
            {
                ScriptJobPersistentId = job.PersistentId, PersistentId = Guid.NewGuid(), StartedOnUtc = DateTime.UtcNow,
                Script = job.Script, RunType = RunType
            };

            db.ScriptJobRuns.Add(run);
            await db.SaveChangesAsync();
        }
        else
        {
            await using var transaction = await db.Database.BeginTransactionAsync();

            try
            {
                // Check if there exists a ScriptJobRun with a null CompletedOnUtc for the given jobId
                var exists = await db.ScriptJobRuns
                    .AnyAsync(x => x.ScriptJobPersistentId == JobId && x.CompletedOnUtc == null);

                if (!exists)
                {
                    await db.ScriptJobRuns.AddAsync(run);
                    await db.SaveChangesAsync();
                }

                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                Log.ForContext(nameof(AllowSimultaneousRuns), AllowSimultaneousRuns)
                    .ForContext(nameof(RunType), RunType)
                    .ForContext("CallbackAfterJobFirstSaveIsNull", CallbackAfterJobFirstSave == null)
                    .Error(ex,
                        "Failed to Create ScriptJobRun with Simultaneous Run Guard for JobId: {JobId}, DatabaseFile: {DatabaseFile}",
                        JobId, DatabaseFile);

                await transaction.RollbackAsync();
                throw;
            }
        }

        _runId = run.PersistentId;

        var obfuscationKey = await ObfuscationKeyHelpers.GetObfuscationKey(DatabaseFile);
        _dbId = await PowerShellRunnerDbQuery.DbId(DatabaseFile);

        DataNotifications.PublishRunDataNotification(nameof(ExecuteJob),
            DataNotifications.DataNotificationUpdateType.New, _dbId, run.ScriptJobPersistentId, run.PersistentId);

        if (CallbackAfterJobFirstSave != null) await CallbackAfterJobFirstSave(run);

        await using var runLog = new RunLog(DatabaseFile, obfuscationKey, run.PersistentId);

        try
        {
            var decryptedScript = job.Script.Decrypt(obfuscationKey);

            await ExecuteScript(decryptedScript, _dbId, job.PersistentId, run.PersistentId,
                job.Name, job.ScriptType, runLog);
        }
        catch (Exception e)
        {
            runLog.SetErrored();
            runLog.Add($"{DateTime.Now:G}>> Exception: {e.Message}");

            Console.WriteLine(e);
            Log.Error(e, "Error Running Script");
        }
        finally
        {
            await runLog.FlushAsync();

            run.CompletedOnUtc = DateTime.UtcNow;

            run.LengthInSeconds = (int)(run.CompletedOnUtc!.Value - run.StartedOnUtc).TotalSeconds;
            if (run.LengthInSeconds == 0) run.LengthInSeconds = 1;

            run.Errors = runLog.HasErrors;

            await db.SaveChangesAsync();

            DataNotifications.PublishRunDataNotification(nameof(ExecuteJob),
                DataNotifications.DataNotificationUpdateType.Update, _dbId, run.ScriptJobPersistentId,
                run.PersistentId);
        }

        return run;
    }

    private async Task ExecuteScript(string toInvoke, Guid databaseId,
        Guid jobId, Guid runId,
        string identifier, string scriptType, RunLog runLog)
    {
        var initialSessionState = InitialSessionState.CreateDefault();
        // 2024-7-23: Leaving this code commented to consider - this would make this program's
        // PowerShell execution a lot more like what the user expects at a command prompt which
        // is good, but it also makes it a lot less portable. I wonder if the Vanilla runspace
        // is the better compromise?
        //initialSessionState.ImportPSModule(["*"]); // Attempt to preload all available modules
        //initialSessionState.ExecutionPolicy = Microsoft.PowerShell.ExecutionPolicy.Unrestricted;

        // create Powershell runspace
        var runSpace = RunspaceFactory.CreateRunspace(initialSessionState);

        // open it
        runSpace.Open();

        // create a pipeline and feed it the script text
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

            DataNotifications.PublishOpenJobsResponse("Power Shell Runner", _dbId, _runId.Value);
            return;
        }

        if (translatedMessage.IsT5)
        {
            var cancelRequest = translatedMessage.AsT5;

            if (cancelRequest.DatabaseId != _dbId) return;
            if (cancelRequest.RunPersistentId != _runId) return;

            try
            {
                _pipeline.StopAsync();
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
            }
        }
    }
}