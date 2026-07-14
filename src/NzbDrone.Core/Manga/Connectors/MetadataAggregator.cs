using System.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NLog;

namespace NzbDrone.Core.Manga.Connectors
{
    /// <summary>
    /// Aggregates metadata from multiple sources (MangaDex + AniList)
    /// MangaDex: Primary for search, chapters, downloads
    /// AniList: Enrichment for ratings, descriptions, recommendations
    /// </summary>
    public interface IMetadataAggregator
    {
        Task<List<MangaSearchResult>> SearchAsync(string query, int limit = 10);
        Task<MangaMetadata> GetEnrichedMetadataAsync(string mangadexId);
        Task<VolumeChapterMap> GetVolumeChapterMapAsync(string mangadexId);
    }

    public class MetadataAggregator : IMetadataAggregator
    {
        private readonly MangaDexConnector _mangadex;
        private readonly AniListConnector _anilist;
        private readonly Logger _logger;

        public MetadataAggregator(
            MangaDexConnector mangadex,
            AniListConnector anilist,
            Logger logger)
        {
            _mangadex = mangadex;
            _anilist = anilist;
            _logger = logger;
        }

        /// <summary>
        /// Search using MangaDex as primary, enrich with AniList data
        /// </summary>
        public async Task<List<MangaSearchResult>> SearchAsync(string query, int limit = 10)
        {
            _logger.Info($"Searching manga: {query}");

            // Primary: MangaDex search
            var mangadexResults = await _mangadex.SearchAsync(query, limit);
            _logger.Debug($"MangaDex returned {mangadexResults.Count} results");

            // Enrich with AniList ratings/descriptions where possible
            foreach (var result in mangadexResults)
            {
                try
                {
                    var anilistResults = await _anilist.SearchAsync(result.Title, 1);
                    var anilistMatch = anilistResults.FirstOrDefault();

                    if (anilistMatch != null)
                    {
                        // Enrich with AniList data (keep MangaDex ID)
                        if (string.IsNullOrEmpty(result.Description) || result.Description.Length < 100)
                        {
                            result.Description = anilistMatch.Description ?? result.Description;
                        }
                        if (result.Year == 0 && anilistMatch.Year > 0)
                        {
                            result.Year = anilistMatch.Year;
                        }
                        // Merge genres
                        if (anilistMatch.Genres?.Count > result.Genres?.Count)
                        {
                            result.Genres = anilistMatch.Genres;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Debug($"AniList enrichment failed for {result.Title}: {ex.Message}");
                }
            }

            return mangadexResults;
        }

        /// <summary>
        /// Get full metadata enriched from both sources
        /// </summary>
        public async Task<MangaMetadata> GetEnrichedMetadataAsync(string mangadexId)
        {
            _logger.Info($"Getting enriched metadata for {mangadexId}");

            // Get MangaDex metadata (primary)
            var metadata = await _mangadex.GetMangaMetadataAsync(mangadexId);

            if (metadata == null)
            {
                _logger.Warn($"MangaDex returned no metadata for {mangadexId}");
                return null;
            }

            // Enrich with AniList
            try
            {
                var anilistResults = await _anilist.SearchAsync(metadata.Title, 1);
                var anilistMatch = anilistResults.FirstOrDefault();

                if (anilistMatch != null)
                {
                    _logger.Debug($"Found AniList match: {anilistMatch.Title}");

                    // Get full AniList metadata
                    var anilistMetadata = await _anilist.GetMangaMetadataAsync(anilistMatch.ForeignMangaId);

                    if (anilistMetadata != null)
                    {
                        // Merge: prefer MangaDex data, fill gaps from AniList
                        metadata.Description = string.IsNullOrEmpty(metadata.Description) || metadata.Description.Length < 100
                            ? anilistMetadata.Description ?? metadata.Description
                            : metadata.Description;

                        metadata.Genres = metadata.Genres?.Count >= anilistMetadata.Genres?.Count
                            ? metadata.Genres
                            : anilistMetadata.Genres ?? metadata.Genres;

                        metadata.Tags = metadata.Tags?.Count >= anilistMetadata.Tags?.Count
                            ? metadata.Tags
                            : anilistMetadata.Tags ?? metadata.Tags;

                        // Use AniList cover if MangaDex doesn't have one
                        if (string.IsNullOrEmpty(metadata.CoverUrl) && !string.IsNullOrEmpty(anilistMetadata.CoverUrl))
                        {
                            metadata.CoverUrl = anilistMetadata.CoverUrl;
                        }

                        // Use AniList volume/chapter counts if MangaDex doesn't have them
                        if (metadata.TotalVolumes == 0 && anilistMetadata.TotalVolumes > 0)
                        {
                            metadata.TotalVolumes = anilistMetadata.TotalVolumes;
                        }
                        if (metadata.TotalChapters == 0 && anilistMetadata.TotalChapters > 0)
                        {
                            metadata.TotalChapters = anilistMetadata.TotalChapters;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Warn($"AniList enrichment failed for {mangadexId}: {ex.Message}");
            }

            return metadata;
        }

        public async Task<VolumeChapterMap> GetVolumeChapterMapAsync(string mangadexId)
        {
            return await _mangadex.GetVolumeChapterMapAsync(mangadexId);
        }
    }
}
