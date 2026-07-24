using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Manga;
using NzbDrone.Core.Manga.Connectors;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.Manga
{
    [TestFixture]
    public class MangaDexDownloaderFixture : CoreTest<MangaDexDownloader>
    {
        private MangaSeries _series;
        private Volume _volume;

        [SetUp]
        public void Setup()
        {
            _series = new MangaSeries
            {
                Id = 1,
                Name = "Test Manga",
                ForeignMangaId = "manga-001",
                Path = "/manga/TestManga",
                RootFolderPath = "/manga"
            };

            _volume = new Volume
            {
                Id = 100,
                VolumeNumber = 1,
                Title = "Volume 1",
                MangaSeriesId = 1
            };
        }

        [Test]
        public async Task download_volume_should_return_null_when_page_download_fails()
        {
            // Chapter with 2 pages, URLs pointing to non-routable address to trigger failure
            Mocker.GetMock<IMangaMetadataConnector>()
                .Setup(x => x.GetChaptersForVolumeAsync("manga-001", 1))
                .ReturnsAsync(new List<ChapterInfo>
                {
                    new ChapterInfo
                    {
                        ForeignChapterId = "ch-1",
                        ChapterNumber = 1,
                        PageCount = 2
                    }
                });

            Mocker.GetMock<IMangaMetadataConnector>()
                .Setup(x => x.GetChapterPagesAsync("ch-1"))
                .ReturnsAsync(new ChapterPages
                {
                    PageUrls = new List<string>
                    {
                        "http://127.0.0.1:1/page1.jpg",
                        "http://127.0.0.1:1/page2.jpg"
                    }
                });

            Mocker.GetMock<IDiskProvider>()
                .Setup(x => x.EnsureFolder(It.IsAny<string>()));

            Mocker.GetMock<IDiskProvider>()
                .Setup(x => x.FolderExists(It.IsAny<string>()))
                .Returns(true);

            var result = await Subject.DownloadVolumeAsync("/output", _series, _volume);

            Assert.That(result, Is.Null);

            // Must not create CBZ, MangaFile, or record volume pack
            Mocker.GetMock<ICbzCreator>()
                .Verify(x => x.CreateCbzFromVolumeAsync(
                    It.IsAny<string>(), It.IsAny<MangaSeries>(),
                    It.IsAny<Volume>(), It.IsAny<Dictionary<Chapter, List<string>>>()),
                    Times.Never());

            Mocker.GetMock<IMangaFileService>()
                .Verify(x => x.Add(It.IsAny<MangaFile>()), Times.Never());

            Mocker.GetMock<IVolumePackTracker>()
                .Verify(x => x.RecordVolumePack(
                    It.IsAny<int>(), It.IsAny<int>(), It.IsAny<List<decimal>>()),
                    Times.Never());
        }

        [Test]
        public async Task download_chapter_should_return_null_when_page_download_fails()
        {
            var chapter = new Chapter
            {
                ForeignChapterId = "ch-5",
                ChapterNumber = 5,
                PageCount = 1
            };

            Mocker.GetMock<IMangaMetadataConnector>()
                .Setup(x => x.GetChapterPagesAsync("ch-5"))
                .ReturnsAsync(new ChapterPages
                {
                    PageUrls = new List<string> { "http://127.0.0.1:1/fail.jpg" }
                });

            Mocker.GetMock<IDiskProvider>()
                .Setup(x => x.EnsureFolder(It.IsAny<string>()));

            Mocker.GetMock<IDiskProvider>()
                .Setup(x => x.FolderExists(It.IsAny<string>()))
                .Returns(true);

            var result = await Subject.DownloadChapterAsync("/output", _series, _volume, chapter);

            Assert.That(result, Is.Null);

            Mocker.GetMock<ICbzCreator>()
                .Verify(x => x.CreateCbzFromChapterAsync(
                    It.IsAny<string>(), It.IsAny<MangaSeries>(),
                    It.IsAny<Volume>(), It.IsAny<Chapter>(), It.IsAny<List<string>>()),
                    Times.Never());

            Mocker.GetMock<IMangaFileService>()
                .Verify(x => x.Add(It.IsAny<MangaFile>()), Times.Never());
        }

        [Test]
        public async Task download_volume_should_clean_temp_dir_on_page_failure()
        {
            Mocker.GetMock<IMangaMetadataConnector>()
                .Setup(x => x.GetChaptersForVolumeAsync("manga-001", 1))
                .ReturnsAsync(new List<ChapterInfo>
                {
                    new ChapterInfo
                    {
                        ForeignChapterId = "ch-1",
                        ChapterNumber = 1,
                        PageCount = 1
                    }
                });

            Mocker.GetMock<IMangaMetadataConnector>()
                .Setup(x => x.GetChapterPagesAsync("ch-1"))
                .ReturnsAsync(new ChapterPages
                {
                    PageUrls = new List<string> { "http://127.0.0.1:1/fail.jpg" }
                });

            Mocker.GetMock<IDiskProvider>()
                .Setup(x => x.EnsureFolder(It.IsAny<string>()));

            Mocker.GetMock<IDiskProvider>()
                .Setup(x => x.FolderExists(It.IsAny<string>()))
                .Returns(true);

            await Subject.DownloadVolumeAsync("/output", _series, _volume);

            // finally block must clean up the temp directory
            Mocker.GetMock<IDiskProvider>()
                .Verify(x => x.DeleteFolder(It.IsAny<string>(), true), Times.Once());
        }
    }
}
