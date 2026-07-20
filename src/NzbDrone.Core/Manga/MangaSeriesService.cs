using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Core.Manga.Connectors;
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
        void FetchAndStoreVolumes(MangaSeries series);
    }

    public class MangaSeriesService : IMangaSeriesService
    {
        private readonly IMangaSeriesRepository _seriesRepository;
        private readonly IMangaMetadataRepository _metadataRepository;
        private readonly IVolumeRepository _volumeRepository;
        private readonly IChapterRepository _chapterRepository;
        private readonly IMangaMetadataConnector _metadataConnector;
        private readonly ISeriesMetadataGenerator _metadataGenerator;
        private readonly IEventAggregator _eventAggregator;
        private readonly Logger _logger;

        public MangaSeriesService(
            IMangaSeriesRepository seriesRepository,
            IMangaMetadataRepository metadataRepository,
            IVolumeRepository volumeRepository,
            IChapterRepository chapterRepository,
            IMangaMetadataConnector metadataConnector,
            ISeriesMetadataGenerator metadataGenerator,
            IEventAggregator eventAggregator,
            Logger logger)
        {
            _seriesRepository = seriesRepository;
            _metadataRepository = metadataRepository;
            _volumeRepository = volumeRepository;
            _chapterRepository = chapterRepository;
            _metadataConnector = metadataConnector;
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
            if (string.IsNullOrEmpty(newSeries.CleanName))
            {
                newSeries.CleanName = (newSeries.Name ?? string.Empty).ToLowerInvariant().Replace(" ", string.Empty);
            }

            // Persist the MangaMetadata record first so we get a valid Id
            var metadata = newSeries.Metadata?.Value;
            if (metadata != null && !string.IsNullOrEmpty(metadata.ForeignMangaId))
            {
                metadata = _metadataRepository.Insert(metadata);
                newSeries.MangaMetadataId = metadata.Id;
                newSeries.Metadata = metadata;
            }

            var series = _seriesRepository.Insert(newSeries);
            _eventAggregator.PublishEvent(new MangaSeriesAddedEvent(series));

            _metadataGenerator.WriteSeriesMetadataFile(series);

            return series;
        }

        public MangaSeries UpdateSeries(MangaSeries series)
        {
            // Update the MangaMetadata record if present
            var metadata = series.Metadata?.Value;
            if (metadata != null && metadata.Id > 0)
            {
                _metadataRepository.Update(metadata);
            }
            else if (metadata != null && !string.IsNullOrEmpty(metadata.ForeignMangaId))
            {
                metadata = _metadataRepository.Insert(metadata);
                series.MangaMetadataId = metadata.Id;
                series.Metadata = metadata;
            }

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

        public void FetchAndStoreVolumes(MangaSeries series)
        {
            var foreignMangaId = series.ForeignMangaId;
            if (string.IsNullOrEmpty(foreignMangaId))
            {
                _logger.Warn("Cannot fetch volumes: series {0} has no ForeignMangaId", series.Id);
                return;
            }

            _logger.Info("Fetching volumes from MangaDex for series {0} (MangaDex ID: {1})", series.Name, foreignMangaId);

            try
            {
                // Get volume-to-chapter mapping from MangaDex
                var volumeMap = _metadataConnector.GetVolumeChapterMapAsync(foreignMangaId).GetAwaiter().GetResult();
                if (volumeMap?.VolumeChapters == null || volumeMap.VolumeChapters.Count == 0)
                {
                    _logger.Warn("No volumes found on MangaDex for {0}", foreignMangaId);
                    return;
                }

                var volumeCount = 0;
                var chapterCount = 0;

                foreach (var entry in volumeMap.VolumeChapters.OrderBy(v => v.Key))
                {
                    var volumeNumber = entry.Key;

                    // Create Volume record
                    var volume = new Volume
                    {
                        ForeignVolumeId = $"{foreignMangaId}_vol{volumeNumber}",
                        Title = $"Volume {volumeNumber}",
                        TitleSlug = $"volume-{volumeNumber}",
                        VolumeNumber = volumeNumber,
                        CleanTitle = $"volume {volumeNumber}",
                        Monitored = true,
                        AnyEditionOk = true,
                        Added = DateTime.UtcNow,
                        MangaMetadataId = series.MangaMetadataId
                    };

                    volume.MangaSeries = new MangaSeries { Id = series.Id };
                    volume.MangaMetadata = new MangaMetadata { Id = series.MangaMetadataId };

                    volume = _volumeRepository.Insert(volume);
                    volumeCount++;

                    // Fetch and store chapters for this volume
                    try
                    {
                        var chapters = _metadataConnector.GetChaptersForVolumeAsync(foreignMangaId, volumeNumber).GetAwaiter().GetResult();
                        if (chapters != null)
                        {
                            foreach (var chapterInfo in chapters)
                            {
                                var chapter = new Chapter
                                {
                                    VolumeId = volume.Id,
                                    ForeignChapterId = chapterInfo.ForeignChapterId,
                                    Title = chapterInfo.Title,
                                    ChapterNumber = chapterInfo.ChapterNumber,
                                    Language = chapterInfo.Language,
                                    ScanlationGroup = chapterInfo.ScanlationGroup,
                                    PageCount = chapterInfo.PageCount,
                                    ReleaseDate = chapterInfo.ReleaseDate,
                                    Monitored = true
                                };

                                _chapterRepository.Insert(chapter);
                                chapterCount++;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Failed to fetch chapters for volume {0} of series {1}", volumeNumber, series.Name);
                    }
                }

                _logger.Info("Stored {0} volumes and {1} chapters for series {2}", volumeCount, chapterCount, series.Name);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to fetch volumes from MangaDex for series {0} (MangaDex ID: {1})", series.Name, foreignMangaId);
            }
        }
    }
}
