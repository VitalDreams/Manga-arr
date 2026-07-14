using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Manga;

namespace NzbDrone.Api.Manga
{
    [ApiController]
    [Route("api/v3/manga-settings")]
    public class MangaSettingsController : Controller
    {
        private readonly IKomgaIntegration _komga;
        private readonly IMangaMetadataConnector _connector;

        public MangaSettingsController(IKomgaIntegration komga, IMangaMetadataConnector connector)
        {
            _komga = komga;
            _connector = connector;
        }

        /// <summary>
        /// Get current MangaDex connector status
        /// </summary>
        [HttpGet("mangadex")]
        public IActionResult GetMangaDexStatus()
        {
            return Ok(new
            {
                Name = _connector.Name,
                BaseUrl = _connector.BaseUrl,
                Enabled = _connector.Enabled,
                ApiKeyRequired = false,
                RateLimits = new
                {
                    General = "5 req/s",
                    Images = "1 req/s",
                    Search = "50 req/min"
                }
            });
        }

        /// <summary>
        /// Get Komga connection status
        /// </summary>
        [HttpGet("komga")]
        public async Task<IActionResult> GetKomgaStatus()
        {
            var isAvailable = await _komga.IsAvailableAsync();
            return Ok(new
            {
                Available = isAvailable,
                BaseUrl = _komga.BaseUrl ?? "not configured"
            });
        }

        /// <summary>
        /// Update Komga settings
        /// </summary>
        [HttpPut("komga")]
        public IActionResult UpdateKomgaSettings([FromBody] KomgaSettingsRequest request)
        {
            _komga.BaseUrl = request.BaseUrl;
            _komga.ApiKey = request.ApiKey;
            return Ok(new { Status = "updated" });
        }

        /// <summary>
        /// Test Komga connection
        /// </summary>
        [HttpPost("komga/test")]
        public async Task<IActionResult> TestKomgaConnection()
        {
            var isAvailable = await _komga.IsAvailableAsync();
            if (isAvailable)
            {
                return Ok(new { Status = "connected" });
            }
            return BadRequest(new { Status = "failed", Message = "Cannot connect to Komga" });
        }

        /// <summary>
        /// Get naming template options
        /// </summary>
        [HttpGet("naming")]
        public IActionResult GetNamingOptions()
        {
            return Ok(new
            {
                Templates = new[]
                {
                    new { Name = "Standard", Pattern = "{SeriesTitle} - Vol.{VolumeNumber:000}.cbz", Example = "Berserk - Vol.042.cbz" },
                    new { Name = "With Year", Pattern = "{SeriesTitle} v{VolumeNumber} ({Year}).cbz", Example = "Berserk v42 (2025).cbz" },
                    new { Name = "Full", Pattern = "{SeriesTitle} Volume {VolumeNumber}.cbz", Example = "Berserk Volume 42.cbz" },
                    new { Name = "Minimal", Pattern = "Vol.{VolumeNumber:000}.cbz", Example = "Vol.042.cbz" }
                }
            });
        }
    }

    public class KomgaSettingsRequest
    {
        public string BaseUrl { get; set; }
        public string ApiKey { get; set; }
    }
}
