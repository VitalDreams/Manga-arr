using NzbDrone.Common.Messaging;

namespace NzbDrone.Core.Manga
{
    public class MangaSeriesAddedEvent : IEvent
    {
        public MangaSeries Series { get; set; }

        public MangaSeriesAddedEvent(MangaSeries series)
        {
            Series = series;
        }
    }

    public class MangaSeriesUpdatedEvent : IEvent
    {
        public MangaSeries Series { get; set; }

        public MangaSeriesUpdatedEvent(MangaSeries series)
        {
            Series = series;
        }
    }

    public class MangaSeriesDeletedEvent : IEvent
    {
        public MangaSeries Series { get; set; }
        public bool DeleteFiles { get; set; }

        public MangaSeriesDeletedEvent(MangaSeries series, bool deleteFiles)
        {
            Series = series;
            DeleteFiles = deleteFiles;
        }
    }
}
