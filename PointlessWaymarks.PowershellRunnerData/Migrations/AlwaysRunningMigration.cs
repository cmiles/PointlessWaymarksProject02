using System.Data;
using FluentMigrator;

namespace PointlessWaymarks.PowerShellRunnerData.Migrations;

[Migration(202603160700)]
public class AlwaysRunningMigration : Migration
{
    public override void Down()
    {
        throw new DataException(
            $"No Down Available for Migration {nameof(AlwaysRunningMigration)}");
    }

    public override void Up()
    {
        if (Schema.Table("ScriptJobs").Column("AlwaysRunning").Exists())
            return;

        Execute.Sql(@"ALTER TABLE ScriptJobs 
                    ADD COLUMN AlwaysRunning INTEGER NOT NULL DEFAULT 0");
    }
}
