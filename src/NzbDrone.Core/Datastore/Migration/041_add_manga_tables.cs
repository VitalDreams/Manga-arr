using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(041)]
    public class add_manga_tables : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Create.TableForModel("MangaMetadata")
                .WithColumn("ForeignMangaId").AsString().NotNullable()
                .WithColumn("Title").AsString().Nullable()
                .WithColumn("TitleSlug").AsString().Nullable()
                .WithColumn("SortTitle").AsString().Nullable()
                .WithColumn("Description").AsString().Nullable()
                .WithColumn("Author").AsString().Nullable()
                .WithColumn("Artist").AsString().Nullable()
                .WithColumn("Publisher").AsString().Nullable()
                .WithColumn("OriginalLanguage").AsString().Nullable()
                .WithColumn("Demographic").AsString().Nullable()
                .WithColumn("Status").AsString().Nullable()
                .WithColumn("ContentRating").AsString().Nullable()
                .WithColumn("Year").AsInt32().WithDefaultValue(0)
                .WithColumn("TotalVolumes").AsInt32().WithDefaultValue(0)
                .WithColumn("TotalChapters").AsInt32().WithDefaultValue(0)
                .WithColumn("CoverUrl").AsString().Nullable()
                .WithColumn("LocalCoverPath").AsString().Nullable()
                .WithColumn("LastInfoSync").AsDateTime().Nullable();

            Create.Index().OnTable("MangaMetadata").OnColumn("ForeignMangaId").Unique();

            Create.TableForModel("MangaSeries")
                .WithColumn("MangaMetadataId").AsInt32().NotNullable()
                .WithColumn("CleanName").AsString().Nullable()
                .WithColumn("Monitored").AsBoolean().WithDefaultValue(true)
                .WithColumn("MonitorNewItems").AsInt32().WithDefaultValue(0)
                .WithColumn("LastInfoSync").AsDateTime().Nullable()
                .WithColumn("Path").AsString().Nullable()
                .WithColumn("RootFolderPath").AsString().Nullable()
                .WithColumn("Added").AsDateTime().NotNullable()
                .WithColumn("QualityProfileId").AsInt32().WithDefaultValue(0)
                .WithColumn("MetadataProfileId").AsInt32().WithDefaultValue(0)
                .WithColumn("Tags").AsString().Nullable();

            Create.Index().OnTable("MangaSeries").OnColumn("CleanName");
            Create.Index().OnTable("MangaSeries").OnColumn("MangaMetadataId");

            Create.TableForModel("Volumes")
                .WithColumn("MangaSeriesId").AsInt32().NotNullable()
                .WithColumn("MangaMetadataId").AsInt32().NotNullable()
                .WithColumn("ForeignVolumeId").AsString().NotNullable()
                .WithColumn("Title").AsString().Nullable()
                .WithColumn("TitleSlug").AsString().Nullable()
                .WithColumn("CleanTitle").AsString().Nullable()
                .WithColumn("VolumeNumber").AsInt32().WithDefaultValue(0)
                .WithColumn("ReleaseDate").AsDateTime().Nullable()
                .WithColumn("Monitored").AsBoolean().WithDefaultValue(true)
                .WithColumn("AnyEditionOk").AsBoolean().WithDefaultValue(true)
                .WithColumn("LastInfoSync").AsDateTime().Nullable()
                .WithColumn("LastSearchTime").AsDateTime().Nullable()
                .WithColumn("Added").AsDateTime().NotNullable();

            Create.Index().OnTable("Volumes").OnColumn("MangaSeriesId");
            Create.Index().OnTable("Volumes").OnColumn("ForeignVolumeId").Unique();

            Create.TableForModel("Chapters")
                .WithColumn("VolumeId").AsInt32().NotNullable()
                .WithColumn("ForeignChapterId").AsString().NotNullable()
                .WithColumn("Title").AsString().Nullable()
                .WithColumn("TitleSlug").AsString().Nullable()
                .WithColumn("ChapterNumber").AsDecimal().WithDefaultValue(0)
                .WithColumn("Language").AsString().Nullable()
                .WithColumn("Overview").AsString().Nullable()
                .WithColumn("ScanlationGroup").AsString().Nullable()
                .WithColumn("PageCount").AsInt32().WithDefaultValue(0)
                .WithColumn("ReleaseDate").AsDateTime().Nullable()
                .WithColumn("Monitored").AsBoolean().WithDefaultValue(true)
                .WithColumn("ManualAdd").AsBoolean().WithDefaultValue(false);

            Create.Index().OnTable("Chapters").OnColumn("VolumeId");
            Create.Index().OnTable("Chapters").OnColumn("ForeignChapterId").Unique();

            Create.TableForModel("MangaFiles")
                .WithColumn("VolumeId").AsInt32().NotNullable()
                .WithColumn("MangaSeriesId").AsInt32().NotNullable()
                .WithColumn("Path").AsString().NotNullable()
                .WithColumn("FileName").AsString().NotNullable()
                .WithColumn("RelativePath").AsString().Nullable()
                .WithColumn("Size").AsInt64().WithDefaultValue(0)
                .WithColumn("AddedAt").AsDateTime().NotNullable();

            Create.Index().OnTable("MangaFiles").OnColumn("VolumeId");
            Create.Index().OnTable("MangaFiles").OnColumn("MangaSeriesId");
        }
    }
}
