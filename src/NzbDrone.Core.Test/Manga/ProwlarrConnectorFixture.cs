using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Http;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Indexers.Newznab;
using NzbDrone.Core.Indexers.Torznab;
using NzbDrone.Core.Manga.Connectors;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.ThingiProvider;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.Manga
{
    [TestFixture]
    public class ProwlarrConnectorFixture : CoreTest<ProwlarrConnector>
    {
        private void SetupIndexerFactory(params IndexerDefinition[] definitions)
        {
            Mocker.GetMock<IIndexerFactory>()
                .Setup(x => x.All())
                .Returns(new List<IndexerDefinition>(definitions));
        }

        private static IndexerDefinition MakeNewznabDefinition(string name, string baseUrl, string apiKey)
        {
            return new IndexerDefinition
            {
                Name = name,
                Implementation = "Newznab",
                Settings = new NewznabSettings
                {
                    BaseUrl = baseUrl,
                    ApiKey = apiKey,
                    ApiPath = "/api",
                    Categories = new[] { 7030 }
                }
            };
        }

        private static IndexerDefinition MakeTorznabDefinition(string name, string baseUrl, string apiKey)
        {
            return new IndexerDefinition
            {
                Name = name,
                Implementation = "Torznab",
                Settings = new TorznabSettings
                {
                    BaseUrl = baseUrl,
                    ApiKey = apiKey,
                    ApiPath = "/api",
                    Categories = new[] { 7030 }
                }
            };
        }

        [Test]
        public void is_configured_should_be_false_when_no_indexers_exist()
        {
            SetupIndexerFactory();

            Assert.That(Subject.IsConfigured, Is.False);
        }

        [Test]
        public void is_configured_should_be_false_when_no_prowlarr_indexers_exist()
        {
            SetupIndexerFactory(
                new IndexerDefinition
                {
                    Name = "Nyaa",
                    Implementation = "Nyaa",
                    Settings = new NullConfig()
                });

            Assert.That(Subject.IsConfigured, Is.False);
        }

        [Test]
        public void is_configured_should_be_true_when_newznab_indexer_exists()
        {
            SetupIndexerFactory(
                MakeNewznabDefinition("Prowlarr (Usenet)", "http://prowlarr:9696/9/", "test-api-key"));

            Assert.That(Subject.IsConfigured, Is.True);
        }

        [Test]
        public void get_download_protocol_should_return_usenet_for_newznab_indexer()
        {
            SetupIndexerFactory(
                MakeNewznabDefinition("NZBgeek", "http://prowlarr:9696/9/", "test-api-key"),
                MakeTorznabDefinition("Nyaa", "http://prowlarr:9696/1/", "test-api-key"));

            Assert.That(Subject.IsConfigured, Is.True);

            var result = new ProwlarrSearchResult
            {
                Title = "Some Manhwa Vol 1",
                Indexer = "NZBgeek",
                DownloadUrl = "http://prowlarr:9696/9/api?t=get&id=abc123"
            };

            Assert.That(Subject.GetDownloadProtocol(result), Is.EqualTo(DownloadProtocol.Usenet));
        }

        [Test]
        public void get_download_protocol_should_return_torrent_for_torznab_indexer()
        {
            SetupIndexerFactory(
                MakeNewznabDefinition("NZBgeek", "http://prowlarr:9696/9/", "test-api-key"),
                MakeTorznabDefinition("Nyaa", "http://prowlarr:9696/1/", "test-api-key"));

            Assert.That(Subject.IsConfigured, Is.True);

            var result = new ProwlarrSearchResult
            {
                Title = "Some Manhwa Vol 1",
                Indexer = "Nyaa",
                DownloadUrl = "http://prowlarr:9696/1/api?t=get&id=abc123"
            };

            Assert.That(Subject.GetDownloadProtocol(result), Is.EqualTo(DownloadProtocol.Torrent));
        }

        [Test]
        public void get_download_protocol_should_trust_magnet_payload_over_a_newznab_indexer_label()
        {
            // A misconfigured/proxying indexer definition claims Newznab (Usenet), but the
            // actual release it returned carries a magnet link - an unambiguous torrent
            // signal that must never be reported as Usenet just because of the label.
            SetupIndexerFactory(
                MakeNewznabDefinition("MisconfiguredIndexer", "http://prowlarr:9696/9/", "test-api-key"));

            Assert.That(Subject.IsConfigured, Is.True);

            var result = new ProwlarrSearchResult
            {
                Title = "Some Manhwa Vol 1",
                Indexer = "MisconfiguredIndexer",
                MagnetUrl = "magnet:?xt=urn:btih:abc123"
            };

            Assert.That(Subject.GetDownloadProtocol(result), Is.EqualTo(DownloadProtocol.Torrent));
        }

        [Test]
        public void get_download_protocol_should_trust_torrent_url_over_a_newznab_indexer_label()
        {
            SetupIndexerFactory(
                MakeNewznabDefinition("MisconfiguredIndexer", "http://prowlarr:9696/9/", "test-api-key"));

            Assert.That(Subject.IsConfigured, Is.True);

            var result = new ProwlarrSearchResult
            {
                Title = "Some Manhwa Vol 1",
                Indexer = "MisconfiguredIndexer",
                DownloadUrl = "http://example.com/release.torrent"
            };

            Assert.That(Subject.GetDownloadProtocol(result), Is.EqualTo(DownloadProtocol.Torrent));
        }

        [Test]
        public void get_download_protocol_should_trust_nzb_url_over_a_torznab_indexer_label()
        {
            // Mirror of the magnet/torrent-over-label tests above: a misconfigured/proxying
            // indexer definition claims Torznab (Torrent), but the actual release it returned
            // carries an .nzb download URL - an unambiguous Usenet signal that must never be
            // reported as Torrent just because of the label.
            SetupIndexerFactory(
                MakeTorznabDefinition("MisconfiguredIndexer", "http://prowlarr:9696/1/", "test-api-key"));

            Assert.That(Subject.IsConfigured, Is.True);

            var result = new ProwlarrSearchResult
            {
                Title = "Some Manhwa Vol 1",
                Indexer = "MisconfiguredIndexer",
                DownloadUrl = "http://example.com/release.nzb"
            };

            Assert.That(Subject.GetDownloadProtocol(result), Is.EqualTo(DownloadProtocol.Usenet));
        }

        [Test]
        public void get_download_protocol_should_fall_back_to_url_heuristics_for_unknown_indexer()
        {
            SetupIndexerFactory(
                MakeNewznabDefinition("NZBgeek", "http://prowlarr:9696/9/", "test-api-key"));

            Assert.That(Subject.IsConfigured, Is.True);

            var result = new ProwlarrSearchResult
            {
                Title = "Some Manhwa Vol 1",
                Indexer = "SomeUnknownIndexer",
                DownloadUrl = "http://example.com/release.nzb"
            };

            Assert.That(Subject.GetDownloadProtocol(result), Is.EqualTo(DownloadProtocol.Usenet));
        }

        [Test]
        public void get_download_protocol_should_detect_torrent_from_magnet_url_with_no_indexer_match()
        {
            SetupIndexerFactory(
                MakeTorznabDefinition("Nyaa", "http://prowlarr:9696/1/", "test-api-key"));

            Assert.That(Subject.IsConfigured, Is.True);

            var result = new ProwlarrSearchResult
            {
                Title = "Some Manhwa Vol 1",
                Indexer = "SomeUnknownIndexer",
                MagnetUrl = "magnet:?xt=urn:btih:abc123"
            };

            Assert.That(Subject.GetDownloadProtocol(result), Is.EqualTo(DownloadProtocol.Torrent));
        }

        [Test]
        public async Task search_manga_volume_packs_should_drop_results_with_no_download_link()
        {
            SetupIndexerFactory(
                MakeNewznabDefinition("NZBgeek", "http://prowlarr:9696/9/", "test-api-key"));

            var json = @"[
                {
                    ""title"": ""Some Manhwa Vol 1"",
                    ""indexer"": ""NZBgeek"",
                    ""size"": 100000
                },
                {
                    ""title"": ""Some Manhwa Vol 1 Alt"",
                    ""indexer"": ""NZBgeek"",
                    ""downloadUrl"": ""http://prowlarr:9696/9/api?t=get&id=abc123"",
                    ""size"": 200000
                }
            ]";

            Mocker.GetMock<IHttpClient>()
                .Setup(x => x.GetAsync(It.IsAny<HttpRequest>()))
                .ReturnsAsync(new HttpResponse(new HttpRequest(""), new HttpHeader(), json));

            var results = await Subject.SearchMangaVolumePacksAsync("Some Manhwa", 1);

            Assert.That(results, Has.Count.EqualTo(1));
            Assert.That(results[0].Title, Is.EqualTo("Some Manhwa Vol 1 Alt"));
        }

        [Test]
        public async Task search_manga_volume_packs_should_prefer_usenet_over_torrent_regardless_of_seeders()
        {
            SetupIndexerFactory(
                MakeNewznabDefinition("NZBgeek", "http://prowlarr:9696/9/", "test-api-key"),
                MakeTorznabDefinition("Nyaa", "http://prowlarr:9696/1/", "test-api-key"));

            var newznabJson = @"[
                {
                    ""title"": ""Some Manhwa Vol 1"",
                    ""indexer"": ""NZBgeek"",
                    ""downloadUrl"": ""http://prowlarr:9696/9/api?t=get&id=nzb1"",
                    ""size"": 200000,
                    ""seeders"": 0
                }
            ]";

            var torznabJson = @"[
                {
                    ""title"": ""Some Manhwa Vol 1 [Torrent]"",
                    ""indexer"": ""Nyaa"",
                    ""downloadUrl"": ""http://prowlarr:9696/1/api?t=get&id=tor1"",
                    ""size"": 200000,
                    ""seeders"": 500
                }
            ]";

            Mocker.GetMock<IHttpClient>()
                .Setup(x => x.GetAsync(It.Is<HttpRequest>(r => r.Url.FullUri.Contains("indexer=NZBgeek"))))
                .ReturnsAsync(new HttpResponse(new HttpRequest(""), new HttpHeader(), newznabJson));

            Mocker.GetMock<IHttpClient>()
                .Setup(x => x.GetAsync(It.Is<HttpRequest>(r => r.Url.FullUri.Contains("indexer=Nyaa"))))
                .ReturnsAsync(new HttpResponse(new HttpRequest(""), new HttpHeader(), torznabJson));

            var results = await Subject.SearchMangaVolumePacksAsync("Some Manhwa", 1);

            Assert.That(results.First().Protocol, Is.EqualTo(DownloadProtocol.Usenet));
        }

        [Test]
        public async Task search_manga_volume_packs_should_reject_audiobook_release_with_matching_title_and_volume()
        {
            // Reproduces the live bug: an audiobook release whose title matches both the
            // manga title and the volume-range pattern must never be returned as a candidate.
            SetupIndexerFactory(
                MakeTorznabDefinition("TorrentLeech", "http://prowlarr:9696/1/", "test-api-key"));

            var json = @"[
                {
                    ""title"": ""Solo Leveling - Vol 1-8 - Chugong, Hye Young Im, J Torres [m4b mp3] [AUDIOBOOK]"",
                    ""indexer"": ""TorrentLeech"",
                    ""downloadUrl"": ""http://prowlarr:9696/1/api?t=get&id=abc123"",
                    ""size"": 3304976640,
                    ""seeders"": 12
                }
            ]";

            Mocker.GetMock<IHttpClient>()
                .Setup(x => x.GetAsync(It.IsAny<HttpRequest>()))
                .ReturnsAsync(new HttpResponse(new HttpRequest(""), new HttpHeader(), json));

            var results = await Subject.SearchMangaVolumePacksAsync("Solo Leveling", 1);

            Assert.That(results, Is.Empty);
        }

        [Test]
        public async Task search_manga_volume_packs_should_keep_valid_cbz_release_while_dropping_audiobook()
        {
            SetupIndexerFactory(
                MakeTorznabDefinition("TorrentLeech", "http://prowlarr:9696/1/", "test-api-key"));

            var json = @"[
                {
                    ""title"": ""Solo Leveling - Vol 1-8 - Chugong, Hye Young Im, J Torres [m4b mp3] [AUDIOBOOK]"",
                    ""indexer"": ""TorrentLeech"",
                    ""downloadUrl"": ""http://prowlarr:9696/1/api?t=get&id=abc123"",
                    ""size"": 3304976640,
                    ""seeders"": 12
                },
                {
                    ""title"": ""Solo Leveling Vol 1 [CBZ]"",
                    ""indexer"": ""TorrentLeech"",
                    ""downloadUrl"": ""http://prowlarr:9696/1/api?t=get&id=def456"",
                    ""size"": 200000000,
                    ""seeders"": 3
                }
            ]";

            Mocker.GetMock<IHttpClient>()
                .Setup(x => x.GetAsync(It.IsAny<HttpRequest>()))
                .ReturnsAsync(new HttpResponse(new HttpRequest(""), new HttpHeader(), json));

            var results = await Subject.SearchMangaVolumePacksAsync("Solo Leveling", 1);

            Assert.That(results, Has.Count.EqualTo(1));
            Assert.That(results[0].Title, Is.EqualTo("Solo Leveling Vol 1 [CBZ]"));
        }

        [Test]
        public async Task search_manga_volume_packs_should_preserve_same_title_nzb_alongside_torrent()
        {
            // Regression: deduplication must not collapse an NZB and a torrent that share
            // the same title. The NZB must survive and rank first due to Usenet-first sorting.
            SetupIndexerFactory(
                MakeNewznabDefinition("NZBgeek", "http://prowlarr:9696/9/", "test-api-key"),
                MakeTorznabDefinition("Nyaa", "http://prowlarr:9696/1/", "test-api-key"));

            var nzbJson = @"[
                {
                    ""title"": ""Some Manhwa Vol 1"",
                    ""indexer"": ""NZBgeek"",
                    ""downloadUrl"": ""http://prowlarr:9696/9/api?t=get&id=nzb1"",
                    ""size"": 200000,
                    ""seeders"": 0
                }
            ]";

            var torrentJson = @"[
                {
                    ""title"": ""Some Manhwa Vol 1"",
                    ""indexer"": ""Nyaa"",
                    ""downloadUrl"": ""http://prowlarr:9696/1/api?t=get&id=tor1"",
                    ""size"": 200000,
                    ""seeders"": 500
                }
            ]";

            Mocker.GetMock<IHttpClient>()
                .Setup(x => x.GetAsync(It.Is<HttpRequest>(r => r.Url.FullUri.Contains("indexer=NZBgeek"))))
                .ReturnsAsync(new HttpResponse(new HttpRequest(""), new HttpHeader(), nzbJson));

            Mocker.GetMock<IHttpClient>()
                .Setup(x => x.GetAsync(It.Is<HttpRequest>(r => r.Url.FullUri.Contains("indexer=Nyaa"))))
                .ReturnsAsync(new HttpResponse(new HttpRequest(""), new HttpHeader(), torrentJson));

            var results = await Subject.SearchMangaVolumePacksAsync("Some Manhwa", 1);

            // Both protocols must survive deduplication
            Assert.That(results, Has.Count.EqualTo(2));
            // Usenet must rank first regardless of torrent seeder count
            Assert.That(results[0].Protocol, Is.EqualTo(DownloadProtocol.Usenet));
            Assert.That(results[1].Protocol, Is.EqualTo(DownloadProtocol.Torrent));
        }
    }
}
