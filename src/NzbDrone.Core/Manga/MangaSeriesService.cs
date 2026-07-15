using System.Collections.Generic;
using System.Linq;
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
        private readonly ISeriesMetadataGenerator _metadataGenerator;
        private readonly IEventAggregator _eventAggregator;
        private readonly Logger _logger;

        public MangaSeriesService(
            IMangaSeriesRepository seriesRepository,
            ISeriesMetadataGenerator metadataGenerator,
            IEventAggregator eventAggregator,
            Logger logger)
        {
            _seriesRepository = seriesRepository;
            _metadataGenerator = metadataGenerator;
            _eventAggregator = eventAggregator;
            _logger = logger;
        }

        public MangaSeries GetSeries(int seriesId)
        {
            return _seriesRepository.Get(seriesId);
        }

        public List<MangaSeries> GetAllSeries()
        {
            return _seriesRepository.All().ToList();
        }

        public MangaSeries AddSeries(MangaSeries newSeries)
        {
            var series = _seriesRepository.Insert(newSeries);
            _eventAggregator.PublishEvent(new MangaSeriesAddedEvent(series));

            _metadataGenerator.WriteSeriesMetadataFile(series);

            return series;
        }

        public MangaSeries UpdateSeries(MangaSeries series)
        {
            var updated = _seriesRepository.Update(series);
            _eventAggregator.PublishEvent(new MangaSeriesUpdatedEvent(updated));

            _metadataGenerator.WriteSeriesMetadataFile(updated);

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
