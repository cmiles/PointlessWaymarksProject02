using System.Data;
using FluentMigrator;

namespace PointlessWaymarks.PowerShellRunnerData.Migrations;

[Migration(202603160800)]
public class AddExitReasonToScriptJobRunMigration : Migration
{
    public override void Down()
    {
        throw new DataException(
            $"No Down Available for Migration {nameof(AddExitReasonToScriptJobRunMigration)}");
    }

    public override void Up()
    {
        if (Schema.Table("ScriptJobRuns").Column("ExitReason").Exists())
            return;

        Execute.Sql(@"ALTER TABLE ScriptJobRuns 
                    ADD COLUMN ExitReason TEXT NOT NULL DEFAULT ''");
    }
}
