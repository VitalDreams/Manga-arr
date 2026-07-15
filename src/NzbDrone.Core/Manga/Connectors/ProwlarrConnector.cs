using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NLog;
using NzbDrone.Common.Http;
using NzbDrone.Core.Indexers;

namespace NzbDrone.Core.Manga.Connectors
{
    /// <summary>
    /// Prowlarr connector for torrent and Usenet indexers
    /// Provides fallback downloads when MangaDex doesn't have content
    /// </summary>
    public interface IProwlarrConnector
    {
        Task<List<ProwlarrSearchResult>> SearchAsync(string query, string category = "7030");
        Task<ProwlarrSearchResult> GetByIdAsync(string indexerId, string id);
        Task<List<ProwlarrIndexer>> GetIndexersAsync();
        Task<List<ProwlarrSearchResult>> SearchMangaVolumePacksAsync(string mangaTitle, int volumeNumber);
        Task<List<ProwlarrSearchResult>> SearchMangaAsync(string mangaTitle, int? volumeNumber = null);
        DownloadProtocol GetDownloadProtocol(ProwlarrSearchResult result);
        List<ProwlarrSearchResult> FilterByTitleAndVolume(List<ProwlarrSearchResult> results, string mangaTitle, int volumeNumber);
        bool IsConfigured { get; }
    }

    public class ProwlarrConnector : IProwlarrConnector
    {
        private readonly IHttpClient _httpClient;
        private readonly Logger _logger;

        public string BaseUrl { get; set; }
        public string ApiKey { get; set; }
        public bool IsConfigured => !string.IsNullOrEmpty(BaseUrl) && !string.IsNullOrEmpty(ApiKey);

        public ProwlarrConnector(IHttpClient httpClient, Logger logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        /// <summary>
        /// Search all Prowlarr indexers for manga
        /// Category 7030 = Comics, 7000 = Books (some manga indexers use this)
        /// </summary>
        public async Task<List<ProwlarrSearchResult>> SearchAsync(string query, string category = "7030")
        {
            if (!IsConfigured)
            {
                _logger.Debug("Prowlarr not configured, skipping search");
                return new List<ProwlarrSearchResult>();
            }

            try
            {
                _logger.Info($"Searching Prowlarr for: {query}");

                // Search both Comics and Books categories
                var url = $"{BaseUrl}/api/v1/search?query={Uri.EscapeDataString(query)}&categories=7030,7000&limit=25";

                var request = new HttpRequestBuilder(url)
                    .SetHeader("X-Api-Key", ApiKey)
                    .Build();

                var response = await _httpClient.GetAsync(request);
                var results = ParseSearchResults(response.Content);

                _logger.Info($"Prowlarr returned {results.Count} results for '{query}'");
                return results;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to search Prowlarr for '{query}'");
                return new List<ProwlarrSearchResult>();
            }
        }

        /// <summary>
        /// Get a specific release by ID
        /// </summary>
        public async Task<ProwlarrSearchResult> GetByIdAsync(string indexerId, string id)
        {
            if (!IsConfigured) return null;

            try
            {
                var url = $"{BaseUrl}/api/v1/{indexerId}/search?id={id}";
                var request = new HttpRequestBuilder(url)
                    .SetHeader("X-Api-Key", ApiKey)
                    .Build();

                var response = await _httpClient.GetAsync(request);
                var results = ParseSearchResults(response.Content);
                return results.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to get release {id} from indexer {indexerId}");
                return null;
            }
        }

        /// <summary>
        /// Get list of available indexers
        /// </summary>
        public async Task<List<ProwlarrIndexer>> GetIndexersAsync()
        {
            if (!IsConfigured) return new List<ProwlarrIndexer>();

            try
            {
                var url = $"{BaseUrl}/api/v1/indexer";
                var request = new HttpRequestBuilder(url)
                    .SetHeader("X-Api-Key", ApiKey)
                    .Build();

                var response = await _httpClient.GetAsync(request);
                return ParseIndexers(response.Content);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get Prowlarr indexers");
                return new List<ProwlarrIndexer>();
            }
        }

        /// <summary>
        /// Search for manga volume packs via Prowlarr
        /// Searches for "[Title] Vol [Number]" or "[Title] Volume [Number]" patterns
        /// </summary>
        public async Task<List<ProwlarrSearchResult>> SearchMangaVolumePacksAsync(string mangaTitle, int volumeNumber)
        {
            if (!IsConfigured)
            {
                _logger.Debug("Prowlarr not configured, skipping volume pack search");
                return new List<ProwlarrSearchResult>();
            }

            _logger.Info("Searching Prowlarr for manga volume pack: {0} Vol {1}", mangaTitle, volumeNumber);

            // Build search queries with common manga naming patterns
            var queries = new List<string>
            {
                $"{mangaTitle} Vol {volumeNumber}",
                $"{mangaTitle} Volume {volumeNumber}",
                $"{mangaTitle} v{volumeNumber}",
                $"{mangaTitle} vol.{volumeNumber}"
            };

            var allResults = new List<ProwlarrSearchResult>();

            foreach (var query in queries)
            {
                try
                {
                    var results = await SearchAsync(query);
                    allResults.AddRange(results);
                }
                catch (Exception ex)
                {
                    _logger.Debug(ex, "Search query failed: {0}", query);
                }
            }

            // Deduplicate by title and filter for relevance
            var filtered = allResults
                .GroupBy(r => r.Title, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

            filtered = FilterByTitleAndVolume(filtered, mangaTitle, volumeNumber);

            // Sort by seeders (more seeders = better availability)
            filtered = filtered.OrderByDescending(r => r.Seeders).ToList();

            _logger.Info("Found {0} volume pack results for {1} Vol {2}", filtered.Count, mangaTitle, volumeNumber);
            return filtered;
        }

        /// <summary>
        /// Search for manga with optional volume number filtering
        /// </summary>
        public async Task<List<ProwlarrSearchResult>> SearchMangaAsync(string mangaTitle, int? volumeNumber = null)
        {
            if (!IsConfigured)
            {
                _logger.Debug("Prowlarr not configured, skipping manga search");
                return new List<ProwlarrSearchResult>();
            }

            if (volumeNumber.HasValue)
            {
                return await SearchMangaVolumePacksAsync(mangaTitle, volumeNumber.Value);
            }

            _logger.Info("Searching Prowlarr for manga: {0}", mangaTitle);
            var results = await SearchAsync(mangaTitle);

            // Filter for manga-related results
            results = results.Where(r => IsMangaRelease(r.Title)).ToList();

            _logger.Info("Found {0} manga results for '{1}'", results.Count, mangaTitle);
            return results;
        }

        /// <summary>
        /// Determine the download protocol for a Prowlarr result
        /// </summary>
        public DownloadProtocol GetDownloadProtocol(ProwlarrSearchResult result)
        {
            // Check for magnet link (torrent)
            if (!string.IsNullOrEmpty(result.MagnetUrl))
            {
                return DownloadProtocol.Torrent;
            }

            // Check download URL for protocol hints
            if (!string.IsNullOrEmpty(result.DownloadUrl))
            {
                var url = result.DownloadUrl.ToLowerInvariant();

                if (url.Contains(".torrent") || url.Contains("magnet:"))
                {
                    return DownloadProtocol.Torrent;
                }

                if (url.Contains(".nzb"))
                {
                    return DownloadProtocol.Usenet;
                }
            }

            // Check categories for hints
            if (result.Categories != null)
            {
                var categoryHints = string.Join(",", result.Categories).ToLowerInvariant();

                if (categoryHints.Contains("torrent"))
                {
                    return DownloadProtocol.Torrent;
                }

                if (categoryHints.Contains("usenet") || categoryHints.Contains("nzb"))
                {
                    return DownloadProtocol.Usenet;
                }
            }

            // Default to torrent for manga (more common)
            _logger.Debug("Could not determine protocol for {0}, defaulting to Torrent", result.Title);
            return DownloadProtocol.Torrent;
        }

        /// <summary>
        /// Filter search results by manga title and volume number
        /// </summary>
        public List<ProwlarrSearchResult> FilterByTitleAndVolume(
            List<ProwlarrSearchResult> results,
            string mangaTitle,
            int volumeNumber)
        {
            if (results == null || !results.Any())
            {
                return new List<ProwlarrSearchResult>();
            }

            var cleanTitle = CleanTitle(mangaTitle);
            var volPatterns = new[]
            {
                $@"\bvol(?:ume)?\.?\s*{volumeNumber}\b",
                $@"\bv\.?{volumeNumber}\b",
                $@"\b{volumeNumber}(?:st|nd|rd|th)?\s*(?:vol|volume)\b"
            };

            return results.Where(r =>
            {
                var resultTitle = r.Title?.ToLowerInvariant() ?? string.Empty;
                var cleanResultTitle = CleanTitle(r.Title);

                // Check title similarity
                if (!IsTitleMatch(cleanResultTitle, cleanTitle))
                {
                    return false;
                }

                // Check volume number match
                foreach (var pattern in volPatterns)
                {
                    if (Regex.IsMatch(resultTitle, pattern, RegexOptions.IgnoreCase))
                    {
                        return true;
                    }
                }

                return false;
            }).ToList();
        }

        /// <summary>
        /// Clean a title for comparison (remove special chars, normalize spaces)
        /// </summary>
        private string CleanTitle(string title)
        {
            if (string.IsNullOrEmpty(title))
            {
                return string.Empty;
            }

            var cleaned = title.ToLowerInvariant();
            cleaned = Regex.Replace(cleaned, @"[^\w\s]", " ");
            cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();
            return cleaned;
        }

        /// <summary>
        /// Check if two cleaned titles are a match (fuzzy)
        /// </summary>
        private bool IsTitleMatch(string title1, string title2)
        {
            if (string.IsNullOrEmpty(title1) || string.IsNullOrEmpty(title2))
            {
                return false;
            }

            // Exact match
            if (title1 == title2)
            {
                return true;
            }

            // One contains the other
            if (title1.Contains(title2) || title2.Contains(title1))
            {
                return true;
            }

            // Check if most significant words match
            var words1 = title1.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var words2 = title2.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            var matchCount = words1.Count(w => words2.Contains(w));
            var totalWords = Math.Max(words1.Length, words2.Length);

            // Require at least 70% word overlap
            return totalWords > 0 && (double)matchCount / totalWords >= 0.7;
        }

        /// <summary>
        /// Check if a title looks like a manga release
        /// </summary>
        private bool IsMangaRelease(string title)
        {
            if (string.IsNullOrEmpty(title))
            {
                return false;
            }

            var lowerTitle = title.ToLowerInvariant();
            return lowerTitle.Contains("manga") ||
                   lowerTitle.Contains("vol") ||
                   lowerTitle.Contains("cbz") ||
                   lowerTitle.Contains("cbr") ||
                   lowerTitle.Contains("graphic novel") ||
                   Regex.IsMatch(lowerTitle, @"\bv\d+\b");
        }

        private List<ProwlarrSearchResult> ParseSearchResults(string json)
        {
            // Parse Prowlarr JSON response
            // This is a simplified parser - in production, use proper JSON deserialization
            var results = new List<ProwlarrSearchResult>();

            try
            {
                // Use System.Text.Json for proper parsing
                var options = new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var items = System.Text.Json.JsonSerializer.Deserialize<List<ProwlarrRelease>>(json, options);

                if (items != null)
                {
                    results = items.Select(r => new ProwlarrSearchResult
                    {
                        Id = r.Guid,
                        Title = r.Title,
                        Size = r.Size,
                        DownloadUrl = r.DownloadUrl,
                        InfoUrl = r.InfoUrl,
                        Indexer = r.Indexer,
                        Categories = r.Categories?.Select(c => c.Name).ToList() ?? new List<string>(),
                        Seeders = r.Seeders,
                        Peers = r.Peers,
                        PublishDate = r.PublishDate,
                        MagnetUrl = r.MagnetUrl
                    }).ToList();
                }
            }
            catch (Exception ex)
            {
                _logger.Warn($"Failed to parse Prowlarr results: {ex.Message}");
            }

            return results;
        }

        private List<ProwlarrIndexer> ParseIndexers(string json)
        {
            var indexers = new List<ProwlarrIndexer>();

            try
            {
                var options = new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var items = System.Text.Json.JsonSerializer.Deserialize<List<ProwlarrIndexerResponse>>(json, options);

                if (items != null)
                {
                    indexers = items.Select(i => new ProwlarrIndexer
                    {
                        Id = i.Id.ToString(),
                        Name = i.Name,
                        Enabled = i.Enable,
                        Type = i.Implementation
                    }).ToList();
                }
            }
            catch (Exception ex)
            {
                _logger.Warn($"Failed to parse Prowlarr indexers: {ex.Message}");
            }

            return indexers;
        }
    }

    // Data models
    public class ProwlarrSearchResult
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public long Size { get; set; }
        public string DownloadUrl { get; set; }
        public string InfoUrl { get; set; }
        public string Indexer { get; set; }
        public List<string> Categories { get; set; }
        public int Seeders { get; set; }
        public int Peers { get; set; }
        public DateTime PublishDate { get; set; }
        public string MagnetUrl { get; set; }
    }

    public class ProwlarrIndexer
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public bool Enabled { get; set; }
        public string Type { get; set; }
    }

    // Prowlarr API response models
    public class ProwlarrRelease
    {
        public string Guid { get; set; }
        public string Title { get; set; }
        public long Size { get; set; }
        public string DownloadUrl { get; set; }
        public string InfoUrl { get; set; }
        public string Indexer { get; set; }
        public List<ProwlarrCategory> Categories { get; set; }
        public int Seeders { get; set; }
        public int Peers { get; set; }
        public DateTime PublishDate { get; set; }
        public string MagnetUrl { get; set; }
    }

    public class ProwlarrCategory
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    public class ProwlarrIndexerResponse
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public bool Enable { get; set; }
        public string Implementation { get; set; }
    }
}
