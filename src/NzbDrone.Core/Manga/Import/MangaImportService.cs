using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Books;
using NzbDrone.Core.Manga.Connectors;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Manga.Import
{
    public enum MangaImportMode
    {
        InPlace,
        Move
    }

    public interface IMangaImportService
    {
        MangaSeries ImportSeries(string directoryPath, string foreignMangaId, MangaImportMode importMode = MangaImportMode.InPlace);
        MangaFile ImportFile(string filePath, int seriesId, int volumeId);
        Task<AutoImportResult> AutoImportFilesAsync(List<string> scanDirectories);
    }

    public class AutoImportResult
    {
        public int FilesScanned { get; set; }
        public int FilesMatched { get; set; }
        public int FilesImported { get; set; }
        public int FilesMoved { get; set; }
        public List<string> ImportedFiles { get; set; } = new();
        public List<string> Errors { get; set; } = new();
    }

    public class MangaImportService : IMangaImportService
    {
        private readonly IMangaFileScanner _fileScanner;
        private readonly IMangaMetadataConnector _metadataConnector;
        private readonly IMangaSeriesService _seriesService;
        private readonly IMangaSeriesRepository _seriesRepository;
        private readonly IVolumeRepository _volumeRepository;
        private readonly IMangaFileService _fileService;
        private readonly ISeriesMetadataGenerator _metadataGenerator;
        private readonly IMangaNamingService _namingService;
        private readonly IDiskProvider _diskProvider;
        private readonly IEventAggregator _eventAggregator;
        private readonly Logger _logger;

        public MangaImportService(
            IMangaFileScanner fileScanner,
            IMangaMetadataConnector metadataConnector,
            IMangaSeriesService seriesService,
            IMangaSeriesRepository seriesRepository,
            IVolumeRepository volumeRepository,
            IMangaFileService fileService,
            ISeriesMetadataGenerator metadataGenerator,
            IMangaNamingService namingService,
            IDiskProvider diskProvider,
            IEventAggregator eventAggregator,
            Logger logger)
        {
            _fileScanner = fileScanner;
            _metadataConnector = metadataConnector;
            _seriesService = seriesService;
            _seriesRepository = seriesRepository;
            _volumeRepository = volumeRepository;
            _fileService = fileService;
            _metadataGenerator = metadataGenerator;
            _namingService = namingService;
            _diskProvider = diskProvider;
            _eventAggregator = eventAggregator;
            _logger = logger;
        }

        public MangaSeries ImportSeries(string directoryPath, string foreignMangaId, MangaImportMode importMode = MangaImportMode.InPlace)
        {
            _logger.Info("Importing manga series from {0} with MangaDex ID {1} (mode: {2})", directoryPath, foreignMangaId, importMode);

            // 1. Scan directory for CBZ files
            var scannedFiles = _fileScanner.ScanDirectory(directoryPath);
            if (scannedFiles.Count == 0)
            {
                _logger.Warn("No CBZ files found in {0}", directoryPath);
                return null;
            }

            // 2. Fetch metadata from MangaDex
            var metadata = FetchMetadata(foreignMangaId);
            if (metadata == null)
            {
                _logger.Error("Failed to fetch metadata for MangaDex ID {0}", foreignMangaId);
                return null;
            }

            // 3. Create MangaSeries + MangaMetadata in DB
            var series = CreateSeries(directoryPath, foreignMangaId, metadata);

            // 4. Create volume and file records
            var volumeGroups = scannedFiles
                .Where(f => f.VolumeNumber.HasValue)
                .GroupBy(f => f.VolumeNumber.Value)
                .OrderBy(g => g.Key)
                .ToList();

            foreach (var volumeGroup in volumeGroups)
            {
                var volumeNumber = volumeGroup.Key;
                var volume = CreateVolume(series, metadata, volumeNumber);

                foreach (var scannedFile in volumeGroup)
                {
                    CreateMangaFile(scannedFile, series, volume, importMode);
                }
            }

            // Handle files with no volume number
            var ungroupedFiles = scannedFiles.Where(f => !f.VolumeNumber.HasValue).ToList();
            if (ungroupedFiles.Count > 0)
            {
                _logger.Warn("{0} files could not be assigned to a volume (no volume number parsed)", ungroupedFiles.Count);
            }

            // 5. Write series.json companion file
            _metadataGenerator.WriteSeriesMetadataFile(series);

            // 6. Publish MangaSeriesAddedEvent
            _eventAggregator.PublishEvent(new MangaSeriesAddedEvent(series));

            _logger.Info("Successfully imported manga series '{0}' with {1} volumes from {2}", series.Name, volumeGroups.Count, directoryPath);
            return series;
        }

        public MangaFile ImportFile(string filePath, int seriesId, int volumeId)
        {
            _logger.Info("Importing single file {0} into series {1}, volume {2}", filePath, seriesId, volumeId);

            if (!_diskProvider.FileExists(filePath))
            {
                _logger.Error("File does not exist: {0}", filePath);
                return null;
            }

            var scannedFiles = _fileScanner.ScanDirectory(Path.GetDirectoryName(filePath));
            var scanned = scannedFiles.FirstOrDefault(f => f.FilePath == filePath);

            if (scanned == null)
            {
                // Fallback: create a basic scanned entry
                var fileInfo = _diskProvider.GetFileInfo(filePath);
                scanned = new ScannedMangaFile
                {
                    FileName = fileInfo.Name,
                    FilePath = filePath,
                    FileSize = fileInfo.Length
                };
            }

            var mangaFile = new MangaFile
            {
                VolumeId = volumeId,
                MangaSeriesId = seriesId,
                Path = filePath,
                FileName = scanned.FileName,
                RelativePath = scanned.FileName,
                Size = scanned.FileSize,
                AddedAt = DateTime.UtcNow,
                IsVolumePack = false
            };

            _fileService.Add(mangaFile);
            _logger.Info("Imported file {0} into series {1}, volume {2}", scanned.FileName, seriesId, volumeId);
            return mangaFile;
        }

        public Task<AutoImportResult> AutoImportFilesAsync(List<string> scanDirectories)
        {
            var result = new AutoImportResult();
            var allSeries = _seriesService.GetAllSeries();

            if (allSeries.Count == 0)
            {
                _logger.Warn("No series in library, nothing to auto-import");
                return Task.FromResult(result);
            }

            // Build clean name lookup for matching
            var seriesLookup = allSeries
                .Where(s => !string.IsNullOrEmpty(s.CleanName))
                .ToDictionary(s => s.CleanName, s => s, StringComparer.OrdinalIgnoreCase);

            foreach (var scanDir in scanDirectories)
            {
                if (!_diskProvider.FolderExists(scanDir))
                {
                    _logger.Debug("Scan directory does not exist, skipping: {0}", scanDir);
                    continue;
                }

                var scannedFiles = _fileScanner.ScanDirectory(scanDir);
                result.FilesScanned += scannedFiles.Count;

                foreach (var scanned in scannedFiles)
                {
                    try
                    {
                        // Match file to an existing series by clean name
                        var cleanFileName = scanned.SeriesName?.ToLowerInvariant().Replace(" ", "") ?? string.Empty;

                        if (!seriesLookup.TryGetValue(cleanFileName, out var matchedSeries))
                        {
                            _logger.Debug("No library match for file '{0}' (parsed series: '{1}')", scanned.FileName, scanned.SeriesName);
                            continue;
                        }

                        result.FilesMatched++;

                        if (!scanned.VolumeNumber.HasValue)
                        {
                            _logger.Warn("Cannot import '{0}': no volume number parsed", scanned.FileName);
                            result.Errors.Add($"No volume number parsed from '{scanned.FileName}'");
                            continue;
                        }

                        // Find or create the volume record
                        var volume = FindOrCreateVolume(matchedSeries, scanned.VolumeNumber.Value);

                        // Determine target path: move to /manga/{SeriesName}/
                        var targetPath = scanned.FilePath;
                        var isDownloadDir = scanDir.StartsWith("/downloads", StringComparison.OrdinalIgnoreCase) ||
                                            scanDir.Contains("downloads");

                        if (isDownloadDir)
                        {
                            var seriesFolder = _namingService.GetSeriesFolder(matchedSeries);
                            var targetDir = Path.Combine(matchedSeries.RootFolderPath ?? "/manga", seriesFolder);

                            if (!_diskProvider.FolderExists(targetDir))
                            {
                                _diskProvider.CreateFolder(targetDir);
                            }

                            targetPath = Path.Combine(targetDir, scanned.FileName);

                            if (!_diskProvider.FileExists(targetPath))
                            {
                                _diskProvider.MoveFile(scanned.FilePath, targetPath);
                                result.FilesMoved++;
                                _logger.Info("Moved '{0}' -> '{1}'", scanned.FilePath, targetPath);
                            }
                            else
                            {
                                _logger.Info("File already exists at target, importing in-place: {0}", targetPath);
                            }
                        }

                        // Create MangaFile record
                        var mangaFile = new MangaFile
                        {
                            VolumeId = volume.Id,
                            MangaSeriesId = matchedSeries.Id,
                            Path = targetPath,
                            FileName = scanned.FileName,
                            RelativePath = Path.GetFileName(targetPath),
                            Size = scanned.FileSize,
                            AddedAt = DateTime.UtcNow,
                            IsVolumePack = true
                        };

                        _fileService.Add(mangaFile);
                        result.FilesImported++;
                        result.ImportedFiles.Add(scanned.FileName);
                        _logger.Info("Imported '{0}' into {1} volume {2}", scanned.FileName, matchedSeries.Name, scanned.VolumeNumber.Value);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Failed to auto-import file '{0}'", scanned.FileName);
                        result.Errors.Add($"Error importing '{scanned.FileName}': {ex.Message}");
                    }
                }
            }

            _logger.Info("Auto-import complete: {0} scanned, {1} matched, {2} imported, {3} moved",
                result.FilesScanned, result.FilesMatched, result.FilesImported, result.FilesMoved);

            return Task.FromResult(result);
        }

        private Volume FindOrCreateVolume(MangaSeries series, int volumeNumber)
        {
            var existing = _volumeRepository.All()
                .FirstOrDefault(v => v.MangaMetadataId == series.MangaMetadataId && v.VolumeNumber == volumeNumber);

            if (existing != null)
            {
                return existing;
            }

            var volume = new Volume
            {
                ForeignVolumeId = $"{series.ForeignMangaId}_vol{volumeNumber}",
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

            volume.MangaMetadata = new MangaMetadata { Id = series.MangaMetadataId };

            return _volumeRepository.Insert(volume);
        }

        private MangaMetadata FetchMetadata(string foreignMangaId)
        {
            try
            {
                return _metadataConnector.GetMangaMetadataAsync(foreignMangaId).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error fetching metadata from MangaDex for {0}", foreignMangaId);
                return null;
            }
        }

        private MangaSeries CreateSeries(string directoryPath, string foreignMangaId, MangaMetadata metadata)
        {
            var series = new MangaSeries
            {
                ForeignMangaId = foreignMangaId,
                Path = directoryPath,
                RootFolderPath = Path.GetDirectoryName(directoryPath),
                CleanName = metadata.Title?.ToLowerInvariant().Replace(" ", "") ?? string.Empty,
                Monitored = true,
                MonitorNewItems = NewItemMonitorTypes.All,
                DownloadMode = DownloadMode.VolumePack,
                Added = DateTime.UtcNow,
                QualityProfileId = 1,
                MetadataProfileId = 1
            };

            // Set metadata on the series
            series.Metadata = new MangaMetadata
            {
                ForeignMangaId = foreignMangaId,
                Title = metadata.Title,
                TitleSlug = metadata.TitleSlug,
                SortTitle = metadata.SortTitle,
                Description = metadata.Description,
                Author = metadata.Author,
                Artist = metadata.Artist,
                Publisher = metadata.Publisher,
                OriginalLanguage = metadata.OriginalLanguage,
                Demographic = metadata.Demographic,
                Status = metadata.Status,
                ContentRating = metadata.ContentRating,
                Year = metadata.Year,
                TotalVolumes = metadata.TotalVolumes,
                TotalChapters = metadata.TotalChapters,
                Genres = metadata.Genres,
                Tags = metadata.Tags,
                Links = metadata.Links,
                AlternateTitles = metadata.AlternateTitles,
                Ratings = metadata.Ratings,
                CoverUrl = metadata.CoverUrl,
                LastInfoSync = DateTime.UtcNow
            };

            return _seriesRepository.Insert(series);
        }

        private Volume CreateVolume(MangaSeries series, MangaMetadata metadata, int volumeNumber)
        {
            var volume = new Volume
            {
                ForeignVolumeId = $"{metadata.ForeignMangaId}_vol{volumeNumber}",
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

            return _volumeRepository.Insert(volume);
        }

        private void CreateMangaFile(ScannedMangaFile scanned, MangaSeries series, Volume volume, MangaImportMode importMode)
        {
            var targetPath = scanned.FilePath;

            if (importMode == MangaImportMode.Move)
            {
                targetPath = GetManagedPath(series, scanned);
                MoveFile(scanned.FilePath, targetPath);
            }

            var mangaFile = new MangaFile
            {
                VolumeId = volume.Id,
                MangaSeriesId = series.Id,
                Path = targetPath,
                FileName = scanned.FileName,
                RelativePath = Path.GetFileName(targetPath),
                Size = scanned.FileSize,
                AddedAt = DateTime.UtcNow,
                IsVolumePack = true
            };

            _fileService.Add(mangaFile);
        }

        private string GetManagedPath(MangaSeries series, ScannedMangaFile scanned)
        {
            var seriesFolder = _namingService.GetSeriesFolder(series);
            var targetDir = Path.Combine(series.RootFolderPath, seriesFolder);

            if (!_diskProvider.FolderExists(targetDir))
            {
                _diskProvider.CreateFolder(targetDir);
            }

            return Path.Combine(targetDir, scanned.FileName);
        }

        private void MoveFile(string source, string destination)
        {
            try
            {
                if (_diskProvider.FileExists(destination))
                {
                    _logger.Warn("Destination file already exists, skipping move: {0}", destination);
                    return;
                }

                _diskProvider.MoveFile(source, destination);
                _logger.Debug("Moved file from {0} to {1}", source, destination);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to move file from {0} to {1}", source, destination);
            }
        }
    }
}
