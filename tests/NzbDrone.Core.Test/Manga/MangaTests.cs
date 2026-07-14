using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using NzbDrone.Core.Manga;
using NzbDrone.Core.Manga.Connectors;
using Xunit;

namespace NzbDrone.Core.Test.Manga
{
    public class MangaDexConnectorTests
    {
        private readonly Mock<IHttpClient> _httpClientMock;
        private readonly MangaDexConnector _connector;

        public MangaDexConnectorTests()
        {
            _httpClientMock = new Mock<IHttpClient>();
            _connector = new MangaDexConnector(_httpClientMock.Object);
        }

        [Fact]
        public void Name_ShouldBeMangaDex()
        {
            Assert.Equal("MangaDex", _connector.Name);
        }

        [Fact]
        public void BaseUrl_ShouldBeCorrect()
        {
            Assert.Equal("https://api.mangadex.org", _connector.BaseUrl);
        }

        [Fact]
        public void Enabled_ShouldBeTrueByDefault()
        {
            Assert.True(_connector.Enabled);
        }
    }

    public class CbzCreatorTests
    {
        [Fact]
        public void SanitizeFileName_ShouldRemoveInvalidChars()
        {
            // Arrange
            var creator = new CbzCreator(null, null);

            // Act
            var result = creator.SanitizeFileName("Berserk: Volume 42");

            // Assert
            Assert.Equal("Berserk_ Volume 42", result);
        }

        [Fact]
        public void GetVolumeFileName_ShouldFormatCorrectly()
        {
            // Arrange
            var creator = new CbzCreator(null, null);
            var series = new MangaSeries { Name = "Berserk" };
            var volume = new Volume { VolumeNumber = 42 };

            // Act
            var result = creator.GetVolumeFileName(series, volume);

            // Assert
            Assert.Equal("Berserk - Vol.042.cbz", result);
        }
    }

    public class MangaSeriesTests
    {
        [Fact]
        public void NewMangaSeries_ShouldHaveDefaultValues()
        {
            // Act
            var series = new MangaSeries();

            // Assert
            Assert.NotNull(series.Tags);
            Assert.Empty(series.Tags);
            Assert.NotNull(series.Metadata);
            Assert.False(series.Monitored);
        }

        [Fact]
        public void Name_ShouldReturnValueFromMetadata()
        {
            // Arrange
            var series = new MangaSeries();
            series.Metadata = new System.Lazy<MangaMetadata>(() => new MangaMetadata { Title = "Berserk" });

            // Act
            var name = series.Name;

            // Assert
            Assert.Equal("Berserk", name);
        }
    }

    public class VolumeTests
    {
        [Fact]
        public void NewVolume_ShouldHaveDefaultValues()
        {
            // Act
            var volume = new Volume();

            // Assert
            Assert.NotNull(volume.Links);
            Assert.Empty(volume.Links);
            Assert.NotNull(volume.Genres);
            Assert.Empty(volume.Genres);
            Assert.True(volume.Monitored);
        }
    }

    public class MetadataAggregatorTests
    {
        private readonly Mock<MangaDexConnector> _mangadexMock;
        private readonly Mock<AniListConnector> _anilistMock;
        private readonly MetadataAggregator _aggregator;

        public MetadataAggregatorTests()
        {
            _mangadexMock = new Mock<MangaDexConnector>();
            _anilistMock = new Mock<AniListConnector>();
            _aggregator = new MetadataAggregator(
                _mangadexMock.Object,
                _anilistMock.Object,
                null);
        }

        [Fact]
        public async Task SearchAsync_ShouldReturnMangaDexResults()
        {
            // Arrange
            var mangadexResults = new List<MangaSearchResult>
            {
                new() { ForeignMangaId = "mangadex-1", Title = "Berserk" }
            };

            _mangadexMock.Setup(x => x.SearchAsync("Berserk", 10))
                .ReturnsAsync(mangadexResults);

            // Act
            var results = await _aggregator.SearchAsync("Berserk", 10);

            // Assert
            Assert.Single(results);
            Assert.Equal("mangadex-1", results[0].ForeignMangaId);
        }
    }

    public class KomgaIntegrationTests
    {
        [Fact]
        public void IsConfigured_ShouldBeFalseWhenBaseUrlEmpty()
        {
            // Arrange
            var komga = new KomgaIntegration(null, null);

            // Act
            var result = komga.IsConfigured;

            // Assert
            Assert.False(result);
        }
    }
}
