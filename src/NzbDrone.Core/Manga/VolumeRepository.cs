using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Manga
{
    public interface IVolumeRepository : IBasicRepository<Volume>
    {
        List<Volume> FindByMangaSeriesId(int mangaSeriesId);
        Volume FindByForeignVolumeId(string foreignVolumeId);
    }

    public class VolumeRepository : BasicRepository<Volume>, IVolumeRepository
    {
        public VolumeRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public List<Volume> FindByMangaSeriesId(int mangaSeriesId)
        {
            return Query(x => x.MangaSeriesId == mangaSeriesId).ToList();
        }

        public Volume FindByForeignVolumeId(string foreignVolumeId)
        {
            return Query(x => x.ForeignVolumeId == foreignVolumeId).FirstOrDefault();
        }
    }
}
