using System.Text.RegularExpressions;

namespace NzbDrone.Core.Manga.Connectors
{
    /// <summary>
    /// Rejects releases whose title signals a non-manga media format (audiobook, music,
    /// ebook-only, video, etc). Title-matching and volume-matching alone aren't enough to
    /// prove a release is usable - "Solo Leveling - Vol 1-8 - ... [m4b mp3] [AUDIOBOOK]"
    /// matches title and volume but is not manga. Shared by ProwlarrConnector and
    /// MangaSearchService so a bad release can't slip past filtering in one place only to
    /// be selected and downloaded via another entry point.
    /// </summary>
    public static class MangaReleaseFormatValidator
    {
        private static readonly Regex InvalidFormatPattern = new Regex(
            @"\b(audiobooks?|audio[\s.-]?books?|audio|unabridged|narrated|m4b|m4a|mp3|flac|aac|wav|music|soundtrack|ost|podcast|e-?books?|epub|mp4|mkv|avi|webrip|dvdrip)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// True if the release title contains no signal of a non-manga media format.
        /// Deliberately does not consider seeders/size - a valid CBZ/CBR/ZIP/RAR archive
        /// or NZB release must never be rejected just because it has zero seeders.
        /// </summary>
        public static bool IsValidFormat(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return true;
            }

            return !InvalidFormatPattern.IsMatch(title);
        }
    }
}
