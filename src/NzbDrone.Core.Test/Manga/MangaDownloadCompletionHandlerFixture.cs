using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Books;
using NzbDrone.Core.Download;
using NzbDrone.Core.Manga;
using NzbDrone.Core.Manga.Download;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Manga
{
    [TestFixture]
    public class MangaDownloadCompletionHandlerFixture : CoreTest<MangaDownloadCompletionHandler>
    {
        private MangaSeries _series;
        private Volume _volume;

        [SetUp]
        public void Setup()
        {
            _series = new MangaSeries
            {
                Id = 7,
                MangaMetadataId = 7,

                // CleanName is left unset here: a series added before CleanName existed keeps this
                // null until the backfill housekeeper next runs (every 24h), which is exactly the
                // live state that caused "No matching library series found" for an existing "Solo
                // Leveling" series. The importer must match on Name in that case, not skip the row.
                RootFolderPath = "/manga",
                Metadata = new MangaMetadata
                {
                    Title = "Solo Leveling",
                    Year = 2021
                }
            };

            _volume = new Volume
            {
                Id = 501,
                MangaMetadataId = 7,
                VolumeNumber = 1
            };

            Mocker.GetMock<IMangaSeriesService>()
                .Setup(x => x.GetAllSeries())
                .Returns(new List<MangaSeries> { _series });

            Mocker.GetMock<IVolumeRepository>()
                .Setup(x => x.All())
                .Returns(new List<Volume> { _volume });

            Mocker.GetMock<IMangaFileService>()
                .Setup(x => x.GetFilesBySeries(It.IsAny<int>()))
                .Returns(new List<MangaFile>());

            Mocker.GetMock<IMangaNamingService>()
                .Setup(x => x.GetSeriesFolder(It.IsAny<MangaSeries>(), null))
                .Returns("Solo Leveling");

            Mocker.GetMock<IMangaNamingService>()
                .Setup(x => x.GetVolumeFileName(It.IsAny<MangaSeries>(), It.IsAny<Volume>(), null))
                .Returns("Solo Leveling Vol.1 (2021).cbz");

            Mocker.GetMock<IMangaNamingService>()
                .Setup(x => x.GetChapterFileName(It.IsAny<MangaSeries>(), It.IsAny<Volume>(), It.IsAny<Chapter>(), null))
                .Returns<MangaSeries, Volume, Chapter, string>((series, volume, chapter, template) =>
                    $"Solo Leveling Vol.{volume.VolumeNumber} Ch.{chapter.ChapterNumber} (2021).cbz");
        }

        // Reproduces the live bug: a completed SABnzbd download titled
        // "Yen.Press-Solo.Leveling.Vol.01.2021.Retail.Comic" logged "No matching library series
        // found" even though a "Solo Leveling" series exists, because its CleanName was still null
        // (not yet backfilled by the housekeeper) and FindMatchingSeries excluded any series with
        // an empty CleanName from matching entirely. The payload is a retail PDF, not a CBZ/image
        // set, and must be moved (not converted) with its extension preserved.
        [Test]
        public async Task should_import_retail_pdf_matching_series_with_unbackfilled_clean_name()
        {
            const string outputPath = "/downloads/complete/manga/Yen.Press-Solo.Leveling.Vol.01.2021.Retail.Comic";
            const string sourceFile = outputPath + "/bb-Solo.Leveling.Vol.1.Comic..pdf";

            Mocker.GetMock<IMangaDownloadService>()
                .Setup(x => x.GetActiveDownloads())
                .ReturnsAsync(new List<MangaDownloadStatus>
                {
                    new MangaDownloadStatus
                    {
                        DownloadId = "a63494dc-8a8c-47c4-b5c2-469e937b0ea8",
                        Title = "Yen.Press-Solo.Leveling.Vol.01.2021.Retail.Comic",
                        Status = DownloadItemStatus.Completed,
                        OutputPath = outputPath
                    }
                });

            Mocker.GetMock<IDiskProvider>()
                .Setup(x => x.FolderExists(outputPath))
                .Returns(true);

            Mocker.GetMock<IDiskProvider>()
                .Setup(x => x.GetFiles(outputPath, true))
                .Returns(new List<string> { sourceFile });

            Mocker.GetMock<IDiskProvider>()
                .Setup(x => x.FileExists(It.IsAny<string>()))
                .Returns(false);

            string capturedTarget = null;
            Mocker.GetMock<IDiskProvider>()
                .Setup(x => x.MoveFile(sourceFile, It.IsAny<string>(), It.IsAny<bool>()))
                .Callback<string, string, bool>((source, dest, overwrite) => capturedTarget = dest);

            await Subject.CheckForCompletedDownloadsAsync();

            Assert.That(capturedTarget, Is.Not.Null);
            Assert.That(capturedTarget, Does.EndWith(".pdf"));
            Assert.That(capturedTarget, Does.Contain("Solo Leveling"));

            Mocker.GetMock<IMangaFileService>()
                .Verify(
                    x => x.Add(It.Is<MangaFile>(f =>
                        f.MangaSeriesId == _series.Id &&
                        f.VolumeId == _volume.Id &&
                        f.Path == capturedTarget)),
                    Times.Once);
        }

        [Test]
        public async Task should_import_when_clean_name_is_populated_despite_publisher_prefix_in_release_title()
        {
            _series.CleanName = "Solo Leveling".CleanAuthorName();

            const string outputPath = "/downloads/complete/manga/Yen.Press-Solo.Leveling.Vol.01.2021.Retail.Comic";
            const string sourceFile = outputPath + "/bb-Solo.Leveling.Vol.1.Comic..pdf";

            Mocker.GetMock<IMangaDownloadService>()
                .Setup(x => x.GetActiveDownloads())
                .ReturnsAsync(new List<MangaDownloadStatus>
                {
                    new MangaDownloadStatus
                    {
                        DownloadId = "a63494dc-8a8c-47c4-b5c2-469e937b0ea8",
                        Title = "Yen.Press-Solo.Leveling.Vol.01.2021.Retail.Comic",
                        Status = DownloadItemStatus.Completed,
                        OutputPath = outputPath
                    }
                });

            Mocker.GetMock<IDiskProvider>()
                .Setup(x => x.FolderExists(outputPath))
                .Returns(true);

            Mocker.GetMock<IDiskProvider>()
                .Setup(x => x.GetFiles(outputPath, true))
                .Returns(new List<string> { sourceFile });

            Mocker.GetMock<IDiskProvider>()
                .Setup(x => x.FileExists(It.IsAny<string>()))
                .Returns(false);

            await Subject.CheckForCompletedDownloadsAsync();

            Mocker.GetMock<IMangaFileService>()
                .Verify(x => x.Add(It.IsAny<MangaFile>()), Times.Once);
        }

        [Test]
        public async Task should_not_process_download_when_series_title_has_no_library_match()
        {
            const string outputPath = "/downloads/complete/manga/Some.Other.Series.Vol.01.Retail.Comic";
            const string sourceFile = outputPath + "/file.pdf";

            Mocker.GetMock<IMangaDownloadService>()
                .Setup(x => x.GetActiveDownloads())
                .ReturnsAsync(new List<MangaDownloadStatus>
                {
                    new MangaDownloadStatus
                    {
                        DownloadId = "unrelated-download-id",
                        Title = "Some.Other.Series.Vol.01.Retail.Comic",
                        Status = DownloadItemStatus.Completed,
                        OutputPath = outputPath
                    }
                });

            Mocker.GetMock<IDiskProvider>()
                .Setup(x => x.FolderExists(outputPath))
                .Returns(true);

            Mocker.GetMock<IDiskProvider>()
                .Setup(x => x.GetFiles(outputPath, true))
                .Returns(new List<string> { sourceFile });

            await Subject.CheckForCompletedDownloadsAsync();

            Mocker.GetMock<IMangaFileService>()
                .Verify(x => x.Add(It.IsAny<MangaFile>()), Times.Never);
        }

        [Test]
        public async Task should_use_chapter_name_for_packaged_chapter_downloads()
        {
            const string outputPath = "/downloads/complete/manga/Solo.Leveling.Vol.01.Ch.3";
            const string sourceFile = outputPath + "/Solo.Leveling.Vol.01.Ch.3.cbz";

            Mocker.GetMock<IMangaDownloadService>()
                .Setup(x => x.GetActiveDownloads())
                .ReturnsAsync(new List<MangaDownloadStatus>
                {
                    new MangaDownloadStatus
                    {
                        DownloadId = "chapter-download-id",
                        Title = "Solo.Leveling.Vol.01.Ch.3",
                        Status = DownloadItemStatus.Completed,
                        OutputPath = outputPath
                    }
                });

            Mocker.GetMock<IDiskProvider>()
                .Setup(x => x.FolderExists(outputPath))
                .Returns(true);
            Mocker.GetMock<IDiskProvider>()
                .Setup(x => x.GetFiles(outputPath, true))
                .Returns(new List<string> { sourceFile });
            Mocker.GetMock<IDiskProvider>()
                .Setup(x => x.FileExists(It.IsAny<string>()))
                .Returns(false);

            string capturedTarget = null;
            Mocker.GetMock<IDiskProvider>()
                .Setup(x => x.MoveFile(sourceFile, It.IsAny<string>(), It.IsAny<bool>()))
                .Callback<string, string, bool>((source, destination, overwrite) => capturedTarget = destination);

            await Subject.CheckForCompletedDownloadsAsync();

            Assert.That(capturedTarget, Does.Contain("Ch.3"));
            Mocker.GetMock<IMangaFileService>()
                .Verify(x => x.Add(It.Is<MangaFile>(f => f.Path == capturedTarget)), Times.Once);
        }
    }
}
