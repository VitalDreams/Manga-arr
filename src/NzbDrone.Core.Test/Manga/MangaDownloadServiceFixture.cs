using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Download;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Manga;
using NzbDrone.Core.Manga.Download;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Manga
{
    [TestFixture]
    public class MangaDownloadServiceFixture : CoreTest<MangaDownloadService>
    {
        private MangaSeries _series;
        private Volume _volume;
        private List<IDownloadClient> _downloadClients;

        [SetUp]
        public void Setup()
        {
            _series = new MangaSeries
            {
                Id = 18,
                Name = "Berserk",
                ForeignMangaId = "a8c42e49-d6f5-4084-9cec-771f5660c90f"
            };

            _volume = new Volume
            {
                Id = 1001,
                VolumeNumber = 1
            };

            _downloadClients = new List<IDownloadClient>();

            // Mirror DownloadClientProvider's real contract: it only returns a client whose
            // Protocol matches what was asked for, never a client for the wrong protocol.
            Mocker.GetMock<IProvideDownloadClient>()
                .Setup(x => x.GetDownloadClient(It.IsAny<DownloadProtocol>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<HashSet<int>>()))
                .Returns<DownloadProtocol, int, bool, HashSet<int>>((protocol, indexerId, filterBlocked, tags) =>
                    _downloadClients.FirstOrDefault(c => c.Protocol == protocol));
        }

        private Mock<IDownloadClient> AddClient(string name, DownloadProtocol protocol, string downloadId = "id-1")
        {
            var mock = new Mock<IDownloadClient>();
            mock.SetupGet(c => c.Name).Returns(name);
            mock.SetupGet(c => c.Protocol).Returns(protocol);
            mock.Setup(c => c.Download(It.IsAny<RemoteBook>(), null)).ReturnsAsync(downloadId);

            _downloadClients.Add(mock.Object);
            return mock;
        }

        [Test]
        public async Task should_send_usenet_release_to_the_usenet_capable_client_not_qbittorrent()
        {
            AddClient("qBittorrent", DownloadProtocol.Torrent, "tor-id");
            var sab = AddClient("SABnzbd", DownloadProtocol.Usenet, "sab-id");

            var result = await Subject.SendToDownloadClient(
                "Berserk Vol 1", "http://example.com/api?t=get&id=nzb1", DownloadProtocol.Usenet, _series, _volume);

            Assert.That(result.Success, Is.True);
            Assert.That(result.ClientName, Is.EqualTo("SABnzbd"));
            Assert.That(result.DownloadId, Is.EqualTo("sab-id"));

            sab.Verify(c => c.Download(It.IsAny<RemoteBook>(), null), Times.Once);
        }

        [Test]
        public async Task should_send_torrent_release_to_the_torrent_capable_client_not_sabnzbd()
        {
            var qbit = AddClient("qBittorrent", DownloadProtocol.Torrent, "tor-id");
            AddClient("SABnzbd", DownloadProtocol.Usenet, "sab-id");

            var result = await Subject.SendToDownloadClient(
                "Berserk Vol 1 [Nyaa]", "magnet:?xt=urn:btih:abc123", DownloadProtocol.Torrent, _series, _volume);

            Assert.That(result.Success, Is.True);
            Assert.That(result.ClientName, Is.EqualTo("qBittorrent"));
            Assert.That(result.DownloadId, Is.EqualTo("tor-id"));

            qbit.Verify(c => c.Download(It.IsAny<RemoteBook>(), null), Times.Once);
        }

        [Test]
        public async Task should_pass_the_nzb_url_through_to_the_download_client_unmodified()
        {
            var sab = AddClient("SABnzbd", DownloadProtocol.Usenet, "sab-id");
            const string nzbUrl = "http://prowlarr:9696/9/api?t=get&id=abc123";

            await Subject.SendToDownloadClient("Berserk Vol 1", nzbUrl, DownloadProtocol.Usenet, _series, _volume);

            sab.Verify(c => c.Download(
                It.Is<RemoteBook>(r => r.Release.DownloadUrl == nzbUrl && r.Release.DownloadProtocol == DownloadProtocol.Usenet),
                null),
                Times.Once);
        }

        [Test]
        public async Task should_fail_gracefully_when_no_client_configured_for_the_protocol()
        {
            // Only a torrent client configured - a Usenet release must not silently fall
            // through to it.
            AddClient("qBittorrent", DownloadProtocol.Torrent, "tor-id");

            var result = await Subject.SendToDownloadClient(
                "Berserk Vol 1", "http://example.com/nzb", DownloadProtocol.Usenet, _series, _volume);

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("Usenet"));
        }
    }
}
