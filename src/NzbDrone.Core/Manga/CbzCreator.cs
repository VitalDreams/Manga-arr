using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Serialization;
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

        public CbzCreator(IDiskProvider diskProvider, IBuildFileNames buildFileNames)
        {
            _diskProvider = diskProvider;
            _buildFileNames = buildFileNames;
        }

        /// <summary>
        /// Create a CBZ file from a single chapter's images
        /// </summary>
        public async Task<string> CreateCbzFromChapterAsync(
            string outputDir,
            MangaSeries series,
            Volume volume,
            Chapter chapter,
            List<string> imagePaths)
        {
            var fileName = GetChapterFileName(series, volume, chapter);
            var outputPath = Path.Combine(outputDir, fileName);

            _diskProvider.EnsureFolder(outputDir);

            using (var zipStream = new FileStream(outputPath, FileMode.Create))
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
            {
                // Add images in order
                for (var i = 0; i < imagePaths.Count; i++)
                {
                    var ext = Path.GetExtension(imagePaths[i]);
                    var entryName = $"{(i + 1):000}{ext}";
                    var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);

                    using (var entryStream = entry.Open())
                    using (var fileStream = new FileStream(imagePaths[i], FileMode.Open, FileAccess.Read))
                    {
                        await fileStream.CopyToAsync(entryStream);
                    }
                }

                // Add ComicInfo.xml metadata
                var comicInfo = CreateComicInfo(series, volume, chapter, imagePaths.Count);
                var comicInfoEntry = archive.CreateEntry("ComicInfo.xml", CompressionLevel.Optimal);

                using (var entryStream = comicInfoEntry.Open())
                using (var writer = new StreamWriter(entryStream))
                {
                    var serializer = new XmlSerializer(typeof(ComicInfo));
                    serializer.Serialize(writer, comicInfo);
                }
            }

            return outputPath;
        }

        /// <summary>
        /// Create a CBZ file from all chapters in a volume (merged)
        /// </summary>
        public async Task<string> CreateCbzFromVolumeAsync(
            string outputDir,
            MangaSeries series,
            Volume volume,
            Dictionary<Chapter, List<string>> chapterImages)
        {
            var fileName = GetVolumeFileName(series, volume);
            var outputPath = Path.Combine(outputDir, fileName);

            _diskProvider.EnsureFolder(outputDir);

            var pageIndex = 0;

            using (var zipStream = new FileStream(outputPath, FileMode.Create))
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
            {
                // Add all chapter images in chapter order
                foreach (var chapter in chapterImages.Keys.OrderBy(c => c.ChapterNumber))
                {
                    var images = chapterImages[chapter];

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

                // Add ComicInfo.xml metadata
                var firstChapter = chapterImages.Keys.OrderBy(c => c.ChapterNumber).First();
                var comicInfo = CreateComicInfo(series, volume, firstChapter, pageIndex);
                var comicInfoEntry = archive.CreateEntry("ComicInfo.xml", CompressionLevel.Optimal);

                using (var entryStream = comicInfoEntry.Open())
                using (var writer = new StreamWriter(entryStream))
                {
                    var serializer = new XmlSerializer(typeof(ComicInfo));
                    serializer.Serialize(writer, comicInfo);
                }
            }

            return outputPath;
        }

        private string GetChapterFileName(MangaSeries series, Volume volume, Chapter chapter)
        {
            // Pattern: {Series} - Vol.{Volume} Ch.{Chapter}.cbz
            return $"{SanitizeFileName(series.Name)} - Vol.{volume.VolumeNumber:000} Ch.{chapter.ChapterNumber:000}.cbz";
        }

        private string GetVolumeFileName(MangaSeries series, Volume volume)
        {
            // Pattern: {Series} - Vol.{Volume}.cbz
            return $"{SanitizeFileName(series.Name)} - Vol.{volume.VolumeNumber:000}.cbz";
        }

        private string SanitizeFileName(string name)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            return new string(name.Select(c => invalidChars.Contains(c) ? '_' : c).ToArray());
        }

        private ComicInfo CreateComicInfo(MangaSeries series, Volume volume, Chapter chapter, int pageCount)
        {
            return new ComicInfo
            {
                Title = volume.Title,
                Series = series.Name,
                Number = volume.VolumeNumber.ToString(),
                Volume = volume.VolumeNumber.ToString(),
                Summary = series.Metadata?.Value?.Description ?? string.Empty,
                Writer = series.Metadata?.Value?.Author ?? string.Empty,
                Penciller = series.Metadata?.Value?.Artist ?? string.Empty,
                Genre = string.Join(", ", series.Metadata?.Value?.Genres ?? new List<string>()),
                PageCount = pageCount,
                LanguageISO = chapter.Language ?? "en",
                Manga = "Yes"
            };
        }
    }

    /// <summary>
    /// ComicInfo.xml schema for CBZ metadata
    /// </summary>
    [XmlRoot("ComicInfo")]
    public class ComicInfo
    {
        public string Title { get; set; }
        public string Series { get; set; }
        public string Number { get; set; }
        public string Volume { get; set; }
        public string Summary { get; set; }
        public string Writer { get; set; }
        public string Penciller { get; set; }
        public string Genre { get; set; }
        public int PageCount { get; set; }
        public string LanguageISO { get; set; }
        public string Manga { get; set; }
    }
}
