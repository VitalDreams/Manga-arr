using System.Linq;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Manga
{
    public interface IMangaMetadataRepository : IBasicRepository<MangaMetadata>
    {
        MangaMetadata FindByForeignMangaId(string foreignMangaId);
    }

    public class MangaMetadataRepository : BasicRepository<MangaMetadata>, IMangaMetadataRepository
    {
        public MangaMetadataRepository(IMainDatabase database,
                                      IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public MangaMetadata FindByForeignMangaId(string foreignMangaId)
        {
            return Query(x => x.ForeignMangaId == foreignMangaId).FirstOrDefault();
        }
    }
}
