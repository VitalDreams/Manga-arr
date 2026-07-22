using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Manga
{
    public interface IChapterRepository : IBasicRepository<Chapter>
    {
        List<Chapter> FindByVolumeId(int volumeId);
    }

    public class ChapterRepository : BasicRepository<Chapter>, IChapterRepository
    {
        public ChapterRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public List<Chapter> FindByVolumeId(int volumeId)
        {
            return Query(x => x.VolumeId == volumeId).ToList();
        }
    }
}
