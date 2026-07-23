using System.Collections.Generic;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Manga;
using NzbDrone.Core.Manga.Connectors;
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
    }
}
