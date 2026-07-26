using System;
using System.Collections.Generic;
using System.IO;

namespace NzbDrone.Core.Manga.Download
{
    /// <summary>
    /// Classifies files found in a completed download's output path. A file is either an
    /// already-packaged manga archive/document that only needs moving into the library, or raw
    /// image content that still needs to be CBZ-ified. Shared by MangaDownloadCompletionHandler's
    /// file discovery and per-file routing so the two can never disagree about which files are
    /// "already packaged" - previously .cbr/.cb7/.zip were discovered as manga files but then
    /// routed into image-folder conversion (which never contains loose images for an archive),
    /// so they were silently dropped instead of imported.
    /// </summary>
    public static class CompletedDownloadArchive
    {
        public static readonly HashSet<string> PackagedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".cbz", ".cbr", ".cb7", ".zip", ".pdf"
        };

        public static bool IsPackagedArchive(string fileName)
        {
            return !string.IsNullOrEmpty(fileName) && PackagedExtensions.Contains(Path.GetExtension(fileName));
        }

        /// <summary>
        /// A .zip archive is byte-for-byte the same container format as .cbz. Normalize the
        /// extension on import so the library scanner and Komga (which only recognize
        /// .cbz/.cbr) can find it - otherwise a Usenet volume pack delivered as a plain .zip
        /// would land in the library folder invisibly.
        /// </summary>
        public static string NormalizeFileName(string fileName)
        {
            if (!string.IsNullOrEmpty(fileName) && fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                return Path.ChangeExtension(fileName, ".cbz");
            }

            return fileName;
        }
    }
}
