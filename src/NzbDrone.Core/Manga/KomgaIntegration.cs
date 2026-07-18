using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using NLog;
using NzbDrone.Common.Http;

namespace NzbDrone.Core.Manga
{
    public interface IKomgaIntegration
    {
        Task TriggerLibraryScanAsync();
        Task TriggerSeriesScanAsync(string seriesId);
        Task<bool> IsAvailableAsync();
        Task<bool> WaitForScanCompletionAsync(TimeSpan? timeout = null);
        Task<bool> VerifyMangaExistsAsync(string seriesTitle, int volumeNumber);
    }

    public class KomgaIntegration : IKomgaIntegration
    {
        private readonly IHttpClient _httpClient;
        private readonly Logger _logger;

        public string BaseUrl { get; set; }
        public string ApiKey { get; set; }

        public KomgaIntegration(IHttpClient httpClient, Logger logger)
        {
            _httpClient = httpClient;
            _logger = logger;

            // Read from environment variables (set via docker-compose Komga__BaseUrl / Komga__ApiKey)
            BaseUrl = Environment.GetEnvironmentVariable("Komga__BaseUrl");
            ApiKey = Environment.GetEnvironmentVariable("Komga__ApiKey");

            if (!string.IsNullOrEmpty(BaseUrl))
            {
                _logger.Info("Komga configured at {0}", BaseUrl);
            }
        }

        public async Task TriggerLibraryScanAsync()
        {
            if (string.IsNullOrEmpty(BaseUrl))
            {
                _logger.Debug("Komga not configured, skipping library scan");
                return;
            }

            try
            {
                _logger.Info("Triggering Komga library scan...");
                var url = $"{BaseUrl}/api/v1/libraries/scan";
                var request = new HttpRequestBuilder(url)
                    .SetHeader("X-API-Key", ApiKey)
                    .Build();

                await _httpClient.PostAsync(request);
                _logger.Info("Komga library scan triggered");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to trigger Komga library scan");
            }
        }

        public async Task TriggerSeriesScanAsync(string seriesId)
        {
            if (string.IsNullOrEmpty(BaseUrl))
            {
                return;
            }

            try
            {
                _logger.Info($"Triggering Komga series scan for {seriesId}...");
                var url = $"{BaseUrl}/api/v1/series/{seriesId}/scan";
                var request = new HttpRequestBuilder(url)
                    .SetHeader("X-API-Key", ApiKey)
                    .Build();

                await _httpClient.PostAsync(request);
                _logger.Info($"Komga series scan triggered for {seriesId}");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to trigger Komga series scan for {seriesId}");
            }
        }

        public async Task<bool> IsAvailableAsync()
        {
            if (string.IsNullOrEmpty(BaseUrl))
            {
                return false;
            }

            try
            {
                var url = $"{BaseUrl}/api/v1/libraries";
                var request = new HttpRequestBuilder(url)
                    .SetHeader("X-API-Key", ApiKey)
                    .Build();

                var response = await _httpClient.GetAsync(request);
                return response.StatusCode == System.Net.HttpStatusCode.OK;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> WaitForScanCompletionAsync(TimeSpan? timeout = null)
        {
            if (string.IsNullOrEmpty(BaseUrl))
            {
                return false;
            }

            var effectiveTimeout = timeout ?? TimeSpan.FromMinutes(5);
            var deadline = DateTime.UtcNow + effectiveTimeout;
            var pollInterval = TimeSpan.FromSeconds(3);

            _logger.Info("Waiting for Komga library scan to complete...");

            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    var url = $"{BaseUrl}/api/v1/libraries";
                    var request = new HttpRequestBuilder(url)
                        .SetHeader("X-API-Key", ApiKey)
                        .Build();

                    var response = await _httpClient.GetAsync(request);

                    if (response.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        var json = response.Content;
                        if (json != null && !json.Contains("\"scanInProgress\":true"))
                        {
                            _logger.Info("Komga library scan completed");
                            return true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Debug(ex, "Error checking Komga scan status");
                }

                await Task.Delay(pollInterval);
            }

            _logger.Warn("Timed out waiting for Komga library scan");
            return false;
        }

        public async Task<bool> VerifyMangaExistsAsync(string seriesTitle, int volumeNumber)
        {
            if (string.IsNullOrEmpty(BaseUrl))
            {
                return false;
            }

            try
            {
                var url = $"{BaseUrl}/api/v1/series?search={Uri.EscapeDataString(seriesTitle)}";
                var request = new HttpRequestBuilder(url)
                    .SetHeader("X-API-Key", ApiKey)
                    .Build();

                var response = await _httpClient.GetAsync(request);

                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    var json = response.Content;
                    if (json != null && json.Contains(seriesTitle, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.Debug("Verified manga '{0}' exists in Komga", seriesTitle);
                        return true;
                    }
                }

                _logger.Debug("Manga '{0}' not found in Komga", seriesTitle);
                return false;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to verify manga existence in Komga");
                return false;
            }
        }
    }
}
