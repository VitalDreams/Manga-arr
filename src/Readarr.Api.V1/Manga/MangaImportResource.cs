using NzbDrone.Core.Manga.Import;
using Readarr.Http.REST;

namespace Readarr.Api.V1.Manga
{
    public class MangaImportResource : RestResource
    {
        public string Path { get; set; }
        public string ForeignMangaId { get; set; }
        public string ImportMode { get; set; } = "inPlace";
    }

    public class MangaImportFileResource : RestResource
    {
        public string FilePath { get; set; }
        public int SeriesId { get; set; }
        public int VolumeId { get; set; }
    }

    public class ScannedMangaFileResource
    {
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public long FileSize { get; set; }
        public string SeriesName { get; set; }
        public int? VolumeNumber { get; set; }
        public decimal? ChapterNumber { get; set; }
    }

    public static class MangaImportResourceMapper
    {
        public static ScannedMangaFileResource ToResource(this ScannedMangaFile model)
        {
            if (model == null)
            {
                return null;
            }

            return new ScannedMangaFileResource
            {
                FileName = model.FileName,
                FilePath = model.FilePath,
                FileSize = model.FileSize,
                SeriesName = model.SeriesName,
                VolumeNumber = model.VolumeNumber,
                ChapterNumber = model.ChapterNumber
            };
        }

        public static ScannedMangaFile ToModel(this ScannedMangaFileResource resource)
        {
            if (resource == null)
            {
                return null;
            }

            return new ScannedMangaFile
            {
                FileName = resource.FileName,
                FilePath = resource.FilePath,
                FileSize = resource.FileSize,
                SeriesName = resource.SeriesName,
                VolumeNumber = resource.VolumeNumber,
                ChapterNumber = resource.ChapterNumber
            };
        }
    }
}
