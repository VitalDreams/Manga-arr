using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NLog;
using NzbDrone.Common.Disk;

namespace NzbDrone.Core.Manga.Import
{
    public interface IMangaFileScanner
    {
        List<ScannedMangaFile> ScanDirectory(string path);
    }

    public class ScannedMangaFile
    {
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public long FileSize { get; set; }
        public string SeriesName { get; set; }
        public int? VolumeNumber { get; set; }
        public decimal? ChapterNumber { get; set; }
    }

    public class MangaFileScanner : IMangaFileScanner
    {
        private readonly IDiskProvider _diskProvider;
        private readonly Logger _logger;

        // Pattern: 'Berserk Vol.001 Ch.001.cbz' or 'Berserk v01 c001.cbz'
        private static readonly Regex VolumeChapterPattern = new Regex(
            @"^(.+?)[\s\-_.]+V(?:ol)?\.?\s*(\d+)[\s\-_.]+C(?:h)?\.?\s*([\d.]+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Pattern: 'Berserk - Vol.001.cbz' or 'Berserk.v01.2003.cbz'
        private static readonly Regex VolumeOnlyPattern = new Regex(
            @"^(.+?)[\s\-_.]+V(?:ol)?\.?\s*(\d+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Pattern: 'Berserk 1 (2003).cbz' -> Series=Berserk, Volume=1 (legacy Mylar3 format)
        private static readonly Regex LegacyMylarPattern = new Regex(
            @"^(.+?)\s+(\d+)\s*\(\d{4}\)",
            RegexOptions.Compiled);

        // Pattern: 'Berserk 001.cbz' -> Series=Berserk, Volume=1 (simple numbered)
        private static readonly Regex SimpleNumberPattern = new Regex(
            @"^(.+?)[\s\-_.]+(\d+)$",
            RegexOptions.Compiled);

        public MangaFileScanner(IDiskProvider diskProvider, Logger logger)
        {
            _diskProvider = diskProvider;
            _logger = logger;
        }

        public List<ScannedMangaFile> ScanDirectory(string path)
        {
            var results = new List<ScannedMangaFile>();

            if (!_diskProvider.FolderExists(path))
            {
                _logger.Warn("Directory does not exist: {0}", path);
                return results;
            }

            var files = _diskProvider.GetFiles(path, true)
                .Where(f => f.EndsWith(".cbz", StringComparison.OrdinalIgnoreCase))
                .ToList();

            _logger.Info("Scanning {0} for manga CBZ files, found {1} files", path, files.Count);

            foreach (var filePath in files)
            {
                try
                {
                    var scanned = ParseFile(filePath);
                    if (scanned != null)
                    {
                        results.Add(scanned);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Warn(ex, "Failed to parse manga file: {0}", filePath);
                }
            }

            _logger.Info("Scanned {0} manga files from {1}", results.Count, path);
            return results;
        }

        private ScannedMangaFile ParseFile(string filePath)
        {
            var fileInfo = _diskProvider.GetFileInfo(filePath);
            var fileName = Path.GetFileNameWithoutExtension(fileInfo.Name);

            var scanned = new ScannedMangaFile
            {
                FileName = fileInfo.Name,
                FilePath = filePath,
                FileSize = fileInfo.Length
            };

            // Try patterns in order of specificity
            if (TryParseVolumeChapter(fileName, scanned) ||
                TryParseVolumeOnly(fileName, scanned) ||
                TryParseLegacyMylar(fileName, scanned) ||
                TryParseSimpleNumber(fileName, scanned))
            {
                return scanned;
            }

            // Fallback: use filename as series name, no volume/chapter
            scanned.SeriesName = CleanSeriesName(fileName);
            _logger.Debug("Could not parse volume/chapter from '{0}', using series name '{1}'", fileName, scanned.SeriesName);
            return scanned;
        }

        private bool TryParseVolumeChapter(string fileName, ScannedMangaFile scanned)
        {
            var match = VolumeChapterPattern.Match(fileName);
            if (!match.Success)
            {
                return false;
            }

            scanned.SeriesName = CleanSeriesName(match.Groups[1].Value);
            scanned.VolumeNumber = int.Parse(match.Groups[2].Value);
            scanned.ChapterNumber = decimal.Parse(match.Groups[3].Value);
            return true;
        }

        private bool TryParseVolumeOnly(string fileName, ScannedMangaFile scanned)
        {
            var match = VolumeOnlyPattern.Match(fileName);
            if (!match.Success)
            {
                return false;
            }

            scanned.SeriesName = CleanSeriesName(match.Groups[1].Value);
            scanned.VolumeNumber = int.Parse(match.Groups[2].Value);
            return true;
        }

        private bool TryParseLegacyMylar(string fileName, ScannedMangaFile scanned)
        {
            var match = LegacyMylarPattern.Match(fileName);
            if (!match.Success)
            {
                return false;
            }

            scanned.SeriesName = CleanSeriesName(match.Groups[1].Value);
            scanned.VolumeNumber = int.Parse(match.Groups[2].Value);
            return true;
        }

        private bool TryParseSimpleNumber(string fileName, ScannedMangaFile scanned)
        {
            var match = SimpleNumberPattern.Match(fileName);
            if (!match.Success)
            {
                return false;
            }

            scanned.SeriesName = CleanSeriesName(match.Groups[1].Value);
            scanned.VolumeNumber = int.Parse(match.Groups[2].Value);
            return true;
        }

        private string CleanSeriesName(string name)
        {
            // Replace common separators with spaces and trim
            return Regex.Replace(name, @"[\-_\.]+", " ").Trim();
        }
    }
}
