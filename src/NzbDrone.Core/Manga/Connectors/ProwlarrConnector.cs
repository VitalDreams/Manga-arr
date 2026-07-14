using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NLog;
using NzbDrone.Common.Http;

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
