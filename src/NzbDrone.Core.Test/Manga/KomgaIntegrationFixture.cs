using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Http;
using NzbDrone.Core.Manga;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Manga
{
    [TestFixture]
    public class KomgaIntegrationFixture : CoreTest<KomgaIntegration>
    {
        [SetUp]
        public void Setup()
        {
            Subject.BaseUrl = "http://komga:25600";
            Subject.ApiKey = "test-api-key";
        }

        private static HttpResponse LibrariesResponse(params string[] ids)
        {
            var libraries = string.Join(",", ids.Select(id => $@"{{""id"":""{id}"",""name"":""Lib {id}""}}"));
            return new HttpResponse(new HttpRequest(""), new HttpHeader(), $"[{libraries}]");
        }

        [Test]
        public async Task trigger_library_scan_should_scan_every_library_by_id()
        {
            var requestUrls = new List<string>();

            Mocker.GetMock<IHttpClient>()
                .Setup(x => x.GetAsync(It.Is<HttpRequest>(r => r.Url.FullUri.Contains("/api/v1/libraries") && !r.Url.FullUri.Contains("/scan"))))
                .ReturnsAsync(LibrariesResponse("lib-1", "lib-2"));

            Mocker.GetMock<IHttpClient>()
                .Setup(x => x.PostAsync(It.Is<HttpRequest>(r => r.Url.FullUri.Contains("/scan"))))
                .Callback<HttpRequest>(r => requestUrls.Add(r.Url.FullUri))
                .ReturnsAsync(new HttpResponse(new HttpRequest(""), new HttpHeader(), string.Empty, HttpStatusCode.Accepted));

            await Subject.TriggerLibraryScanAsync();

            Assert.That(requestUrls, Is.EquivalentTo(new[]
            {
                "http://komga:25600/api/v1/libraries/lib-1/scan",
                "http://komga:25600/api/v1/libraries/lib-2/scan"
            }));
        }

        [Test]
        public async Task trigger_library_scan_should_never_call_the_nonexistent_bulk_scan_endpoint()
        {
            // Komga has no POST /api/v1/libraries/scan (bulk) endpoint - only
            // POST /api/v1/libraries/{libraryId}/scan per library.
            Mocker.GetMock<IHttpClient>()
                .Setup(x => x.GetAsync(It.IsAny<HttpRequest>()))
                .ReturnsAsync(LibrariesResponse("lib-1"));

            Mocker.GetMock<IHttpClient>()
                .Setup(x => x.PostAsync(It.IsAny<HttpRequest>()))
                .ReturnsAsync(new HttpResponse(new HttpRequest(""), new HttpHeader(), string.Empty, HttpStatusCode.Accepted));

            await Subject.TriggerLibraryScanAsync();

            Mocker.GetMock<IHttpClient>()
                .Verify(x => x.PostAsync(It.Is<HttpRequest>(r => r.Url.FullUri.EndsWith("/api/v1/libraries/scan"))), Times.Never());
        }

        [Test]
        public async Task trigger_library_scan_should_do_nothing_when_no_libraries_exist()
        {
            Mocker.GetMock<IHttpClient>()
                .Setup(x => x.GetAsync(It.IsAny<HttpRequest>()))
                .ReturnsAsync(LibrariesResponse());

            await Subject.TriggerLibraryScanAsync();

            Mocker.GetMock<IHttpClient>()
                .Verify(x => x.PostAsync(It.IsAny<HttpRequest>()), Times.Never());
        }

        [Test]
        public async Task trigger_library_scan_should_do_nothing_when_not_configured()
        {
            Subject.BaseUrl = null;

            await Subject.TriggerLibraryScanAsync();

            Mocker.GetMock<IHttpClient>().Verify(x => x.GetAsync(It.IsAny<HttpRequest>()), Times.Never());
            Mocker.GetMock<IHttpClient>().Verify(x => x.PostAsync(It.IsAny<HttpRequest>()), Times.Never());
        }

        [Test]
        public async Task trigger_library_scan_should_swallow_exceptions_when_library_list_fails()
        {
            Mocker.GetMock<IHttpClient>()
                .Setup(x => x.GetAsync(It.IsAny<HttpRequest>()))
                .ThrowsAsync(new HttpException(new HttpRequest("http://komga:25600/api/v1/libraries"), new HttpResponse(new HttpRequest(""), new HttpHeader(), string.Empty, HttpStatusCode.Unauthorized)));

            await Subject.TriggerLibraryScanAsync();

            Mocker.GetMock<IHttpClient>().Verify(x => x.PostAsync(It.IsAny<HttpRequest>()), Times.Never());
        }
    }
}
