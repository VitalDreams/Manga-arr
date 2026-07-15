using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(043)]
    public class add_story_arcs : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Create.TableForModel("StoryArcs")
                .WithColumn("MangaMetadataId").AsInt32().NotNullable()
                .WithColumn("ForeignArcId").AsString().Nullable()
                .WithColumn("Name").AsString().NotNullable()
                .WithColumn("Description").AsString().Nullable()
                .WithColumn("ArcOrder").AsInt32().WithDefaultValue(0)
                .WithColumn("ChapterRange").AsString().Nullable();

            Create.Index().OnTable("StoryArcs").OnColumn("MangaMetadataId");
            Create.Index().OnTable("StoryArcs").OnColumn("ForeignArcId");

            // Add StoryArcId FK to Chapters table
            Alter.Table("Chapters")
                .AddColumn("StoryArcId").AsInt32().Nullable();

            Create.Index().OnTable("Chapters").OnColumn("StoryArcId");
        }
    }
}
