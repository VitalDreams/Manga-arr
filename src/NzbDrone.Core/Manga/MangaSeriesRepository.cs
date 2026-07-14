using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Manga
{
    public interface IMangaSeriesRepository : IBasicRepository<MangaSeries>
    {
    }

    public class MangaSeriesRepository : BasicRepository<MangaSeries>, IMangaSeriesRepository
    {
        public MangaSeriesRepository(IMainDatabase database,
                                    IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }
    }
}
