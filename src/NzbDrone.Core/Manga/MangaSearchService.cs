using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NLog;
using NzbDrone.Core.Manga.Connectors;
using NzbDrone.Core.Manga.Download;
using NzbDrone.Core.Manga.Monitoring;

namespace NzbDrone.Core.Manga
{
    public interface IMangaSearchService
    {
        Task<MangaSearchAndDownloadResult> SearchAndDownloadAsync(int seriesId, int? volumeNumber = null);
        Task<MangaSearchAndDownloadResult> SearchAndDownloadAsync(MangaSeries series, Volume volume);
    }

    public class MangaSearchService : IMangaSearchService
    {
        private readonly IMangaSeriesService _seriesService;
        private readonly IMangaMetadataConnector _mangaDexConnector;
        private readonly IProwlarrConnector _prowlarrConnector;
        private readonly IMangaDexDownloader _mangaDexDownloader;
        private readonly IMangaDownloadService _downloadService;
        private readonly IKomgaIntegration _komga;
        private readonly INotificationService _notifications;
        private readonly Logger _logger;

        public MangaSearchService(
            IMangaSeriesService seriesService,
            IMangaMetadataConnector mangaDexConnector,
            IProwlarrConnector prowlarrConnector,
            IMangaDexDownloader mangaDexDownloader,
            IMangaDownloadService downloadService,
            IKomgaIntegration komga,
            INotificationService notifications,
            Logger logger)
        {
            _seriesService = seriesService;
            _mangaDexConnector = mangaDexConnector;
            _prowlarrConnector = prowlarrConnector;
            _mangaDexDownloader = mangaDexDownloader;
            _downloadService = downloadService;
            _komga = komga;
            _notifications = notifications;
            _logger = logger;
        }

        public async Task<MangaSearchAndDownloadResult> SearchAndDownloadAsync(int seriesId, int? volumeNumber = null)
        {
            var result = new MangaSearchAndDownloadResult();

            // Step 1: Get series metadata from DB
            var series = _seriesService.GetSeries(seriesId);
            if (series == null)
            {
                result.Success = false;
                result.ErrorMessage = $"Series with ID {seriesId} not found";
                return result;
            }

            result.SeriesTitle = series.Name;
            result.ForeignMangaId = series.ForeignMangaId;

            _logger.Info("Starting search and download for {0} (ID: {1}, Volume: {2})",
                series.Name, seriesId, volumeNumber?.ToString() ?? "all");

            try
            {
                // Step 2: Search MangaDex for available chapters/volumes
                var volumeChapterMap = await _mangaDexConnector.GetVolumeChapterMapAsync(series.ForeignMangaId);

                if (volumeChapterMap?.VolumeChapters != null && volumeChapterMap.VolumeChapters.Any())
                {
                    var volumesToDownload = volumeNumber.HasValue
                        ? new List<int> { volumeNumber.Value }
                        : volumeChapterMap.VolumeChapters.Keys.OrderBy(v => v).ToList();

                    foreach (var volNum in volumesToDownload)
                    {
                        if (!volumeChapterMap.VolumeChapters.ContainsKey(volNum))
                        {
                            _logger.Warn("Volume {0} not found on MangaDex for {1}", volNum, series.Name);
                            result.FailedVolumes.Add(new FailedVolumeResult
                            {
                                VolumeNumber = volNum,
                                ErrorMessage = $"Volume {volNum} not found on MangaDex",
                                Source = "MangaDex"
                            });
                            continue;
                        }

                        var volume = new Volume
                        {
                            VolumeNumber = volNum,
                            Title = $"{series.Name} Vol. {volNum:000}"
                        };

                        // Step 4: Use MangaDexDownloader to download directly
                        _logger.Info("Downloading volume {0} from MangaDex...", volNum);
                        var downloadPath = await _mangaDexDownloader.DownloadVolumeAsync(
                            series.RootFolderPath ?? series.Path, series, volume);

                        if (downloadPath != null)
                        {
                            _logger.Info("Successfully downloaded volume {0} to {1}", volNum, downloadPath);
                            result.DownloadedVolumes.Add(new DownloadedVolumeResult
                            {
                                VolumeNumber = volNum,
                                FilePath = downloadPath,
                                Source = "MangaDex"
                            });
                        }
                        else
                        {
                            _logger.Warn("MangaDex download failed for volume {0}, trying Prowlarr fallback...", volNum);
                            var prowlarrResult = await TryProwlarrFallbackAsync(series, volume, volNum);
                            if (prowlarrResult != null)
                            {
                                result.DownloadedVolumes.Add(prowlarrResult);
                            }
                            else
                            {
                                result.FailedVolumes.Add(new FailedVolumeResult
                                {
                                    VolumeNumber = volNum,
                                    ErrorMessage = "Both MangaDex and Prowlarr failed",
                                    Source = "Both"
                                });
                            }
                        }
                    }
                }
                else
                {
                    // Step 3: MangaDex has no chapters - search Prowlarr as fallback
                    _logger.Info("No MangaDex chapters found for {0}, trying Prowlarr...", series.Name);

                    var volumesToDownload = volumeNumber.HasValue
                        ? new List<int> { volumeNumber.Value }
                        : await GetWantedVolumesAsync(series);

                    foreach (var volNum in volumesToDownload)
                    {
                        var volume = new Volume
                        {
                            VolumeNumber = volNum,
                            Title = $"{series.Name} Vol. {volNum:000}"
                        };

                        var prowlarrResult = await TryProwlarrFallbackAsync(series, volume, volNum);
                        if (prowlarrResult != null)
                        {
                            result.DownloadedVolumes.Add(prowlarrResult);
                        }
                        else
                        {
                            result.FailedVolumes.Add(new FailedVolumeResult
                            {
                                VolumeNumber = volNum,
                                ErrorMessage = "No results found on Prowlarr",
                                Source = "Prowlarr"
                            });
                        }
                    }
                }

                // Step 8: Trigger Komga library scan if anything was downloaded
                if (result.DownloadedVolumes.Any())
                {
                    try
                    {
                        await _komga.TriggerLibraryScanAsync();
                        _logger.Info("Komga library scan triggered after download");
                    }
                    catch (Exception ex)
                    {
                        _logger.Warn(ex, "Failed to trigger Komga library scan");
                    }

                    // Step 9: Send notification
                    try
                    {
                        var downloadedCount = result.DownloadedVolumes.Count;
                        var failedCount = result.FailedVolumes.Count;
                        await _notifications.SendAsync(new Notification
                        {
                            Title = "Manga Search Complete",
                            Message = $"{series.Name}: {downloadedCount} volume(s) downloaded, {failedCount} failed",
                            Type = downloadedCount > 0 ? NotificationType.Download : NotificationType.Warning,
                            Data = new
                            {
                                SeriesId = seriesId,
                                SeriesTitle = series.Name,
                                DownloadedCount = downloadedCount,
                                FailedCount = failedCount
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.Warn(ex, "Failed to send notification");
                    }
                }

                result.Success = result.DownloadedVolumes.Any();
                result.TotalVolumesSearched = result.DownloadedVolumes.Count + result.FailedVolumes.Count;

                _logger.Info("Search and download complete for {0}: {1} downloaded, {2} failed",
                    series.Name, result.DownloadedVolumes.Count, result.FailedVolumes.Count);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error during search and download for {0}", series.Name);
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        public async Task<MangaSearchAndDownloadResult> SearchAndDownloadAsync(MangaSeries series, Volume volume)
        {
            var result = new MangaSearchAndDownloadResult
            {
                SeriesTitle = series.Name,
                ForeignMangaId = series.ForeignMangaId
            };

            _logger.Info("Starting search and download for {0} volume {1}", series.Name, volume.VolumeNumber);

            try
            {
                // Try MangaDex first
                var volumeChapterMap = await _mangaDexConnector.GetVolumeChapterMapAsync(series.ForeignMangaId);

                if (volumeChapterMap?.VolumeChapters != null &&
                    volumeChapterMap.VolumeChapters.ContainsKey(volume.VolumeNumber))
                {
                    _logger.Info("Downloading volume {0} from MangaDex...", volume.VolumeNumber);
                    var downloadPath = await _mangaDexDownloader.DownloadVolumeAsync(
                        series.RootFolderPath ?? series.Path, series, volume);

                    if (downloadPath != null)
                    {
                        result.DownloadedVolumes.Add(new DownloadedVolumeResult
                        {
                            VolumeNumber = volume.VolumeNumber,
                            FilePath = downloadPath,
                            Source = "MangaDex"
                        });
                        result.Success = true;
                        result.TotalVolumesSearched = 1;
                        return result;
                    }
                }

                // Fallback to Prowlarr
                _logger.Info("MangaDex unavailable for volume {0}, trying Prowlarr...", volume.VolumeNumber);
                var prowlarrResult = await TryProwlarrFallbackAsync(series, volume, volume.VolumeNumber);

                if (prowlarrResult != null)
                {
                    result.DownloadedVolumes.Add(prowlarrResult);
                    result.Success = true;
                }
                else
                {
                    result.FailedVolumes.Add(new FailedVolumeResult
                    {
                        VolumeNumber = volume.VolumeNumber,
                        ErrorMessage = "Both MangaDex and Prowlarr failed",
                        Source = "Both"
                    });
                }

                result.TotalVolumesSearched = 1;

                // Komga scan + notification
                if (result.DownloadedVolumes.Any())
                {
                    try { await _komga.TriggerLibraryScanAsync(); }
                    catch (Exception ex) { _logger.Warn(ex, "Failed to trigger Komga library scan"); }

                    try
                    {
                        await _notifications.SendAsync(new Notification
                        {
                            Title = "New Manga Volume Downloaded",
                            Message = $"{series.Name} Volume {volume.VolumeNumber} downloaded from {result.DownloadedVolumes.First().Source}",
                            Type = NotificationType.Download
                        });
                    }
                    catch (Exception ex) { _logger.Warn(ex, "Failed to send notification"); }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error during search and download for {0} volume {1}", series.Name, volume.VolumeNumber);
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        private async Task<DownloadedVolumeResult> TryProwlarrFallbackAsync(
            MangaSeries series, Volume volume, int volumeNumber)
        {
            if (!_prowlarrConnector.IsConfigured)
            {
                _logger.Debug("Prowlarr not configured, skipping fallback for volume {0}", volumeNumber);
                return null;
            }

            try
            {
                var searchResults = await _prowlarrConnector.SearchMangaVolumePacksAsync(
                    series.Name, volumeNumber);

                if (!searchResults.Any())
                {
                    _logger.Warn("No Prowlarr results for {0} volume {1}", series.Name, volumeNumber);
                    return null;
                }

                // Pick the best result (most seeders)
                var bestResult = searchResults
                    .OrderByDescending(r => r.Seeders)
                    .First();

                _logger.Info("Found Prowlarr release: {0} (seeders: {1})",
                    bestResult.Title, bestResult.Seeders);

                // Step 5: Send to qBittorrent/SABnzbd via MangaDownloadService
                var downloadUrl = !string.IsNullOrEmpty(bestResult.MagnetUrl)
                    ? bestResult.MagnetUrl
                    : bestResult.DownloadUrl;

                var protocol = _prowlarrConnector.GetDownloadProtocol(bestResult);

                var downloadResult = await _downloadService.SendToDownloadClient(
                    bestResult.Title, downloadUrl, protocol, series, volume);

                if (downloadResult.Success)
                {
                    _logger.Info("Sent to download client: {0} (ID: {1})",
                        downloadResult.Title, downloadResult.DownloadId);

                    return new DownloadedVolumeResult
                    {
                        VolumeNumber = volumeNumber,
                        FilePath = null, // Will be set by completion handler
                        Source = "Prowlarr",
                        DownloadId = downloadResult.DownloadId,
                        DownloadClient = downloadResult.ClientName
                    };
                }

                _logger.Warn("Failed to send to download client: {0}", downloadResult.ErrorMessage);
                return null;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Prowlarr fallback failed for {0} volume {1}", series.Name, volumeNumber);
                return null;
            }
        }

        private Task<List<int>> GetWantedVolumesAsync(MangaSeries series)
        {
            // Return volumes 1 through TotalVolumes if known, otherwise just volume 1
            var totalVolumes = series.Metadata?.Value?.TotalVolumes ?? 0;
            if (totalVolumes > 0)
            {
                return Task.FromResult(Enumerable.Range(1, totalVolumes).ToList());
            }

            return Task.FromResult(new List<int> { 1 });
        }
    }

    public class MangaSearchAndDownloadResult
    {
        public bool Success { get; set; }
        public string SeriesTitle { get; set; }
        public string ForeignMangaId { get; set; }
        public string ErrorMessage { get; set; }
        public int TotalVolumesSearched { get; set; }
        public List<DownloadedVolumeResult> DownloadedVolumes { get; set; } = new();
        public List<FailedVolumeResult> FailedVolumes { get; set; } = new();
    }

    public class DownloadedVolumeResult
    {
        public int VolumeNumber { get; set; }
        public string FilePath { get; set; }
        public string Source { get; set; }
        public string DownloadId { get; set; }
        public string DownloadClient { get; set; }
    }

    public class FailedVolumeResult
    {
        public int VolumeNumber { get; set; }
        public string ErrorMessage { get; set; }
        public string Source { get; set; }
    }
}
