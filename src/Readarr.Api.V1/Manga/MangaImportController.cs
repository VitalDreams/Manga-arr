using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Manga;
using NzbDrone.Core.Manga.Import;
using NzbDrone.SignalR;
using Readarr.Http;
using Readarr.Http.REST;

namespace Readarr.Api.V1.Manga
{
    [V1ApiController]
    public class MangaImportController : RestController
    {
        private readonly IMangaImportService _importService;
        private readonly IMangaFileScanner _fileScanner;
        private readonly IMangaSeriesService _seriesService;

        public MangaImportController(
            IMangaImportService importService,
            IMangaFileScanner fileScanner,
            IMangaSeriesService seriesService,
            IBroadcastSignalRMessage signalRBroadcaster)
            : base(signalRBroadcaster)
        {
            _importService = importService;
            _fileScanner = fileScanner;
            _seriesService = seriesService;
        }

        [HttpPost("import")]
        public ActionResult<MangaResource> ImportSeries([FromBody] MangaImportResource resource)
        {
            if (string.IsNullOrEmpty(resource.Path))
            {
                return BadRequest("Path is required");
            }

            if (string.IsNullOrEmpty(resource.ForeignMangaId))
            {
                return BadRequest("ForeignMangaId is required");
            }

            var importMode = ParseImportMode(resource.ImportMode);
            var series = _importService.ImportSeries(resource.Path, resource.ForeignMangaId, importMode);

            if (series == null)
            {
                return BadRequest("Failed to import series. Check logs for details.");
            }

            return Created(series.Id);
        }

        [HttpPost("importfile")]
        public ActionResult<MangaFile> ImportFile([FromBody] MangaImportFileResource resource)
        {
            if (string.IsNullOrEmpty(resource.FilePath))
            {
                return BadRequest("FilePath is required");
            }

            if (resource.SeriesId <= 0)
            {
                return BadRequest("Valid SeriesId is required");
            }

            if (resource.VolumeId <= 0)
            {
                return BadRequest("Valid VolumeId is required");
            }

            var mangaFile = _importService.ImportFile(resource.FilePath, resource.SeriesId, resource.VolumeId);

            if (mangaFile == null)
            {
                return BadRequest("Failed to import file. Check logs for details.");
            }

            return Ok(mangaFile);
        }

        [HttpPost("scan")]
        public ActionResult<List<ScannedMangaFileResource>> ScanDirectory([FromBody] MangaScanResource resource)
        {
            if (string.IsNullOrEmpty(resource.Path))
            {
                return BadRequest("Path is required");
            }

            var scannedFiles = _fileScanner.ScanDirectory(resource.Path);
            return Ok(scannedFiles.Select(f => f.ToResource()).ToList());
        }

        private MangaImportMode ParseImportMode(string mode)
        {
            if (string.IsNullOrEmpty(mode))
            {
                return MangaImportMode.InPlace;
            }

            return mode.ToLowerInvariant() switch
            {
                "move" => MangaImportMode.Move,
                "inplace" or "in-place" or "inPlace" => MangaImportMode.InPlace,
                _ => MangaImportMode.InPlace
            };
        }
    }

    public class MangaScanResource : RestResource
    {
        public string Path { get; set; }
    }
}
