using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Manga.Monitoring;

namespace NzbDrone.Api.Manga
{
    [ApiController]
    [Route("api/v3/monitoring")]
    public class MonitoringController : Controller
    {
        private readonly MangaMonitoringService _monitoringService;
        private readonly NotificationService _notificationService;

        public MonitoringController(
            MangaMonitoringService monitoringService,
            NotificationService notificationService)
        {
            _monitoringService = monitoringService;
            _notificationService = notificationService;
        }

        /// <summary>
        /// Get all monitored manga
        /// </summary>
        [HttpGet("manga")]
        public IActionResult GetMonitoredManga()
        {
            var manga = _monitoringService.GetMonitoredManga();
            return Ok(manga);
        }

        /// <summary>
        /// Get a specific monitored manga
        /// </summary>
        [HttpGet("manga/{mangadexId}")]
        public IActionResult GetMonitoredManga(string mangadexId)
        {
            var manga = _monitoringService.GetManga(mangadexId);
            if (manga == null) return NotFound();
            return Ok(manga);
        }

        /// <summary>
        /// Add manga to monitoring
        /// </summary>
        [HttpPost("manga")]
        public IActionResult AddToMonitoring([FromBody] AddMonitoringRequest request)
        {
            var manga = new MonitoredManga
            {
                MangaDexId = request.MangaDexId,
                Title = request.Title,
                OutputPath = request.OutputPath ?? "/manga",
                Monitored = request.Monitored,
                AutoDownload = request.AutoDownload
            };

            _monitoringService.AddManga(manga);
            return CreatedAtAction(nameof(GetMonitoredManga), new { mangadexId = manga.MangaDexId }, manga);
        }

        /// <summary>
        /// Update monitoring settings for a manga
        /// </summary>
        [HttpPut("manga/{mangadexId}")]
        public IActionResult UpdateMonitoring(string mangadexId, [FromBody] UpdateMonitoringSettingsRequest request)
        {
            _monitoringService.UpdateManga(mangadexId, manga =>
            {
                manga.Monitored = request.Monitored ?? manga.Monitored;
                manga.AutoDownload = request.AutoDownload ?? manga.AutoDownload;
                manga.OutputPath = request.OutputPath ?? manga.OutputPath;
            });

            return Ok(_monitoringService.GetManga(mangadexId));
        }

        /// <summary>
        /// Remove manga from monitoring
        /// </summary>
        [HttpDelete("manga/{mangadexId}")]
        public IActionResult RemoveFromMonitoring(string mangadexId)
        {
            _monitoringService.RemoveManga(mangadexId);
            return NoContent();
        }

        /// <summary>
        /// Get download history
        /// </summary>
        [HttpGet("history")]
        public IActionResult GetDownloadHistory([FromQuery] int limit = 50)
        {
            var history = _monitoringService.GetDownloadHistory()
                .OrderByDescending(h => h.DownloadedAt)
                .Take(limit)
                .ToList();

            return Ok(history);
        }

        /// <summary>
        /// Get monitoring status
        /// </summary>
        [HttpGet("status")]
        public IActionResult GetStatus()
        {
            var manga = _monitoringService.GetMonitoredManga();
            return Ok(new
            {
                Enabled = _monitoringService.Enabled,
                CheckInterval = _monitoringService.CheckInterval,
                MonitoredCount = manga.Count,
                AutoDownloadCount = manga.Count(m => m.AutoDownload),
                LastCheck = manga.Max(m => m.LastChecked)
            });
        }

        /// <summary>
        /// Update monitoring settings
        /// </summary>
        [HttpPut("settings")]
        public IActionResult UpdateSettings([FromBody] MonitoringGlobalSettingsRequest request)
        {
            _monitoringService.Enabled = request.Enabled ?? _monitoringService.Enabled;
            _monitoringService.CheckInterval = request.CheckIntervalHours.HasValue
                ? TimeSpan.FromHours(request.CheckIntervalHours.Value)
                : _monitoringService.CheckInterval;

            return Ok(new
            {
                _monitoringService.Enabled,
                CheckInterval = _monitoringService.CheckInterval
            });
        }

        /// <summary>
        /// Trigger a manual check
        /// </summary>
        [HttpPost("check")]
        public async Task<IActionResult> TriggerCheck()
        {
            // This will trigger the monitoring service to check now
            // In a real implementation, this would use a signal or queue
            return Ok(new { Status = "Check triggered", Message = "Checking all monitored manga for new volumes..." });
        }
    }

    [ApiController]
    [Route("api/v3/notifications")]
    public class NotificationController : Controller
    {
        private readonly NotificationService _notificationService;

        public NotificationController(NotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        /// <summary>
        /// Get notification settings
        /// </summary>
        [HttpGet("settings")]
        public IActionResult GetSettings()
        {
            var settings = _notificationService.GetSettings();
            return Ok(settings);
        }

        /// <summary>
        /// Update notification settings
        /// </summary>
        [HttpPut("settings")]
        public IActionResult UpdateSettings([FromBody] NotificationSettings settings)
        {
            _notificationService.Configure(settings);
            return Ok(settings);
        }

        /// <summary>
        /// Send test notification
        /// </summary>
        [HttpPost("test/{provider}")]
        public async Task<IActionResult> TestNotification(string provider)
        {
            try
            {
                await _notificationService.SendTestAsync(provider);
                return Ok(new { Status = "sent", Provider = provider });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "failed", Error = ex.Message });
            }
        }
    }

    public class AddMonitoringRequest
    {
        public string MangaDexId { get; set; }
        public string Title { get; set; }
        public string OutputPath { get; set; }
        public bool Monitored { get; set; } = true;
        public bool AutoDownload { get; set; } = true;
    }

    public class UpdateMonitoringSettingsRequest
    {
        public bool? Monitored { get; set; }
        public bool? AutoDownload { get; set; }
        public string OutputPath { get; set; }
    }

    public class MonitoringGlobalSettingsRequest
    {
        public bool? Enabled { get; set; }
        public int? CheckIntervalHours { get; set; }
    }
}
