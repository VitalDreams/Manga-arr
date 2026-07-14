using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Manga;

namespace NzbDrone.Api.Manga
{
    [ApiController]
    [Route("api/v3/health")]
    public class HealthController : Controller
    {
        private readonly IKomgaIntegration _komga;
        private readonly IMangaMetadataConnector _connector;

        public HealthController(IKomgaIntegration komga, IMangaMetadataConnector connector)
        {
            _komga = komga;
            _connector = connector;
        }

        /// <summary>
        /// System health check
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetHealth()
        {
            var komgaAvailable = await _komga.IsAvailableAsync();

            return Ok(new
            {
                Status = "healthy",
                Version = "0.1.0",
                Services = new
                {
                    MangaDex = new
                    {
                        Status = _connector.Enabled ? "connected" : "disabled",
                        Name = _connector.Name
                    },
                    Komga = new
                    {
                        Status = komgaAvailable ? "connected" : "not configured",
                        BaseUrl = _komga.BaseUrl ?? "not set"
                    }
                },
                Timestamp = DateTime.UtcNow
            });
        }
    }
}
