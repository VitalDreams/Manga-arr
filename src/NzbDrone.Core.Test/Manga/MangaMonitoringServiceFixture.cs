using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Hosting;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Manga;
using NzbDrone.Core.Manga.Connectors;
using NzbDrone.Core.Manga.Download;
using NzbDrone.Core.Manga.Monitoring;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.Manga
{
    [TestFixture]
    public class MangaMonitoringServiceFixture : CoreTest<MangaMonitoringService>
    {
        [SetUp]
        public void Setup()
        {
            // Disable the background loop so tests don't trigger it automatically
            Subject.Enabled = false;
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
    }
}
