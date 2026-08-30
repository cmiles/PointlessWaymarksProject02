using System.Data;
using FluentMigrator;

namespace PointlessWaymarks.CmsData.Database.Migrations;

[Migration(202608231120)]
public class AddWorkoutItemTable : Migration
{
    public override void Down()
    {
        throw new DataException("No Down Available for Migration AddWorkoutItemTable");
    }

    public override void Up()
    {
        if (!Schema.Table("WorkoutItems").Exists())
            Execute.Sql("""
                        CREATE TABLE "WorkoutItems" (
                            "Id" INTEGER NOT NULL CONSTRAINT "PK_WorkoutItems" PRIMARY KEY AUTOINCREMENT,
                            "Calories" INTEGER NULL,
                            "ContentId" TEXT NOT NULL,
                            "DistanceMiles" REAL NULL,
                            "DurationMinutes" INTEGER NOT NULL,
                            "ElevationFeet" INTEGER NULL,
                            "Note" TEXT NOT NULL,
                            "WorkoutBy" TEXT NOT NULL,
                            "WorkoutOn" TEXT NOT NULL,
                            "WorkoutType" TEXT NOT NULL
                        )
                        """);
    }
}