using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Manga;

namespace NzbDrone.Api.Manga
{
    [ApiController]
    [Route("api/v3/series")]
    public class MangaSeriesController : Controller
    {
        private readonly IMangaMetadataConnector _metadataConnector;
        private readonly IMangaDexDownloader _downloader;
        private readonly IKomgaIntegration _komga;

        // In-memory store for now (will be replaced with DB)
        private static readonly List<MangaSeries> _series = new();

        public MangaSeriesController(
            IMangaMetadataConnector metadataConnector,
            IMangaDexDownloader downloader,
            IKomgaIntegration komga)
        {
            _metadataConnector = metadataConnector;
            _downloader = downloader;
            _komga = komga;
        }

        /// <summary>
        /// Get all manga series in the library
        /// </summary>
        [HttpGet]
        public IActionResult GetAll()
        {
            return Ok(_series);
        }

        /// <summary>
        /// Get a specific manga series
        /// </summary>
        [HttpGet("{id}")]
        public IActionResult GetById(int id)
        {
            var series = _series.FirstOrDefault(s => s.Id == id);
            if (series == null) return NotFound();
            return Ok(series);
        }

        /// <summary>
        /// Add a manga series to the library
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Add([FromBody] AddMangaSeriesRequest request)
        {
            // Check if already exists
            if (_series.Any(s => s.ForeignMangaId == request.ForeignMangaId))
            {
                return Conflict(new { Message = "Series already in library" });
            }

            // Get metadata from MangaDex
            var metadata = await _metadataConnector.GetMangaMetadataAsync(request.ForeignMangaId);

            var series = new MangaSeries
            {
                Id = _series.Count + 1,
                ForeignMangaId = request.ForeignMangaId,
                Name = metadata.Title,
                CleanName = metadata.Title?.ToLower().Replace(" ", "-"),
                Path = System.IO.Path.Combine(request.RootFolderPath ?? "/manga", SanitizePath(metadata.Title)),
                RootFolderPath = request.RootFolderPath ?? "/manga",
                Monitored = request.Monitored,
                Added = DateTime.UtcNow,
                Metadata = new Lazy<MangaMetadata>(() => metadata)
            };

            _series.Add(series);

            return CreatedAtAction(nameof(GetById), new { id = series.Id }, series);
        }

        /// <summary>
        /// Update monitoring status for a series
        /// </summary>
        [HttpPut("{id}/monitor")]
        public IActionResult UpdateMonitoring(int id, [FromBody] UpdateMonitoringRequest request)
        {
            var series = _series.FirstOrDefault(s => s.Id == id);
            if (series == null) return NotFound();

            series.Monitored = request.Monitored;
            return Ok(series);
        }

        /// <summary>
        /// Delete a series from the library
        /// </summary>
        [HttpDelete("{id}")]
        public IActionResult Delete(int id)
        {
            var series = _series.FirstOrDefault(s => s.Id == id);
            if (series == null) return NotFound();

            _series.Remove(series);
            return NoContent();
        }

        /// <summary>
        /// Get all volumes for a series
        /// </summary>
        [HttpGet("{id}/volumes")]
        public async Task<IActionResult> GetVolumes(int id)
        {
            var series = _series.FirstOrDefault(s => s.Id == id);
            if (series == null) return NotFound();

            var volumeMap = await _metadataConnector.GetVolumeChapterMapAsync(series.ForeignMangaId);

            var volumes = volumeMap.VolumeChapters.Select(kv => new
            {
                VolumeNumber = kv.Key,
                ChapterCount = kv.Value.Count,
                ChapterIds = kv.Value
            }).OrderBy(v => v.VolumeNumber);

            return Ok(volumes);
        }

        /// <summary>
        /// Download a specific volume
        /// </summary>
        [HttpPost("{id}/download/{volumeNumber}")]
        public async Task<IActionResult> DownloadVolume(int id, int volumeNumber)
        {
            var series = _series.FirstOrDefault(s => s.Id == id);
            if (series == null) return NotFound();

            var volume = new Volume
            {
                VolumeNumber = volumeNumber,
                Title = $"{series.Name} Vol. {volumeNumber:000}"
            };

            var result = await _downloader.DownloadVolumeAsync(series.Path, series, volume);

            if (result != null)
            {
                await _komga.TriggerLibraryScanAsync();
                return Ok(new { Path = result, Status = "completed" });
            }

            return BadRequest(new { Status = "failed" });
        }

        private string SanitizePath(string name)
        {
            var invalidChars = System.IO.Path.GetInvalidPathChars();
            return new string(name.Select(c => invalidChars.Contains(c) ? '_' : c).ToArray());
        }
    }

    public class AddMangaSeriesRequest
    {
        public string ForeignMangaId { get; set; }
        public string RootFolderPath { get; set; }
        public bool Monitored { get; set; } = true;
    }

    public class UpdateMonitoringRequest
    {
        public bool Monitored { get; set; }
    }
}
