using NUnit.Framework;
using NzbDrone.Core.Parser;

namespace NzbDrone.Core.Test.Manga
{
    [TestFixture]
    public class MangaResourceMapperFixture
    {
        [Test]
        public void clean_author_name_should_strip_articles_and_punctuation()
        {
            // MangaResourceMapper.ToAuthorModel() sets CleanName = (Title ?? "").CleanAuthorName()
            // "Berserk: The Golden Age" -> articles ("The" in middle) and punctuation (":") stripped
            var result = "Berserk: The Golden Age".CleanAuthorName();

            Assert.That(result, Is.EqualTo("berserkgoldenage"));
        }

        [Test]
        public void clean_author_name_should_handle_null_as_empty()
        {
            // MangaResourceMapper.ToAuthorModel() uses (Title ?? "").CleanAuthorName()
            var result = ((string)null).CleanAuthorName();

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.EqualTo(string.Empty));
        }

        [Test]
        public void clean_author_name_should_keep_leading_articles()
        {
            // "The Promised Neverland" -> leading "The" is kept (regex preserves start-of-string articles)
            var result = "The Promised Neverland".CleanAuthorName();

            Assert.That(result, Is.EqualTo("thepromisedneverland"));
        }

        [Test]
        public void clean_author_name_should_normalize_accents()
        {
            // "Pokémon Adventures" -> accent removed by RemoveAccent()
            var result = "Pokémon Adventures".CleanAuthorName();

            Assert.That(result, Does.Contain("pokemon"));
        }

        [Test]
        public void clean_author_name_should_handle_empty_string()
        {
            var result = string.Empty.CleanAuthorName();

            Assert.That(result, Is.EqualTo(string.Empty));
        }
    }
}
