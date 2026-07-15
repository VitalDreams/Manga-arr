using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Organizer;

namespace NzbDrone.Core.Manga
{
    public interface ICbzCreator
    {
        Task<string> CreateCbzFromChapterAsync(string outputDir, MangaSeries series, Volume volume, Chapter chapter, List<string> imagePaths);
        Task<string> CreateCbzFromVolumeAsync(string outputDir, MangaSeries series, Volume volume, Dictionary<Chapter, List<string>> chapterImages);
    }

    public class CbzCreator : ICbzCreator
    {
        private readonly IDiskProvider _diskProvider;
        private readonly IBuildFileNames _buildFileNames;
        private readonly IComicInfoGenerator _comicInfoGenerator;
        private readonly IMangaNamingService _namingService;

        public CbzCreator(
            IDiskProvider diskProvider,
            IBuildFileNames buildFileNames,
            IComicInfoGenerator comicInfoGenerator,
            IMangaNamingService namingService)
        {
            _diskProvider = diskProvider;
            _buildFileNames = buildFileNames;
            _comicInfoGenerator = comicInfoGenerator;
            _namingService = namingService;
        }

        public async Task<string> CreateCbzFromChapterAsync(
            string outputDir,
            MangaSeries series,
            Volume volume,
            Chapter chapter,
            List<string> imagePaths)
        {
            var fileName = _namingService.GetChapterFileName(series, volume, chapter);
            var outputPath = Path.Combine(outputDir, fileName);

            _diskProvider.EnsureFolder(outputDir);

            using (var zipStream = new FileStream(outputPath, FileMode.Create))
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
            {
                // Add ComicInfo.xml first
                var comicInfoXml = _comicInfoGenerator.GenerateComicInfoXml(series, volume, chapter, imagePaths.Count);
                var comicInfoEntry = archive.CreateEntry("ComicInfo.xml", CompressionLevel.Optimal);

                using (var entryStream = comicInfoEntry.Open())
                using (var writer = new StreamWriter(entryStream))
                {
                    await writer.WriteAsync(comicInfoXml);
                }

                // Add images sorted by filename/page number
                var sortedImages = imagePaths.OrderBy(p => p, System.StringComparer.Ordinal).ToList();

                for (var i = 0; i < sortedImages.Count; i++)
                {
                    var ext = Path.GetExtension(sortedImages[i]);
                    var entryName = $"{(i + 1):000}{ext}";
                    var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);

                    using (var entryStream = entry.Open())
                    using (var fileStream = new FileStream(sortedImages[i], FileMode.Open, FileAccess.Read))
                    {
                        await fileStream.CopyToAsync(entryStream);
                    }
                }
            }

            return outputPath;
        }

        public async Task<string> CreateCbzFromVolumeAsync(
            string outputDir,
            MangaSeries series,
            Volume volume,
            Dictionary<Chapter, List<string>> chapterImages)
        {
            var fileName = _namingService.GetVolumeFileName(series, volume);
            var outputPath = Path.Combine(outputDir, fileName);

            _diskProvider.EnsureFolder(outputDir);

            var pageIndex = 0;

            using (var zipStream = new FileStream(outputPath, FileMode.Create))
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
            {
                // Add ComicInfo.xml first (uses first chapter's data)
                var firstChapter = chapterImages.Keys.OrderBy(c => c.ChapterNumber).First();
                var totalPages = chapterImages.Values.Sum(imgs => imgs.Count);
                var comicInfoXml = _comicInfoGenerator.GenerateComicInfoXml(series, volume, firstChapter, totalPages);
                var comicInfoEntry = archive.CreateEntry("ComicInfo.xml", CompressionLevel.Optimal);

                using (var entryStream = comicInfoEntry.Open())
                using (var writer = new StreamWriter(entryStream))
                {
                    await writer.WriteAsync(comicInfoXml);
                }

                // Add all chapter images in chapter order, sorted by filename within each chapter
                foreach (var chapter in chapterImages.Keys.OrderBy(c => c.ChapterNumber))
                {
                    var images = chapterImages[chapter].OrderBy(p => p, System.StringComparer.Ordinal).ToList();

                    for (var i = 0; i < images.Count; i++)
                    {
                        var ext = Path.GetExtension(images[i]);
                        var entryName = $"{(pageIndex + 1):000}{ext}";
                        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);

                        using (var entryStream = entry.Open())
                        using (var fileStream = new FileStream(images[i], FileMode.Open, FileAccess.Read))
                        {
                            await fileStream.CopyToAsync(entryStream);
                        }

                        pageIndex++;
                    }
                }
            }

            return outputPath;
        }
    }
}
