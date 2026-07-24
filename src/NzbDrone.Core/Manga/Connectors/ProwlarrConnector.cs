using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NLog;
using NzbDrone.Common.Http;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Indexers.Newznab;

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
        private readonly IIndexerFactory _indexerFactory;
        private readonly Logger _logger;

        private string _serverUrl;
        private string _apiKey;
        private Dictionary<string, DownloadProtocol> _indexerProtocols;
        private bool _initialized;

        public bool IsConfigured
        {
            get
            {
                EnsureInitialized();
                return !string.IsNullOrEmpty(_serverUrl) && !string.IsNullOrEmpty(_apiKey);
            }
        }

        public ProwlarrConnector(IHttpClient httpClient, IIndexerFactory indexerFactory, Logger logger)
        {
            _httpClient = httpClient;
            _indexerFactory = indexerFactory;
            _logger = logger;
        }

        private void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;

            try
            {
                var definitions = _indexerFactory.All();
                var protocols = new Dictionary<string, DownloadProtocol>(StringComparer.OrdinalIgnoreCase);
                string serverUrl = null;
                string apiKey = null;

                foreach (var definition in definitions)
                {
                    if (!(definition.Settings is NewznabSettings settings))
                    {
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(settings.BaseUrl) || string.IsNullOrWhiteSpace(settings.ApiKey))
                    {
                        continue;
                    }

                    try
                    {
                        var baseUri = new Uri(settings.BaseUrl.EndsWith("/") ? settings.BaseUrl : settings.BaseUrl + "/");
                        var serverRoot = $"{baseUri.Scheme}://{baseUri.Authority}";

                        if (serverUrl == null)
                        {
                            serverUrl = serverRoot;
                            apiKey = settings.ApiKey;
                        }
                    }
                    catch
                    {
                        _logger.Debug("Skipping indexer {0} with invalid BaseUrl: {1}", definition.Name, settings.BaseUrl);
                        continue;
                    }

                    var protocol = string.Equals(definition.Implementation, "Newznab", StringComparison.OrdinalIgnoreCase)
                        ? DownloadProtocol.Usenet
                        : DownloadProtocol.Torrent;

                    if (!protocols.ContainsKey(definition.Name))
                    {
                        protocols[definition.Name] = protocol;
                    }
                }

                if (protocols.Any())
                {
                    _serverUrl = serverUrl;
                    _apiKey = apiKey;
                    _indexerProtocols = protocols;
                    _logger.Info("Prowlarr connector configured from {0} indexer definition(s), server: {1}", protocols.Count, _serverUrl);
                }
                else
                {
                    _logger.Debug("No Prowlarr indexer definitions found in configuration");
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to initialize Prowlarr connector from indexer definitions");
            }
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
                _logger.Info("Searching Prowlarr for: {0}", query);

                var allResults = new List<ProwlarrSearchResult>();

                foreach (var route in _indexerProtocols)
                {
                    try
                    {
                        var url = $"{_serverUrl}/api/v1/search?query={Uri.EscapeDataString(query)}&categories=7030&categories=7000&indexer={Uri.EscapeDataString(route.Key)}&limit=25";

                        var request = new HttpRequestBuilder(url)
                            .SetHeader("X-Api-Key", _apiKey)
                            .Build();

                        var response = await _httpClient.GetAsync(request);
                        var results = ParseSearchResults(response.Content);

                        foreach (var r in results)
                        {
                            // Tag with the indexer we queried (API responses don't always echo it back),
                            // then re-derive protocol so an unambiguous torrent/magnet payload can never
                            // be reported as Usenet just because the route we queried is labeled Newznab.
                            if (string.IsNullOrEmpty(r.Indexer))
                            {
                                r.Indexer = route.Key;
                            }

                            r.Protocol = GetDownloadProtocol(r);
                        }

                        allResults.AddRange(results);
                    }
                    catch (Exception ex)
                    {
                        _logger.Debug(ex, "Failed to search Prowlarr indexer {0}", route.Key);
                    }
                }

                _logger.Info("Prowlarr returned {0} results for '{1}'", allResults.Count, query);
                return allResults;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to search Prowlarr for '{0}'", query);
                return new List<ProwlarrSearchResult>();
            }
        }

        /// <summary>
        /// Get a specific release by ID
        /// </summary>
        public async Task<ProwlarrSearchResult> GetByIdAsync(string indexerId, string id)
        {
            if (!IsConfigured)
            {
                return null;
            }

            try
            {
                var url = $"{_serverUrl}/api/v1/{indexerId}/search?id={id}";
                var request = new HttpRequestBuilder(url)
                    .SetHeader("X-Api-Key", _apiKey)
                    .Build();

                var response = await _httpClient.GetAsync(request);
                var results = ParseSearchResults(response.Content);
                return results.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get release {0} from indexer {1}", id, indexerId);
                return null;
            }
        }

        /// <summary>
        /// Get list of available indexers
        /// </summary>
        public async Task<List<ProwlarrIndexer>> GetIndexersAsync()
        {
            if (!IsConfigured)
            {
                return new List<ProwlarrIndexer>();
            }

            try
            {
                var url = $"{_serverUrl}/api/v1/indexer";
                var request = new HttpRequestBuilder(url)
                    .SetHeader("X-Api-Key", _apiKey)
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

            // Deduplicate by title AND protocol so that an NZB and a torrent with
            // the same title are both preserved for the protocol-aware sort below.
            var filtered = allResults
                .GroupBy(r => new { Title = r.Title?.ToLowerInvariant(), r.Protocol })
                .Select(g => g.First())
                .ToList();

            filtered = FilterByTitleAndVolume(filtered, mangaTitle, volumeNumber);

            // Drop results with no usable download link (nothing to hand to the download client)
            filtered = filtered
                .Where(r => !string.IsNullOrEmpty(r.MagnetUrl) || !string.IsNullOrEmpty(r.DownloadUrl))
                .ToList();

            // Reject releases whose title signals a non-manga format (audiobook, ebook-only,
            // video, etc). Applied after protocol classification (already set per-result in
            // SearchAsync) and before selection/sorting, so an invalid release can never be
            // chosen as "best" regardless of title/volume match or seeder count.
            filtered = filtered
                .Where(r => MangaReleaseFormatValidator.IsValidFormat(r.Title))
                .ToList();

            // Protocol-aware sort: Usenet has zero seeders by design, so boost
            // Usenet results alongside torrent results with seeders.
            filtered = filtered
                .OrderByDescending(r => r.Protocol == DownloadProtocol.Usenet ? 1 : 0)
                .ThenByDescending(r => r.Seeders)
                .ThenByDescending(r => r.Size)
                .ToList();

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

            // Filter for manga-related results, rejecting non-manga formats (audiobook, video, etc.)
            results = results
                .Where(r => IsMangaRelease(r.Title) && MangaReleaseFormatValidator.IsValidFormat(r.Title))
                .ToList();

            _logger.Info("Found {0} manga results for '{1}'", results.Count, mangaTitle);
            return results;
        }

        /// <summary>
        /// Determine the download protocol for a Prowlarr result.
        /// Unambiguous payload signals (magnet link, .torrent URL, .nzb URL) are checked
        /// first because they can't lie — a misconfigured or proxying indexer can be
        /// labeled Newznab/Usenet (or Torznab/Torrent) in its definition while still
        /// handing back the opposite payload, and trusting the label in that case would
        /// send the wrong kind of link to the download client.
        /// Only once the payload gives no signal do we fall back to the indexer→protocol
        /// map, then category heuristics.
        /// </summary>
        public DownloadProtocol GetDownloadProtocol(ProwlarrSearchResult result)
        {
            // Payload signals first (most trustworthy - can't be misconfigured)
            if (!string.IsNullOrEmpty(result.MagnetUrl))
            {
                return DownloadProtocol.Torrent;
            }

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

            // Check indexer→protocol map (reliable for correctly-configured Newznab/Torznab indexers)
            if (_indexerProtocols != null && !string.IsNullOrEmpty(result.Indexer) &&
                _indexerProtocols.TryGetValue(result.Indexer, out var mappedProtocol))
            {
                return mappedProtocol;
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
            var results = new List<ProwlarrSearchResult>();

            try
            {
                var options = new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var items = System.Text.Json.JsonSerializer.Deserialize<List<ProwlarrRelease>>(json, options);

                if (items != null)
                {
                    results = items.Select(r =>
                    {
                        var searchResult = new ProwlarrSearchResult
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
                        };
                        searchResult.Protocol = GetDownloadProtocol(searchResult);
                        return searchResult;
                    }).ToList();
                }
            }
            catch (Exception ex)
            {
                _logger.Warn("Failed to parse Prowlarr results: {0}", ex.Message);
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
                _logger.Warn("Failed to parse Prowlarr indexers: {0}", ex.Message);
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
        public DownloadProtocol Protocol { get; set; }
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
