using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Download;
using NzbDrone.Core.Manga.Connectors;

namespace NzbDrone.Core.Manga.Download
{
    /// <summary>
    /// Background service that monitors download clients for completed manga downloads.
    /// Processes completed downloads: extracts/CBZ-ifies files, moves to manga library,
    /// updates MangaFile records, and triggers Komga library scan.
    /// </summary>
    public class MangaDownloadCompletionHandler : BackgroundService
    {
        private readonly IProvideDownloadClient _downloadClientProvider;
        private readonly IMangaDownloadService _downloadService;
        private readonly ICbzCreator _cbzCreator;
        private readonly IMangaNamingService _namingService;
        private readonly IMangaFileService _mangaFileService;
        private readonly IVolumePackTracker _volumePackTracker;
        private readonly IKomgaIntegration _komga;
        private readonly IMangaMetadataConnector _metadataConnector;
        private readonly IDiskProvider _diskProvider;
        private readonly Logger _logger;

        // Track which downloads we've already processed to avoid duplicates
        private readonly HashSet<string> _processedDownloads = new HashSet<string>();
        private readonly object _processedLock = new object();

        public TimeSpan PollInterval { get; set; } = TimeSpan.FromMinutes(1);
        public bool Enabled { get; set; } = true;

        public MangaDownloadCompletionHandler(
            IProvideDownloadClient downloadClientProvider,
            IMangaDownloadService downloadService,
            ICbzCreator cbzCreator,
            IMangaNamingService namingService,
            IMangaFileService mangaFileService,
            IVolumePackTracker volumePackTracker,
            IKomgaIntegration komga,
            IMangaMetadataConnector metadataConnector,
            IDiskProvider diskProvider,
            Logger logger)
        {
            _downloadClientProvider = downloadClientProvider;
            _downloadService = downloadService;
            _cbzCreator = cbzCreator;
            _namingService = namingService;
            _mangaFileService = mangaFileService;
            _volumePackTracker = volumePackTracker;
            _komga = komga;
            _metadataConnector = metadataConnector;
            _diskProvider = diskProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.Info("Manga download completion handler started");

            while (!stoppingToken.IsCancellationRequested)
            {
                if (Enabled)
                {
                    try
                    {
                        await CheckForCompletedDownloadsAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Error checking for completed manga downloads");
                    }
                }

                await Task.Delay(PollInterval, stoppingToken);
            }

            _logger.Info("Manga download completion handler stopped");
        }

        /// <summary>
        /// Poll all download clients for completed manga items and process them
        /// </summary>
        private async Task CheckForCompletedDownloadsAsync()
        {
            var activeDownloads = await _downloadService.GetActiveDownloads();

            foreach (var download in activeDownloads)
            {
                if (download.Status != DownloadItemStatus.Completed)
                {
                    continue;
                }

                lock (_processedLock)
                {
                    if (_processedDownloads.Contains(download.DownloadId))
                    {
                        continue;
                    }
                }

                // Only process manga downloads (identified by our title pattern)
                if (!IsMangaDownload(download.Title))
                {
                    continue;
                }

                _logger.Info("Completed manga download detected: {0} (ID: {1})", download.Title, download.DownloadId);

                try
                {
                    await ProcessCompletedDownloadAsync(download);

                    lock (_processedLock)
                    {
                        _processedDownloads.Add(download.DownloadId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to process completed download: {0}", download.Title);
                }
            }
        }

        /// <summary>
        /// Process a single completed manga download
        /// </summary>
        private async Task ProcessCompletedDownloadAsync(MangaDownloadStatus download)
        {
            _logger.Info("Processing completed manga download: {0}", download.Title);

            if (string.IsNullOrEmpty(download.OutputPath))
            {
                _logger.Warn("Download has no output path, cannot process: {0}", download.Title);
                return;
            }

            // Parse the manga title to extract series/volume info
            var parseResult = ParseMangaTitle(download.Title);

            if (parseResult == null)
            {
                _logger.Warn("Could not parse manga info from title: {0}", download.Title);
                return;
            }

            // Find the downloaded files
            var downloadedFiles = FindDownloadedFiles(download.OutputPath);

            if (!downloadedFiles.Any())
            {
                _logger.Warn("No manga files found in download output: {0}", download.OutputPath);
                return;
            }

            _logger.Info("Found {0} manga files in download", downloadedFiles.Count);

            // Process each file: move to library and update DB
            foreach (var filePath in downloadedFiles)
            {
                await ProcessDownloadedFileAsync(filePath, parseResult, download);
            }

            // Trigger Komga library scan
            await _komga.TriggerLibraryScanAsync();

            _logger.Info("Completed processing manga download: {0}", download.Title);
        }

        /// <summary>
        /// Process a single downloaded file - move to library and create MangaFile record
        /// </summary>
        private async Task ProcessDownloadedFileAsync(
            string filePath,
            MangaTitleParseResult parseResult,
            MangaDownloadStatus download)
        {
            var fileName = Path.GetFileName(filePath);

            // If the file is already a CBZ, move it directly
            // If it's raw images or an archive, CBZ-ify it
            string finalCbzPath;

            if (fileName.EndsWith(".cbz", StringComparison.OrdinalIgnoreCase))
            {
                finalCbzPath = await MoveCbzToLibrary(filePath, parseResult);
            }
            else
            {
                finalCbzPath = await ConvertAndMoveToLibrary(filePath, parseResult);
            }

            if (finalCbzPath == null)
            {
                _logger.Warn("Failed to process file: {0}", filePath);
                return;
            }

            // Create MangaFile record
            var fileInfo = new FileInfo(finalCbzPath);
            var mangaFile = new MangaFile
            {
                VolumeId = parseResult.VolumeId,
                MangaSeriesId = parseResult.SeriesId,
                Path = finalCbzPath,
                FileName = Path.GetFileName(finalCbzPath),
                RelativePath = Path.GetFileName(finalCbzPath),
                Size = fileInfo.Exists ? fileInfo.Length : 0,
                AddedAt = DateTime.UtcNow,
                IsVolumePack = true
            };

            _mangaFileService.Add(mangaFile);
            _logger.Info("Created MangaFile record for: {0}", finalCbzPath);
        }

        /// <summary>
        /// Move a CBZ file to the manga library
        /// </summary>
        private async Task<string> MoveCbzToLibrary(string sourcePath, MangaTitleParseResult parseResult)
        {
            var targetDir = Path.Combine(parseResult.RootFolderPath, parseResult.SeriesFolder);
            _diskProvider.EnsureFolder(targetDir);

            var targetPath = Path.Combine(targetDir, Path.GetFileName(sourcePath));

            if (_diskProvider.FileExists(targetPath))
            {
                _logger.Warn("Target file already exists, skipping: {0}", targetPath);
                return targetPath;
            }

            try
            {
                _diskProvider.MoveFile(sourcePath, targetPath);
                _logger.Info("Moved CBZ to library: {0}", targetPath);
                return targetPath;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to move CBZ from {0} to {1}", sourcePath, targetPath);
                return null;
            }
        }

        /// <summary>
        /// Convert downloaded files to CBZ and move to library
        /// </summary>
        private async Task<string> ConvertAndMoveToLibrary(string sourcePath, MangaTitleParseResult parseResult)
        {
            var targetDir = Path.Combine(parseResult.RootFolderPath, parseResult.SeriesFolder);
            _diskProvider.EnsureFolder(targetDir);

            // Collect image files from the download
            var imageFiles = CollectImageFiles(sourcePath);

            if (!imageFiles.Any())
            {
                _logger.Warn("No image files found in: {0}", sourcePath);
                return null;
            }

            // Create a temporary chapter for CBZ creation
            var chapter = new Chapter
            {
                ChapterNumber = parseResult.ChapterNumber ?? 0,
                Language = "en"
            };

            var volume = new Volume
            {
                VolumeNumber = parseResult.VolumeNumber,
                Title = $"Volume {parseResult.VolumeNumber}"
            };

            var series = new MangaSeries
            {
                Name = parseResult.SeriesTitle,
                ForeignMangaId = parseResult.ForeignMangaId ?? "unknown"
            };

            try
            {
                var cbzPath = await _cbzCreator.CreateCbzFromChapterAsync(
                    targetDir, series, volume, chapter, imageFiles);

                _logger.Info("Created CBZ from downloaded files: {0}", cbzPath);
                return cbzPath;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to create CBZ from: {0}", sourcePath);
                return null;
            }
        }

        /// <summary>
        /// Collect image files from a directory or archive
        /// </summary>
        private List<string> CollectImageFiles(string path)
        {
            var imageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".tiff", ".tif"
            };

            var images = new List<string>();

            if (_diskProvider.FolderExists(path))
            {
                // Scan directory for images
                var files = _diskProvider.GetFiles(path, SearchOption.AllDirectories);
                images = files.Where(f => imageExtensions.Contains(Path.GetExtension(f)))
                              .OrderBy(f => f)
                              .ToList();
            }
            else if (_diskProvider.FileExists(path))
            {
                // Single file - check if it's an image
                if (imageExtensions.Contains(Path.GetExtension(path)))
                {
                    images.Add(path);
                }
            }

            return images;
        }

        /// <summary>
        /// Find downloaded files in the download client output path
        /// </summary>
        private List<string> FindDownloadedFiles(string outputPath)
        {
            var mangaExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".cbz", ".cbr", ".cb7", ".pdf"
            };

            var imageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp"
            };

            var files = new List<string>();

            if (_diskProvider.FolderExists(outputPath))
            {
                var allFiles = _diskProvider.GetFiles(outputPath, SearchOption.AllDirectories);

                // Prioritize CBZ/archive files
                var archiveFiles = allFiles.Where(f => mangaExtensions.Contains(Path.GetExtension(f))).ToList();

                if (archiveFiles.Any())
                {
                    files = archiveFiles;
                }
                else
                {
                    // If no archives, collect image files
                    files = allFiles.Where(f => imageExtensions.Contains(Path.GetExtension(f))).ToList();
                }
            }
            else if (_diskProvider.FileExists(outputPath))
            {
                files.Add(outputPath);
            }

            return files;
        }

        /// <summary>
        /// Check if a download title indicates a manga download
        /// </summary>
        private bool IsMangaDownload(string title)
        {
            if (string.IsNullOrEmpty(title))
            {
                return false;
            }

            // Manga titles typically contain volume/chapter indicators
            var lowerTitle = title.ToLowerInvariant();
            return lowerTitle.Contains("vol") ||
                   lowerTitle.Contains("manga") ||
                   lowerTitle.Contains("cbz") ||
                   lowerTitle.Contains("cbr") ||
                   lowerTitle.Contains("chapter") ||
                   lowerTitle.Contains("ch.");
        }

        /// <summary>
        /// Parse manga title to extract series, volume, and chapter info
        /// </summary>
        private MangaTitleParseResult ParseMangaTitle(string title)
        {
            if (string.IsNullOrEmpty(title))
            {
                return null;
            }

            var result = new MangaTitleParseResult
            {
                RawTitle = title
            };

            // Try to extract volume number
            var volMatch = System.Text.RegularExpressions.Regex.Match(
                title, @"[Vv]ol(?:ume)?\.?\s*(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (volMatch.Success)
            {
                result.VolumeNumber = int.Parse(volMatch.Groups[1].Value);
            }

            // Try to extract chapter number
            var chMatch = System.Text.RegularExpressions.Regex.Match(
                title, @"[Cc]h(?:apter)?\.?\s*(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (chMatch.Success)
            {
                result.ChapterNumber = int.Parse(chMatch.Groups[1].Value);
            }

            // Extract series title (everything before vol/ch indicators)
            var seriesTitle = title;
            var volIndex = seriesTitle.IndexOf("Vol", StringComparison.OrdinalIgnoreCase);
            if (volIndex > 0)
            {
                seriesTitle = seriesTitle.Substring(0, volIndex).Trim();
            }
            var chIndex = seriesTitle.IndexOf("Ch", StringComparison.OrdinalIgnoreCase);
            if (chIndex > 0)
            {
                seriesTitle = seriesTitle.Substring(0, chIndex).Trim();
            }

            result.SeriesTitle = seriesTitle;
            result.SeriesFolder = _namingService.GetSeriesFolder(new MangaSeries { Name = seriesTitle });

            return result;
        }
    }

    /// <summary>
    /// Result of parsing a manga download title
    /// </summary>
    public class MangaTitleParseResult
    {
        public string RawTitle { get; set; }
        public string SeriesTitle { get; set; }
        public string SeriesFolder { get; set; }
        public int VolumeNumber { get; set; }
        public int? ChapterNumber { get; set; }
        public int SeriesId { get; set; }
        public int VolumeId { get; set; }
        public string RootFolderPath { get; set; }
        public string ForeignMangaId { get; set; }
    }
}
