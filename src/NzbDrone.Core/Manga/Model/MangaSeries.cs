using System;
using System.Collections.Generic;
using Equ;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Books;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Profiles.Metadata;
using NzbDrone.Core.Profiles.Qualities;

namespace NzbDrone.Core.Manga
{
    public class MangaSeries : Entity<MangaSeries>
    {
        public MangaSeries()
        {
            Tags = new HashSet<int>();
            Metadata = new MangaMetadata();
        }

        // Database columns
        public int MangaMetadataId { get; set; }
        public string CleanName { get; set; }
        public bool Monitored { get; set; }
        public NewItemMonitorTypes MonitorNewItems { get; set; }
        public DownloadMode DownloadMode { get; set; } = DownloadMode.VolumePack;
        public DateTime? LastInfoSync { get; set; }
        public string Path { get; set; }
        public string RootFolderPath { get; set; }
        public DateTime Added { get; set; }
        public int QualityProfileId { get; set; }
        public int MetadataProfileId { get; set; }
        public HashSet<int> Tags { get; set; }
        [MemberwiseEqualityIgnore]
        public AddMangaSeriesOptions AddOptions { get; set; }

        // Dynamic loaded from DB
        [MemberwiseEqualityIgnore]
        public LazyLoaded<MangaMetadata> Metadata { get; set; }
        [MemberwiseEqualityIgnore]
        public LazyLoaded<QualityProfile> QualityProfile { get; set; }
        [MemberwiseEqualityIgnore]
        public LazyLoaded<MetadataProfile> MetadataProfile { get; set; }
        [MemberwiseEqualityIgnore]
        public LazyLoaded<List<Volume>> Volumes { get; set; }

        // Compatibility properties
        [MemberwiseEqualityIgnore]
        public string Name
        {
            get { return Metadata.Value.Title; }
            set { Metadata.Value.Title = value; }
        }

        [MemberwiseEqualityIgnore]
        public string ForeignMangaId
        {
            get { return Metadata.Value.ForeignMangaId; }
            set { Metadata.Value.ForeignMangaId = value; }
        }

        public override string ToString()
        {
            return string.Format("[{0}][{1}]", Metadata.Value.ForeignMangaId.NullSafe(), Metadata.Value.Title.NullSafe());
        }

        public override void UseMetadataFrom(MangaSeries other)
        {
            CleanName = other.CleanName;
        }

        public override void UseDbFieldsFrom(MangaSeries other)
        {
            Id = other.Id;
            MangaMetadataId = other.MangaMetadataId;
            Monitored = other.Monitored;
            MonitorNewItems = other.MonitorNewItems;
            DownloadMode = other.DownloadMode;
            LastInfoSync = other.LastInfoSync;
            Path = other.Path;
            RootFolderPath = other.RootFolderPath;
            Added = other.Added;
            QualityProfileId = other.QualityProfileId;
            QualityProfile = other.QualityProfile;
            MetadataProfileId = other.MetadataProfileId;
            MetadataProfile = other.MetadataProfile;
            Tags = other.Tags;
            AddOptions = other.AddOptions;
        }

        public override void ApplyChanges(MangaSeries other)
        {
            Path = other.Path;
            QualityProfileId = other.QualityProfileId;
            QualityProfile = other.QualityProfile;
            MetadataProfileId = other.MetadataProfileId;
            MetadataProfile = other.MetadataProfile;
            Volumes = other.Volumes;
            Tags = other.Tags;
            AddOptions = other.AddOptions;
            RootFolderPath = other.RootFolderPath;
            Monitored = other.Monitored;
            MonitorNewItems = other.MonitorNewItems;
            DownloadMode = other.DownloadMode;
        }
    }
}
