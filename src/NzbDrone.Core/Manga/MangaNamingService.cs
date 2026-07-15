using System;
using System.IO;
using System.Linq;
using System.Text;

namespace NzbDrone.Core.Manga
{
    public interface IMangaNamingService
    {
        string GetChapterFileName(MangaSeries series, Volume volume, Chapter chapter, string template = null);
        string GetVolumeFileName(MangaSeries series, Volume volume, string template = null);
        string GetSeriesFolder(MangaSeries series, string template = null);
        string GetChapterFilePath(string rootFolderPath, MangaSeries series, Volume volume, Chapter chapter);
        string GetVolumeFilePath(string rootFolderPath, MangaSeries series, Volume volume);
    }

    public class MangaNamingService : IMangaNamingService
    {
        public const string DefaultChapterTemplate = "$Series Vol.$Volume Ch.$Chapter ($Year).cbz";
        public const string DefaultVolumeTemplate = "$Series Vol.$Volume ($Year).cbz";
        public const string DefaultFolderTemplate = "$Series ($Year)";

        public string GetChapterFileName(MangaSeries series, Volume volume, Chapter chapter, string template = null)
        {
            template ??= DefaultChapterTemplate;

            var result = ReplaceTokens(template, series, volume, chapter);
            return SanitizeFileName(result);
        }

        public string GetVolumeFileName(MangaSeries series, Volume volume, string template = null)
        {
            template ??= DefaultVolumeTemplate;

            var result = ReplaceTokens(template, series, volume, null);
            return SanitizeFileName(result);
        }

        public string GetSeriesFolder(MangaSeries series, string template = null)
        {
            template ??= DefaultFolderTemplate;

            var result = ReplaceTokens(template, series, null, null);
            return SanitizeFolderName(result);
        }

        public string GetChapterFilePath(string rootFolderPath, MangaSeries series, Volume volume, Chapter chapter)
        {
            var folder = GetSeriesFolder(series);
            var fileName = GetChapterFileName(series, volume, chapter);
            return Path.Combine(rootFolderPath, folder, fileName);
        }

        public string GetVolumeFilePath(string rootFolderPath, MangaSeries series, Volume volume)
        {
            var folder = GetSeriesFolder(series);
            var fileName = GetVolumeFileName(series, volume);
            return Path.Combine(rootFolderPath, folder, fileName);
        }

        private string ReplaceTokens(string template, MangaSeries series, Volume volume, Chapter chapter)
        {
            var metadata = series.Metadata?.Value;
            var result = template;

            result = result.Replace("$Series", series.Name ?? "Unknown");
            result = result.Replace("$Year", metadata?.Year.ToString() ?? "Unknown");
            result = result.Replace("$Author", metadata?.Author ?? "Unknown");
            result = result.Replace("$Publisher", metadata?.Publisher ?? "Unknown");
            result = result.Replace("$Language", chapter?.Language ?? "en");
            result = result.Replace("$LanguageISO", MapLanguageToISO(metadata?.OriginalLanguage));

            if (volume != null)
            {
                result = result.Replace("$Volume", volume.VolumeNumber.ToString());
            }

            if (chapter != null)
            {
                result = result.Replace("$Chapter", chapter.ChapterNumber.ToString("0.###"));
            }

            return result;
        }

        private string MapLanguageToISO(string language)
        {
            if (string.IsNullOrEmpty(language))
            {
                return "Unknown";
            }

            return language.ToLowerInvariant() switch
            {
                "ja" => "JP",
                "ko" => "KR",
                "zh" => "CN",
                "en" => "EN",
                _ => language.ToUpperInvariant()
            };
        }

        private string SanitizeFileName(string name)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder(name.Length);

            foreach (var c in name)
            {
                if (invalidChars.Contains(c))
                {
                    sb.Append('_');
                }
                else
                {
                    sb.Append(c);
                }
            }

            // Trim trailing dots and spaces (Windows compatibility)
            var result = sb.ToString().TrimEnd('.', ' ');

            return string.IsNullOrEmpty(result) ? "Unknown" : result;
        }

        private string SanitizeFolderName(string name)
        {
            var invalidChars = Path.GetInvalidPathChars().Concat(new[] { ':', '*', '?', '"', '<', '>', '|' }).ToArray();
            var sb = new StringBuilder(name.Length);

            foreach (var c in name)
            {
                if (invalidChars.Contains(c))
                {
                    sb.Append('_');
                }
                else
                {
                    sb.Append(c);
                }
            }

            var result = sb.ToString().TrimEnd('.', ' ');

            return string.IsNullOrEmpty(result) ? "Unknown" : result;
        }
    }
}
