using System;
using System.Collections.Generic;
using System.Linq;
using Equ;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Books;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.MediaFiles;

namespace NzbDrone.Core.Manga
{
    public class Chapter : Entity<Chapter>
    {
        public Chapter()
        {
            Overview = string.Empty;
            Images = new List<MediaCover.MediaCover>();
            Links = new List<Links>();
            Ratings = new Ratings();
        }

        // Database columns - metadata
        public int VolumeId { get; set; }
        public string ForeignChapterId { get; set; }
        public string TitleSlug { get; set; }
        public string Title { get; set; }
        public decimal ChapterNumber { get; set; }
        public string Language { get; set; }
        public string Overview { get; set; }
        public string ScanlationGroup { get; set; }
        public int PageCount { get; set; }
        public DateTime? ReleaseDate { get; set; }
        public List<MediaCover.MediaCover> Images { get; set; }
        public List<Links> Links { get; set; }
        public Ratings Ratings { get; set; }

        // MangaArr generated/config
        public bool Monitored { get; set; }
        public bool ManualAdd { get; set; }

        // Dynamic loaded from DB
        [MemberwiseEqualityIgnore]
        public LazyLoaded<Volume> Volume { get; set; }
        [MemberwiseEqualityIgnore]
        public LazyLoaded<List<MangaFile>> MangaFiles { get; set; }

        public override string ToString()
        {
            return string.Format("[{0}][{1}]", ForeignChapterId, Title.NullSafe());
        }

        public override void UseMetadataFrom(Chapter other)
        {
            ForeignChapterId = other.ForeignChapterId;
            TitleSlug = other.TitleSlug;
            Title = other.Title;
            ChapterNumber = other.ChapterNumber;
            Language = other.Language;
            Overview = other.Overview.IsNullOrWhiteSpace() ? Overview : other.Overview;
            ScanlationGroup = other.ScanlationGroup;
            PageCount = other.PageCount;
            ReleaseDate = other.ReleaseDate;
            Images = other.Images.Any() ? other.Images : Images;
            Links = other.Links;
            Ratings = other.Ratings;
        }

        public override void UseDbFieldsFrom(Chapter other)
        {
            Id = other.Id;
            VolumeId = other.VolumeId;
            Volume = other.Volume;
            Monitored = other.Monitored;
            ManualAdd = other.ManualAdd;
        }

        public override void ApplyChanges(Chapter other)
        {
            ForeignChapterId = other.ForeignChapterId;
            Monitored = other.Monitored;
        }
    }
}
