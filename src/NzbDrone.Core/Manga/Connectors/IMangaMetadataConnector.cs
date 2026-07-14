using System.Collections.Generic;
using System.Threading.Tasks;

namespace NzbDrone.Core.Manga.Connectors
{
    /// <summary>
    /// Interface for manga metadata sources (MangaDex, AniList, etc.)
    /// </summary>
    public interface IMangaMetadataConnector
    {
        string Name { get; }
        string BaseUrl { get; }
        bool Enabled { get; }

        /// <summary>
        /// Search for manga by title
        /// </summary>
        Task<List<MangaSearchResult>> SearchAsync(string query, int limit = 10);

        /// <summary>
        /// Get full manga metadata by foreign ID
        /// </summary>
        Task<MangaMetadata> GetMangaMetadataAsync(string foreignMangaId);

        /// <summary>
        /// Get volume/chapter mapping for a manga
        /// </summary>
        Task<VolumeChapterMap> GetVolumeChapterMapAsync(string foreignMangaId);

        /// <summary>
        /// Get chapters for a specific volume
        /// </summary>
        Task<List<ChapterInfo>> GetChaptersForVolumeAsync(string foreignMangaId, int volumeNumber);

        /// <summary>
        /// Get image URLs for a chapter
        /// </summary>
        Task<ChapterPages> GetChapterPagesAsync(string foreignChapterId);

        /// <summary>
        /// Get cover image URL for a manga
        /// </summary>
        Task<string> GetCoverUrlAsync(string foreignMangaId);
    }

    /// <summary>
    /// Result from a manga search
    /// </summary>
    public class MangaSearchResult
    {
        public string ForeignMangaId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Author { get; set; }
        public string Artist { get; set; }
        public string Status { get; set; }
        public string Demographic { get; set; }
        public int Year { get; set; }
        public string CoverUrl { get; set; }
        public List<string> Genres { get; set; }
        public string ContentRating { get; set; }
    }

    /// <summary>
    /// Maps volumes to their chapters
    /// </summary>
    public class VolumeChapterMap
    {
        public string ForeignMangaId { get; set; }
        public Dictionary<int, List<string>> VolumeChapters { get; set; }
        // Key: volume number, Value: list of chapter IDs
    }

    /// <summary>
    /// Chapter information from a source
    /// </summary>
    public class ChapterInfo
    {
        public string ForeignChapterId { get; set; }
        public string Title { get; set; }
        public decimal ChapterNumber { get; set; }
        public int VolumeNumber { get; set; }
        public string Language { get; set; }
        public string ScanlationGroup { get; set; }
        public int PageCount { get; set; }
        public DateTime? ReleaseDate { get; set; }
    }

    /// <summary>
    /// Page image URLs for a chapter
    /// </summary>
    public class ChapterPages
    {
        public string ForeignChapterId { get; set; }
        public string BaseUrl { get; set; }
        public List<string> PageUrls { get; set; }
        public string Hash { get; set; }
    }
}
