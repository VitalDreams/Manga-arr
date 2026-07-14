using System.Collections.Generic;
using System.Linq;
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

        [HttpGet("lookup")]
        [Produces("application/json")]
        public object LookupManga([FromQuery] string term)
        {
            // Search MangaDex for manga
            // This will be wired to MangaDexConnector later
            return new List<object>();
        }

        [RestPostById]
        public ActionResult<MangaResource> AddManga(MangaResource mangaResource)
        {
            var series = _mangaService.AddSeries(mangaResource.ToModel());
            return Created(series.Id);
        }

        [RestPutById]
        public ActionResult<MangaResource> UpdateManga(MangaResource mangaResource)
        {
            var existing = _mangaService.GetSeries(mangaResource.Id);
            var model = mangaResource.ToModel(existing);
            _mangaService.UpdateSeries(model);
            BroadcastResourceChange(ModelAction.Updated, mangaResource);
            return Accepted(mangaResource.Id);
        }

        [RestDeleteById]
        public void DeleteManga(int id, bool deleteFiles = false)
        {
            _mangaService.DeleteSeries(id, deleteFiles);
        }
    }
}
