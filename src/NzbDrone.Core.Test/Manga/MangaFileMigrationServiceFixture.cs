using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Books;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Manga;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Manga
{
    [TestFixture]
    public class MangaFileMigrationServiceFixture : CoreTest<MangaFileMigrationService>
    {
        private int _authorId;
        private int _seriesId;
        private List<Volume> _volumes;
        private List<BookFile> _legacyFiles;
        private Dictionary<int, List<MangaFile>> _existingFilesByVolume;

        [SetUp]
        public void Setup()
        {
            _authorId = 1;
            _seriesId = 100;
            _volumes = new List<Volume>();
            _legacyFiles = new List<BookFile>();
            _existingFilesByVolume = new Dictionary<int, List<MangaFile>>();
        }

        [Test]
        public void should_migrate_matching_bookfile_to_mangafile()
        {
            var volume = new Volume { Id = 10, ForeignVolumeId = "manga-123_vol1" };
            _volumes.Add(volume);

            var bookFile = new BookFile
            {
                Id = 50,
                Path = "/manga/test/vol1.cbz",
                Size = 1024,
                DateAdded = new DateTime(2025, 1, 1)
            };
            bookFile.Edition = new LazyLoaded<Edition>(new Edition
            {
                Book = new LazyLoaded<Book>(new Book { ForeignBookId = "manga-123_vol1" })
            });
            _legacyFiles.Add(bookFile);

            _existingFilesByVolume[10] = new List<MangaFile>();

            Subject.MigrateLegacyBookFiles(_authorId, _volumes, _legacyFiles, _existingFilesByVolume, _seriesId);

            Mocker.GetMock<IMangaFileService>()
                .Verify(x => x.Add(It.Is<MangaFile>(f =>
                    f.Path == "/manga/test/vol1.cbz" &&
                    f.VolumeId == 10 &&
                    f.MangaSeriesId == _seriesId &&
                    f.IsVolumePack &&
                    f.Size == 1024)),
                    Times.Once());
        }

        [Test]
        public void should_skip_bookfile_with_nonmatching_foreignbookid()
        {
            var volume = new Volume { Id = 10, ForeignVolumeId = "manga-123_vol1" };
            _volumes.Add(volume);

            var bookFile = new BookFile
            {
                Id = 51,
                Path = "/manga/test/vol2.cbz",
                Size = 2048,
                DateAdded = new DateTime(2025, 1, 1)
            };
            bookFile.Edition = new LazyLoaded<Edition>(new Edition
            {
                Book = new LazyLoaded<Book>(new Book { ForeignBookId = "manga-123_vol2" })
            });
            _legacyFiles.Add(bookFile);

            _existingFilesByVolume[10] = new List<MangaFile>();

            Subject.MigrateLegacyBookFiles(_authorId, _volumes, _legacyFiles, _existingFilesByVolume, _seriesId);

            Mocker.GetMock<IMangaFileService>()
                .Verify(x => x.Add(It.IsAny<MangaFile>()), Times.Never());
        }

        [Test]
        public void should_skip_bookfile_already_migrated_by_path()
        {
            var volume = new Volume { Id = 10, ForeignVolumeId = "manga-123_vol1" };
            _volumes.Add(volume);

            var bookFile = new BookFile
            {
                Id = 52,
                Path = "/manga/test/vol1.cbz",
                Size = 1024,
                DateAdded = new DateTime(2025, 1, 1)
            };
            bookFile.Edition = new LazyLoaded<Edition>(new Edition
            {
                Book = new LazyLoaded<Book>(new Book { ForeignBookId = "manga-123_vol1" })
            });
            _legacyFiles.Add(bookFile);

            _existingFilesByVolume[10] = new List<MangaFile>
            {
                new MangaFile { Path = "/manga/test/vol1.cbz" }
            };

            Subject.MigrateLegacyBookFiles(_authorId, _volumes, _legacyFiles, _existingFilesByVolume, _seriesId);

            Mocker.GetMock<IMangaFileService>()
                .Verify(x => x.Add(It.IsAny<MangaFile>()), Times.Never());
        }

        [Test]
        public void should_skip_volume_without_foreignvolumeid()
        {
            var volume = new Volume { Id = 10, ForeignVolumeId = null };
            _volumes.Add(volume);

            var bookFile = new BookFile
            {
                Id = 53,
                Path = "/manga/test/vol1.cbz",
                Size = 1024,
                DateAdded = new DateTime(2025, 1, 1)
            };
            bookFile.Edition = new LazyLoaded<Edition>(new Edition
            {
                Book = new LazyLoaded<Book>(new Book { ForeignBookId = "manga-123_vol1" })
            });
            _legacyFiles.Add(bookFile);

            _existingFilesByVolume[10] = new List<MangaFile>();

            Subject.MigrateLegacyBookFiles(_authorId, _volumes, _legacyFiles, _existingFilesByVolume, _seriesId);

            Mocker.GetMock<IMangaFileService>()
                .Verify(x => x.Add(It.IsAny<MangaFile>()), Times.Never());
        }

        [Test]
        public void should_skip_bookfile_with_null_edition()
        {
            var volume = new Volume { Id = 10, ForeignVolumeId = "manga-123_vol1" };
            _volumes.Add(volume);

            var bookFile = new BookFile
            {
                Id = 54,
                Path = "/manga/test/vol1.cbz",
                Size = 1024,
                DateAdded = new DateTime(2025, 1, 1)
            };
            bookFile.Edition = null;
            _legacyFiles.Add(bookFile);

            _existingFilesByVolume[10] = new List<MangaFile>();

            Subject.MigrateLegacyBookFiles(_authorId, _volumes, _legacyFiles, _existingFilesByVolume, _seriesId);

            Mocker.GetMock<IMangaFileService>()
                .Verify(x => x.Add(It.IsAny<MangaFile>()), Times.Never());
        }

        [Test]
        public void should_handle_multiple_volumes_and_multiple_bookfiles()
        {
            var vol1 = new Volume { Id = 10, ForeignVolumeId = "manga-123_vol1" };
            var vol2 = new Volume { Id = 11, ForeignVolumeId = "manga-123_vol2" };
            _volumes.AddRange(new[] { vol1, vol2 });

            var bookFile1 = new BookFile
            {
                Id = 55,
                Path = "/manga/test/vol1.cbz",
                Size = 1024,
                DateAdded = new DateTime(2025, 1, 1)
            };
            bookFile1.Edition = new LazyLoaded<Edition>(new Edition
            {
                Book = new LazyLoaded<Book>(new Book { ForeignBookId = "manga-123_vol1" })
            });

            var bookFile2 = new BookFile
            {
                Id = 56,
                Path = "/manga/test/vol2.cbz",
                Size = 2048,
                DateAdded = new DateTime(2025, 2, 1)
            };
            bookFile2.Edition = new LazyLoaded<Edition>(new Edition
            {
                Book = new LazyLoaded<Book>(new Book { ForeignBookId = "manga-123_vol2" })
            });

            _legacyFiles.AddRange(new[] { bookFile1, bookFile2 });

            _existingFilesByVolume[10] = new List<MangaFile>();
            _existingFilesByVolume[11] = new List<MangaFile>();

            Subject.MigrateLegacyBookFiles(_authorId, _volumes, _legacyFiles, _existingFilesByVolume, _seriesId);

            Mocker.GetMock<IMangaFileService>()
                .Verify(x => x.Add(It.Is<MangaFile>(f => f.VolumeId == 10)), Times.Once());
            Mocker.GetMock<IMangaFileService>()
                .Verify(x => x.Add(It.Is<MangaFile>(f => f.VolumeId == 11)), Times.Once());
        }

        [Test]
        public void should_migrate_only_unmigrated_files_when_some_already_exist()
        {
            var volume = new Volume { Id = 10, ForeignVolumeId = "manga-123_vol1" };
            _volumes.Add(volume);

            var bookFile1 = new BookFile
            {
                Id = 57,
                Path = "/manga/test/vol1a.cbz",
                Size = 1024,
                DateAdded = new DateTime(2025, 1, 1)
            };
            bookFile1.Edition = new LazyLoaded<Edition>(new Edition
            {
                Book = new LazyLoaded<Book>(new Book { ForeignBookId = "manga-123_vol1" })
            });

            var bookFile2 = new BookFile
            {
                Id = 58,
                Path = "/manga/test/vol1b.cbz",
                Size = 2048,
                DateAdded = new DateTime(2025, 2, 1)
            };
            bookFile2.Edition = new LazyLoaded<Edition>(new Edition
            {
                Book = new LazyLoaded<Book>(new Book { ForeignBookId = "manga-123_vol1" })
            });

            _legacyFiles.AddRange(new[] { bookFile1, bookFile2 });

            // vol1a already migrated
            _existingFilesByVolume[10] = new List<MangaFile>
            {
                new MangaFile { Path = "/manga/test/vol1a.cbz" }
            };

            Subject.MigrateLegacyBookFiles(_authorId, _volumes, _legacyFiles, _existingFilesByVolume, _seriesId);

            // Only vol1b should be added
            Mocker.GetMock<IMangaFileService>()
                .Verify(x => x.Add(It.Is<MangaFile>(f => f.Path == "/manga/test/vol1b.cbz")), Times.Once());
            Mocker.GetMock<IMangaFileService>()
                .Verify(x => x.Add(It.Is<MangaFile>(f => f.Path == "/manga/test/vol1a.cbz")), Times.Never());
        }
    }
}
