using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Manga;
using NzbDrone.Core.Manga.Connectors;

namespace NzbDrone.Api.Manga
{
    [ApiController]
    [Route("api/v3/manga")]
    public class MangaController : Controller
    {
        private readonly IMangaMetadataConnector _metadataConnector;
        private readonly IMangaDexDownloader _downloader;
        private readonly IKomgaIntegration _komga;

        public MangaController(
            IMangaMetadataConnector metadataConnector,
            IMangaDexDownloader downloader,
            IKomgaIntegration komga)
        {
            _metadataConnector = metadataConnector;
            _downloader = downloader;
            _komga = komga;
        }

        /// <summary>
        /// Search for manga on MangaDex
        /// </summary>
        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] string query, [FromQuery] int limit = 10)
        {
            var results = await _metadataConnector.SearchAsync(query, limit);
            return Ok(results);
        }

        /// <summary>
        /// Get manga metadata by MangaDex ID
        /// </summary>
        [HttpGet("metadata/{foreignMangaId}")]
        public async Task<IActionResult> GetMetadata(string foreignMangaId)
        {
            var metadata = await _metadataConnector.GetMangaMetadataAsync(foreignMangaId);
            return Ok(metadata);
        }

        /// <summary>
        /// Get volume-to-chapter mapping for a manga
        /// </summary>
        [HttpGet("volumes/{foreignMangaId}")]
        public async Task<IActionResult> GetVolumeMap(string foreignMangaId)
        {
            var map = await _metadataConnector.GetVolumeChapterMapAsync(foreignMangaId);
            return Ok(map);
        }

        /// <summary>
        /// Get chapters for a specific volume
        /// </summary>
        [HttpGet("volumes/{foreignMangaId}/{volumeNumber}/chapters")]
        public async Task<IActionResult> GetChapters(string foreignMangaId, int volumeNumber)
        {
            var chapters = await _metadataConnector.GetChaptersForVolumeAsync(foreignMangaId, volumeNumber);
            return Ok(chapters);
        }

        /// <summary>
        /// Download a volume as CBZ
        /// </summary>
        [HttpPost("download/volume")]
        public async Task<IActionResult> DownloadVolume([FromBody] DownloadVolumeRequest request)
        {
            var series = new MangaSeries
            {
                ForeignMangaId = request.ForeignMangaId,
                Name = request.SeriesTitle
            };

            var volume = new Volume
            {
                VolumeNumber = request.VolumeNumber,
                Title = $"{request.SeriesTitle} Vol. {request.VolumeNumber:000}"
            };

            var result = await _downloader.DownloadVolumeAsync(request.OutputDir, series, volume);

            if (result != null)
            {
                // Trigger Komga scan
                await _komga.TriggerLibraryScanAsync();
                return Ok(new { Path = result, Status = "completed" });
            }

            return BadRequest(new { Status = "failed", Message = "No chapters found" });
        }

        /// <summary>
        /// Download a single chapter as CBZ
        /// </summary>
        [HttpPost("download/chapter")]
        public async Task<IActionResult> DownloadChapter([FromBody] DownloadChapterRequest request)
        {
            var series = new MangaSeries
            {
                ForeignMangaId = request.ForeignMangaId,
                Name = request.SeriesTitle
            };

            var volume = new Volume
            {
                VolumeNumber = request.VolumeNumber,
                Title = $"{request.SeriesTitle} Vol. {request.VolumeNumber:000}"
            };

            var chapter = new Chapter
            {
                ForeignChapterId = request.ForeignChapterId,
                ChapterNumber = request.ChapterNumber
            };

            var result = await _downloader.DownloadChapterAsync(request.OutputDir, series, volume, chapter);

            if (result != null)
            {
                await _komga.TriggerLibraryScanAsync();
                return Ok(new { Path = result, Status = "completed" });
            }

            return BadRequest(new { Status = "failed", Message = "Download failed" });
        }

        /// <summary>
        /// Trigger Komga library scan
        /// </summary>
        [HttpPost("komga/scan")]
        public async Task<IActionResult> TriggerKomgaScan()
        {
            await _komga.TriggerLibraryScanAsync();
            return Ok(new { Status = "scan triggered" });
        }
    }

    public class DownloadVolumeRequest
    {
        public string ForeignMangaId { get; set; }
        public string SeriesTitle { get; set; }
        public int VolumeNumber { get; set; }
        public string OutputDir { get; set; }
    }

    public class DownloadChapterRequest
    {
        public string ForeignMangaId { get; set; }
        public string SeriesTitle { get; set; }
        public int VolumeNumber { get; set; }
        public string ForeignChapterId { get; set; }
        public decimal ChapterNumber { get; set; }
        public string OutputDir { get; set; }
    }
}
