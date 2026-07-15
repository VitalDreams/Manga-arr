using System;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Manga
{
    public class MangaFile : ModelBase
    {
        public int VolumeId { get; set; }
        public int MangaSeriesId { get; set; }
        public string Path { get; set; }
        public string FileName { get; set; }
        public string RelativePath { get; set; }
        public long Size { get; set; }
        public DateTime AddedAt { get; set; } = DateTime.UtcNow;
        public bool IsVolumePack { get; set; }
        public string CoveredChapters { get; set; }
    }
}
