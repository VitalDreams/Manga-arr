using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Manga;
using NzbDrone.Core.Manga.Connectors;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.Manga
{
    [TestFixture]
    public class MangaSeriesServiceFixture : CoreTest<MangaSeriesService>
    {
        private MangaSeries _series;

        [SetUp]
        public void Setup()
        {
            _series = new MangaSeries
            {
                Id = 12,
                MangaMetadataId = 34,
                Metadata = new LazyLoaded<MangaMetadata>(new MangaMetadata
                {
                    Id = 34,
                    ForeignMangaId = "manga-123",
                    Title = "Test Manga"
                })
            };
        }

        [Test]
        public async Task should_reuse_supplied_volume_map_without_fetching_it_again()
        {
            var volumeMap = new VolumeChapterMap
            {
                ForeignMangaId = "manga-123",
                VolumeChapters = new Dictionary<int, List<string>>
                {
                    { 1, new List<string>() }
                }
            };

            Mocker.GetMock<IVolumeRepository>()
                .Setup(x => x.Insert(It.IsAny<Volume>()))
                .Returns((Volume volume) =>
                {
                    volume.Id = 99;
                    return volume;
                });

            Mocker.GetMock<IMangaMetadataConnector>()
                .Setup(x => x.GetChaptersForVolumeAsync("manga-123", 1))
                .ReturnsAsync(new List<ChapterInfo>());

            await Subject.FetchAndStoreVolumesAsync(_series, volumeMap);

            Mocker.GetMock<IMangaMetadataConnector>()
                .Verify(x => x.GetVolumeChapterMapAsync(It.IsAny<string>()), Times.Never());
            Mocker.GetMock<IVolumeRepository>()
                .Verify(x => x.Insert(It.Is<Volume>(v => v.VolumeNumber == 1)), Times.Once());
        }

        [Test]
        public async Task should_skip_empty_volume_map_without_inserting_or_fetching_chapters()
        {
            var volumeMap = new VolumeChapterMap
            {
                ForeignMangaId = "manga-123",
                VolumeChapters = new Dictionary<int, List<string>>()
            };

            await Subject.FetchAndStoreVolumesAsync(_series, volumeMap);

            ExceptionVerification.ExpectedWarns(1);

            Mocker.GetMock<IVolumeRepository>()
                .Verify(x => x.Insert(It.IsAny<Volume>()), Times.Never());
            Mocker.GetMock<IMangaMetadataConnector>()
                .Verify(x => x.GetChaptersForVolumeAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never());
            Mocker.GetMock<IMangaMetadataConnector>()
                .Verify(x => x.GetVolumeChapterMapAsync(It.IsAny<string>()), Times.Never());
        }
    }
}
