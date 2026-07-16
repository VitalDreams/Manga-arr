using System.Collections.Generic;
using Equ;
using NzbDrone.Core.Books;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Manga
{
    public class StoryArc : Entity<StoryArc>
    {
        public StoryArc()
        {
            Chapters = new List<Chapter>();
        }

        // Database columns
        public int MangaMetadataId { get; set; }
        public string ForeignArcId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int ArcOrder { get; set; }
        public string ChapterRange { get; set; }

        // Dynamic loaded from DB
        [MemberwiseEqualityIgnore]
        public LazyLoaded<MangaMetadata> MangaMetadata { get; set; }
        [MemberwiseEqualityIgnore]
        public LazyLoaded<List<Chapter>> Chapters { get; set; }

        public override string ToString()
        {
            return string.Format("[{0}][{1}]", ForeignArcId, Name);
        }

        public override void UseMetadataFrom(StoryArc other)
        {
            ForeignArcId = other.ForeignArcId;
            Name = other.Name;
            Description = other.Description;
            ArcOrder = other.ArcOrder;
            ChapterRange = other.ChapterRange;
        }

        public override void UseDbFieldsFrom(StoryArc other)
        {
            Id = other.Id;
            MangaMetadataId = other.MangaMetadataId;
            MangaMetadata = other.MangaMetadata;
            Chapters = other.Chapters;
        }

        public override void ApplyChanges(StoryArc other)
        {
            ForeignArcId = other.ForeignArcId;
            Name = other.Name;
            Description = other.Description;
            ArcOrder = other.ArcOrder;
            ChapterRange = other.ChapterRange;
        }
    }
}
