using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Manga
{
    public interface IMangaMetadataRepository : IBasicRepository<MangaMetadata>
    {
    }

    public class MangaMetadataRepository : BasicRepository<MangaMetadata>, IMangaMetadataRepository
    {
        public MangaMetadataRepository(IMainDatabase database,
                                      IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }
    }
}
