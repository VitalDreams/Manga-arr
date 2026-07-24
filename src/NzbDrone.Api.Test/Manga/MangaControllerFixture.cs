using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Books;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Manga;
using NzbDrone.Test.Common;
using Readarr.Api.V1.Manga;

namespace NzbDrone.Api.Test.Manga
{
    [TestFixture]
    public class MangaControllerFixture : TestBase<MangaController>
    {
        private MangaSeries _series;
        private Volume _volume;

        [SetUp]
        public void Setup()
        {
            _series = new MangaSeries
            {
                Id = 18,
                Monitored = true,
                Metadata = new LazyLoaded<MangaMetadata>(new MangaMetadata
                {
                    Id = 10,
                    ForeignMangaId = "a8c42e49-d6f5-4084-9cec-771f5660c90f",
                    Title = "Berserk"
                })
            };

            _volume = new Volume
            {
                Id = 1001,
                VolumeNumber = 3,
                Title = "Volume 3",
                MangaSeriesId = 18
            };
        }

        // --- Native ID resolution: canonical MangaSeries/Volume, no legacy bridge ---

        [Test]
        public async Task search_volume_with_native_series_id_should_not_call_author_or_book_service()
        {
            Mocker.GetMock<IMangaSeriesRepository>()
                .Setup(x => x.Find(18))
                .Returns(_series);

            Mocker.GetMock<IVolumeRepository>()
                .Setup(x => x.Find(1001))
                .Returns(_volume);

            Mocker.GetMock<IMangaSearchService>()
                .Setup(x => x.SearchAndDownloadAsync(_series, _volume))
                .ReturnsAsync(new MangaSearchAndDownloadResult { Success = true });

            await Subject.SearchVolume(18, 1001);

            Mocker.GetMock<IAuthorService>()
                .Verify(x => x.GetAuthor(It.IsAny<int>()), Times.Never());
            Mocker.GetMock<IBookService>()
                .Verify(x => x.GetBook(It.IsAny<int>()), Times.Never());
        }

        [Test]
        public async Task search_volume_should_pass_the_canonical_db_volume_downstream()
        {
            Mocker.GetMock<IMangaSeriesRepository>()
                .Setup(x => x.Find(18))
                .Returns(_series);

            Mocker.GetMock<IVolumeRepository>()
                .Setup(x => x.Find(1001))
                .Returns(_volume);

            Mocker.GetMock<IMangaSearchService>()
                .Setup(x => x.SearchAndDownloadAsync(It.IsAny<MangaSeries>(), It.IsAny<Volume>()))
                .ReturnsAsync(new MangaSearchAndDownloadResult { Success = true });

            var result = await Subject.SearchVolume(18, 1001);

            // The exact repository-backed Volume (Id=1001) must reach the search service,
            // not a reconstructed stub built from a volume number.
            Mocker.GetMock<IMangaSearchService>()
                .Verify(
                    x => x.SearchAndDownloadAsync(
                        _series,
                        It.Is<Volume>(v => v.Id == 1001 && v.VolumeNumber == 3)),
                    Times.Once());

            var okResult = result.Result as OkObjectResult;
            Assert.That(okResult, Is.Not.Null);
            var body = okResult.Value as MangaSearchAndDownloadResult;
            Assert.That(body.Success, Is.True);
        }

        [Test]
        public async Task search_all_volumes_with_native_series_id_should_not_call_author_service()
        {
            Mocker.GetMock<IMangaSeriesRepository>()
                .Setup(x => x.Find(18))
                .Returns(_series);

            Mocker.GetMock<IMangaSearchService>()
                .Setup(x => x.SearchAndDownloadAsync(18, null))
                .ReturnsAsync(new MangaSearchAndDownloadResult { Success = true });

            var result = await Subject.SearchAllVolumes(18);

            Mocker.GetMock<IAuthorService>()
                .Verify(x => x.GetAuthor(It.IsAny<int>()), Times.Never());

            Mocker.GetMock<IMangaSearchService>()
                .Verify(x => x.SearchAndDownloadAsync(18, null), Times.Once());

            var okResult = result.Result as OkObjectResult;
            Assert.That(okResult, Is.Not.Null);
        }

        // --- Missing series / missing volume errors ---

        [Test]
        public async Task search_volume_should_return_error_result_when_series_not_found()
        {
            Mocker.GetMock<IMangaSeriesRepository>()
                .Setup(x => x.Find(999))
                .Returns((MangaSeries)null);

            Mocker.GetMock<IAuthorService>()
                .Setup(x => x.GetAuthor(999))
                .Throws(new ModelNotFoundException(typeof(Author), 999));

            var result = await Subject.SearchVolume(999, 1001);

            var okResult = result.Result as OkObjectResult;
            Assert.That(okResult, Is.Not.Null);
            var body = okResult.Value as MangaSearchAndDownloadResult;
            Assert.That(body.Success, Is.False);
            Assert.That(body.ErrorMessage, Does.Contain("999"));

            Mocker.GetMock<IMangaSearchService>()
                .Verify(x => x.SearchAndDownloadAsync(It.IsAny<MangaSeries>(), It.IsAny<Volume>()), Times.Never());
        }

        [Test]
        public async Task search_volume_should_return_not_found_when_volume_missing()
        {
            Mocker.GetMock<IMangaSeriesRepository>()
                .Setup(x => x.Find(18))
                .Returns(_series);

            Mocker.GetMock<IVolumeRepository>()
                .Setup(x => x.Find(4242))
                .Returns((Volume)null);

            var result = await Subject.SearchVolume(18, 4242);

            Assert.That(result.Result, Is.InstanceOf<NotFoundObjectResult>());

            Mocker.GetMock<IMangaSearchService>()
                .Verify(x => x.SearchAndDownloadAsync(It.IsAny<MangaSeries>(), It.IsAny<Volume>()), Times.Never());
        }

        [Test]
        public async Task search_volume_should_return_not_found_when_volume_belongs_to_different_series()
        {
            var otherSeriesVolume = new Volume
            {
                Id = 2002,
                VolumeNumber = 1,
                MangaSeriesId = 99
            };

            Mocker.GetMock<IMangaSeriesRepository>()
                .Setup(x => x.Find(18))
                .Returns(_series);

            Mocker.GetMock<IVolumeRepository>()
                .Setup(x => x.Find(2002))
                .Returns(otherSeriesVolume);

            var result = await Subject.SearchVolume(18, 2002);

            Assert.That(result.Result, Is.InstanceOf<NotFoundObjectResult>());

            Mocker.GetMock<IMangaSearchService>()
                .Verify(x => x.SearchAndDownloadAsync(It.IsAny<MangaSeries>(), It.IsAny<Volume>()), Times.Never());
        }

        // --- Legacy Author ID fallback (back-compat only; never the primary path) ---

        [Test]
        public async Task search_volume_should_fall_back_to_legacy_author_id_when_no_native_series_matches()
        {
            var author = new Author
            {
                Id = 42,
                Metadata = new LazyLoaded<AuthorMetadata>(new AuthorMetadata
                {
                    Id = 7,
                    ForeignAuthorId = _series.ForeignMangaId
                })
            };

            // No native MangaSeries has Id=42 (native IDs and legacy Author IDs are different
            // spaces), so resolution must bridge through AuthorService as a fallback only.
            Mocker.GetMock<IMangaSeriesRepository>()
                .Setup(x => x.Find(42))
                .Returns((MangaSeries)null);

            Mocker.GetMock<IAuthorService>()
                .Setup(x => x.GetAuthor(42))
                .Returns(author);

            Mocker.GetMock<IMangaMetadataRepository>()
                .Setup(x => x.FindByForeignMangaId(_series.ForeignMangaId))
                .Returns(_series.Metadata.Value);

            Mocker.GetMock<IMangaSeriesRepository>()
                .Setup(x => x.FindByMangaMetadataId(10))
                .Returns(_series);

            Mocker.GetMock<IVolumeRepository>()
                .Setup(x => x.Find(1001))
                .Returns(_volume);

            Mocker.GetMock<IMangaSearchService>()
                .Setup(x => x.SearchAndDownloadAsync(_series, _volume))
                .ReturnsAsync(new MangaSearchAndDownloadResult { Success = true });

            var result = await Subject.SearchVolume(42, 1001);

            Mocker.GetMock<IAuthorService>()
                .Verify(x => x.GetAuthor(42), Times.Once());

            Mocker.GetMock<IMangaSearchService>()
                .Verify(x => x.SearchAndDownloadAsync(_series, It.Is<Volume>(v => v.Id == 1001)), Times.Once());

            var okResult = result.Result as OkObjectResult;
            Assert.That(okResult, Is.Not.Null);
        }
    }
}
