using System.Data;
using FluentMigrator;

namespace PointlessWaymarks.FeedReaderData.Migrations;

[Migration(202607030100)]
public class AddFeedsLastFailedUpdateMigration : Migration
{
    public override void Down()
    {
        throw new DataException($"No Down Available for Migration {nameof(AddFeedsLastFailedUpdateMigration)}");
    }

    public override void Up()
    {
        if (Schema.Table("Feeds").Column("LastFailedUpdate").Exists())
            return;
        
        Execute.Sql(@"ALTER TABLE Feeds
                    ADD COLUMN LastFailedUpdate TEXT");
    }
}