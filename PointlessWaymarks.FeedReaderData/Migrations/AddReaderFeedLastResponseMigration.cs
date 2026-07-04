using System.Data;
using FluentMigrator;

namespace PointlessWaymarks.FeedReaderData.Migrations;

[Migration(202607030000)]
public class AddFeedLastResponseMigration : Migration
{
    public override void Down()
    {
        throw new DataException($"No Down Available for Migration {nameof(AddFeedLastResponseMigration)}");
    }

    public override void Up()
    {
        if (Schema.Table("Feeds").Column("LastResponse").Exists())
            return;
        
        Execute.Sql(@"ALTER TABLE Feeds
                    ADD COLUMN LastResponse TEXT NOT NULL DEFAULT ''");
    }
}