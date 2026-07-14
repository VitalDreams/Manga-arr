using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Http;
using NzbDrone.Core.Manga.Connectors;

namespace NzbDrone.Core.Manga
{
    public interface IMangaDexDownloader
    {
        Task<string> DownloadVolumeAsync(string outputDir, MangaSeries series, Volume volume);
        Task<string> DownloadChapterAsync(string outputDir, MangaSeries series, Volume volume, Chapter chapter);
    }

    public class MangaDexDownloader : IMangaDexDownloader
    {
        private readonly IMangaMetadataConnector _connector;
        private readonly ICbzCreator _cbzCreator;
        private readonly IHttpClient _httpClient;
        private readonly IDiskProvider _diskProvider;
        private readonly Logger _logger;

        public MangaDexDownloader(
            IMangaMetadataConnector connector,
            ICbzCreator cbzCreator,
            IHttpClient httpClient,
            IDiskProvider diskProvider,
            Logger logger)
        {
            _connector = connector;
            _cbzCreator = cbzCreator;
            _httpClient = httpClient;
            _diskProvider = diskProvider;
            _logger = logger;
        }

        /// <summary>
        /// Download all chapters in a volume as a single CBZ
        /// </summary>
        public async Task<string> DownloadVolumeAsync(string outputDir, MangaSeries series, Volume volume)
        {
            _logger.Info($"Downloading volume {volume.VolumeNumber} of {series.Name}...");

            // Get chapters for this volume
            var chapters = await _connector.GetChaptersForVolumeAsync(series.ForeignMangaId, volume.VolumeNumber);

            if (!chapters.Any())
            {
                _logger.Warn($"No chapters found for volume {volume.VolumeNumber}");
                return null;
            }

            _logger.Info($"Found {chapters.Count} chapters in volume {volume.VolumeNumber}");

            // Download all chapter pages
            var chapterImages = new Dictionary<Chapter, List<string>>();
            var tempDir = Path.Combine(Path.GetTempPath(), "manga-arr", series.ForeignMangaId, $"vol-{volume.VolumeNumber}");

            try
            {
                foreach (var chapterInfo in chapters)
                {
                    _logger.Info($"Downloading chapter {chapterInfo.ChapterNumber} ({chapterInfo.PageCount} pages)...");

                    var pages = await _connector.GetChapterPagesAsync(chapterInfo.ForeignChapterId);
                    var imagePaths = await DownloadPagesAsync(tempDir, chapterInfo.ChapterNumber, pages);

                    // Create a Chapter object for the CBZ creator
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

                // Create CBZ with all chapters merged
                var cbzPath = await _cbzCreator.CreateCbzFromVolumeAsync(outputDir, series, volume, chapterImages);
                _logger.Info($"Created CBZ: {cbzPath}");

                return cbzPath;
            }
            finally
            {
                // Cleanup temp files
                if (_diskProvider.FolderExists(tempDir))
                {
                    _diskProvider.DeleteFolder(tempDir, true);
                }
            }
        }

        /// <summary>
        /// Download a single chapter as a CBZ
        /// </summary>
        public async Task<string> DownloadChapterAsync(string outputDir, MangaSeries series, Volume volume, Chapter chapter)
        {
            _logger.Info($"Downloading chapter {chapter.ChapterNumber} of {series.Name} volume {volume.VolumeNumber}...");

            var pages = await _connector.GetChapterPagesAsync(chapter.ForeignChapterId);
            var tempDir = Path.Combine(Path.GetTempPath(), "manga-arr", series.ForeignMangaId, $"ch-{chapter.ChapterNumber}");

            try
            {
                var imagePaths = await DownloadPagesAsync(tempDir, chapter.ChapterNumber, pages);
                var cbzPath = await _cbzCreator.CreateCbzFromChapterAsync(outputDir, series, volume, chapter, imagePaths);
                _logger.Info($"Created CBZ: {cbzPath}");

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

                _logger.Debug($"Downloading page {i + 1}/{pages.PageUrls.Count}: {url}");

                try
                {
                    var request = new HttpRequestBuilder(url).Build();
                    var response = await _httpClient.GetAsync(request);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await response.Content.CopyToAsync(fileStream);
                    }

                    imagePaths.Add(filePath);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, $"Failed to download page {i + 1}: {url}");
                }

                // Rate limit: 1 req/s for images
                await Task.Delay(1100);
            }

            return imagePaths;
        }

        private string GetExtension(string url)
        {
            var path = new Uri(url).AbsolutePath;
            var ext = Path.GetExtension(path);
            return string.IsNullOrEmpty(ext) ? ".jpg" : ext;
        }
    }
}
