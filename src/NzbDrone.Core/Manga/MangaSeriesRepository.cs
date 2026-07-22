using System.Linq;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Manga
{
    public interface IMangaSeriesRepository : IBasicRepository<MangaSeries>
    {
        MangaSeries FindByMangaMetadataId(int mangaMetadataId);
    }

    public class MangaSeriesRepository : BasicRepository<MangaSeries>, IMangaSeriesRepository
    {
        public MangaSeriesRepository(IMainDatabase database,
                                    IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public MangaSeries FindByMangaMetadataId(int mangaMetadataId)
        {
            return Query(x => x.MangaMetadataId == mangaMetadataId).FirstOrDefault();
        }
    }
}
