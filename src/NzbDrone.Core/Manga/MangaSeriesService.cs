using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Threading.Tasks;
using NLog;
using NzbDrone.Core.Books.Events;
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

        /// <summary>
        /// Fetches volumes/chapters for a series and stores them. If <paramref name="volumeMap"/>
        /// is supplied (e.g. already fetched by the caller for the same ForeignMangaId), it is
        /// reused instead of issuing a duplicate MangaDex request.
        /// </summary>
        Task FetchAndStoreVolumesAsync(MangaSeries series, VolumeChapterMap volumeMap = null);
    }

    public class MangaSeriesService : IMangaSeriesService, IHandle<AuthorDeletedEvent>
    {
        private readonly IMangaSeriesRepository _seriesRepository;
        private readonly IMangaMetadataRepository _metadataRepository;
        private readonly IVolumeRepository _volumeRepository;
        private readonly IChapterRepository _chapterRepository;
        private readonly IMangaFileService _mangaFileService;
        private readonly IMangaMetadataConnector _metadataConnector;
        private readonly ISeriesMetadataGenerator _metadataGenerator;
        private readonly IEventAggregator _eventAggregator;
        private readonly Logger _logger;

        public MangaSeriesService(
            IMangaSeriesRepository seriesRepository,
            IMangaMetadataRepository metadataRepository,
            IVolumeRepository volumeRepository,
            IChapterRepository chapterRepository,
            IMangaFileService mangaFileService,
            IMangaMetadataConnector metadataConnector,
            ISeriesMetadataGenerator metadataGenerator,
            IEventAggregator eventAggregator,
            Logger logger)
        {
            _seriesRepository = seriesRepository;
            _metadataRepository = metadataRepository;
            _volumeRepository = volumeRepository;
            _chapterRepository = chapterRepository;
            _mangaFileService = mangaFileService;
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

            // Persist the MangaMetadata record first so we get a valid Id. ForeignMangaId is
            // uniquely indexed, so two concurrent Add-Manga requests for the same manga can race
            // between the caller's "does it already exist" check and this insert. If we lose that
            // race, fall back to the row the other request just committed instead of surfacing a
            // constraint violation to the caller.
            var metadata = newSeries.Metadata?.Value;
            if (metadata != null && !string.IsNullOrEmpty(metadata.ForeignMangaId))
            {
                try
                {
                    metadata = _metadataRepository.Insert(metadata);
                }
                catch (SQLiteException)
                {
                    var existingMetadata = _metadataRepository.FindByForeignMangaId(metadata.ForeignMangaId);
                    if (existingMetadata == null)
                    {
                        throw;
                    }

                    metadata = existingMetadata;
                }

                newSeries.MangaMetadataId = metadata.Id;
                newSeries.Metadata = metadata;

                // MangaSeries.MangaMetadataId has no unique constraint, so re-check right before
                // inserting to shrink (though not eliminate) the same race window for the series row.
                var existingSeries = _seriesRepository.FindByMangaMetadataId(metadata.Id);
                if (existingSeries != null)
                {
                    return existingSeries;
                }
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
            if (series == null)
            {
                return;
            }

            RemoveMirroredSeries(series);

            _eventAggregator.PublishEvent(new MangaSeriesDeletedEvent(series, deleteFiles));
        }

        public void Handle(AuthorDeletedEvent message)
        {
            // The manga-native tables (MangaMetadata/MangaSeries/Volume/Chapter/MangaFile) are a
            // mirror of the Author/Book data used by the manga search/download pipeline. When the
            // author is deleted these mirrored records must be cleaned up as well, otherwise they
            // become orphaned and re-adding the same manga later fails/reuses stale data because
            // MangaMetadata.ForeignMangaId is unique.
            var foreignMangaId = message.Author?.Metadata?.Value?.ForeignAuthorId;
            if (string.IsNullOrEmpty(foreignMangaId))
            {
                return;
            }

            var metadata = _metadataRepository.FindByForeignMangaId(foreignMangaId);
            if (metadata == null)
            {
                return;
            }

            var series = _seriesRepository.FindByMangaMetadataId(metadata.Id);

            if (series != null)
            {
                RemoveMirroredSeries(series);
                _eventAggregator.PublishEvent(new MangaSeriesDeletedEvent(series, message.DeleteFiles));
            }
            else
            {
                // No MangaSeries row (partial mirror) - still remove the orphaned metadata so the
                // unique ForeignMangaId index doesn't block re-adding this manga later.
                _metadataRepository.Delete(metadata.Id);
            }
        }

        private void RemoveMirroredSeries(MangaSeries series)
        {
            var volumes = _volumeRepository.FindByMangaSeriesId(series.Id);

            foreach (var volume in volumes)
            {
                var chapters = _chapterRepository.FindByVolumeId(volume.Id);
                if (chapters.Count > 0)
                {
                    _chapterRepository.DeleteMany(chapters);
                }
            }

            _mangaFileService.DeleteBySeries(series.Id);

            if (volumes.Count > 0)
            {
                _volumeRepository.DeleteMany(volumes);
            }

            _seriesRepository.Delete(series.Id);
            _metadataRepository.Delete(series.MangaMetadataId);
        }

        public async Task FetchAndStoreVolumesAsync(MangaSeries series, VolumeChapterMap volumeMap = null)
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
                // Reuse a caller-supplied volume/chapter map (e.g. already fetched while adding the
                // manga) instead of issuing a duplicate MangaDex request for the same data.
                if (volumeMap == null)
                {
                    volumeMap = await _metadataConnector.GetVolumeChapterMapAsync(foreignMangaId);
                }

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
                        MangaMetadataId = series.MangaMetadataId,
                        MangaSeriesId = series.Id
                    };

                    volume.MangaSeries = new MangaSeries { Id = series.Id };
                    volume.MangaMetadata = new MangaMetadata { Id = series.MangaMetadataId };

                    // ForeignVolumeId is uniquely indexed - a concurrent fetch for the same series
                    // (e.g. a duplicate Add-Manga request) can race here. Fall back to the existing
                    // row rather than failing the whole import.
                    try
                    {
                        volume = _volumeRepository.Insert(volume);
                    }
                    catch (SQLiteException)
                    {
                        var existingVolume = _volumeRepository.FindByForeignVolumeId(volume.ForeignVolumeId);
                        if (existingVolume == null)
                        {
                            throw;
                        }

                        _logger.Debug("Volume {0} already exists for series {1}, reusing existing record", volume.ForeignVolumeId, series.Name);
                        volume = existingVolume;
                    }

                    volumeCount++;

                    // Fetch and store chapters for this volume
                    try
                    {
                        var chapters = await _metadataConnector.GetChaptersForVolumeAsync(foreignMangaId, volumeNumber);
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
