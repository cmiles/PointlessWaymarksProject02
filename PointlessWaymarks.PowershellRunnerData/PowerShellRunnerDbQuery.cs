using Microsoft.EntityFrameworkCore;
using PointlessWaymarks.CommonTools;
using PointlessWaymarks.PowerShellRunnerData.Models;
using Serilog;

namespace PointlessWaymarks.PowerShellRunnerData;

public static class PowerShellRunnerDbQuery
{
    public const string DbPersistentIdSettingsKey = "DbPersistentIdSettingsKey";
    public const string ObfuscationService = "https://pointlesswaymarks.powershellrunner.private";
    public const string ObfuscationServiceAccountSettingsKey = "ObfuscationServiceAccountSettingsKey";

    public static async Task<PowerShellRunnerDayStatistics> DayStatistics(string dbFileName)
    {
        var frozenStart = DateTime.Now.Date;
        var returnStats = new PowerShellRunnerDayStatistics { Day = frozenStart };
        var db = await PowerShellRunnerDbContext.CreateInstance(dbFileName);
        var runs = db.ScriptJobRuns
            .Where(x => x.CompletedOnUtc != null && x.CompletedOnUtc.Value >= frozenStart.ToUniversalTime())
            .ToList();
        returnStats.Success = runs.Count(x => !x.Errors);
        returnStats.Error = runs.Count(x => x.Errors);
        returnStats.Running = runs.Count(x => x.CompletedOnUtc is null);
        return returnStats;
    }

    public static async Task<Guid> DbId(this PowerShellRunnerDbContext context)
    {
        var keyValuePair = await context.PowerShellRunnerSettings.SingleAsync(x => x.Key == DbPersistentIdSettingsKey);

        return Guid.Parse(keyValuePair.Value);
    }

    public static async Task<Guid> DbId(string dbFileName)
    {
        var db = await PowerShellRunnerDbContext.CreateInstance(dbFileName);
        return await db.DbId();
    }

    public static async Task DeleteScriptJobRunsBasedOnDeleteScriptJobRunsAfterMonthsSetting(string dbFileName)
    {
        var db = await PowerShellRunnerDbContext.CreateInstance(dbFileName);
        var dbId = await DbId(dbFileName);
        var allJob = await db.ScriptJobs.OrderByDescending(x => x.Name).ToListAsync();

        var frozenUtcNow = DateTime.UtcNow;

        foreach (var loopJobs in allJob)
        {
            var deleteBefore = frozenUtcNow.AddMonths(-Math.Abs(loopJobs.DeleteScriptJobRunsAfterMonths));

            var runsToDelete = await db.ScriptJobRuns
                .Where(x => x.ScriptJobPersistentId == loopJobs.PersistentId &&
                            ((x.StartedOnUtc < deleteBefore && x.CompletedOnUtc == null) ||
                             (x.StartedOnUtc < deleteBefore && x.CompletedOnUtc < deleteBefore)))
                .ToListAsync();

            db.ScriptJobRuns.RemoveRange(runsToDelete);
            await db.SaveChangesAsync();

            Log.ForContext("JobPersistentId", loopJobs.PersistentId)
                .ForContext(nameof(runsToDelete), runsToDelete.SafeObjectDump()).Information(
                    "Deleting {0} ScriptJobRuns from '{1}' because they are older than {2} (UTC)", runsToDelete.Count,
                    loopJobs.Name, deleteBefore);

            foreach (var loopDeleteRuns in runsToDelete)
                DataNotifications.PublishRunDataNotification($"Automated Deletes for Runs Before {deleteBefore:G}",
                    DataNotifications.DataNotificationUpdateType.Delete, dbId, loopJobs.PersistentId,
                    loopDeleteRuns.PersistentId);
        }
    }

    public static async Task<string?> ObfuscationAccountName(this PowerShellRunnerDbContext context)
    {
        var possibleEntry =
            await context.PowerShellRunnerSettings.FirstOrDefaultAsync(x =>
                x.Key == ObfuscationServiceAccountSettingsKey);

        return possibleEntry?.Value;
    }

    public static async Task<string> ObfuscationAccountNameWithCreateAsNeeded(this PowerShellRunnerDbContext context)
    {
        //Clear any invalid entries
        var invalidEntries =
            context.PowerShellRunnerSettings.Where(x =>
                x.Key == ObfuscationServiceAccountSettingsKey && string.IsNullOrWhiteSpace(x.Value));

        if (invalidEntries.Any())
        {
            await invalidEntries.ExecuteDeleteAsync();
            await context.SaveChangesAsync();
        }

        var currentSettings = await context.PowerShellRunnerSettings
            .Where(x => x.Key == ObfuscationServiceAccountSettingsKey)
            .ToListAsync();

        if (currentSettings.Count > 1) context.PowerShellRunnerSettings.RemoveRange(currentSettings.Skip(1));

        if (currentSettings.Any()) return currentSettings[0].Value;

        await context.SetNewObfuscationAccountName();

        return (await context.ObfuscationAccountName())!;
    }

    public static async Task<List<PowerShellRunnerPreviousDayStatistics>> PreviousDayStatistics(string dbFileName,
        int daysBack,
        List<PowerShellRunnerPreviousDayStatistics> existingData)
    {
        var frozenStart = DateTime.Now.AddDays(-1);
        var returnList = new List<PowerShellRunnerPreviousDayStatistics>();

        for (var i = 0; i <= daysBack; i++)
            returnList.Add(new PowerShellRunnerPreviousDayStatistics { Day = frozenStart.AddDays(-i).Date });

        var db = await PowerShellRunnerDbContext.CreateInstance(dbFileName);

        existingData = existingData.OrderByDescending(x => x.Day).Skip(1).ToList();

        var minUtc = returnList.Min(y => y.Day).ToUniversalTime();

        var timeLimitedRuns = await db.ScriptJobRuns
            .Where(x => x.CompletedOnUtc != null &&
                        x.CompletedOnUtc >= minUtc).ToListAsync();

        foreach (var loopStats in returnList)
        {
            var possibleExistingData = existingData.SingleOrDefault(x => x.Day == loopStats.Day);

            if (possibleExistingData != null)
            {
                loopStats.Success = possibleExistingData.Success;
                loopStats.Error = possibleExistingData.Error;
                continue;
            }

            var runs = timeLimitedRuns.Where(x => x.CompletedOnUtc != null && x.CompletedOnUtc.Value.ToLocalTime().Date == loopStats.Day).ToList();

            loopStats.Success = runs.Count(x => !x.Errors);
            loopStats.Error = runs.Count(x => x.Errors);
        }

        return returnList.OrderByDescending(x => x.Day).ToList();
    }

    private static async Task SetNewObfuscationAccountName(this PowerShellRunnerDbContext context)
    {
        await context.PowerShellRunnerSettings.Where(x => x.Key == ObfuscationServiceAccountSettingsKey)
            .ExecuteDeleteAsync();

        await context.PowerShellRunnerSettings.AddAsync(new PowerShellRunnerSetting
        {
            Key = ObfuscationServiceAccountSettingsKey,
            Value = Guid.NewGuid().ToString()
        });

        await context.SaveChangesAsync();
    }

    public static async Task VerifyOrAddDbId(this PowerShellRunnerDbContext context)
    {
        var existingKey =
            await context.PowerShellRunnerSettings.FirstOrDefaultAsync(x => x.Key == DbPersistentIdSettingsKey);

        if (existingKey != null && Guid.TryParse(existingKey.Value, out _)) return;

        await context.PowerShellRunnerSettings.Where(x => x.Key == DbPersistentIdSettingsKey).ExecuteDeleteAsync();

        await context.PowerShellRunnerSettings.AddAsync(new PowerShellRunnerSetting
        {
            Key = DbPersistentIdSettingsKey,
            Value = Guid.NewGuid().ToString()
        });

        await context.SaveChangesAsync();
    }
}