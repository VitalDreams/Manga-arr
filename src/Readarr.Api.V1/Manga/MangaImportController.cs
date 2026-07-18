using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Manga;
using NzbDrone.Core.Manga.Import;
using Readarr.Http;
using Readarr.Http.REST;

namespace Readarr.Api.V1.Manga
{
    [V1ApiController("manga")]
    public class MangaImportController : Controller
    {
        private readonly IMangaImportService _importService;
        private readonly IMangaFileScanner _fileScanner;
        private readonly IMangaSeriesService _seriesService;
        private readonly IKomgaIntegration _komga;
        private readonly IDiskProvider _diskProvider;

        public MangaImportController(
            IMangaImportService importService,
            IMangaFileScanner fileScanner,
            IMangaSeriesService seriesService,
            IKomgaIntegration komga,
            IDiskProvider diskProvider)
        {
            _importService = importService;
            _fileScanner = fileScanner;
            _seriesService = seriesService;
            _komga = komga;
            _diskProvider = diskProvider;
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

            return Created($"/api/v1/manga/{series.Id}", series.ToResource());
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

        [HttpPost("autoimport")]
        public async Task<ActionResult<AutoImportResult>> AutoImport([FromBody] AutoImportResource resource)
        {
            var scanDirs = new List<string>();

            if (resource?.Paths != null && resource.Paths.Any())
            {
                scanDirs.AddRange(resource.Paths);
            }
            else
            {
                // Default scan directories
                scanDirs.Add("/manga");
                scanDirs.Add("/downloads/complete/manga");
            }

            var result = await _importService.AutoImportFilesAsync(scanDirs);

            // Trigger Komga library scan after import
            if (result.FilesImported > 0)
            {
                try
                {
                    await _komga.TriggerLibraryScanAsync();
                    result.ImportedFiles.Add("Komga library scan triggered");
                }
                catch (System.Exception ex)
                {
                    result.Errors.Add($"Failed to trigger Komga scan: {ex.Message}");
                }
            }

            return Ok(result);
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
                "inplace" or "in-place" => MangaImportMode.InPlace,
                _ => MangaImportMode.InPlace
            };
        }
    }

    public class MangaScanResource : RestResource
    {
        public string Path { get; set; }
    }

    public class AutoImportResource : RestResource
    {
        public List<string> Paths { get; set; }
    }
}
