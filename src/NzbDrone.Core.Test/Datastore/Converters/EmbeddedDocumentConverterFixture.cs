using System.Data.SQLite;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Datastore.Converters;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Datastore.Converters
{
    [TestFixture]
    public class EmbeddedDocumentConverterFixture : CoreTest
    {
        private EmbeddedDocumentConverter<QualityModel> _subject;
        private SQLiteParameter _param;

        [SetUp]
        public void Setup()
        {
            // Constructed with QualityIntConverter, matching TableMapping registration
            _subject = new EmbeddedDocumentConverter<QualityModel>(new QualityIntConverter());
            _param = new SQLiteParameter();
        }

        [Test]
        public void should_roundtrip_valid_quality_json()
        {
            var model = new QualityModel(Quality.EPUB, new Revision(1, 0));

            _subject.SetValue(_param, model);
            var result = _subject.Parse(_param.Value);

            result.Quality.Should().Be(Quality.EPUB);
            result.Revision.Version.Should().Be(1);
            result.Revision.Real.Should().Be(0);
        }

        [Test]
        public void should_parse_legacy_quality_with_unquoted_keys()
        {
            // Legacy Readarr format: bare property names (JS object literal)
            var legacyJson = "{quality: 2, revision: {version: 1, real: 0}}";

            var result = _subject.Parse(legacyJson);

            result.Quality.Should().Be(Quality.MOBI);
            result.Revision.Version.Should().Be(1);
            result.Revision.Real.Should().Be(0);
        }

        [Test]
        public void should_parse_legacy_quality_with_nested_unquoted_keys()
        {
            // Nested object with unquoted keys and boolean value
            var legacyJson = "{quality: 3, revision: {version: 2, real: 1, isRepack: true}}";

            var result = _subject.Parse(legacyJson);

            result.Quality.Should().Be(Quality.EPUB);
            result.Revision.Version.Should().Be(2);
            result.Revision.Real.Should().Be(1);
            result.Revision.IsRepack.Should().BeTrue();
        }

        [Test]
        public void should_parse_legacy_quality_with_spaces_around_keys()
        {
            // Extra whitespace around keys
            var legacyJson = "{  quality : 10 ,  revision : {  version : 1 ,  real : 0 } }";

            var result = _subject.Parse(legacyJson);

            result.Quality.Should().Be(Quality.MP3);
            result.Revision.Version.Should().Be(1);
        }

        [Test]
        public void should_throw_on_completely_invalid_json()
        {
            var invalidJson = "this is not json at all";

            Assert.Throws<System.Text.Json.JsonException>(() => _subject.Parse(invalidJson));
        }
    }
}
