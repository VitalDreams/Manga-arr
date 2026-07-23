using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Manga;
using NzbDrone.Core.Manga.Import;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Manga
{
    [TestFixture]
    public class MangaImportReconcileFixture : CoreTest<MangaImportService>
    {
        private MangaSeries _series;
        private List<Volume> _volumes;
        private List<ScannedMangaFile> _scannedFiles;

        [SetUp]
        public void Setup()
        {
            _series = new MangaSeries
            {
                Id = 18,
                MangaMetadataId = 100,
                Path = "/manga/Berserk (2003)",
                RootFolderPath = "/manga",
                CleanName = "berserk",
                Metadata = new LazyLoaded<MangaMetadata>(new MangaMetadata
                {
                    Id = 100,
                    ForeignMangaId = "manga-berserk",
                    Title = "Berserk"
                })
            };

            _volumes = new List<Volume>();
            for (int i = 1; i <= 43; i++)
            {
                _volumes.Add(new Volume
                {
                    Id = 1000 + i,
                    VolumeNumber = i,
                    MangaSeriesId = 18,
                    MangaMetadataId = 100,
                    ForeignVolumeId = $"manga-berserk_vol{i}",
                    Title = $"Volume {i}",
                    Monitored = true
                });
            }

            _scannedFiles = new List<ScannedMangaFile>();

            Mocker.GetMock<IDiskProvider>()
                .Setup(x => x.FolderExists("/manga/Berserk (2003)"))
                .Returns(true);
        }

        private void SetupScanner(List<ScannedMangaFile> files)
        {
            _scannedFiles = files;
            Mocker.GetMock<IMangaFileScanner>()
                .Setup(x => x.ScanDirectory("/manga/Berserk (2003)"))
                .Returns(files);
        }

        private void SetupExistingVolumes(List<Volume> volumes)
        {
            Mocker.GetMock<IVolumeRepository>()
                .Setup(x => x.FindByMangaSeriesId(18))
                .Returns(volumes);
        }

        private void SetupExistingFiles(List<MangaFile> files)
        {
            Mocker.GetMock<IMangaFileService>()
                .Setup(x => x.GetFilesBySeries(18))
                .Returns(files);
        }

        [Test]
        public void should_create_mangafile_for_each_matched_volume_on_disk()
        {
            SetupExistingVolumes(_volumes);
            SetupExistingFiles(new List<MangaFile>());

            var scanned = new List<ScannedMangaFile>();
            for (int i = 1; i <= 41; i++)
            {
                scanned.Add(new ScannedMangaFile
                {
                    FileName = $"Berserk {i} (2003).cbz",
                    FilePath = $"/manga/Berserk (2003)/Berserk {i} (2003).cbz",
                    FileSize = 1024 * i,
                    SeriesName = "Berserk",
                    VolumeNumber = i
                });
            }
            SetupScanner(scanned);

            var imported = Subject.ReconcileSeries(_series);

            Assert.That(imported, Is.EqualTo(41));

            Mocker.GetMock<IMangaFileService>()
                .Verify(x => x.Add(It.Is<MangaFile>(f =>
                    f.MangaSeriesId == 18 &&
                    f.VolumeId >= 1001 && f.VolumeId <= 1041)),
                    Times.Exactly(41));
        }

        [Test]
        public void should_skip_files_already_tracked_by_path()
        {
            SetupExistingVolumes(_volumes);

            var existingFile = new MangaFile
            {
                Path = "/manga/Berserk (2003)/Berserk 1 (2003).cbz",
                VolumeId = 1001,
                MangaSeriesId = 18
            };
            SetupExistingFiles(new List<MangaFile> { existingFile });

            var scanned = new List<ScannedMangaFile>
            {
                new ScannedMangaFile
                {
                    FileName = "Berserk 1 (2003).cbz",
                    FilePath = "/manga/Berserk (2003)/Berserk 1 (2003).cbz",
                    FileSize = 1024,
                    SeriesName = "Berserk",
                    VolumeNumber = 1
                },
                new ScannedMangaFile
                {
                    FileName = "Berserk 2 (2003).cbz",
                    FilePath = "/manga/Berserk (2003)/Berserk 2 (2003).cbz",
                    FileSize = 2048,
                    SeriesName = "Berserk",
                    VolumeNumber = 2
                }
            };
            SetupScanner(scanned);

            var imported = Subject.ReconcileSeries(_series);

            Assert.That(imported, Is.EqualTo(1));

            Mocker.GetMock<IMangaFileService>()
                .Verify(x => x.Add(It.Is<MangaFile>(f =>
                    f.Path == "/manga/Berserk (2003)/Berserk 2 (2003).cbz")),
                    Times.Once());
            Mocker.GetMock<IMangaFileService>()
                .Verify(x => x.Add(It.Is<MangaFile>(f =>
                    f.Path == "/manga/Berserk (2003)/Berserk 1 (2003).cbz")),
                    Times.Never());
        }

        [Test]
        public void should_match_cbr_files_to_volumes()
        {
            SetupExistingVolumes(_volumes);
            SetupExistingFiles(new List<MangaFile>());

            var scanned = new List<ScannedMangaFile>
            {
                new ScannedMangaFile
                {
                    FileName = "Berserk 33 (2019).cbr",
                    FilePath = "/manga/Berserk (2003)/Berserk 33 (2019).cbr",
                    FileSize = 3072,
                    SeriesName = "Berserk",
                    VolumeNumber = 33
                },
                new ScannedMangaFile
                {
                    FileName = "Berserk 39 (2022).cbr",
                    FilePath = "/manga/Berserk (2003)/Berserk 39 (2022).cbr",
                    FileSize = 4096,
                    SeriesName = "Berserk",
                    VolumeNumber = 39
                }
            };
            SetupScanner(scanned);

            var imported = Subject.ReconcileSeries(_series);

            Assert.That(imported, Is.EqualTo(2));

            Mocker.GetMock<IMangaFileService>()
                .Verify(x => x.Add(It.Is<MangaFile>(f =>
                    f.VolumeId == 1033 && f.FileName == "Berserk 33 (2019).cbr")),
                    Times.Once());
            Mocker.GetMock<IMangaFileService>()
                .Verify(x => x.Add(It.Is<MangaFile>(f =>
                    f.VolumeId == 1039 && f.FileName == "Berserk 39 (2022).cbr")),
                    Times.Once());
        }

        [Test]
        public void should_skip_files_with_no_volume_number()
        {
            SetupExistingVolumes(_volumes);
            SetupExistingFiles(new List<MangaFile>());

            var scanned = new List<ScannedMangaFile>
            {
                new ScannedMangaFile
                {
                    FileName = "random_file.cbz",
                    FilePath = "/manga/Berserk (2003)/random_file.cbz",
                    FileSize = 1024,
                    SeriesName = "Random",
                    VolumeNumber = null
                }
            };
            SetupScanner(scanned);

            var imported = Subject.ReconcileSeries(_series);

            Assert.That(imported, Is.EqualTo(0));
            Mocker.GetMock<IMangaFileService>()
                .Verify(x => x.Add(It.IsAny<MangaFile>()), Times.Never());
        }

        [Test]
        public void should_skip_files_with_no_matching_volume_in_db()
        {
            SetupExistingVolumes(_volumes);
            SetupExistingFiles(new List<MangaFile>());

            var scanned = new List<ScannedMangaFile>
            {
                new ScannedMangaFile
                {
                    FileName = "Berserk 99 (2025).cbz",
                    FilePath = "/manga/Berserk (2003)/Berserk 99 (2025).cbz",
                    FileSize = 1024,
                    SeriesName = "Berserk",
                    VolumeNumber = 99
                }
            };
            SetupScanner(scanned);

            var imported = Subject.ReconcileSeries(_series);

            Assert.That(imported, Is.EqualTo(0));
            Mocker.GetMock<IMangaFileService>()
                .Verify(x => x.Add(It.IsAny<MangaFile>()), Times.Never());
        }

        [Test]
        public void should_not_reconcile_when_series_path_does_not_exist()
        {
            Mocker.GetMock<IDiskProvider>()
                .Setup(x => x.FolderExists("/manga/Berserk (2003)"))
                .Returns(false);

            var imported = Subject.ReconcileSeries(_series);

            Assert.That(imported, Is.EqualTo(0));
            Mocker.GetMock<IMangaFileScanner>()
                .Verify(x => x.ScanDirectory(It.IsAny<string>()), Times.Never());
        }

        [Test]
        public void should_not_reconcile_when_no_volumes_in_db()
        {
            SetupExistingVolumes(new List<Volume>());

            var imported = Subject.ReconcileSeries(_series);

            Assert.That(imported, Is.EqualTo(0));
            Mocker.GetMock<IMangaFileScanner>()
                .Verify(x => x.ScanDirectory(It.IsAny<string>()), Times.Never());
        }

        [Test]
        public void should_set_isvolumepack_based_on_chapter_number()
        {
            SetupExistingVolumes(_volumes);
            SetupExistingFiles(new List<MangaFile>());

            var scanned = new List<ScannedMangaFile>
            {
                new ScannedMangaFile
                {
                    FileName = "Berserk 1 (2003).cbz",
                    FilePath = "/manga/Berserk (2003)/Berserk 1 (2003).cbz",
                    FileSize = 1024,
                    SeriesName = "Berserk",
                    VolumeNumber = 1,
                    ChapterNumber = null
                },
                new ScannedMangaFile
                {
                    FileName = "Berserk - Vol.2 Ch.0.1.cbz",
                    FilePath = "/manga/Berserk (2003)/Berserk - Vol.2 Ch.0.1.cbz",
                    FileSize = 512,
                    SeriesName = "Berserk",
                    VolumeNumber = 2,
                    ChapterNumber = 0.1m
                }
            };
            SetupScanner(scanned);

            var imported = Subject.ReconcileSeries(_series);

            Assert.That(imported, Is.EqualTo(2));

            Mocker.GetMock<IMangaFileService>()
                .Verify(x => x.Add(It.Is<MangaFile>(f =>
                    f.VolumeId == 1001 && f.IsVolumePack == true)),
                    Times.Once());
            Mocker.GetMock<IMangaFileService>()
                .Verify(x => x.Add(It.Is<MangaFile>(f =>
                    f.VolumeId == 1002 && f.IsVolumePack == false)),
                    Times.Once());
        }

        [Test]
        public void should_not_auto_import_legacy_folder_files()
        {
            // Series path is /manga/Berserk (2003), legacy is at /manga/Berserk/
            // The scanner is called only for the series path, not the legacy folder
            SetupExistingVolumes(_volumes);
            SetupExistingFiles(new List<MangaFile>());

            var scanned = new List<ScannedMangaFile>
            {
                new ScannedMangaFile
                {
                    FileName = "Berserk 1 (2003).cbz",
                    FilePath = "/manga/Berserk (2003)/Berserk 1 (2003).cbz",
                    FileSize = 1024,
                    SeriesName = "Berserk",
                    VolumeNumber = 1
                }
            };
            SetupScanner(scanned);

            var imported = Subject.ReconcileSeries(_series);

            Assert.That(imported, Is.EqualTo(1));

            // Verify the scanner was called only for the series path
            Mocker.GetMock<IMangaFileScanner>()
                .Verify(x => x.ScanDirectory("/manga/Berserk (2003)"), Times.Once());
            Mocker.GetMock<IMangaFileScanner>()
                .Verify(x => x.ScanDirectory("/manga/Berserk"), Times.Never());
        }
    }
}
