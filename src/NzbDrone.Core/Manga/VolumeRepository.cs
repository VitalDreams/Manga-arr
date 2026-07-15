using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Manga
{
    public interface IVolumeRepository : IBasicRepository<Volume>
    {
    }

    public class VolumeRepository : BasicRepository<Volume>, IVolumeRepository
    {
        public VolumeRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }
    }
}
