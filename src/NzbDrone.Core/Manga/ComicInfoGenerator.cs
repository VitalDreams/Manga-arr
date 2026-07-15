using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace NzbDrone.Core.Manga
{
    public interface IComicInfoGenerator
    {
        ComicInfo GenerateComicInfo(MangaSeries series, Volume volume, Chapter chapter, int pageCount);
        string GenerateComicInfoXml(MangaSeries series, Volume volume, Chapter chapter, int pageCount);
    }

    public class ComicInfoGenerator : IComicInfoGenerator
    {
        public ComicInfo GenerateComicInfo(MangaSeries series, Volume volume, Chapter chapter, int pageCount)
        {
            var metadata = series.Metadata?.Value;
            var originalLanguage = metadata?.OriginalLanguage ?? "en";

            return new ComicInfo
            {
                Title = volume.Title ?? string.Empty,
                Series = series.Name ?? string.Empty,
                Number = chapter.ChapterNumber.ToString("0.###"),
                Volume = volume.VolumeNumber.ToString(),
                Summary = metadata?.Description ?? string.Empty,
                Year = metadata?.Year.ToString() ?? string.Empty,
                Writer = metadata?.Author ?? string.Empty,
                Penciller = metadata?.Artist ?? string.Empty,
                Publisher = metadata?.Publisher ?? string.Empty,
                Genre = string.Join(", ", metadata?.Genres ?? new List<string>()),
                LanguageISO = originalLanguage,
                Manga = GetReadingDirection(originalLanguage),
                Web = GetMangaDexUrl(series),
                AgeRating = MapContentRating(metadata?.ContentRating),
                PageCount = pageCount
            };
        }

        public string GenerateComicInfoXml(MangaSeries series, Volume volume, Chapter chapter, int pageCount)
        {
            var comicInfo = GenerateComicInfo(series, volume, chapter, pageCount);

            using (var stringWriter = new StringWriter())
            {
                var serializer = new XmlSerializer(typeof(ComicInfo));
                serializer.Serialize(stringWriter, comicInfo);
                return stringWriter.ToString();
            }
        }

        private string GetReadingDirection(string language)
        {
            if (string.IsNullOrEmpty(language))
            {
                return "Yes";
            }

            return language.ToLowerInvariant() switch
            {
                "ja" => "YesAndRightToLeft",
                "ko" => "YesAndRightToLeft",
                "zh" => "YesAndRightToLeft",
                _ => "Yes"
            };
        }

        private string GetMangaDexUrl(MangaSeries series)
        {
            var foreignId = series.ForeignMangaId;
            if (string.IsNullOrEmpty(foreignId))
            {
                return string.Empty;
            }

            return $"https://mangadex.org/title/{foreignId}";
        }

        private string MapContentRating(string contentRating)
        {
            if (string.IsNullOrEmpty(contentRating))
            {
                return "Unknown";
            }

            return contentRating.ToLowerInvariant() switch
            {
                "safe" => "Everyone",
                "suggestive" => "Teen",
                "erotica" => "Mature",
                "violence" => "Teen",
                _ => "Unknown"
            };
        }
    }

    /// <summary>
    /// ComicInfo.xml schema for CBZ metadata.
    /// Used by Komga, Kavita, and other manga/comic readers.
    /// </summary>
    [XmlRoot("ComicInfo")]
    public class ComicInfo
    {
        public string Title { get; set; }
        public string Series { get; set; }
        public string Number { get; set; }
        public string Volume { get; set; }
        public string Summary { get; set; }
        public string Year { get; set; }
        public string Writer { get; set; }
        public string Penciller { get; set; }
        public string Publisher { get; set; }
        public string Genre { get; set; }
        public string Web { get; set; }
        public string LanguageISO { get; set; }
        public string Manga { get; set; }
        public string AgeRating { get; set; }
        public int PageCount { get; set; }
    }
}
