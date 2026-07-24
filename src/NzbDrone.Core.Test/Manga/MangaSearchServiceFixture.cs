using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Manga;
using NzbDrone.Core.Manga.Connectors;
using NzbDrone.Core.Manga.Download;
using NzbDrone.Core.Manga.Monitoring;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.Manga
{
    [TestFixture]
    public class MangaSearchServiceFixture : CoreTest<MangaSearchService>
    {
        private MangaSeries _series;
        private Volume _volume1;

        [SetUp]
        public void Setup()
        {
            _series = new MangaSeries
            {
                Id = 18,
                Name = "Berserk",
                Monitored = true,
                ForeignMangaId = "a8c42e49-d6f5-4084-9cec-771f5660c90f",
                Path = "/manga/Berserk",
                RootFolderPath = "/manga",
                Metadata = new LazyLoaded<MangaMetadata>(new MangaMetadata
                {
                    Id = 10,
                    ForeignMangaId = "a8c42e49-d6f5-4084-9cec-771f5660c90f",
                    Title = "Berserk",
                    TotalVolumes = 43
                })
            };

            _volume1 = new Volume
            {
                Id = 1001,
                VolumeNumber = 1,
                Title = "Volume 1",
                MangaSeriesId = 18,
                Monitored = true
            };
        }

        [Test]
        public async Task search_should_resolve_volume_from_db_before_download()
        {
            Mocker.GetMock<IMangaSeriesService>()
                .Setup(x => x.GetSeries(18))
                .Returns(_series);

            Mocker.GetMock<IVolumeRepository>()
                .Setup(x => x.FindBySeriesAndVolumeNumber(18, 1))
                .Returns(_volume1);

            Mocker.GetMock<IMangaMetadataConnector>()
                .Setup(x => x.GetVolumeChapterMapAsync(_series.ForeignMangaId))
                .ReturnsAsync(new VolumeChapterMap
                {
                    ForeignMangaId = _series.ForeignMangaId,
                    VolumeChapters = new Dictionary<int, List<string>>
                    {
                        { 1, new List<string> { "ch-1", "ch-2" } }
                    }
                });

            Mocker.GetMock<IMangaDexDownloader>()
                .Setup(x => x.DownloadVolumeAsync(_series.RootFolderPath, _series, _volume1))
                .ReturnsAsync("/manga/Berserk/Berserk Vol. 001.cbz");

            var result = await Subject.SearchAndDownloadAsync(18, 1);

            Assert.That(result.Success, Is.True);
            Assert.That(result.DownloadedVolumes, Has.Count.EqualTo(1));
            Assert.That(result.DownloadedVolumes[0].VolumeNumber, Is.EqualTo(1));
            Assert.That(result.DownloadedVolumes[0].Source, Is.EqualTo("MangaDex"));

            // Verify the actual DB volume (Id=1001) was passed to the downloader, not a stub with Id=0
            Mocker.GetMock<IMangaDexDownloader>()
                .Verify(x => x.DownloadVolumeAsync(
                    _series.RootFolderPath,
                    _series,
                    It.Is<Volume>(v => v.Id == 1001 && v.VolumeNumber == 1)),
                    Times.Once());
        }

        [Test]
        public async Task search_should_fallback_to_inline_volume_when_not_in_db()
        {
            Mocker.GetMock<IMangaSeriesService>()
                .Setup(x => x.GetSeries(18))
                .Returns(_series);

            Mocker.GetMock<IVolumeRepository>()
                .Setup(x => x.FindBySeriesAndVolumeNumber(18, 5))
                .Returns((Volume)null);

            Mocker.GetMock<IMangaMetadataConnector>()
                .Setup(x => x.GetVolumeChapterMapAsync(_series.ForeignMangaId))
                .ReturnsAsync(new VolumeChapterMap
                {
                    ForeignMangaId = _series.ForeignMangaId,
                    VolumeChapters = new Dictionary<int, List<string>>
                    {
                        { 5, new List<string> { "ch-20" } }
                    }
                });

            Mocker.GetMock<IMangaDexDownloader>()
                .Setup(x => x.DownloadVolumeAsync(
                    _series.RootFolderPath,
                    _series,
                    It.Is<Volume>(v => v.VolumeNumber == 5)))
                .ReturnsAsync("/manga/Berserk/Berserk Vol. 005.cbz");

            var result = await Subject.SearchAndDownloadAsync(18, 5);

            Assert.That(result.Success, Is.True);
            Assert.That(result.DownloadedVolumes[0].VolumeNumber, Is.EqualTo(5));

            // The inline volume should have Id=0 since it wasn't in the DB
            Mocker.GetMock<IMangaDexDownloader>()
                .Verify(x => x.DownloadVolumeAsync(
                    _series.RootFolderPath,
                    _series,
                    It.Is<Volume>(v => v.Id == 0 && v.VolumeNumber == 5)),
                    Times.Once());
        }

        [Test]
        public async Task search_should_return_error_when_series_not_found()
        {
            Mocker.GetMock<IMangaSeriesService>()
                .Setup(x => x.GetSeries(999))
                .Returns((MangaSeries)null);

            var result = await Subject.SearchAndDownloadAsync(999, 1);

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("999"));
        }

        [Test]
        public async Task search_should_fallback_to_prowlarr_when_mangadex_has_no_chapters()
        {
            Mocker.GetMock<IMangaSeriesService>()
                .Setup(x => x.GetSeries(18))
                .Returns(_series);

            Mocker.GetMock<IVolumeRepository>()
                .Setup(x => x.FindBySeriesAndVolumeNumber(18, 1))
                .Returns(_volume1);

            // MangaDex returns empty volume map
            Mocker.GetMock<IMangaMetadataConnector>()
                .Setup(x => x.GetVolumeChapterMapAsync(_series.ForeignMangaId))
                .ReturnsAsync(new VolumeChapterMap
                {
                    ForeignMangaId = _series.ForeignMangaId,
                    VolumeChapters = new Dictionary<int, List<string>>()
                });

            // Prowlarr returns a result
            Mocker.GetMock<IProwlarrConnector>()
                .Setup(x => x.IsConfigured)
                .Returns(true);

            Mocker.GetMock<IProwlarrConnector>()
                .Setup(x => x.SearchMangaVolumePacksAsync("Berserk", 1))
                .ReturnsAsync(new List<ProwlarrSearchResult>
                {
                    new ProwlarrSearchResult
                    {
                        Title = "Berserk Vol 1",
                        DownloadUrl = "http://example.com/download",
                        Seeders = 10
                    }
                });

            Mocker.GetMock<IProwlarrConnector>()
                .Setup(x => x.GetDownloadProtocol(It.IsAny<ProwlarrSearchResult>()))
                .Returns(DownloadProtocol.Torrent);

            Mocker.GetMock<IMangaDownloadService>()
                .Setup(x => x.SendToDownloadClient(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<DownloadProtocol>(),
                    It.IsAny<MangaSeries>(),
                    It.IsAny<Volume>()))
                .ReturnsAsync(new MangaDownloadResult
                {
                    Success = true,
                    DownloadId = "dl-123",
                    Title = "Berserk Vol 1",
                    ClientName = "qBittorrent"
                });

            var result = await Subject.SearchAndDownloadAsync(18, 1);

            Assert.That(result.Success, Is.True);
            Assert.That(result.DownloadedVolumes, Has.Count.EqualTo(1));
            Assert.That(result.DownloadedVolumes[0].Source, Is.EqualTo("Prowlarr"));
            Assert.That(result.DownloadedVolumes[0].DownloadId, Is.EqualTo("dl-123"));

            // Verify the actual DB volume was passed to the download service
            Mocker.GetMock<IMangaDownloadService>()
                .Verify(x => x.SendToDownloadClient(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<DownloadProtocol>(),
                    _series,
                    It.Is<Volume>(v => v.Id == 1001)),
                    Times.Once());
        }

        [Test]
        public async Task search_should_report_failure_when_both_sources_fail()
        {
            Mocker.GetMock<IMangaSeriesService>()
                .Setup(x => x.GetSeries(18))
                .Returns(_series);

            Mocker.GetMock<IVolumeRepository>()
                .Setup(x => x.FindBySeriesAndVolumeNumber(18, 1))
                .Returns(_volume1);

            // MangaDex returns empty volume map
            Mocker.GetMock<IMangaMetadataConnector>()
                .Setup(x => x.GetVolumeChapterMapAsync(_series.ForeignMangaId))
                .ReturnsAsync(new VolumeChapterMap
                {
                    ForeignMangaId = _series.ForeignMangaId,
                    VolumeChapters = new Dictionary<int, List<string>>()
                });

            // Prowlarr not configured
            Mocker.GetMock<IProwlarrConnector>()
                .Setup(x => x.IsConfigured)
                .Returns(false);

            var result = await Subject.SearchAndDownloadAsync(18, 1);

            Assert.That(result.Success, Is.False);
            Assert.That(result.FailedVolumes, Has.Count.EqualTo(1));
            Assert.That(result.FailedVolumes[0].VolumeNumber, Is.EqualTo(1));
        }
    }
}
