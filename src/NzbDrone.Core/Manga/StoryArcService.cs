using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Manga
{
    public interface IStoryArcService
    {
        List<StoryArc> GetArcs(int mangaMetadataId);
        StoryArc GetArc(int arcId);
        StoryArc AddArc(StoryArc arc);
        StoryArc UpdateArc(StoryArc arc);
        void DeleteArc(int arcId);
    }

    public class StoryArcService : IStoryArcService
    {
        private readonly IStoryArcRepository _arcRepository;
        private readonly IEventAggregator _eventAggregator;
        private readonly Logger _logger;

        public StoryArcService(
            IStoryArcRepository arcRepository,
            IEventAggregator eventAggregator,
            Logger logger)
        {
            _arcRepository = arcRepository;
            _eventAggregator = eventAggregator;
            _logger = logger;
        }

        public List<StoryArc> GetArcs(int mangaMetadataId)
        {
            return _arcRepository.GetArcsByMangaMetadata(mangaMetadataId);
        }

        public StoryArc GetArc(int arcId)
        {
            return _arcRepository.Get(arcId);
        }

        public StoryArc AddArc(StoryArc arc)
        {
            var inserted = _arcRepository.Insert(arc);
            _eventAggregator.PublishEvent(new StoryArcAddedEvent(inserted));
            _logger.Info("Added story arc: {0} for manga metadata {1}", arc.Name, arc.MangaMetadataId);
            return inserted;
        }

        public StoryArc UpdateArc(StoryArc arc)
        {
            var updated = _arcRepository.Update(arc);
            _eventAggregator.PublishEvent(new StoryArcUpdatedEvent(updated));
            _logger.Info("Updated story arc: {0}", arc.Name);
            return updated;
        }

        public void DeleteArc(int arcId)
        {
            var arc = GetArc(arcId);
            _arcRepository.Delete(arcId);
            _eventAggregator.PublishEvent(new StoryArcDeletedEvent(arc));
            _logger.Info("Deleted story arc: {0}", arc.Name);
        }
    }
}
