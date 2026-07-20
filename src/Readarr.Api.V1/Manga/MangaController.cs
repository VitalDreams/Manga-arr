using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Common.Http;
using NzbDrone.Core.Datastore.Events;
using NzbDrone.Core.Books;
using NzbDrone.Core.Manga;
using NzbDrone.Core.MediaCover;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Http.REST.Attributes;
using NzbDrone.SignalR;
using Readarr.Api.V1.Books;
using Readarr.Http;
using Readarr.Http.REST;

namespace Readarr.Api.V1.Manga
{
    [V1ApiController]
    public class MangaController : RestControllerWithSignalR<MangaResource, MangaSeries>
    {
        private readonly IMangaSeriesService _mangaService;
        private readonly IMangaSearchService _searchService;
        private readonly IVolumeRepository _volumeRepository;
        private readonly IMapCoversToLocal _coverMapper;
        private readonly IHttpClient _httpClient;

        public MangaController(
            IMangaSeriesService mangaService,
            IMangaSearchService searchService,
            IVolumeRepository volumeRepository,
            IMapCoversToLocal coverMapper,
            IHttpClient httpClient,
            IBroadcastSignalRMessage signalRBroadcaster)
            : base(signalRBroadcaster)
        {
            _mangaService = mangaService;
            _searchService = searchService;
            _volumeRepository = volumeRepository;
            _coverMapper = coverMapper;
            _httpClient = httpClient;

            SharedValidator.RuleFor(s => s.Title).NotEmpty();
            SharedValidator.RuleFor(s => s.Path).NotEmpty();
            PostValidator.RuleFor(s => s.ForeignMangaId).NotEmpty();
        }

        protected override MangaResource GetResourceById(int id)
        {
            var series = _mangaService.GetSeries(id);
            var resource = series.ToResource();

            // Include volumes in the response and compute statistics
            var volumes = _volumeRepository.All()
                .Where(v => v.MangaMetadataId == series.MangaMetadataId)
                .OrderBy(v => v.VolumeNumber)
                .ToList();
            resource.Volumes = volumes.ToResource();

            resource.Statistics = new MangaStatisticsResource
            {
                TotalVolumes = volumes.Count,
                MonitoredVolumes = volumes.Count(v => v.Monitored),
                DownloadedVolumes = volumes.Count(v => v.Monitored),
                TotalChapters = 0,
                DownloadedChapters = 0
            };

            return resource;
        }

        [HttpGet]
        [Produces("application/json")]
        public List<MangaResource> AllManga()
        {
            var allSeries = _mangaService.GetAllSeries();
            var resources = allSeries.ToResource();

            // Include volumes for each series and compute statistics
            var allVolumes = _volumeRepository.All().ToList();
            foreach (var resource in resources)
            {
                var mangaVolumes = allVolumes
                    .Where(v => v.MangaMetadataId == resource.MangaMetadataId)
                    .OrderBy(v => v.VolumeNumber)
                    .ToList();

                resource.Volumes = mangaVolumes.ToResource();

                resource.Statistics = new MangaStatisticsResource
                {
                    TotalVolumes = mangaVolumes.Count,
                    MonitoredVolumes = mangaVolumes.Count(v => v.Monitored),
                    DownloadedVolumes = mangaVolumes.Count(v => v.Monitored),
                    TotalChapters = 0,
                    DownloadedChapters = 0
                };
            }

            return resources;
        }


        [RestPostById]
        public ActionResult<MangaResource> AddManga([FromBody] MangaResource mangaResource)
        {
            var series = mangaResource.ToModel();
            series.CleanName = (mangaResource.Title ?? string.Empty).ToLowerInvariant().Replace(" ", string.Empty);

            // Populate MangaMetadata from the resource so AddSeries can persist it
            var metadata = series.Metadata?.Value ?? new NzbDrone.Core.Manga.MangaMetadata();
            metadata.ForeignMangaId = mangaResource.ForeignMangaId;
            metadata.Title = mangaResource.Title;
            metadata.CoverUrl = mangaResource.CoverUrl;
            metadata.Year = mangaResource.Year;
            metadata.TotalVolumes = mangaResource.TotalVolumes;
            metadata.TotalChapters = mangaResource.TotalChapters;
            metadata.Description = mangaResource.Overview;
            metadata.Author = mangaResource.Author;
            metadata.Artist = mangaResource.Artist;
            metadata.Status = mangaResource.Status;
            metadata.Demographic = mangaResource.Demographic;
            metadata.Genres = mangaResource.Genres ?? new global::System.Collections.Generic.List<string>();
            metadata.Tags = mangaResource.Tags ?? new global::System.Collections.Generic.List<string>();
            series.Metadata = metadata;

            _mangaService.AddSeries(series);

            // Fetch volumes and chapters from MangaDex
            _mangaService.FetchAndStoreVolumes(series);

            var resource = series.ToResource();

            // Include volumes in the response
            var volumes = _volumeRepository.All()
                .Where(v => v.MangaMetadataId == series.MangaMetadataId)
                .OrderBy(v => v.VolumeNumber)
                .ToList();
            resource.Volumes = volumes.ToResource();

            // Map cover URL to local path after download
            if (resource.CoverUrl != null)
            {
                var covers = new List<NzbDrone.Core.MediaCover.MediaCover>
                {
                    new NzbDrone.Core.MediaCover.MediaCover(NzbDrone.Core.MediaCover.MediaCoverTypes.Cover, resource.CoverUrl)
                };
                _coverMapper.ConvertToLocalUrls(series.Id, NzbDrone.Core.MediaCover.MediaCoverEntity.Manga, covers);
                resource.CoverUrl = covers[0].Url;
            }

            return Created(series.Id);
        }

        [RestPutById]
        public ActionResult<MangaResource> UpdateManga([FromBody] MangaResource mangaResource)
        {
            var existing = _mangaService.GetSeries(mangaResource.Id);
            var model = mangaResource.ToModel();
            model.Id = existing.Id;
            model.Added = existing.Added;
            model.MangaMetadataId = existing.MangaMetadataId;

            // Propagate metadata changes
            var metadata = existing.Metadata?.Value ?? new NzbDrone.Core.Manga.MangaMetadata();
            metadata.Title = mangaResource.Title;
            metadata.CoverUrl = mangaResource.CoverUrl;
            metadata.Year = mangaResource.Year;
            metadata.TotalVolumes = mangaResource.TotalVolumes;
            metadata.TotalChapters = mangaResource.TotalChapters;
            metadata.Description = mangaResource.Overview;
            metadata.Author = mangaResource.Author;
            metadata.Artist = mangaResource.Artist;
            metadata.Status = mangaResource.Status;
            metadata.Demographic = mangaResource.Demographic;
            model.Metadata = metadata;

            _mangaService.UpdateSeries(model);
            BroadcastResourceChange(ModelAction.Updated, mangaResource);
            return Accepted(mangaResource.Id);
        }

        [RestDeleteById]
        public void DeleteManga(int id, bool deleteFiles = false)
        {
            _mangaService.DeleteSeries(id, deleteFiles);
        }

        [AllowAnonymous]
        [HttpGet("cover")]
        public IActionResult GetCover([FromQuery] string url)
        {
            if (string.IsNullOrEmpty(url) || !url.StartsWith("https://uploads.mangadex.org/"))
            {
                return BadRequest("Invalid cover URL");
            }

            try
            {
                var request = new HttpRequest(url);
                var response = _httpClient.Get(request);

                var contentType = response.Headers.ContentType ?? "image/jpeg";

                Response.Headers["Cache-Control"] = "public, max-age=86400";

                return File(response.ResponseData, contentType);
            }
            catch (Exception)
            {
                return StatusCode(502, "Failed to fetch cover image");
            }
        }

        [HttpPost("{id}/search")]
        public async Task<ActionResult<MangaSearchAndDownloadResult>> SearchAllVolumes(int id)
        {
            var series = _mangaService.GetSeries(id);
            if (series == null)
            {
                return NotFound();
            }

            var result = await _searchService.SearchAndDownloadAsync(id);
            return Ok(result);
        }

        [HttpPost("{id}/search/{volumeId}")]
        public async Task<ActionResult<MangaSearchAndDownloadResult>> SearchVolume(int id, int volumeId)
        {
            var series = _mangaService.GetSeries(id);
            if (series == null)
            {
                return NotFound();
            }

            var result = await _searchService.SearchAndDownloadAsync(id, volumeId);
            return Ok(result);
        }

        [HttpGet("{id}/books")]
        public List<BookResource> GetMangaBooks(int id)
        {
            var series = _mangaService.GetSeries(id);
            if (series == null)
            {
                return new List<BookResource>();
            }

            var volumes = _volumeRepository.All()
                .Where(v => v.MangaSeriesId == id)
                .OrderBy(v => v.VolumeNumber)
                .ToList();

            return volumes.Select(v => new BookResource
            {
                Id = v.Id,
                Title = v.Title ?? $"Volume {v.VolumeNumber}",
                AuthorId = v.MangaSeriesId,
                ForeignBookId = v.ForeignVolumeId,
                TitleSlug = v.TitleSlug,
                Monitored = v.Monitored,
                AnyEditionOk = v.AnyEditionOk,
                ReleaseDate = v.ReleaseDate,
                PageCount = v.Chapters?.Value?.Count ?? 0,
                Genres = v.Genres,
                Ratings = v.Ratings ?? new Ratings(),
                Added = v.Added,
                SeriesTitle = $"Volume {v.VolumeNumber}",
                LastSearchTime = v.LastSearchTime
            }).ToList();
        }
    }
}
