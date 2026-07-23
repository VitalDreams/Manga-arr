using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Http;
using NzbDrone.Core.Manga.Connectors;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.Manga
{
    [TestFixture]
    public class MangaDexConnectorFixture : CoreTest<MangaDexConnector>
    {
        [Test]
        public async Task get_chapters_for_volume_should_use_aggregate_then_chapter_endpoint_with_ids()
        {
            var foreignMangaId = "29c42e49-d6f5-4084-9cec-771f5660c90f";
            var volumeNumber = 27;

            // Mock aggregate endpoint response - returns chapter IDs for the volume
            var aggregateJson = @"{
                ""volumes"": {
                    ""27"": {
                        ""chapters"": {
                            ""100"": { ""id"": ""chapter-id-100"" },
                            ""101"": { ""id"": ""chapter-id-101"" }
                        }
                    }
                }
            }";

            // Mock chapter endpoint response (not /feed, which doesn't support ids[])
            var chapterJson = @"{
                ""data"": [
                    {
                        ""id"": ""chapter-id-100"",
                        ""attributes"": {
                            ""title"": ""Chapter 100"",
                            ""chapter"": ""100"",
                            ""translatedLanguage"": ""en"",
                            ""pages"": 20
                        }
                    },
                    {
                        ""id"": ""chapter-id-101"",
                        ""attributes"": {
                            ""title"": ""Chapter 101"",
                            ""chapter"": ""101"",
                            ""translatedLanguage"": ""en"",
                            ""pages"": 22
                        }
                    }
                ]
            }";

            Mocker.GetMock<IHttpClient>()
                .Setup(x => x.GetAsync(It.Is<HttpRequest>(r => r.Url.FullUri.Contains("/aggregate"))))
                .ReturnsAsync(new HttpResponse(new HttpRequest(""), new HttpHeader(), aggregateJson));

            Mocker.GetMock<IHttpClient>()
                .Setup(x => x.GetAsync(It.Is<HttpRequest>(r => r.Url.FullUri.Contains("/chapter") && !r.Url.FullUri.Contains("/aggregate"))))
                .ReturnsAsync(new HttpResponse(new HttpRequest(""), new HttpHeader(), chapterJson));

            var result = await Subject.GetChaptersForVolumeAsync(foreignMangaId, volumeNumber);

            // Verify the chapter URL uses /chapter endpoint with ids[] parameter (not /feed which rejects ids[])
            Mocker.GetMock<IHttpClient>()
                .Verify(x => x.GetAsync(It.Is<HttpRequest>(r =>
                    r.Url.FullUri.Contains("/chapter?") &&
                    r.Url.FullUri.Contains("ids[]=chapter-id-100") &&
                    r.Url.FullUri.Contains("ids[]=chapter-id-101") &&
                    !r.Url.FullUri.Contains("/manga/") &&
                    !r.Url.FullUri.Contains("volume[]="))), Times.Once);

            Assert.That(result, Has.Count.EqualTo(2));
            Assert.That(result[0].ForeignChapterId, Is.EqualTo("chapter-id-100"));
            Assert.That(result[0].VolumeNumber, Is.EqualTo(27));
            Assert.That(result[1].ForeignChapterId, Is.EqualTo("chapter-id-101"));
        }

        [Test]
        public async Task get_chapters_for_volume_should_return_empty_when_no_chapters_in_volume()
        {
            var foreignMangaId = "29c42e49-d6f5-4084-9cec-771f5660c90f";
            var volumeNumber = 99;

            // Mock aggregate endpoint response - volume doesn't exist
            var aggregateJson = @"{ ""volumes"": {} }";

            Mocker.GetMock<IHttpClient>()
                .Setup(x => x.GetAsync(It.Is<HttpRequest>(r => r.Url.FullUri.Contains("/aggregate"))))
                .ReturnsAsync(new HttpResponse(new HttpRequest(""), new HttpHeader(), aggregateJson));

            var result = await Subject.GetChaptersForVolumeAsync(foreignMangaId, volumeNumber);

            // Should not call chapter endpoint at all (volume doesn't exist)
            Mocker.GetMock<IHttpClient>()
                .Verify(x => x.GetAsync(It.Is<HttpRequest>(r => r.Url.FullUri.Contains("/chapter"))), Times.Never);

            Assert.That(result, Is.Empty);
        }
    }
}
