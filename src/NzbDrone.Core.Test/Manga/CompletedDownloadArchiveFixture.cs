using NUnit.Framework;
using NzbDrone.Core.Manga.Download;

namespace NzbDrone.Core.Test.Manga
{
    [TestFixture]
    public class CompletedDownloadArchiveFixture
    {
        [TestCase("Berserk Vol 001.cbz")]
        [TestCase("Berserk Vol 001.cbr")]
        [TestCase("Berserk Vol 001.cb7")]
        [TestCase("Berserk Vol 001.zip")]
        [TestCase("Berserk Vol 001.pdf")]
        [TestCase("BERSERK VOL 001.CBZ")]
        public void is_packaged_archive_should_be_true_for_finished_manga_containers(string fileName)
        {
            // Reproduces the live bug: a completed Usenet volume pack delivered as .cbr/.cb7/.zip
            // was discovered by FindDownloadedFiles as a "manga file" but then routed into
            // image-folder conversion (which never contains loose images for an archive), so it
            // was silently dropped instead of imported. Only raw image content should still need
            // CBZ conversion.
            Assert.That(CompletedDownloadArchive.IsPackagedArchive(fileName), Is.True);
        }

        [TestCase("page001.jpg")]
        [TestCase("page001.png")]
        [TestCase("Berserk Vol 001")]
        public void is_packaged_archive_should_be_false_for_raw_image_content(string fileName)
        {
            Assert.That(CompletedDownloadArchive.IsPackagedArchive(fileName), Is.False);
        }

        [Test]
        public void normalize_file_name_should_rewrite_zip_extension_to_cbz()
        {
            // A .zip archive is byte-for-byte the same container format as .cbz. The library
            // scanner and Komga only recognize .cbz/.cbr, so a Usenet pack delivered as plain
            // .zip must be renamed on import or it lands in the library folder invisibly.
            Assert.That(CompletedDownloadArchive.NormalizeFileName("Berserk Vol 001.zip"), Is.EqualTo("Berserk Vol 001.cbz"));
        }

        [TestCase("Berserk Vol 001.cbr")]
        [TestCase("Berserk Vol 001.cb7")]
        [TestCase("Berserk Vol 001.pdf")]
        public void normalize_file_name_should_leave_non_zip_extensions_unchanged(string fileName)
        {
            Assert.That(CompletedDownloadArchive.NormalizeFileName(fileName), Is.EqualTo(fileName));
        }
    }
}
