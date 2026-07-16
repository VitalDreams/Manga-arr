using System.Collections.Generic;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Datastore.Events;
using NzbDrone.Core.Manga;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.SignalR;
using Readarr.Http;
using Readarr.Http.REST;

namespace Readarr.Api.V1.Manga
{
    [V1ApiController]
    public class StoryArcController : RestControllerWithSignalR<StoryArcResource, StoryArc>
    {
        private readonly IStoryArcService _arcService;
        private readonly IMangaSeriesService _mangaService;

        public StoryArcController(
            IStoryArcService arcService,
            IMangaSeriesService mangaService,
            IBroadcastSignalRMessage signalRBroadcaster)
            : base(signalRBroadcaster)
        {
            _arcService = arcService;
            _mangaService = mangaService;

            SharedValidator.RuleFor(a => a.Name).NotEmpty();
            PostValidator.RuleFor(a => a.ForeignArcId).NotEmpty();
        }

        protected override StoryArcResource GetResourceById(int id)
        {
            return _arcService.GetArc(id).ToResource();
        }

        [HttpGet("/api/v1/manga/{mangaId}/arcs")]
        [Produces("application/json")]
        public List<StoryArcResource> GetArcsForManga(int mangaId)
        {
            var series = _mangaService.GetSeries(mangaId);
            return _arcService.GetArcs(series.MangaMetadataId).ToResource();
        }

        [HttpGet("/api/v1/manga/{mangaId}/arcs/{arcId}")]
        [Produces("application/json")]
        public StoryArcResource GetArc(int mangaId, int arcId)
        {
            return _arcService.GetArc(arcId).ToResource();
        }

        [HttpPost("/api/v1/manga/{mangaId}/arcs")]
        public ActionResult<StoryArcResource> AddArc(int mangaId, [FromBody] StoryArcResource arcResource)
        {
            var series = _mangaService.GetSeries(mangaId);
            var model = arcResource.ToModel();
            model.MangaMetadataId = series.MangaMetadataId;

            var arc = _arcService.AddArc(model);
            return Created(arc.Id);
        }

        [HttpPut("/api/v1/manga/{mangaId}/arcs/{arcId}")]
        public ActionResult<StoryArcResource> UpdateArc(int mangaId, int arcId, [FromBody] StoryArcResource arcResource)
        {
            var existing = _arcService.GetArc(arcId);
            var model = arcResource.ToModel();
            model.Id = existing.Id;
            model.MangaMetadataId = existing.MangaMetadataId;

            _arcService.UpdateArc(model);
            BroadcastResourceChange(ModelAction.Updated, arcResource);
            return Accepted(arcResource.Id);
        }

        [HttpDelete("/api/v1/manga/{mangaId}/arcs/{arcId}")]
        public void DeleteArc(int mangaId, int arcId)
        {
            _arcService.DeleteArc(arcId);
        }
    }
}
