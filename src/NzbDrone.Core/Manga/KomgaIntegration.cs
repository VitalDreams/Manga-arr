using System;
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
    }
}
