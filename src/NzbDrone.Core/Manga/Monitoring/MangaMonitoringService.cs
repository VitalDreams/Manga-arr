using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using NLog;
using NzbDrone.Core.Manga.Connectors;

namespace NzbDrone.Core.Manga.Monitoring
{
    /// <summary>
    /// Background service that monitors manga for new volumes
    /// Checks MangaDex periodically and auto-downloads new volumes
    /// </summary>
    public class MangaMonitoringService : BackgroundService
    {
        private readonly IMetadataAggregator _metadataAggregator;
        private readonly IMangaDexDownloader _downloader;
        private readonly IKomgaIntegration _komga;
        private readonly INotificationService _notifications;
        private readonly Logger _logger;

        // In-memory store (will be replaced with DB)
        private static readonly List<MonitoredManga> _monitoredManga = new();
        private static readonly List<DownloadHistory> _downloadHistory = new();

        public TimeSpan CheckInterval { get; set; } = TimeSpan.FromHours(24);
        public bool Enabled { get; set; } = true;

        public MangaMonitoringService(
            IMetadataAggregator metadataAggregator,
            IMangaDexDownloader downloader,
            IKomgaIntegration komga,
            INotificationService notifications,
            Logger logger)
        {
            _metadataAggregator = metadataAggregator;
            _downloader = downloader;
            _komga = komga;
            _notifications = notifications;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.Info("Manga monitoring service started");

            while (!stoppingToken.IsCancellationRequested)
            {
                if (Enabled)
                {
                    try
                    {
                        await CheckForNewVolumesAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Error during manga monitoring check");
                    }
                }

                await Task.Delay(CheckInterval, stoppingToken);
            }

            _logger.Info("Manga monitoring service stopped");
        }

        /// <summary>
        /// Check all monitored manga for new volumes
        /// </summary>
        private async Task CheckForNewVolumesAsync()
        {
            _logger.Info($"Checking {_monitoredManga.Count} monitored manga for new volumes...");

            foreach (var manga in _monitoredManga.Where(m => m.Monitored))
            {
                try
                {
                    await CheckMangaForNewVolumesAsync(manga);
                    manga.LastChecked = DateTime.UtcNow;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, $"Error checking {manga.Title}");
                }
            }

            _logger.Info("Manga monitoring check complete");
        }

        /// <summary>
        /// Check a specific manga for new volumes
        /// </summary>
        private async Task CheckMangaForNewVolumesAsync(MonitoredManga manga)
        {
            _logger.Debug($"Checking {manga.Title} for new volumes...");

            // Get current volume list from MangaDex
            var volumeMap = await _metadataAggregator.GetVolumeChapterMapAsync(manga.MangaDexId);
            var currentVolumes = volumeMap.VolumeChapters.Keys.OrderBy(v => v).ToList();

            if (!currentVolumes.Any())
            {
                _logger.Debug($"No volumes found for {manga.Title}");
                return;
            }

            var latestVolume = currentVolumes.Max();
            _logger.Debug($"{manga.Title}: latest volume on MangaDex is {latestVolume}, last downloaded is {manga.LastDownloadedVolume}");

            // Check for new volumes since last download
            var newVolumes = currentVolumes
                .Where(v => v > manga.LastDownloadedVolume)
                .OrderBy(v => v)
                .ToList();

            if (!newVolumes.Any())
            {
                _logger.Debug($"No new volumes for {manga.Title}");
                return;
            }

            _logger.Info($"Found {newVolumes.Count} new volumes for {manga.Title}: {string.Join(", ", newVolumes.ToArray())}");

            // Auto-download new volumes
            foreach (var volumeNumber in newVolumes)
            {
                if (!manga.AutoDownload)
                {
                    _logger.Info($"Auto-download disabled for {manga.Title}, skipping volume {volumeNumber}");
                    continue;
                }

                await DownloadVolumeAsync(manga, volumeNumber);
            }
        }

        /// <summary>
        /// Download a specific volume and update tracking
        /// </summary>
        private async Task DownloadVolumeAsync(MonitoredManga manga, int volumeNumber)
        {
            _logger.Info($"Auto-downloading {manga.Title} volume {volumeNumber}...");

            var series = new MangaSeries
            {
                ForeignMangaId = manga.MangaDexId,
                Name = manga.Title,
                Path = manga.OutputPath
            };

            var volume = new Volume
            {
                VolumeNumber = volumeNumber,
                Title = $"{manga.Title} Vol. {volumeNumber:000}"
            };

            try
            {
                var result = await _downloader.DownloadVolumeAsync(manga.OutputPath, series, volume);

                if (result != null)
                {
                    _logger.Info($"Successfully downloaded {manga.Title} volume {volumeNumber} to {result}");

                    // Update tracking
                    manga.LastDownloadedVolume = volumeNumber;
                    manga.LastDownloadedAt = DateTime.UtcNow;

                    _downloadHistory.Add(new DownloadHistory
                    {
                        MangaDexId = manga.MangaDexId,
                        Title = manga.Title,
                        VolumeNumber = volumeNumber,
                        DownloadedAt = DateTime.UtcNow,
                        FilePath = result,
                        Status = "completed"
                    });

                    // Trigger Komga scan
                    await _komga.TriggerLibraryScanAsync();

                    // Send notification
                    await _notifications.SendAsync(new Notification
                    {
                        Title = "New Manga Volume Downloaded",
                        Message = $"{manga.Title} Volume {volumeNumber} has been downloaded",
                        Type = NotificationType.Download,
                        Data = new
                        {
                            MangaTitle = manga.Title,
                            VolumeNumber = volumeNumber,
                            FilePath = result
                        }
                    });
                }
                else
                {
                    _logger.Warn($"Failed to download {manga.Title} volume {volumeNumber}");

                    _downloadHistory.Add(new DownloadHistory
                    {
                        MangaDexId = manga.MangaDexId,
                        Title = manga.Title,
                        VolumeNumber = volumeNumber,
                        DownloadedAt = DateTime.UtcNow,
                        Status = "failed"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Error downloading {manga.Title} volume {volumeNumber}");
            }
        }

        // Public API methods
        public void AddManga(MonitoredManga manga)
        {
            if (!_monitoredManga.Any(m => m.MangaDexId == manga.MangaDexId))
            {
                _monitoredManga.Add(manga);
                _logger.Info($"Added {manga.Title} to monitoring");
            }
        }

        public void RemoveManga(string mangadexId)
        {
            var manga = _monitoredManga.FirstOrDefault(m => m.MangaDexId == mangadexId);
            if (manga != null)
            {
                _monitoredManga.Remove(manga);
                _logger.Info($"Removed {manga.Title} from monitoring");
            }
        }

        public void UpdateManga(string mangadexId, Action<MonitoredManga> update)
        {
            var manga = _monitoredManga.FirstOrDefault(m => m.MangaDexId == mangadexId);
            if (manga != null)
            {
                update(manga);
                _logger.Info($"Updated monitoring settings for {manga.Title}");
            }
        }

        public List<MonitoredManga> GetMonitoredManga() => _monitoredManga.ToList();

        public List<DownloadHistory> GetDownloadHistory() => _downloadHistory.ToList();

        public MonitoredManga GetManga(string mangadexId) =>
            _monitoredManga.FirstOrDefault(m => m.MangaDexId == mangadexId);
    }

    public class MonitoredManga
    {
        public string MangaDexId { get; set; }
        public string Title { get; set; }
        public string OutputPath { get; set; }
        public bool Monitored { get; set; } = true;
        public bool AutoDownload { get; set; } = true;
        public int LastDownloadedVolume { get; set; }
        public DateTime? LastDownloadedAt { get; set; }
        public DateTime? LastChecked { get; set; }
        public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    }

    public class DownloadHistory
    {
        public string MangaDexId { get; set; }
        public string Title { get; set; }
        public int VolumeNumber { get; set; }
        public DateTime DownloadedAt { get; set; }
        public string FilePath { get; set; }
        public string Status { get; set; }
    }
}
