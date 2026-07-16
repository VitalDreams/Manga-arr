using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Manga
{
    public interface IStoryArcRepository : IBasicRepository<StoryArc>
    {
        List<StoryArc> GetArcsByMangaMetadata(int mangaMetadataId);
        StoryArc GetArcByForeignId(int mangaMetadataId, string foreignArcId);
    }

    public class StoryArcRepository : BasicRepository<StoryArc>, IStoryArcRepository
    {
        public StoryArcRepository(IMainDatabase database,
                                  IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public List<StoryArc> GetArcsByMangaMetadata(int mangaMetadataId)
        {
            return Query(a => a.MangaMetadataId == mangaMetadataId).OrderBy(a => a.ArcOrder).ToList();
        }

        public StoryArc GetArcByForeignId(int mangaMetadataId, string foreignArcId)
        {
            return Query(a => a.MangaMetadataId == mangaMetadataId && a.ForeignArcId == foreignArcId).SingleOrDefault();
        }
    }
}
