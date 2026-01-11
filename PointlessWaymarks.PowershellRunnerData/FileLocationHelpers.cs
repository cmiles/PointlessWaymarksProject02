using Microsoft.EntityFrameworkCore;
using PointlessWaymarks.CommonTools;
using Serilog;

namespace PointlessWaymarks.PowerShellRunnerData;

public static class FileLocationHelpers
{
    public static DirectoryInfo CodeTempDirectory()
    {
        var directory =
            new DirectoryInfo(Path.Combine(Path.Combine(Path.GetTempPath(), "pw-psr-temp")));

        if (!directory.Exists) directory.Create();

        directory.Refresh();

        return directory;
    }

    public static DirectoryInfo DefaultStorageDirectory()
    {
        var directory =
            new DirectoryInfo(Path.Combine(FileLocationTools.DefaultStorageDirectory().FullName,
                "Powershell Run and Record"));

        if (!directory.Exists) directory.Create();

        directory.Refresh();

        return directory;
    }

    public static DirectoryInfo ReportsDirectory()
    {
        var directory =
            new DirectoryInfo(Path.Combine(DefaultStorageDirectory().FullName,
                "Reports"));

        if (!directory.Exists) directory.Create();

        directory.Refresh();

        return directory;
    }

    public static DirectoryInfo RunCodeTempDirectory(Guid runId)
    {
        var programTempDirectory = CodeTempDirectory();

        var directory =
            new DirectoryInfo(Path.Combine(programTempDirectory.FullName,
                runId.ToString()));

        if (!directory.Exists) directory.Create();

        directory.Refresh();

        return directory;
    }

    public static async Task RunCodeTempDirectoryCleanUp(string databaseFile)
    {
        await using var db = await PowerShellRunnerDbContext.CreateInstance(databaseFile);
        var activeRuns = await db.ScriptJobRuns
            .Where(run => run.CompletedOnUtc == null)
            .Select(run => run.PersistentId)
            .ToListAsync();

        var activeRunIds = activeRuns.ToHashSet();
        var codeTemp = CodeTempDirectory();

        foreach (var directory in codeTemp.GetDirectories())
        {
            if (!Guid.TryParse(directory.Name, out var directoryId)) continue;
            if (activeRunIds.Contains(directoryId)) continue;

            try
            {
                directory.Delete(true);
            }
            catch (Exception exception)
            {
                Log.Error(exception,
                    "Failed to delete temp directory '{DirectoryFullName}': {ExceptionMessage}", directory.FullName,
                    exception.Message);
            }
        }
    }
}