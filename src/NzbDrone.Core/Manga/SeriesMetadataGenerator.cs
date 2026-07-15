using System.Collections.Generic;
using System.IO;
using System.Linq;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Serializer;

namespace NzbDrone.Core.Manga
{
    public interface ISeriesMetadataGenerator
    {
        SeriesMetadata GenerateSeriesMetadata(MangaSeries series);
        string GenerateSeriesMetadataJson(MangaSeries series);
        void WriteSeriesMetadataFile(MangaSeries series);
    }

    public class SeriesMetadataGenerator : ISeriesMetadataGenerator
    {
        private readonly IMangaNamingService _namingService;
        private readonly IDiskProvider _diskProvider;
        private readonly Logger _logger;

        public SeriesMetadataGenerator(
            IMangaNamingService namingService,
            IDiskProvider diskProvider,
            Logger logger)
        {
            _namingService = namingService;
            _diskProvider = diskProvider;
            _logger = logger;
        }

        public SeriesMetadata GenerateSeriesMetadata(MangaSeries series)
        {
            var metadata = series.Metadata?.Value;
            if (metadata == null)
            {
                _logger.Warn("Cannot generate series.json for {0}: metadata is null", series.Name);
                return null;
            }

            var coverPath = GetCoverPath(series);

            return new SeriesMetadata
            {
                Name = metadata.Title ?? string.Empty,
                Description = metadata.Description ?? string.Empty,
                Author = metadata.Author ?? string.Empty,
                Artist = metadata.Artist ?? string.Empty,
                Status = metadata.Status ?? "unknown",
                Year = metadata.Year,
                Genres = metadata.Genres ?? new List<string>(),
                Tags = metadata.Tags ?? new List<string>(),
                TotalVolumes = metadata.TotalVolumes,
                TotalChapters = metadata.TotalChapters,
                MangaDexId = metadata.ForeignMangaId ?? string.Empty,
                AnilistId = ExtractAnilistId(metadata),
                MalId = ExtractMalId(metadata),
                CoverImage = coverPath,
                Language = metadata.OriginalLanguage ?? "en",
                ContentRating = metadata.ContentRating ?? "safe",
                Publisher = metadata.Publisher ?? string.Empty,
                AlternateTitles = metadata.AlternateTitles ?? new Dictionary<string, string>()
            };
        }

        public string GenerateSeriesMetadataJson(MangaSeries series)
        {
            var seriesMetadata = GenerateSeriesMetadata(series);
            if (seriesMetadata == null)
            {
                return null;
            }

            return seriesMetadata.ToJson();
        }

        public void WriteSeriesMetadataFile(MangaSeries series)
        {
            var seriesPath = series.Path;
            if (string.IsNullOrEmpty(seriesPath))
            {
                seriesPath = GetSeriesFolderPath(series);
            }

            if (string.IsNullOrEmpty(seriesPath))
            {
                _logger.Warn("Cannot write series.json for {0}: series path is unknown", series.Name);
                return;
            }

            var filePath = Path.Combine(seriesPath, "series.json");
            var json = GenerateSeriesMetadataJson(series);

            if (json == null)
            {
                return;
            }

            try
            {
                if (!_diskProvider.FolderExists(seriesPath))
                {
                    _diskProvider.CreateFolder(seriesPath);
                }

                _diskProvider.WriteAllText(filePath, json);
                _logger.Info("Wrote series.json for {0}", series.Name);
            }
            catch (IOException ex)
            {
                _logger.Warn(ex, "Failed to write series.json for {0}", series.Name);
            }
        }

        private string GetSeriesFolderPath(MangaSeries series)
        {
            var rootFolderPath = series.RootFolderPath;
            if (string.IsNullOrEmpty(rootFolderPath))
            {
                return null;
            }

            var seriesFolder = _namingService.GetSeriesFolder(series);
            return Path.Combine(rootFolderPath, seriesFolder);
        }

        private string GetCoverPath(MangaSeries series)
        {
            var metadata = series.Metadata?.Value;

            if (metadata?.LocalCoverPath != null && _diskProvider.FileExists(metadata.LocalCoverPath))
            {
                return metadata.LocalCoverPath;
            }

            if (!string.IsNullOrEmpty(metadata?.CoverUrl))
            {
                return metadata.CoverUrl;
            }

            return string.Empty;
        }

        private string ExtractAnilistId(MangaMetadata metadata)
        {
            if (metadata.Links == null)
            {
                return string.Empty;
            }

            var anilistLink = metadata.Links.FirstOrDefault(l =>
                l.Name != null && l.Name.ToLowerInvariant().Contains("anilist"));

            if (anilistLink != null && !string.IsNullOrEmpty(anilistLink.Url))
            {
                var uri = anilistLink.Url.TrimEnd('/');
                var segments = uri.Split('/');
                return segments.LastOrDefault() ?? string.Empty;
            }

            return string.Empty;
        }

        private string ExtractMalId(MangaMetadata metadata)
        {
            if (metadata.Links == null)
            {
                return string.Empty;
            }

            var malLink = metadata.Links.FirstOrDefault(l =>
                l.Name != null && (l.Name.ToLowerInvariant().Contains("myanimelist") || l.Name.ToLowerInvariant() == "mal"));

            if (malLink != null && !string.IsNullOrEmpty(malLink.Url))
            {
                var uri = malLink.Url.TrimEnd('/');
                var segments = uri.Split('/');
                return segments.LastOrDefault() ?? string.Empty;
            }

            return string.Empty;
        }
    }

    /// <summary>
    /// series.json schema for manga metadata companion files.
    /// Used by Komga, Kavita, Tachiyomi, and other manga readers.
    /// </summary>
    public class SeriesMetadata
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Author { get; set; }
        public string Artist { get; set; }
        public string Status { get; set; }
        public int Year { get; set; }
        public List<string> Genres { get; set; }
        public List<string> Tags { get; set; }
        public int TotalVolumes { get; set; }
        public int TotalChapters { get; set; }
        public string MangaDexId { get; set; }
        public string AnilistId { get; set; }
        public string MalId { get; set; }
        public string CoverImage { get; set; }
        public string Language { get; set; }
        public string ContentRating { get; set; }
        public string Publisher { get; set; }
        public Dictionary<string, string> AlternateTitles { get; set; }
    }
}
