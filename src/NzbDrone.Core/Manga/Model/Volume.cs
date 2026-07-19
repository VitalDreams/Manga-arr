using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json.Serialization;
using Equ;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Books;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.MediaFiles;

namespace NzbDrone.Core.Manga
{
    [DebuggerDisplay("{GetType().FullName} ID = {Id} [{ForeignVolumeId}][{Title}]")]
    public class Volume : Entity<Volume>
    {
        public Volume()
        {
            Links = new List<Links>();
            Genres = new List<string>();
            RelatedVolumes = new List<int>();
            Ratings = new Ratings();
            MangaSeries = new MangaSeries();
            AddOptions = new AddVolumeOptions();
        }

        // Database columns - metadata
        public int MangaMetadataId { get; set; }
        public string ForeignVolumeId { get; set; }
        public string TitleSlug { get; set; }
        public string Title { get; set; }
        public int VolumeNumber { get; set; }
        public DateTime? ReleaseDate { get; set; }
        public List<Links> Links { get; set; }
        public List<string> Genres { get; set; }
        public List<int> RelatedVolumes { get; set; }
        public Ratings Ratings { get; set; }
        public DateTime? LastSearchTime { get; set; }

        // MangaArr generated/config
        public string CleanTitle { get; set; }
        public bool Monitored { get; set; }
        public bool AnyEditionOk { get; set; }
        public DateTime? LastInfoSync { get; set; }
        public DateTime Added { get; set; }
        [MemberwiseEqualityIgnore]
        public AddVolumeOptions AddOptions { get; set; }

        // Dynamic loaded from DB
        [MemberwiseEqualityIgnore]
        public LazyLoaded<MangaMetadata> MangaMetadata { get; set; }
        [MemberwiseEqualityIgnore]
        public LazyLoaded<MangaSeries> MangaSeries { get; set; }
        [MemberwiseEqualityIgnore]
        public LazyLoaded<List<Chapter>> Chapters { get; set; }
        [MemberwiseEqualityIgnore]
        public LazyLoaded<List<MangaFile>> MangaFiles { get; set; }

        // Database column - FK to MangaSeries
        [MemberwiseEqualityIgnore]
        [JsonIgnore]
        public int MangaSeriesId { get; set; }

        public override string ToString()
        {
            return string.Format("[{0}][{1}]", ForeignVolumeId, Title.NullSafe());
        }

        public override void UseMetadataFrom(Volume other)
        {
            ForeignVolumeId = other.ForeignVolumeId;
            TitleSlug = other.TitleSlug;
            Title = other.Title;
            VolumeNumber = other.VolumeNumber;
            ReleaseDate = other.ReleaseDate;
            Links = other.Links;
            Genres = other.Genres;
            RelatedVolumes = other.RelatedVolumes;
            Ratings = other.Ratings;
            CleanTitle = other.CleanTitle;
        }

        public override void UseDbFieldsFrom(Volume other)
        {
            Id = other.Id;
            MangaMetadataId = other.MangaMetadataId;
            Monitored = other.Monitored;
            AnyEditionOk = other.AnyEditionOk;
            LastInfoSync = other.LastInfoSync;
            LastSearchTime = other.LastSearchTime;
            Added = other.Added;
            AddOptions = other.AddOptions;
        }

        public override void ApplyChanges(Volume other)
        {
            ForeignVolumeId = other.ForeignVolumeId;
            AddOptions = other.AddOptions;
            Monitored = other.Monitored;
            AnyEditionOk = other.AnyEditionOk;
        }
    }
}
