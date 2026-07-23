using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.MediaFiles;

namespace NzbDrone.Core.Manga
{
    public interface IMangaFileMigrationService
    {
        void MigrateLegacyBookFiles(int authorId, IEnumerable<Volume> volumes, IEnumerable<BookFile> legacyFiles, IDictionary<int, List<MangaFile>> existingFilesByVolume, int mangaSeriesId);
    }

    public class MangaFileMigrationService : IMangaFileMigrationService
    {
        private readonly IMangaFileService _mangaFileService;

        public MangaFileMigrationService(IMangaFileService mangaFileService)
        {
            _mangaFileService = mangaFileService;
        }

        public void MigrateLegacyBookFiles(
            int authorId,
            IEnumerable<Volume> volumes,
            IEnumerable<BookFile> legacyFiles,
            IDictionary<int, List<MangaFile>> existingFilesByVolume,
            int mangaSeriesId)
        {
            foreach (var volume in volumes)
            {
                if (volume.ForeignVolumeId == null)
                {
                    continue;
                }

                var existingPaths = new HashSet<string>(
                    (existingFilesByVolume.TryGetValue(volume.Id, out var existing) ? existing : Enumerable.Empty<MangaFile>())
                    .Select(f => f.Path));

                foreach (var bookFile in legacyFiles)
                {
                    var foreignBookId = bookFile.Edition?.Value?.Book?.Value?.ForeignBookId;
                    if (foreignBookId == null || foreignBookId != volume.ForeignVolumeId)
                    {
                        continue;
                    }

                    if (existingPaths.Contains(bookFile.Path))
                    {
                        continue;
                    }

                    var mangaFile = new MangaFile
                    {
                        Path = bookFile.Path,
                        FileName = System.IO.Path.GetFileName(bookFile.Path),
                        RelativePath = System.IO.Path.GetFileName(bookFile.Path),
                        Size = bookFile.Size,
                        AddedAt = bookFile.DateAdded,
                        VolumeId = volume.Id,
                        MangaSeriesId = mangaSeriesId,
                        IsVolumePack = true
                    };

                    _mangaFileService.Add(mangaFile);
                }
            }
        }
    }
}
