using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Manga.Connectors;

namespace NzbDrone.Core.Manga
{
    public interface IMangaDexDownloader
    {
        Task<string> DownloadVolumeAsync(string outputDir, MangaSeries series, Volume volume);
        Task<string> DownloadChapterAsync(string outputDir, MangaSeries series, Volume volume, Chapter chapter);
        Task<string> DownloadByModeAsync(string outputDir, MangaSeries series, Volume volume, DownloadMode mode);
    }

    public class MangaDexDownloader : IMangaDexDownloader
    {
        private readonly IMangaMetadataConnector _connector;
        private readonly ICbzCreator _cbzCreator;
        private readonly IMangaNamingService _namingService;
        private readonly IVolumePackTracker _volumePackTracker;
        private readonly IMangaFileService _mangaFileService;
        private readonly ISeriesMetadataGenerator _metadataGenerator;
        private readonly IStoryArcService _storyArcService;
        private static readonly HttpClient _httpClient = new HttpClient();
        private readonly IDiskProvider _diskProvider;
        private readonly Logger _logger;

        public MangaDexDownloader(
            IMangaMetadataConnector connector,
            ICbzCreator cbzCreator,
            IMangaNamingService namingService,
            IVolumePackTracker volumePackTracker,
            IMangaFileService mangaFileService,
            ISeriesMetadataGenerator metadataGenerator,
            IStoryArcService storyArcService,
            IDiskProvider diskProvider,
            Logger logger)
        {
            _connector = connector;
            _cbzCreator = cbzCreator;
            _namingService = namingService;
            _volumePackTracker = volumePackTracker;
            _mangaFileService = mangaFileService;
            _metadataGenerator = metadataGenerator;
            _storyArcService = storyArcService;
            _diskProvider = diskProvider;
            _logger = logger;
        }

        public async Task<string> DownloadByModeAsync(string outputDir, MangaSeries series, Volume volume, DownloadMode mode)
        {
            if (mode == DownloadMode.VolumePack)
            {
                return await DownloadVolumeAsync(outputDir, series, volume);
            }
            else
            {
                // Chapter mode - download each chapter individually
                var chapters = await _connector.GetChaptersForVolumeAsync(series.ForeignMangaId, volume.VolumeNumber);

                if (!chapters.Any())
                {
                    _logger.Warn($"No chapters found for volume {volume.VolumeNumber}");
                    return null;
                }

                var downloadedPaths = new List<string>();

                foreach (var chapterInfo in chapters)
                {
                    // Check if chapter is already covered by a volume pack
                    if (_volumePackTracker.IsChapterCoveredByPack(series.Id, chapterInfo.ChapterNumber))
                    {
                        _logger.Info($"Chapter {chapterInfo.ChapterNumber} is already covered by a volume pack, skipping");
                        continue;
                    }

                    var chapter = new Chapter
                    {
                        ForeignChapterId = chapterInfo.ForeignChapterId,
                        ChapterNumber = chapterInfo.ChapterNumber,
                        Language = chapterInfo.Language,
                        ScanlationGroup = chapterInfo.ScanlationGroup,
                        PageCount = chapterInfo.PageCount
                    };

                    var path = await DownloadChapterAsync(outputDir, series, volume, chapter);
                    if (path != null)
                    {
                        downloadedPaths.Add(path);
                    }
                }

                return downloadedPaths.Any() ? downloadedPaths.Last() : null;
            }
        }

        public async Task<string> DownloadVolumeAsync(string outputDir, MangaSeries series, Volume volume)
        {
            _logger.Info($"Downloading volume {volume.VolumeNumber} of {series.Name}...");

            var chapters = await _connector.GetChaptersForVolumeAsync(series.ForeignMangaId, volume.VolumeNumber);

            if (!chapters.Any())
            {
                _logger.Warn($"No chapters found for volume {volume.VolumeNumber}");
                return null;
            }

            _logger.Info($"Found {chapters.Count} chapters in volume {volume.VolumeNumber}");

            var chapterImages = new Dictionary<Chapter, List<string>>();
            var tempDir = Path.Combine(Path.GetTempPath(), "manga-arr", series.ForeignMangaId, $"vol-{volume.VolumeNumber}");

            try
            {
                foreach (var chapterInfo in chapters)
                {
                    _logger.Info($"Downloading chapter {chapterInfo.ChapterNumber} ({chapterInfo.PageCount} pages)...");

                    var pages = await _connector.GetChapterPagesAsync(chapterInfo.ForeignChapterId);
                    var imagePaths = await DownloadPagesAsync(tempDir, chapterInfo.ChapterNumber, pages);

                    if (imagePaths == null)
                    {
                        _logger.Error("Aborting volume {0} download: chapter {1} page downloads failed", volume.VolumeNumber, chapterInfo.ChapterNumber);
                        return null;
                    }

                    var chapter = new Chapter
                    {
                        ForeignChapterId = chapterInfo.ForeignChapterId,
                        ChapterNumber = chapterInfo.ChapterNumber,
                        Language = chapterInfo.Language,
                        ScanlationGroup = chapterInfo.ScanlationGroup,
                        PageCount = chapterInfo.PageCount
                    };

                    chapterImages[chapter] = imagePaths;
                }

                // Use naming service to determine the series folder under the root
                var seriesFolder = _namingService.GetSeriesFolder(series);
                var fullOutputDir = Path.Combine(outputDir, seriesFolder);

                var cbzPath = await _cbzCreator.CreateCbzFromVolumeAsync(fullOutputDir, series, volume, chapterImages);
                _logger.Info($"Created CBZ: {cbzPath}");

                // Record volume pack coverage
                var chapterNumbers = chapters.Select(c => c.ChapterNumber).ToList();
                _volumePackTracker.RecordVolumePack(series.Id, volume.Id, chapterNumbers);

                // Save MangaFile record with volume pack metadata
                var fileInfo = new FileInfo(cbzPath);
                var coveredChaptersStr = VolumePackChapterSerializer.SerializeChapters(chapterNumbers);
                var mangaFile = new MangaFile
                {
                    VolumeId = volume.Id,
                    MangaSeriesId = series.Id,
                    Path = cbzPath,
                    FileName = Path.GetFileName(cbzPath),
                    RelativePath = Path.GetRelativePath(outputDir, cbzPath),
                    Size = fileInfo.Exists ? fileInfo.Length : 0,
                    IsVolumePack = true,
                    CoveredChapters = coveredChaptersStr
                };

                _mangaFileService.Add(mangaFile);

                // Tag chapters with story arc info if applicable
                TagChaptersWithArcs(series.MangaMetadataId, chapterNumbers);

                _metadataGenerator.WriteSeriesMetadataFile(series);

                return cbzPath;
            }
            finally
            {
                if (_diskProvider.FolderExists(tempDir))
                {
                    _diskProvider.DeleteFolder(tempDir, true);
                }
            }
        }

        public async Task<string> DownloadChapterAsync(string outputDir, MangaSeries series, Volume volume, Chapter chapter)
        {
            _logger.Info($"Downloading chapter {chapter.ChapterNumber} of {series.Name} volume {volume.VolumeNumber}...");

            var pages = await _connector.GetChapterPagesAsync(chapter.ForeignChapterId);
            var tempDir = Path.Combine(Path.GetTempPath(), "manga-arr", series.ForeignMangaId, $"ch-{chapter.ChapterNumber}");

            try
            {
                var imagePaths = await DownloadPagesAsync(tempDir, chapter.ChapterNumber, pages);

                if (imagePaths == null)
                {
                    _logger.Error("Aborting chapter {0} download: page downloads failed", chapter.ChapterNumber);
                    return null;
                }

                // Use naming service to determine the series folder under the root
                var seriesFolder = _namingService.GetSeriesFolder(series);
                var fullOutputDir = Path.Combine(outputDir, seriesFolder);

                var cbzPath = await _cbzCreator.CreateCbzFromChapterAsync(fullOutputDir, series, volume, chapter, imagePaths);
                _logger.Info($"Created CBZ: {cbzPath}");

                // Save MangaFile record for individual chapter
                var fileInfo = new FileInfo(cbzPath);
                var mangaFile = new MangaFile
                {
                    VolumeId = volume.Id,
                    MangaSeriesId = series.Id,
                    Path = cbzPath,
                    FileName = Path.GetFileName(cbzPath),
                    RelativePath = Path.GetRelativePath(outputDir, cbzPath),
                    Size = fileInfo.Exists ? fileInfo.Length : 0,
                    IsVolumePack = false,
                    CoveredChapters = chapter.ChapterNumber.ToString("0.###")
                };

                _mangaFileService.Add(mangaFile);

                // Tag chapter with story arc info if applicable
                TagChaptersWithArcs(series.MangaMetadataId, new List<decimal> { chapter.ChapterNumber });

                _metadataGenerator.WriteSeriesMetadataFile(series);

                return cbzPath;
            }
            finally
            {
                if (_diskProvider.FolderExists(tempDir))
                {
                    _diskProvider.DeleteFolder(tempDir, true);
                }
            }
        }

        private const int MaxPageRetries = 3;
        private const int BaseRetryDelayMs = 1000;

        private async Task<List<string>> DownloadPagesAsync(string tempDir, decimal chapterNumber, ChapterPages pages)
        {
            _diskProvider.EnsureFolder(tempDir);

            var imagePaths = new List<string>();

            for (var i = 0; i < pages.PageUrls.Count; i++)
            {
                var url = pages.PageUrls[i];
                var ext = GetExtension(url);
                var fileName = $"{chapterNumber:000}_{(i + 1):000}{ext}";
                var filePath = Path.Combine(tempDir, fileName);

                var downloaded = await DownloadPageWithRetryAsync(url, filePath, i + 1, pages.PageUrls.Count);
                if (!downloaded)
                {
                    _logger.Error("Chapter {0} download aborted: page {1}/{2} failed after retries", chapterNumber, i + 1, pages.PageUrls.Count);
                    return null;
                }

                imagePaths.Add(filePath);

                // Rate limit: 1 req/s for images
                await Task.Delay(1100);
            }

            return imagePaths;
        }

        private async Task<bool> DownloadPageWithRetryAsync(string url, string filePath, int pageNum, int totalPages)
        {
            for (var attempt = 0; attempt <= MaxPageRetries; attempt++)
            {
                try
                {
                    if (attempt > 0)
                    {
                        var delay = BaseRetryDelayMs * (1 << (attempt - 1)); // exponential backoff
                        _logger.Debug("Retry {0}/{1} for page {2} after {3}ms", attempt, MaxPageRetries, pageNum, delay);
                        await Task.Delay(delay);
                    }

                    _logger.Debug("Downloading page {0}/{1}: {2}", pageNum, totalPages, url);

                    var response = await _httpClient.GetAsync(url);

                    if ((int)response.StatusCode == 429)
                    {
                        _logger.Warn("HTTP 429 (Too Many Requests) for page {0}, attempt {1}/{2}", pageNum, attempt + 1, MaxPageRetries + 1);
                        continue; // retry with backoff
                    }

                    response.EnsureSuccessStatusCode();

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await response.Content.CopyToAsync(fileStream);
                    }

                    return true;
                }
                catch (HttpRequestException ex)
                {
                    _logger.Warn("HTTP error downloading page {0} (attempt {1}/{2}): {3}", pageNum, attempt + 1, MaxPageRetries + 1, ex.Message);
                    if (attempt == MaxPageRetries)
                    {
                        _logger.Error("Giving up on page {0} after {1} attempts: {2}", pageNum, MaxPageRetries + 1, url);
                        return false;
                    }
                }
                catch (TaskCanceledException ex)
                {
                    _logger.Warn("Timeout downloading page {0} (attempt {1}/{2}): {3}", pageNum, attempt + 1, MaxPageRetries + 1, ex.Message);
                    if (attempt == MaxPageRetries)
                    {
                        _logger.Error("Giving up on page {0} after {1} attempts (timeout): {2}", pageNum, MaxPageRetries + 1, url);
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Unexpected error downloading page {0}: {1}", pageNum, url);
                    return false; // non-transient, don't retry
                }
            }

            return false;
        }

        private string GetExtension(string url)
        {
            var path = new Uri(url).AbsolutePath;
            var ext = Path.GetExtension(path);
            return string.IsNullOrEmpty(ext) ? ".jpg" : ext;
        }

        private void TagChaptersWithArcs(int mangaMetadataId, List<decimal> chapterNumbers)
        {
            var arcs = _storyArcService.GetArcs(mangaMetadataId);
            if (arcs.Count == 0)
            {
                return;
            }

            foreach (var arc in arcs)
            {
                if (string.IsNullOrEmpty(arc.ChapterRange))
                {
                    continue;
                }

                var rangeParts = arc.ChapterRange.Split('-');
                if (rangeParts.Length != 2 ||
                    !decimal.TryParse(rangeParts[0], out var rangeStart) ||
                    !decimal.TryParse(rangeParts[1], out var rangeEnd))
                {
                    continue;
                }

                foreach (var chapterNumber in chapterNumbers)
                {
                    if (chapterNumber >= rangeStart && chapterNumber <= rangeEnd)
                    {
                        _logger.Debug("Chapter {0} belongs to story arc '{1}' (range {2})", chapterNumber, arc.Name, arc.ChapterRange);
                    }
                }
            }
        }
    }
}
