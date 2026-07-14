using System.Collections.Generic;
using NLog;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Manga
{
    public interface IMangaSeriesService
    {
        MangaSeries GetSeries(int seriesId);
        List<MangaSeries> GetAllSeries();
        MangaSeries AddSeries(MangaSeries newSeries);
        MangaSeries UpdateSeries(MangaSeries series);
        void DeleteSeries(int seriesId, bool deleteFiles);
    }

    public class MangaSeriesService : IMangaSeriesService
    {
        private readonly IMangaSeriesRepository _seriesRepository;
        private readonly IEventAggregator _eventAggregator;
        private readonly Logger _logger;

        public MangaSeriesService(
            IMangaSeriesRepository seriesRepository,
            IEventAggregator eventAggregator,
            Logger logger)
        {
            _seriesRepository = seriesRepository;
            _eventAggregator = eventAggregator;
            _logger = logger;
        }

        public MangaSeries GetSeries(int seriesId)
        {
            return _seriesRepository.Get(seriesId);
        }

        public List<MangaSeries> GetAllSeries()
        {
            return _seriesRepository.All();
        }

        public MangaSeries AddSeries(MangaSeries newSeries)
        {
            var series = _seriesRepository.Insert(newSeries);
            _eventAggregator.PublishEvent(new MangaSeriesAddedEvent(series));
            return series;
        }

        public MangaSeries UpdateSeries(MangaSeries series)
        {
            var updated = _seriesRepository.Update(series);
            _eventAggregator.PublishEvent(new MangaSeriesUpdatedEvent(updated));
            return updated;
        }

        public void DeleteSeries(int seriesId, bool deleteFiles)
        {
            var series = GetSeries(seriesId);
            _seriesRepository.Delete(seriesId);
            _eventAggregator.PublishEvent(new MangaSeriesDeletedEvent(series, deleteFiles));
        }
    }
}
