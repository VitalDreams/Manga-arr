using System.IO.Abstractions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Manga.Import;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Manga
{
    [TestFixture]
    public class MangaFileScannerFixture : CoreTest<MangaFileScanner>
    {
        private string _testPath;

        [SetUp]
        public void Setup()
        {
            _testPath = "/manga/Berserk (2003)";

            Mocker.GetMock<IDiskProvider>()
                .Setup(x => x.FolderExists(_testPath))
                .Returns(true);
        }

        private void SetupFiles(params string[] fileNames)
        {
            var paths = new string[fileNames.Length];
            for (int i = 0; i < fileNames.Length; i++)
            {
                var name = fileNames[i];
                var fullPath = $"{_testPath}/{name}";
                paths[i] = fullPath;

                var fileInfo = new Mock<IFileInfo>();
                fileInfo.SetupGet(f => f.Name).Returns(name);
                fileInfo.SetupGet(f => f.FullName).Returns(fullPath);
                fileInfo.SetupGet(f => f.Length).Returns(1024L);

                Mocker.GetMock<IDiskProvider>()
                    .Setup(x => x.GetFileInfo(fullPath))
                    .Returns(fileInfo.Object);
            }

            Mocker.GetMock<IDiskProvider>()
                .Setup(x => x.GetFiles(_testPath, true))
                .Returns(paths);
        }

        [Test]
        public void should_find_cbz_files()
        {
            SetupFiles("Berserk 1 (2003).cbz", "Berserk 2 (2003).cbz");

            var result = Subject.ScanDirectory(_testPath);

            Assert.That(result.Count, Is.EqualTo(2));
        }

        [Test]
        public void should_find_cbr_files()
        {
            SetupFiles("Berserk 33 (2019).cbr", "Berserk 39 (2022).cbr");

            var result = Subject.ScanDirectory(_testPath);

            Assert.That(result.Count, Is.EqualTo(2));
            Assert.That(result[0].VolumeNumber, Is.EqualTo(33));
            Assert.That(result[1].VolumeNumber, Is.EqualTo(39));
        }

        [Test]
        public void should_find_mixed_cbz_and_cbr_files()
        {
            SetupFiles(
                "Berserk 1 (2003).cbz",
                "Berserk 33 (2019).cbr",
                "Berserk 39 (2022).cbr",
                "Berserk 41 (2022).cbz");

            var result = Subject.ScanDirectory(_testPath);

            Assert.That(result.Count, Is.EqualTo(4));
        }

        [Test]
        public void should_ignore_non_manga_files()
        {
            SetupFiles("Berserk 1 (2003).cbz", "readme.txt", "cover.jpg");

            var result = Subject.ScanDirectory(_testPath);

            Assert.That(result.Count, Is.EqualTo(1));
        }

        [Test]
        public void should_parse_legacy_mylar_format_volume_number()
        {
            SetupFiles("Berserk 1 (2003).cbz");

            var result = Subject.ScanDirectory(_testPath);

            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0].VolumeNumber, Is.EqualTo(1));
            Assert.That(result[0].SeriesName, Is.EqualTo("Berserk"));
        }

        [Test]
        public void should_parse_vol_chapter_format()
        {
            SetupFiles("Berserk - Vol.1 Ch.0.1.cbz");

            var result = Subject.ScanDirectory(_testPath);

            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0].VolumeNumber, Is.EqualTo(1));
            Assert.That(result[0].ChapterNumber, Is.EqualTo(0.1m));
            Assert.That(result[0].SeriesName, Is.EqualTo("Berserk"));
        }

        [Test]
        public void should_parse_all_berserk_volumes_1_through_41()
        {
            var files = new string[41];
            for (int i = 0; i < 41; i++)
            {
                files[i] = $"Berserk {i + 1} (2003).cbz";
            }
            SetupFiles(files);

            var result = Subject.ScanDirectory(_testPath);

            Assert.That(result.Count, Is.EqualTo(41));
            for (int i = 0; i < 41; i++)
            {
                Assert.That(result[i].VolumeNumber, Is.EqualTo(i + 1));
            }
        }
    }
}
