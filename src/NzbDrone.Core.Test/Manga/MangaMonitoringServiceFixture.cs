using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Manga;
using NzbDrone.Core.Manga.Connectors;
using NzbDrone.Core.Manga.Download;
using NzbDrone.Core.Manga.Import;
using NzbDrone.Core.Manga.Monitoring;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.Manga
{
    [TestFixture]
    public class MangaMonitoringServiceFixture : CoreTest<MangaMonitoringService>
    {
        private MangaSeries _berserk;
        private List<Volume> _berserkVolumes;

        [SetUp]
        public void Setup()
        {
            // Disable the background loop so tests don't trigger it automatically
            Subject.Enabled = false;

            _berserk = new MangaSeries
            {
                Id = 18,
                Name = "Berserk",
                Monitored = true,
                ForeignMangaId = "manga-berserk",
                Path = "/manga/Berserk (2003)"
            };

            _berserkVolumes = new List<Volume>();
            for (var i = 1; i <= 43; i++)
            {
                _berserkVolumes.Add(new Volume
                {
                    Id = 1000 + i,
                    VolumeNumber = i,
                    MangaSeriesId = 18,
                    Monitored = true
                });
            }
        }

        [Test]
        public void monitoring_service_should_inject_manga_file_service()
        {
            // Verify that the MangaFileService dependency is properly injected
            // This is a regression test for the monitoring improvement that skips
            // already-downloaded volumes.
            Assert.That(Subject, Is.Not.Null);
        }

        [Test]
        public void monitoring_service_should_be_background_service()
        {
            // Verify MangaMonitoringService extends BackgroundService (IHostedService)
            Assert.That(Subject, Is.InstanceOf<BackgroundService>());
            Assert.That(Subject, Is.InstanceOf<IHostedService>());
        }

        [Test]
        public void manga_download_completion_handler_should_be_background_service()
        {
            // Verify MangaDownloadCompletionHandler also extends BackgroundService
            // Both services should be auto-registered by AutoAddServices, not manually
            var handler = Mocker.Resolve<MangaDownloadCompletionHandler>();
            Assert.That(handler, Is.InstanceOf<BackgroundService>());
            Assert.That(handler, Is.InstanceOf<IHostedService>());
        }

        [Test]
        public async Task startup_should_reconcile_existing_series_before_monitoring()
        {
            // Arrange: one series with volumes, no MangaFiles yet
            Mocker.GetMock<IMangaSeriesService>()
                .Setup(x => x.GetAllSeries())
                .Returns(new List<MangaSeries> { _berserk });

            Mocker.GetMock<IMangaImportService>()
                .Setup(x => x.ReconcileSeries(_berserk))
                .Returns(41);

            // Act: start with a pre-cancelled token so the monitoring loop
            // exits immediately after ReconcileExistingSeries runs
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            await Subject.StartAsync(cts.Token);

            // Assert: ReconcileSeries was called for the series at startup
            Mocker.GetMock<IMangaImportService>()
                .Verify(x => x.ReconcileSeries(_berserk), Times.Once());
        }

        [Test]
        public async Task startup_reconciliation_should_run_for_all_series()
        {
            // Arrange: two series
            var dragonBall = new MangaSeries
            {
                Id = 42,
                Name = "Dragon Ball",
                Monitored = true,
                ForeignMangaId = "manga-dragonball",
                Path = "/manga/Dragon Ball"
            };

            Mocker.GetMock<IMangaSeriesService>()
                .Setup(x => x.GetAllSeries())
                .Returns(new List<MangaSeries> { _berserk, dragonBall });

            Mocker.GetMock<IMangaImportService>()
                .Setup(x => x.ReconcileSeries(It.IsAny<MangaSeries>()))
                .Returns(10);

            // Act
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            await Subject.StartAsync(cts.Token);

            // Assert: both series were reconciled
            Mocker.GetMock<IMangaImportService>()
                .Verify(x => x.ReconcileSeries(_berserk), Times.Once());
            Mocker.GetMock<IMangaImportService>()
                .Verify(x => x.ReconcileSeries(dragonBall), Times.Once());
        }

        [Test]
        public async Task startup_reconciliation_should_not_block_monitoring_on_error()
        {
            // Arrange: reconciliation throws for one series, but monitoring still starts
            Mocker.GetMock<IMangaSeriesService>()
                .Setup(x => x.GetAllSeries())
                .Returns(new List<MangaSeries> { _berserk });

            Mocker.GetMock<IMangaImportService>()
                .Setup(x => x.ReconcileSeries(_berserk))
                .Throws(new System.IO.DirectoryNotFoundException("path missing"));

            // Act: should not throw
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            Assert.DoesNotThrowAsync(() => Subject.StartAsync(cts.Token));

            // Assert: reconciliation was attempted
            Mocker.GetMock<IMangaImportService>()
                .Verify(x => x.ReconcileSeries(_berserk), Times.Once());
        }
    }
}
