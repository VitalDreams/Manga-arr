using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Manga.Repositories;

namespace NzbDrone.Core.Manga.PostProcessing
{
    /// <summary>
    /// Handles post-processing of downloaded manga:
    /// 1. Move CBZ to library folder
    /// 2. Apply naming template
    /// 3. Update database
    /// 4. Trigger Komga scan
    /// </summary>
    public interface IPostProcessingService
    {
        Task<PostProcessingResult> ProcessAsync(string filePath, int mangaSeriesId, int volumeNumber);
        Task<PostProcessingResult> ProcessDownloadAsync(string downloadDir, string mangaDexId, int volumeNumber);
    }

    public class PostProcessingService : IPostProcessingService
    {
        private readonly IMangaSeriesRepository _seriesRepository;
        private readonly IVolumeRepository _volumeRepository;
        private readonly IMangaFileRepository _fileRepository;
        private readonly IKomgaIntegration _komga;
        private readonly IDiskProvider _diskProvider;
        private readonly Logger _logger;

        public PostProcessingService(
            IMangaSeriesRepository seriesRepository,
            IVolumeRepository volumeRepository,
            IMangaFileRepository fileRepository,
            IKomgaIntegration komga,
            IDiskProvider diskProvider,
            Logger logger)
        {
            _seriesRepository = seriesRepository;
            _volumeRepository = volumeRepository;
            _fileRepository = fileRepository;
            _komga = komga;
            _diskProvider = diskProvider;
            _logger = logger;
        }

        /// <summary>
        /// Process a single CBZ file
        /// </summary>
        public async Task<PostProcessingResult> ProcessAsync(string filePath, int mangaSeriesId, int volumeNumber)
        {
            _logger.Info($"Post-processing: {filePath} for series {mangaSeriesId}, volume {volumeNumber}");

            try
            {
                // Get series info
                var series = await _seriesRepository.GetByIdAsync(mangaSeriesId);
                if (series == null)
                {
                    return new PostProcessingResult
                    {
                        Status = "failed",
                        Message = $"Series {mangaSeriesId} not found"
                    };
                }

                // Get or create volume
                var volume = (await _volumeRepository.GetByMangaSeriesIdAsync(mangaSeriesId))
                    .FirstOrDefault(v => v.VolumeNumber == volumeNumber);

                if (volume == null)
                {
                    volume = new Volume
                    {
                        MangaSeriesId = mangaSeriesId,
                        VolumeNumber = volumeNumber,
                        Title = $"{series.Name} Vol. {volumeNumber:000}",
                        ForeignVolumeId = $"local-{mangaSeriesId}-{volumeNumber}",
                        Monitored = true,
                        Added = DateTime.UtcNow
                    };
                    await _volumeRepository.AddAsync(volume);
                }

                // Generate target path using naming template
                var targetPath = GetTargetPath(series, volumeNumber, filePath);

                // Ensure directory exists
                var targetDir = Path.GetDirectoryName(targetPath);
                _diskProvider.EnsureFolder(targetDir);

                // Move file
                if (_diskProvider.FileExists(targetPath))
                {
                    _logger.Warn($"File already exists at {targetPath}, overwriting");
                    _diskProvider.DeleteFile(targetPath);
                }

                _diskProvider.MoveFile(filePath, targetPath);
                _logger.Info($"Moved CBZ to {targetPath}");

                // Create manga file record
                var mangaFile = new MangaFile
                {
                    VolumeId = volume.Id,
                    MangaSeriesId = mangaSeriesId,
                    Path = targetPath,
                    FileName = Path.GetFileName(targetPath),
                    RelativePath = Path.GetRelativePath(series.RootFolderPath, targetPath),
                    Size = new FileInfo(targetPath).Length,
                    AddedAt = DateTime.UtcNow
                };

                await _fileRepository.AddAsync(mangaFile);

                // Trigger Komga scan
                await _komga.TriggerLibraryScanAsync();

                return new PostProcessingResult
                {
                    Status = "completed",
                    Message = $"Successfully processed {Path.GetFileName(targetPath)}",
                    FilePath = targetPath,
                    VolumeId = volume.Id
                };
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to post-process {filePath}");
                return new PostProcessingResult
                {
                    Status = "failed",
                    Message = ex.Message
                };
            }
        }

        /// <summary>
        /// Process all CBZ files in a download directory
        /// </summary>
        public async Task<PostProcessingResult> ProcessDownloadAsync(string downloadDir, string mangaDexId, int volumeNumber)
        {
            _logger.Info($"Post-processing download directory: {downloadDir}");

            var series = await _seriesRepository.GetByForeignIdAsync(mangaDexId);
            if (series == null)
            {
                return new PostProcessingResult
                {
                    Status = "failed",
                    Message = $"Series with MangaDex ID {mangaDexId} not found"
                };
            }

            // Find CBZ files in download directory
            var cbzFiles = Directory.GetFiles(downloadDir, "*.cbz", SearchOption.AllDirectories);

            if (!cbzFiles.Any())
            {
                return new PostProcessingResult
                {
                    Status = "failed",
                    Message = "No CBZ files found in download directory"
                };
            }

            // Process each CBZ file
            var results = new System.Collections.Generic.List<PostProcessingResult>();

            foreach (var cbzFile in cbzFiles)
            {
                var result = await ProcessAsync(cbzFile, series.Id, volumeNumber);
                results.Add(result);
            }

            var successful = results.Count(r => r.Status == "completed");
            var failed = results.Count(r => r.Status == "failed");

            return new PostProcessingResult
            {
                Status = failed == 0 ? "completed" : "partial",
                Message = $"Processed {successful} files, {failed} failed",
                FilePath = results.FirstOrDefault()?.FilePath
            };
        }

        private string GetTargetPath(MangaSeries series, int volumeNumber, string sourcePath)
        {
            // Naming template: {Series} - Vol.{Number}.cbz
            var fileName = $"{SanitizeFileName(series.Name)} - Vol.{volumeNumber:000}.cbz";
            return Path.Combine(series.Path, fileName);
        }

        private string SanitizeFileName(string name)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            return new string(name.Select(c => invalidChars.Contains(c) ? '_' : c).ToArray());
        }
    }

    public class PostProcessingResult
    {
        public string Status { get; set; } // completed, failed, partial
        public string Message { get; set; }
        public string FilePath { get; set; }
        public int? VolumeId { get; set; }
    }
}
