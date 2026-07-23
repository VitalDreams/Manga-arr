using System.Linq;
using NzbDrone.Core.Manga;
using NzbDrone.Core.Parser;

namespace NzbDrone.Core.Housekeeping.Housekeepers
{
    public class UpdateCleanNameForMangaSeries : IHousekeepingTask
    {
        private readonly IMangaSeriesRepository _seriesRepository;
        private readonly IMangaMetadataRepository _metadataRepository;

        public UpdateCleanNameForMangaSeries(IMangaSeriesRepository seriesRepository, IMangaMetadataRepository metadataRepository)
        {
            _seriesRepository = seriesRepository;
            _metadataRepository = metadataRepository;
        }

        public void Clean()
        {
            var allSeries = _seriesRepository.All().ToList();

            foreach (var series in allSeries)
            {
                var metadata = series.MangaMetadataId > 0
                    ? _metadataRepository.Get(series.MangaMetadataId)
                    : null;

                var title = metadata?.Title ?? series.Name ?? string.Empty;
                var canonicalCleanName = title.CleanAuthorName();

                if (series.CleanName != canonicalCleanName)
                {
                    series.CleanName = canonicalCleanName;
                    _seriesRepository.Update(series);
                }
            }
        }
    }
}
