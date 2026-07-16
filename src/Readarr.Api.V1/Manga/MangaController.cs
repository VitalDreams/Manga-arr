using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Common.Http;
using NzbDrone.Core.Datastore.Events;
using NzbDrone.Core.Manga;
using NzbDrone.Core.MediaCover;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Http.REST.Attributes;
using NzbDrone.SignalR;
using Readarr.Http;
using Readarr.Http.REST;

namespace Readarr.Api.V1.Manga
{
    [V1ApiController]
    public class MangaController : RestControllerWithSignalR<MangaResource, MangaSeries>
    {
        private readonly IMangaSeriesService _mangaService;
        private readonly IMangaSearchService _searchService;
        private readonly IMapCoversToLocal _coverMapper;
        private readonly IHttpClient _httpClient;

        public MangaController(
            IMangaSeriesService mangaService,
            IMangaSearchService searchService,
            IMapCoversToLocal coverMapper,
            IHttpClient httpClient,
            IBroadcastSignalRMessage signalRBroadcaster)
            : base(signalRBroadcaster)
        {
            _mangaService = mangaService;
            _searchService = searchService;
            _coverMapper = coverMapper;
            _httpClient = httpClient;

            SharedValidator.RuleFor(s => s.Title).NotEmpty();
            SharedValidator.RuleFor(s => s.Path).NotEmpty();
            PostValidator.RuleFor(s => s.ForeignMangaId).NotEmpty();
        }

        protected override MangaResource GetResourceById(int id)
        {
            var series = _mangaService.GetSeries(id);
            return series.ToResource();
        }

        [HttpGet]
        [Produces("application/json")]
        public List<MangaResource> AllManga()
        {
            return _mangaService.GetAllSeries().ToResource();
        }


        [RestPostById]
        public ActionResult<MangaResource> AddManga([FromBody] MangaResource mangaResource)
        {
            var series = _mangaService.AddSeries(mangaResource.ToModel());
            var resource = series.ToResource();

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
            _mangaService.UpdateSeries(model);
            BroadcastResourceChange(ModelAction.Updated, mangaResource);
            return Accepted(mangaResource.Id);
        }

        [RestDeleteById]
        public void DeleteManga(int id, bool deleteFiles = false)
        {
            _mangaService.DeleteSeries(id, deleteFiles);
        }

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
    }
}
