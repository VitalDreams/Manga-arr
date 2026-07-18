using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Manga
{
    public interface IChapterRepository : IBasicRepository<Chapter>
    {
    }

    public class ChapterRepository : BasicRepository<Chapter>, IChapterRepository
    {
        public ChapterRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }
    }
}
