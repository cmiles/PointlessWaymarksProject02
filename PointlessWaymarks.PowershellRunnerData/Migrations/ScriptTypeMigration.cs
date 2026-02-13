using System.Data;
using FluentMigrator;

namespace PointlessWaymarks.PowerShellRunnerData.Migrations;

[Migration(202602130700)]
public class ScriptTypeMigration : Migration
{
    public override void Down()
    {
        throw new DataException(
            $"No Down Available for Migration {nameof(AddDeleteScriptJobRunsAfterMonthsMigration)}");
    }

    public override void Up()
    {
        Execute.Sql("UPDATE ScriptJobs SET ScriptType = 'DotNetSingleFile' WHERE ScriptType = 'CsScript'");
        Execute.Sql("UPDATE ScriptJobRuns SET RunType = 'DotNetSingleFile' WHERE RunType = 'CsScript'");
    }
}