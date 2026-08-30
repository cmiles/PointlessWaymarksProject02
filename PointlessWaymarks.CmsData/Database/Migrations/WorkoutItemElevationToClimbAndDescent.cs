using System.Data;
using FluentMigrator;

namespace PointlessWaymarks.CmsData.Database.Migrations;

[Migration(202608301000)]
public class WorkoutItemElevationToClimbAndDescent : Migration
{
    public override void Down()
    {
        throw new DataException("No Down Available for Migration WorkoutItemElevationToClimbAndDescent");
    }

    public override void Up()
    {
        if (!Schema.Table("WorkoutItems").Exists()) return;

        if (!Schema.Table("WorkoutItems").Column("ClimbFeet").Exists())
        {
            Execute.Sql("ALTER TABLE WorkoutItems ADD COLUMN ClimbFeet INTEGER NOT NULL DEFAULT 0;");
        }

        if (!Schema.Table("WorkoutItems").Column("DescentFeet").Exists())
        {
            Execute.Sql("ALTER TABLE WorkoutItems ADD COLUMN DescentFeet INTEGER NOT NULL DEFAULT 0;");
        }

        if (Schema.Table("WorkoutItems").Column("ElevationFeet").Exists())
        {
            Execute.Sql("UPDATE WorkoutItems SET ClimbFeet = COALESCE(ElevationFeet, 0);");
            Execute.Sql("ALTER TABLE WorkoutItems DROP COLUMN ElevationFeet;");
        }
    }
}
