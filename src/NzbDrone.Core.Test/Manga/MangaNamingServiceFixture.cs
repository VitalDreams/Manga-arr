using NUnit.Framework;
using NzbDrone.Core.Manga;

namespace NzbDrone.Core.Test.Manga
{
    [TestFixture]
    public class MangaNamingServiceFixture
    {
        [Test]
        public void should_omit_unknown_year_from_series_folder_and_volume_name()
        {
            var series = new MangaSeries
            {
                Metadata = new MangaMetadata
                {
                    Title = "Solo Leveling",
                    Year = 0
                }
            };
            var volume = new Volume { VolumeNumber = 1 };
            var subject = new MangaNamingService();

            Assert.That(subject.GetSeriesFolder(series), Is.EqualTo("Solo Leveling"));
            Assert.That(subject.GetVolumeFileName(series, volume), Is.EqualTo("Solo Leveling Vol.1.cbz"));
        }

        [Test]
        public void should_preserve_known_year_in_series_folder_and_volume_name()
        {
            var series = new MangaSeries
            {
                Metadata = new MangaMetadata
                {
                    Title = "Solo Leveling",
                    Year = 2021
                }
            };
            var volume = new Volume { VolumeNumber = 1 };
            var subject = new MangaNamingService();

            Assert.That(subject.GetSeriesFolder(series), Is.EqualTo("Solo Leveling (2021)"));
            Assert.That(subject.GetVolumeFileName(series, volume), Is.EqualTo("Solo Leveling Vol.1 (2021).cbz"));
        }

        [Test]
        public void should_treat_negative_year_as_unknown()
        {
            var series = new MangaSeries
            {
                Metadata = new MangaMetadata
                {
                    Title = "Solo Leveling",
                    Year = -1
                }
            };
            var subject = new MangaNamingService();

            Assert.That(subject.GetSeriesFolder(series), Is.EqualTo("Solo Leveling"));
        }

        [Test]
        public void should_preserve_custom_template_text_when_year_is_unknown()
        {
            var series = new MangaSeries
            {
                Metadata = new MangaMetadata
                {
                    Title = "Solo Leveling",
                    Year = 0
                }
            };
            var subject = new MangaNamingService();

            Assert.That(subject.GetSeriesFolder(series, "Shelf ($Year) [Special]"), Is.EqualTo("Shelf [Special]"));
            Assert.That(subject.GetSeriesFolder(series, "($Series)"), Is.EqualTo("(Solo Leveling)"));
        }

        [Test]
        public void should_omit_unknown_year_from_chapter_file_name()
        {
            var series = new MangaSeries
            {
                Metadata = new MangaMetadata
                {
                    Title = "Solo Leveling",
                    Year = 0
                }
            };
            var volume = new Volume { VolumeNumber = 1 };
            var chapter = new Chapter { ChapterNumber = 1 };
            var subject = new MangaNamingService();

            Assert.That(subject.GetChapterFileName(series, volume, chapter), Is.EqualTo("Solo Leveling Vol.1 Ch.1.cbz"));
        }
    }
}
