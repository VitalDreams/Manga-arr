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

        // --- Monitoring-path regression tests ---
        // MangaMonitoringService calls SearchAndDownloadAsync(series, volume) with an inline
        // Volume that has Id=0. These tests verify the overload resolves the DB volume
        // before passing it to MangaDex/Prowlarr download paths.

        [Test]
        public async Task monitoring_overload_should_resolve_db_volume_before_mangadex_download()
        {
            // Simulate monitoring: caller passes an inline Volume with Id=0
            var inlineVolume = new Volume
            {
                Id = 0,
                VolumeNumber = 1,
                Title = "Berserk Vol. 001"
            };

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
                .Setup(x => x.DownloadVolumeAsync(
                    _series.RootFolderPath, _series,
                    It.Is<Volume>(v => v.Id == 1001 && v.VolumeNumber == 1)))
                .ReturnsAsync("/manga/Berserk/Berserk Vol. 001.cbz");

            var result = await Subject.SearchAndDownloadAsync(_series, inlineVolume);

            Assert.That(result.Success, Is.True);
            Assert.That(result.DownloadedVolumes, Has.Count.EqualTo(1));
            Assert.That(result.DownloadedVolumes[0].Source, Is.EqualTo("MangaDex"));

            // Critical: the persisted Volume (Id=1001) must be passed, not the inline stub (Id=0)
            Mocker.GetMock<IMangaDexDownloader>()
                .Verify(x => x.DownloadVolumeAsync(
                    _series.RootFolderPath,
                    _series,
                    It.Is<Volume>(v => v.Id == 1001 && v.VolumeNumber == 1)),
                    Times.Once());
        }

        [Test]
        public async Task monitoring_overload_should_resolve_db_volume_for_prowlarr_fallback()
        {
            var inlineVolume = new Volume
            {
                Id = 0,
                VolumeNumber = 1,
                Title = "Berserk Vol. 001"
            };

            Mocker.GetMock<IVolumeRepository>()
                .Setup(x => x.FindBySeriesAndVolumeNumber(18, 1))
                .Returns(_volume1);

            // MangaDex has no chapters for this volume
            Mocker.GetMock<IMangaMetadataConnector>()
                .Setup(x => x.GetVolumeChapterMapAsync(_series.ForeignMangaId))
                .ReturnsAsync(new VolumeChapterMap
                {
                    ForeignMangaId = _series.ForeignMangaId,
                    VolumeChapters = new Dictionary<int, List<string>>()
                });

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
                    DownloadId = "dl-456",
                    Title = "Berserk Vol 1",
                    ClientName = "qBittorrent"
                });

            var result = await Subject.SearchAndDownloadAsync(_series, inlineVolume);

            Assert.That(result.Success, Is.True);
            Assert.That(result.DownloadedVolumes, Has.Count.EqualTo(1));
            Assert.That(result.DownloadedVolumes[0].Source, Is.EqualTo("Prowlarr"));

            // Critical: the persisted Volume (Id=1001) must be passed to Prowlarr path too
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
        public async Task monitoring_overload_should_trigger_komga_and_notification_on_mangadex_success()
        {
            var inlineVolume = new Volume
            {
                Id = 0,
                VolumeNumber = 1,
                Title = "Berserk Vol. 001"
            };

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
                        { 1, new List<string> { "ch-1" } }
                    }
                });

            Mocker.GetMock<IMangaDexDownloader>()
                .Setup(x => x.DownloadVolumeAsync(
                    _series.RootFolderPath, _series,
                    It.Is<Volume>(v => v.Id == 1001)))
                .ReturnsAsync("/manga/Berserk/Berserk Vol. 001.cbz");

            var result = await Subject.SearchAndDownloadAsync(_series, inlineVolume);

            Assert.That(result.Success, Is.True);

            // Komga scan and notification must fire on MangaDex success path
            Mocker.GetMock<IKomgaIntegration>()
                .Verify(x => x.TriggerLibraryScanAsync(), Times.Once());

            Mocker.GetMock<INotificationService>()
                .Verify(x => x.SendAsync(It.Is<Notification>(n =>
                    n.Title == "New Manga Volume Downloaded")),
                    Times.Once());
        }

        [Test]
        public async Task monitoring_overload_should_fallback_to_inline_volume_when_not_in_db()
        {
            var inlineVolume = new Volume
            {
                Id = 0,
                VolumeNumber = 7,
                Title = "Berserk Vol. 007"
            };

            // Volume not in DB
            Mocker.GetMock<IVolumeRepository>()
                .Setup(x => x.FindBySeriesAndVolumeNumber(18, 7))
                .Returns((Volume)null);

            Mocker.GetMock<IMangaMetadataConnector>()
                .Setup(x => x.GetVolumeChapterMapAsync(_series.ForeignMangaId))
                .ReturnsAsync(new VolumeChapterMap
                {
                    ForeignMangaId = _series.ForeignMangaId,
                    VolumeChapters = new Dictionary<int, List<string>>
                    {
                        { 7, new List<string> { "ch-30" } }
                    }
                });

            Mocker.GetMock<IMangaDexDownloader>()
                .Setup(x => x.DownloadVolumeAsync(
                    _series.RootFolderPath, _series,
                    It.Is<Volume>(v => v.Id == 0 && v.VolumeNumber == 7)))
                .ReturnsAsync("/manga/Berserk/Berserk Vol. 007.cbz");

            var result = await Subject.SearchAndDownloadAsync(_series, inlineVolume);

            Assert.That(result.Success, Is.True);
            Assert.That(result.DownloadedVolumes[0].VolumeNumber, Is.EqualTo(7));

            // Fallback: inline volume with Id=0 is used when DB has no row
            Mocker.GetMock<IMangaDexDownloader>()
                .Verify(x => x.DownloadVolumeAsync(
                    _series.RootFolderPath, _series,
                    It.Is<Volume>(v => v.Id == 0 && v.VolumeNumber == 7)),
                    Times.Once());
        }

        // --- 429/failure fallback tests ---

        [Test]
        public async Task search_should_fallback_to_prowlarr_when_mangadex_download_returns_null()
        {
            // MangaDex download returns null (e.g. all pages failed with 429)
            Mocker.GetMock<IMangaSeriesService>()
                .Setup(x => x.GetSeries(18))
                .Returns(_series);

            Mocker.GetMock<IVolumeRepository>()
                .Setup(x => x.FindBySeriesAndVolumeNumber(18, 42))
                .Returns(new Volume { Id = 4200, VolumeNumber = 42, Title = "Volume 42", MangaSeriesId = 18 });

            Mocker.GetMock<IMangaMetadataConnector>()
                .Setup(x => x.GetVolumeChapterMapAsync(_series.ForeignMangaId))
                .ReturnsAsync(new VolumeChapterMap
                {
                    ForeignMangaId = _series.ForeignMangaId,
                    VolumeChapters = new Dictionary<int, List<string>>
                    {
                        { 42, new List<string> { "ch-200", "ch-201" } }
                    }
                });

            // MangaDex download fails (returns null — simulating 429 page failures)
            Mocker.GetMock<IMangaDexDownloader>()
                .Setup(x => x.DownloadVolumeAsync(
                    _series.RootFolderPath, _series,
                    It.Is<Volume>(v => v.VolumeNumber == 42)))
                .ReturnsAsync((string)null);

            // Prowlarr fallback succeeds
            Mocker.GetMock<IProwlarrConnector>()
                .Setup(x => x.IsConfigured)
                .Returns(true);

            Mocker.GetMock<IProwlarrConnector>()
                .Setup(x => x.SearchMangaVolumePacksAsync("Berserk", 42))
                .ReturnsAsync(new List<ProwlarrSearchResult>
                {
                    new ProwlarrSearchResult
                    {
                        Title = "Berserk Vol 42",
                        DownloadUrl = "http://example.com/nzb",
                        Seeders = 0,
                        Protocol = DownloadProtocol.Usenet,
                        Indexer = "NZBgeek"
                    }
                });

            Mocker.GetMock<IProwlarrConnector>()
                .Setup(x => x.GetDownloadProtocol(It.IsAny<ProwlarrSearchResult>()))
                .Returns(DownloadProtocol.Usenet);

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
                    DownloadId = "nzb-42",
                    Title = "Berserk Vol 42",
                    Protocol = DownloadProtocol.Usenet,
                    ClientName = "SABnzbd"
                });

            var result = await Subject.SearchAndDownloadAsync(18, 42);

            Assert.That(result.Success, Is.True);
            Assert.That(result.DownloadedVolumes, Has.Count.EqualTo(1));
            Assert.That(result.DownloadedVolumes[0].Source, Is.EqualTo("Prowlarr"));
            Assert.That(result.DownloadedVolumes[0].DownloadId, Is.EqualTo("nzb-42"));

            // Verify Prowlarr was called
            Mocker.GetMock<IProwlarrConnector>()
                .Verify(x => x.SearchMangaVolumePacksAsync("Berserk", 42), Times.Once);

            // Verify Usenet protocol was passed to download client
            Mocker.GetMock<IMangaDownloadService>()
                .Verify(x => x.SendToDownloadClient(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    DownloadProtocol.Usenet,
                    _series,
                    It.Is<Volume>(v => v.Id == 4200)),
                    Times.Once());
        }

        [Test]
        public async Task monitoring_overload_should_fallback_to_prowlarr_when_mangadex_returns_null()
        {
            var inlineVolume = new Volume
            {
                Id = 0,
                VolumeNumber = 42,
                Title = "Berserk Vol. 042"
            };

            Mocker.GetMock<IVolumeRepository>()
                .Setup(x => x.FindBySeriesAndVolumeNumber(18, 42))
                .Returns(new Volume { Id = 4200, VolumeNumber = 42, Title = "Volume 42", MangaSeriesId = 18 });

            Mocker.GetMock<IMangaMetadataConnector>()
                .Setup(x => x.GetVolumeChapterMapAsync(_series.ForeignMangaId))
                .ReturnsAsync(new VolumeChapterMap
                {
                    ForeignMangaId = _series.ForeignMangaId,
                    VolumeChapters = new Dictionary<int, List<string>>
                    {
                        { 42, new List<string> { "ch-200" } }
                    }
                });

            // MangaDex download fails (null = 429/page failure)
            Mocker.GetMock<IMangaDexDownloader>()
                .Setup(x => x.DownloadVolumeAsync(
                    _series.RootFolderPath, _series,
                    It.Is<Volume>(v => v.VolumeNumber == 42)))
                .ReturnsAsync((string)null);

            Mocker.GetMock<IProwlarrConnector>()
                .Setup(x => x.IsConfigured)
                .Returns(true);

            Mocker.GetMock<IProwlarrConnector>()
                .Setup(x => x.SearchMangaVolumePacksAsync("Berserk", 42))
                .ReturnsAsync(new List<ProwlarrSearchResult>
                {
                    new ProwlarrSearchResult
                    {
                        Title = "Berserk Vol 42",
                        DownloadUrl = "http://example.com/nzb",
                        Seeders = 0,
                        Protocol = DownloadProtocol.Usenet,
                        Indexer = "NZBgeek"
                    }
                });

            Mocker.GetMock<IProwlarrConnector>()
                .Setup(x => x.GetDownloadProtocol(It.IsAny<ProwlarrSearchResult>()))
                .Returns(DownloadProtocol.Usenet);

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
                    DownloadId = "nzb-42",
                    Title = "Berserk Vol 42",
                    Protocol = DownloadProtocol.Usenet,
                    ClientName = "SABnzbd"
                });

            var result = await Subject.SearchAndDownloadAsync(_series, inlineVolume);

            Assert.That(result.Success, Is.True);
            Assert.That(result.DownloadedVolumes[0].Source, Is.EqualTo("Prowlarr"));

            // DB volume (Id=4200) must be passed, not inline stub
            Mocker.GetMock<IMangaDownloadService>()
                .Verify(x => x.SendToDownloadClient(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    DownloadProtocol.Usenet,
                    _series,
                    It.Is<Volume>(v => v.Id == 4200)),
                    Times.Once());
        }

        // --- Mixed torrent/Usenet selection tests ---

        [Test]
        public async Task prowlarr_fallback_should_select_usenet_over_torrent_with_seeders()
        {
            Mocker.GetMock<IMangaSeriesService>()
                .Setup(x => x.GetSeries(18))
                .Returns(_series);

            Mocker.GetMock<IVolumeRepository>()
                .Setup(x => x.FindBySeriesAndVolumeNumber(18, 1))
                .Returns(_volume1);

            // MangaDex has no chapters
            Mocker.GetMock<IMangaMetadataConnector>()
                .Setup(x => x.GetVolumeChapterMapAsync(_series.ForeignMangaId))
                .ReturnsAsync(new VolumeChapterMap
                {
                    ForeignMangaId = _series.ForeignMangaId,
                    VolumeChapters = new Dictionary<int, List<string>>()
                });

            Mocker.GetMock<IProwlarrConnector>()
                .Setup(x => x.IsConfigured)
                .Returns(true);

            // Return both torrent (with seeders) and Usenet (zero seeders)
            Mocker.GetMock<IProwlarrConnector>()
                .Setup(x => x.SearchMangaVolumePacksAsync("Berserk", 1))
                .ReturnsAsync(new List<ProwlarrSearchResult>
                {
                    new ProwlarrSearchResult
                    {
                        Title = "Berserk Vol 1 [torrent]",
                        DownloadUrl = "http://example.com/torrent",
                        Seeders = 50,
                        Size = 500_000_000,
                        Protocol = DownloadProtocol.Torrent,
                        Indexer = "Nyaa"
                    },
                    new ProwlarrSearchResult
                    {
                        Title = "Berserk Vol 1 [nzb]",
                        DownloadUrl = "http://example.com/nzb",
                        Seeders = 0,
                        Size = 450_000_000,
                        Protocol = DownloadProtocol.Usenet,
                        Indexer = "NZBgeek"
                    }
                });

            Mocker.GetMock<IProwlarrConnector>()
                .Setup(x => x.GetDownloadProtocol(It.Is<ProwlarrSearchResult>(r => r.Protocol == DownloadProtocol.Usenet)))
                .Returns(DownloadProtocol.Usenet);
            Mocker.GetMock<IProwlarrConnector>()
                .Setup(x => x.GetDownloadProtocol(It.Is<ProwlarrSearchResult>(r => r.Protocol == DownloadProtocol.Torrent)))
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
                    DownloadId = "nzb-001",
                    Title = "Berserk Vol 1 [nzb]",
                    Protocol = DownloadProtocol.Usenet,
                    ClientName = "SABnzbd"
                });

            var result = await Subject.SearchAndDownloadAsync(18, 1);

            Assert.That(result.Success, Is.True);

            // Usenet (NZBgeek) should be selected despite zero seeders
            Mocker.GetMock<IMangaDownloadService>()
                .Verify(x => x.SendToDownloadClient(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    DownloadProtocol.Usenet,
                    _series,
                    It.IsAny<Volume>()),
                    Times.Once());
        }

        [Test]
        public async Task prowlarr_fallback_should_select_torrent_when_only_torrents_available()
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
                    VolumeChapters = new Dictionary<int, List<string>>()
                });

            Mocker.GetMock<IProwlarrConnector>()
                .Setup(x => x.IsConfigured)
                .Returns(true);

            Mocker.GetMock<IProwlarrConnector>()
                .Setup(x => x.SearchMangaVolumePacksAsync("Berserk", 1))
                .ReturnsAsync(new List<ProwlarrSearchResult>
                {
                    new ProwlarrSearchResult
                    {
                        Title = "Berserk Vol 1 [low seeders]",
                        DownloadUrl = "http://example.com/t1",
                        Seeders = 5,
                        Size = 500_000_000,
                        Protocol = DownloadProtocol.Torrent,
                        Indexer = "Nyaa"
                    },
                    new ProwlarrSearchResult
                    {
                        Title = "Berserk Vol 1 [high seeders]",
                        DownloadUrl = "http://example.com/t2",
                        Seeders = 100,
                        Size = 480_000_000,
                        Protocol = DownloadProtocol.Torrent,
                        Indexer = "Nyaa"
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
                    DownloadId = "tor-001",
                    Title = "Berserk Vol 1 [high seeders]",
                    Protocol = DownloadProtocol.Torrent,
                    ClientName = "qBittorrent"
                });

            var result = await Subject.SearchAndDownloadAsync(18, 1);

            Assert.That(result.Success, Is.True);

            // Highest seeders torrent should be selected
            Mocker.GetMock<IMangaDownloadService>()
                .Verify(x => x.SendToDownloadClient(
                    It.IsAny<string>(),
                    "http://example.com/t2",
                    DownloadProtocol.Torrent,
                    _series,
                    It.IsAny<Volume>()),
                    Times.Once());
        }

        [Test]
        public async Task prowlarr_fallback_should_select_usenet_when_only_usenet_available()
        {
            // Different manhwa series entirely - proves the selection logic is generic,
            // not tied to any specific title.
            var manhwaSeries = new MangaSeries
            {
                Id = 99,
                Name = "Solo Leveling",
                Monitored = true,
                ForeignMangaId = "b1c42e49-d6f5-4084-9cec-771f5660c911",
                Path = "/manga/Solo Leveling",
                RootFolderPath = "/manga"
            };

            var manhwaVolume1 = new Volume
            {
                Id = 2001,
                VolumeNumber = 1,
                Title = "Volume 1",
                MangaSeriesId = 99,
                Monitored = true
            };

            Mocker.GetMock<IMangaSeriesService>()
                .Setup(x => x.GetSeries(99))
                .Returns(manhwaSeries);

            Mocker.GetMock<IVolumeRepository>()
                .Setup(x => x.FindBySeriesAndVolumeNumber(99, 1))
                .Returns(manhwaVolume1);

            Mocker.GetMock<IMangaMetadataConnector>()
                .Setup(x => x.GetVolumeChapterMapAsync(manhwaSeries.ForeignMangaId))
                .ReturnsAsync(new VolumeChapterMap
                {
                    ForeignMangaId = manhwaSeries.ForeignMangaId,
                    VolumeChapters = new Dictionary<int, List<string>>()
                });

            Mocker.GetMock<IProwlarrConnector>()
                .Setup(x => x.IsConfigured)
                .Returns(true);

            Mocker.GetMock<IProwlarrConnector>()
                .Setup(x => x.SearchMangaVolumePacksAsync("Solo Leveling", 1))
                .ReturnsAsync(new List<ProwlarrSearchResult>
                {
                    new ProwlarrSearchResult
                    {
                        Title = "Solo Leveling Vol 1",
                        DownloadUrl = "http://example.com/nzb",
                        Seeders = 0,
                        Size = 300_000_000,
                        Protocol = DownloadProtocol.Usenet,
                        Indexer = "NZBgeek"
                    }
                });

            Mocker.GetMock<IProwlarrConnector>()
                .Setup(x => x.GetDownloadProtocol(It.IsAny<ProwlarrSearchResult>()))
                .Returns(DownloadProtocol.Usenet);

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
                    DownloadId = "nzb-solo-1",
                    Title = "Solo Leveling Vol 1",
                    Protocol = DownloadProtocol.Usenet,
                    ClientName = "SABnzbd"
                });

            var result = await Subject.SearchAndDownloadAsync(99, 1);

            Assert.That(result.Success, Is.True);

            Mocker.GetMock<IMangaDownloadService>()
                .Verify(x => x.SendToDownloadClient(
                    It.IsAny<string>(),
                    "http://example.com/nzb",
                    DownloadProtocol.Usenet,
                    manhwaSeries,
                    It.IsAny<Volume>()),
                    Times.Once());
        }

        // --- Release-format validation regression tests ---
        // Reproduces the live incident: MangaSearchService selected an audiobook release
        // ("Solo Leveling - Vol 1-8 - ... [m4b mp3] [AUDIOBOOK]") that matched title and
        // volume, and sent it to qBittorrent. These verify invalid releases never reach
        // SendToDownloadClient, even if a mocked/alternate IProwlarrConnector returns them.

        [Test]
        public async Task prowlarr_fallback_should_never_send_audiobook_release_to_download_client()
        {
            // Solo Leveling is a manhwa - matches the live bug report exactly.
            var manhwaSeries = new MangaSeries
            {
                Id = 99,
                Name = "Solo Leveling",
                Monitored = true,
                ForeignMangaId = "b1c42e49-d6f5-4084-9cec-771f5660c911",
                Path = "/manga/Solo Leveling",
                RootFolderPath = "/manga"
            };

            var manhwaVolume1 = new Volume
            {
                Id = 2001,
                VolumeNumber = 1,
                Title = "Volume 1",
                MangaSeriesId = 99,
                Monitored = true
            };

            Mocker.GetMock<IMangaSeriesService>()
                .Setup(x => x.GetSeries(99))
                .Returns(manhwaSeries);

            Mocker.GetMock<IVolumeRepository>()
                .Setup(x => x.FindBySeriesAndVolumeNumber(99, 1))
                .Returns(manhwaVolume1);

            Mocker.GetMock<IMangaMetadataConnector>()
                .Setup(x => x.GetVolumeChapterMapAsync(manhwaSeries.ForeignMangaId))
                .ReturnsAsync(new VolumeChapterMap
                {
                    ForeignMangaId = manhwaSeries.ForeignMangaId,
                    VolumeChapters = new Dictionary<int, List<string>>()
                });

            Mocker.GetMock<IProwlarrConnector>()
                .Setup(x => x.IsConfigured)
                .Returns(true);

            // The exact release from the incident: title matches title and volume, but it's
            // an audiobook, not manga.
            Mocker.GetMock<IProwlarrConnector>()
                .Setup(x => x.SearchMangaVolumePacksAsync("Solo Leveling", 1))
                .ReturnsAsync(new List<ProwlarrSearchResult>
                {
                    new ProwlarrSearchResult
                    {
                        Title = "Solo Leveling - Vol 1-8 - Chugong, Hye Young Im, J Torres [m4b mp3] [AUDIOBOOK]",
                        DownloadUrl = "http://example.com/audiobook.torrent",
                        Seeders = 12,
                        Size = 3304976640,
                        Protocol = DownloadProtocol.Torrent,
                        Indexer = "TorrentLeech"
                    }
                });

            var result = await Subject.SearchAndDownloadAsync(99, 1);

            ExceptionVerification.ExpectedWarns(1);

            Assert.That(result.Success, Is.False);
            Assert.That(result.FailedVolumes, Has.Count.EqualTo(1));

            Mocker.GetMock<IMangaDownloadService>()
                .Verify(x => x.SendToDownloadClient(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<DownloadProtocol>(),
                    It.IsAny<MangaSeries>(),
                    It.IsAny<Volume>()),
                    Times.Never());
        }

        [Test]
        public async Task prowlarr_fallback_should_skip_audiobook_and_download_valid_usenet_release_instead()
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
                    VolumeChapters = new Dictionary<int, List<string>>()
                });

            Mocker.GetMock<IProwlarrConnector>()
                .Setup(x => x.IsConfigured)
                .Returns(true);

            // Mix an invalid audiobook torrent (high seeders) with a valid NZB manga release
            // (zero seeders, as Usenet always reports). Usenet-first priority plus format
            // validation must select the valid NZB, never the audiobook.
            Mocker.GetMock<IProwlarrConnector>()
                .Setup(x => x.SearchMangaVolumePacksAsync("Berserk", 1))
                .ReturnsAsync(new List<ProwlarrSearchResult>
                {
                    new ProwlarrSearchResult
                    {
                        Title = "Berserk Vol 1 [m4b mp3] [AUDIOBOOK]",
                        DownloadUrl = "http://example.com/audiobook.torrent",
                        Seeders = 500,
                        Size = 3000000000,
                        Protocol = DownloadProtocol.Torrent,
                        Indexer = "TorrentLeech"
                    },
                    new ProwlarrSearchResult
                    {
                        Title = "Berserk Vol 1 [CBZ]",
                        DownloadUrl = "http://example.com/valid.nzb",
                        Seeders = 0,
                        Size = 200000000,
                        Protocol = DownloadProtocol.Usenet,
                        Indexer = "NZBgeek"
                    }
                });

            Mocker.GetMock<IProwlarrConnector>()
                .Setup(x => x.GetDownloadProtocol(It.IsAny<ProwlarrSearchResult>()))
                .Returns(DownloadProtocol.Usenet);

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
                    DownloadId = "nzb-valid-1",
                    Title = "Berserk Vol 1 [CBZ]",
                    Protocol = DownloadProtocol.Usenet,
                    ClientName = "SABnzbd"
                });

            var result = await Subject.SearchAndDownloadAsync(18, 1);

            Assert.That(result.Success, Is.True);
            Assert.That(result.DownloadedVolumes[0].Source, Is.EqualTo("Prowlarr"));

            Mocker.GetMock<IMangaDownloadService>()
                .Verify(x => x.SendToDownloadClient(
                    It.IsAny<string>(),
                    "http://example.com/valid.nzb",
                    DownloadProtocol.Usenet,
                    _series,
                    It.IsAny<Volume>()),
                    Times.Once());

            // The audiobook release must never be sent to the download client
            Mocker.GetMock<IMangaDownloadService>()
                .Verify(x => x.SendToDownloadClient(
                    It.IsAny<string>(),
                    "http://example.com/audiobook.torrent",
                    It.IsAny<DownloadProtocol>(),
                    It.IsAny<MangaSeries>(),
                    It.IsAny<Volume>()),
                    Times.Never());
        }

        [Test]
        public async Task prowlarr_fallback_should_skip_invalid_usenet_and_download_valid_torrent_instead()
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
                    VolumeChapters = new Dictionary<int, List<string>>()
                });

            Mocker.GetMock<IProwlarrConnector>()
                .Setup(x => x.IsConfigured)
                .Returns(true);

            // Mirror of the audiobook-torrent-vs-valid-usenet case, but inverted: the only
            // Usenet release is an invalid format (audiobook), while a valid manga torrent
            // exists. Usenet priority must not override format validation - the invalid
            // Usenet release must be skipped and the valid torrent selected as fallback.
            Mocker.GetMock<IProwlarrConnector>()
                .Setup(x => x.SearchMangaVolumePacksAsync("Berserk", 1))
                .ReturnsAsync(new List<ProwlarrSearchResult>
                {
                    new ProwlarrSearchResult
                    {
                        Title = "Berserk Vol 1 [m4b mp3] [AUDIOBOOK]",
                        DownloadUrl = "http://example.com/audiobook.nzb",
                        Seeders = 0,
                        Size = 3000000000,
                        Protocol = DownloadProtocol.Usenet,
                        Indexer = "NZBgeek"
                    },
                    new ProwlarrSearchResult
                    {
                        Title = "Berserk Vol 1 [CBZ]",
                        DownloadUrl = "http://example.com/valid.torrent",
                        Seeders = 25,
                        Size = 200000000,
                        Protocol = DownloadProtocol.Torrent,
                        Indexer = "Nyaa"
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
                    DownloadId = "tor-valid-1",
                    Title = "Berserk Vol 1 [CBZ]",
                    Protocol = DownloadProtocol.Torrent,
                    ClientName = "qBittorrent"
                });

            var result = await Subject.SearchAndDownloadAsync(18, 1);

            Assert.That(result.Success, Is.True);
            Assert.That(result.DownloadedVolumes[0].Source, Is.EqualTo("Prowlarr"));

            Mocker.GetMock<IMangaDownloadService>()
                .Verify(x => x.SendToDownloadClient(
                    It.IsAny<string>(),
                    "http://example.com/valid.torrent",
                    DownloadProtocol.Torrent,
                    _series,
                    It.IsAny<Volume>()),
                    Times.Once());

            // The invalid audiobook NZB must never be sent to the download client
            Mocker.GetMock<IMangaDownloadService>()
                .Verify(x => x.SendToDownloadClient(
                    It.IsAny<string>(),
                    "http://example.com/audiobook.nzb",
                    It.IsAny<DownloadProtocol>(),
                    It.IsAny<MangaSeries>(),
                    It.IsAny<Volume>()),
                    Times.Never());
        }
    }
}
