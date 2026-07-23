using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using NLog;
using NzbDrone.Core.Manga.Connectors;

namespace NzbDrone.Core.Manga.Monitoring
{
    public class MangaMonitoringService : BackgroundService
    {
        private readonly IMangaSeriesService _seriesService;
        private readonly IVolumeRepository _volumeRepository;
        private readonly IMangaFileService _mangaFileService;
        private readonly IMetadataAggregator _metadataAggregator;
        private readonly IMangaSearchService _searchService;
        private readonly IKomgaIntegration _komga;
        private readonly Logger _logger;

        public TimeSpan CheckInterval { get; set; } = TimeSpan.FromHours(24);
        public bool Enabled { get; set; } = true;

        public MangaMonitoringService(
            IMangaSeriesService seriesService,
            IVolumeRepository volumeRepository,
            IMangaFileService mangaFileService,
            IMetadataAggregator metadataAggregator,
            IMangaSearchService searchService,
            IKomgaIntegration komga,
            Logger logger)
        {
            _seriesService = seriesService;
            _volumeRepository = volumeRepository;
            _mangaFileService = mangaFileService;
            _metadataAggregator = metadataAggregator;
            _searchService = searchService;
            _komga = komga;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.Info("Manga monitoring service started (interval: {0})", CheckInterval);

            while (!stoppingToken.IsCancellationRequested)
            {
                if (Enabled)
                {
                    try
                    {
                        await CheckForNewChaptersAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Error during manga monitoring check");
                    }
                }

                try
                {
                    await Task.Delay(CheckInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            _logger.Info("Manga monitoring service stopped");
        }

        private async Task CheckForNewChaptersAsync()
        {
            var allSeries = _seriesService.GetAllSeries();
            var monitoredSeries = allSeries.Where(s => s.Monitored).ToList();

            _logger.Info("Checking {0} monitored manga series for new chapters...", monitoredSeries.Count);

            var totalSeriesChecked = 0;
            var totalNewChapters = 0;
            var totalDownloaded = 0;
            var totalFailed = 0;

            foreach (var series in monitoredSeries)
            {
                try
                {
                    var result = await CheckSeriesForNewChaptersAsync(series);
                    totalSeriesChecked++;

                    if (result.NewVolumes > 0)
                    {
                        totalNewChapters += result.NewVolumes;
                        totalDownloaded += result.Downloaded;
                        totalFailed += result.Failed;
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error checking series {0} (ID: {1})", series.Name, series.Id);
                }
            }

            _logger.Info(
                "Monitoring cycle complete: {0} series checked, {1} new volumes found, {2} downloaded, {3} failed",
                totalSeriesChecked, totalNewChapters, totalDownloaded, totalFailed);

            if (totalDownloaded > 0)
            {
                try
                {
                    await _komga.TriggerLibraryScanAsync();
                    _logger.Info("Komga library scan triggered after monitoring downloads");
                }
                catch (Exception ex)
                {
                    _logger.Warn(ex, "Failed to trigger Komga library scan after monitoring cycle");
                }
            }
        }

        private async Task<CheckResult> CheckSeriesForNewChaptersAsync(MangaSeries series)
        {
            var result = new CheckResult();
            var foreignMangaId = series.ForeignMangaId;

            if (string.IsNullOrEmpty(foreignMangaId))
            {
                _logger.Debug("Skipping series {0} (ID: {1}): no ForeignMangaId", series.Name, series.Id);
                return result;
            }

            _logger.Debug("Checking {0} (MangaDex: {1}) for new chapters...", series.Name, foreignMangaId);

            var volumeChapterMap = await _metadataAggregator.GetVolumeChapterMapAsync(foreignMangaId);

            if (volumeChapterMap?.VolumeChapters == null || !volumeChapterMap.VolumeChapters.Any())
            {
                _logger.Debug("No volumes found on MangaDex for {0}", series.Name);
                return result;
            }

            var remoteVolumeNumbers = volumeChapterMap.VolumeChapters.Keys.OrderBy(v => v).ToList();
            var existingVolumes = _volumeRepository.All()
                .Where(v => v.MangaSeriesId == series.Id)
                .ToList();
            var existingVolumeNumbers = existingVolumes.Select(v => v.VolumeNumber).ToHashSet();

            // Skip volumes that already have downloaded files
            var downloadedVolumeNumbers = existingVolumes
                .Where(v => _mangaFileService.GetFilesByVolume(v.Id).Any())
                .Select(v => v.VolumeNumber)
                .ToHashSet();

            var newVolumeNumbers = remoteVolumeNumbers
                .Where(v => !existingVolumeNumbers.Contains(v) && !downloadedVolumeNumbers.Contains(v))
                .OrderBy(v => v)
                .ToList();

            result.NewVolumes = newVolumeNumbers.Count;

            if (!newVolumeNumbers.Any())
            {
                _logger.Debug("No new volumes for {0} (latest on MangaDex: {1})", series.Name, remoteVolumeNumbers.Max());
                return result;
            }

            _logger.Info("Found {0} new volume(s) for {1}: {2}",
                newVolumeNumbers.Count, series.Name, string.Join(", ", newVolumeNumbers));

            foreach (var volumeNumber in newVolumeNumbers)
            {
                try
                {
                    var volume = new Volume
                    {
                        VolumeNumber = volumeNumber,
                        Title = $"{series.Name} Vol. {volumeNumber:000}"
                    };

                    var searchResult = await _searchService.SearchAndDownloadAsync(series, volume);

                    if (searchResult.Success && searchResult.DownloadedVolumes.Any())
                    {
                        var downloaded = searchResult.DownloadedVolumes.First();
                        _logger.Info("Auto-downloaded {0} volume {1} from {2} (file: {3})",
                            series.Name, volumeNumber, downloaded.Source, downloaded.FilePath);
                        result.Downloaded++;
                    }
                    else
                    {
                        var error = searchResult.FailedVolumes.FirstOrDefault()?.ErrorMessage ?? "Unknown error";
                        _logger.Warn("Failed to auto-download {0} volume {1}: {2}",
                            series.Name, volumeNumber, error);
                        result.Failed++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error downloading {0} volume {1}", series.Name, volumeNumber);
                    result.Failed++;
                }
            }

            series.LastInfoSync = DateTime.UtcNow;
            _seriesService.UpdateSeries(series);

            return result;
        }

        private class CheckResult
        {
            public int NewVolumes { get; set; }
            public int Downloaded { get; set; }
            public int Failed { get; set; }
        }
    }
}
