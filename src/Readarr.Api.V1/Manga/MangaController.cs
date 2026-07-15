using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using global::System.Threading.Tasks;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Datastore.Events;
using NzbDrone.Core.Manga;
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
        private static readonly HttpClient _httpClient = new HttpClient();

        public MangaController(
            IMangaSeriesService mangaService,
            IBroadcastSignalRMessage signalRBroadcaster)
            : base(signalRBroadcaster)
        {
            _mangaService = mangaService;

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
        public async Task<IActionResult> GetCover([FromQuery] string url)
        {
            if (string.IsNullOrEmpty(url) || !url.StartsWith("https://uploads.mangadex.org/"))
            {
                return BadRequest("Invalid cover URL");
            }

            try
            {
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                var bytes = await response.Content.ReadAsByteArrayAsync();
                var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
                return File(bytes, contentType);
            }
            catch (Exception)
            {
                return StatusCode(502, "Failed to fetch cover image");
            }
        }
    }
}
