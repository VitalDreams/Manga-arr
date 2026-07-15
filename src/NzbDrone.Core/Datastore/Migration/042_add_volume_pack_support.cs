using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(042)]
    public class add_volume_pack_support : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            // Add DownloadMode column to MangaSeries (0 = Chapter, 1 = VolumePack)
            Alter.Table("MangaSeries")
                .AddColumn("DownloadMode").AsInt32().WithDefaultValue(1);

            // Add volume pack tracking columns to MangaFiles
            Alter.Table("MangaFiles")
                .AddColumn("IsVolumePack").AsBoolean().WithDefaultValue(false)
                .AddColumn("CoveredChapters").AsString().Nullable();

            // Index for efficient volume pack lookups
            Create.Index().OnTable("MangaFiles").OnColumn("IsVolumePack");
        }
    }
}
