using NUnit.Framework;
using NzbDrone.Core.Parser;
using Readarr.Api.V1.Manga;

namespace NzbDrone.Core.Test.Manga
{
    [TestFixture]
    public class MangaResourceMapperFixture
    {
        [Test]
        public void to_author_model_should_use_canonical_cleannam_normalization()
        {
            var resource = new MangaResource
            {
                Title = "Berserk: The Golden Age",
                Path = "/manga/berserk",
                QualityProfileId = 1,
                MetadataProfileId = 1
            };

            var author = resource.ToAuthorModel();

            // CleanAuthorName strips articles ("the") and punctuation (":")
            // "Berserk: The Golden Age" -> "berserkgoldenage"
            Assert.That(author.CleanName, Is.EqualTo("berserkgoldenage"));
        }

        [Test]
        public void to_author_model_should_handle_null_title()
        {
            var resource = new MangaResource
            {
                Title = null,
                Path = "/manga/test",
                QualityProfileId = 1,
                MetadataProfileId = 1
            };

            var author = resource.ToAuthorModel();

            Assert.That(author.CleanName, Is.Not.Null);
            Assert.That(author.CleanName, Is.EqualTo(string.Empty));
        }

        [Test]
        public void to_author_model_should_handle_title_with_articles()
        {
            var resource = new MangaResource
            {
                Title = "The Promised Neverland",
                Path = "/manga/promised",
                QualityProfileId = 1,
                MetadataProfileId = 1
            };

            var author = resource.ToAuthorModel();

            // "The Promised Neverland" -> "promisedneverland" (article "the" stripped)
            Assert.That(author.CleanName, Is.EqualTo("promisedneverland"));
        }

        [Test]
        public void to_author_model_should_normalize_accents()
        {
            var resource = new MangaResource
            {
                Title = "Pokémon Adventures",
                Path = "/manga/pokemon",
                QualityProfileId = 1,
                MetadataProfileId = 1
            };

            var author = resource.ToAuthorModel();

            // Accents removed, article "adventures" is NOT an article so it stays
            // But the key point is "Pokémon" -> "pokemon" (accent removed)
            Assert.That(author.CleanName, Does.Contain("pokemon"));
        }

        [Test]
        public void clean_author_name_consistency_between_parser_and_resource()
        {
            // Verify that MangaResource.ToAuthorModel() produces the same CleanName
            // as Parser.CleanAuthorName() would for the same input.
            var title = "Fullmetal Alchemist";

            var resource = new MangaResource
            {
                Title = title,
                Path = "/manga/fma",
                QualityProfileId = 1,
                MetadataProfileId = 1
            };

            var author = resource.ToAuthorModel();
            var expectedCleanName = title.CleanAuthorName();

            Assert.That(author.CleanName, Is.EqualTo(expectedCleanName));
        }
    }
}
