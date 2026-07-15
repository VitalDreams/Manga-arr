using System;
using System.Collections.Generic;
using System.Linq;
using NLog;

namespace NzbDrone.Core.Manga
{
    public interface IVolumePackTracker
    {
        bool IsChapterCoveredByPack(int seriesId, decimal chapterNumber);
        void RecordVolumePack(int seriesId, int volumeId, IEnumerable<decimal> chapterNumbers);
        List<decimal> GetCoveredChapters(int seriesId);
        void ClearCoverageForVolume(int seriesId, int volumeId);
    }

    public class VolumePackTracker : IVolumePackTracker
    {
        private readonly IMangaFileService _mangaFileService;
        private readonly Logger _logger;

        // In-memory cache of covered chapters per series
        // Key: seriesId, Value: set of covered chapter numbers
        private readonly Dictionary<int, HashSet<decimal>> _coverageCache = new();
        private readonly object _cacheLock = new object();

        public VolumePackTracker(IMangaFileService mangaFileService, Logger logger)
        {
            _mangaFileService = mangaFileService;
            _logger = logger;
        }

        public bool IsChapterCoveredByPack(int seriesId, decimal chapterNumber)
        {
            lock (_cacheLock)
            {
                if (_coverageCache.TryGetValue(seriesId, out var covered))
                {
                    return covered.Contains(chapterNumber);
                }
            }

            // Cache miss - load from database
            LoadCoverageFromDatabase(seriesId);

            lock (_cacheLock)
            {
                if (_coverageCache.TryGetValue(seriesId, out var covered))
                {
                    return covered.Contains(chapterNumber);
                }
            }

            return false;
        }

        public void RecordVolumePack(int seriesId, int volumeId, IEnumerable<decimal> chapterNumbers)
        {
            var chapters = chapterNumbers.ToList();

            lock (_cacheLock)
            {
                if (!_coverageCache.TryGetValue(seriesId, out var covered))
                {
                    covered = new HashSet<decimal>();
                    _coverageCache[seriesId] = covered;
                }

                foreach (var chapter in chapters)
                {
                    covered.Add(chapter);
                }
            }

            _logger.Info($"Recorded volume pack coverage for series {seriesId}, volume {volumeId}: {chapters.Count} chapters");
        }

        public List<decimal> GetCoveredChapters(int seriesId)
        {
            lock (_cacheLock)
            {
                if (_coverageCache.TryGetValue(seriesId, out var covered))
                {
                    return covered.ToList();
                }
            }

            LoadCoverageFromDatabase(seriesId);

            lock (_cacheLock)
            {
                if (_coverageCache.TryGetValue(seriesId, out var covered))
                {
                    return covered.ToList();
                }
            }

            return new List<decimal>();
        }

        public void ClearCoverageForVolume(int seriesId, int volumeId)
        {
            // Remove from cache
            lock (_cacheLock)
            {
                if (_coverageCache.TryGetValue(seriesId, out var covered))
                {
                    // We need to reload from DB since we don't track which chapters belong to which volume in cache
                    _coverageCache.Remove(seriesId);
                }
            }

            _logger.Info($"Cleared volume pack coverage for series {seriesId}, volume {volumeId}");
        }

        private void LoadCoverageFromDatabase(int seriesId)
        {
            try
            {
                var volumePackFiles = _mangaFileService.GetFilesBySeries(seriesId)
                    .Where(f => f.IsVolumePack && !string.IsNullOrEmpty(f.CoveredChapters))
                    .ToList();

                var coveredChapters = new HashSet<decimal>();

                foreach (var file in volumePackFiles)
                {
                    var chapters = ParseCoveredChapters(file.CoveredChapters);
                    foreach (var chapter in chapters)
                    {
                        coveredChapters.Add(chapter);
                    }
                }

                lock (_cacheLock)
                {
                    _coverageCache[seriesId] = coveredChapters;
                }

                _logger.Debug($"Loaded {coveredChapters.Count} covered chapters for series {seriesId}");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to load volume pack coverage for series {seriesId}");
            }
        }

        private static List<decimal> ParseCoveredChapters(string coveredChapters)
        {
            if (string.IsNullOrWhiteSpace(coveredChapters))
            {
                return new List<decimal>();
            }

            return coveredChapters.Split(',')
                .Select(s => decimal.TryParse(s.Trim(), out var d) ? d : -1)
                .Where(d => d >= 0)
                .ToList();
        }
    }

    public static class VolumePackChapterSerializer
    {
        public static string SerializeChapters(IEnumerable<decimal> chapterNumbers)
        {
            return string.Join(",", chapterNumbers.Select(c => c.ToString("0.###")));
        }

        public static List<decimal> DeserializeChapters(string coveredChapters)
        {
            if (string.IsNullOrWhiteSpace(coveredChapters))
            {
                return new List<decimal>();
            }

            return coveredChapters.Split(',')
                .Select(s => decimal.TryParse(s.Trim(), out var d) ? d : -1)
                .Where(d => d >= 0)
                .ToList();
        }
    }
}
