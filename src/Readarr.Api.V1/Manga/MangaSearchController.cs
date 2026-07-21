using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NLog;
using NzbDrone.Core.Books;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Manga.Connectors;
using NzbDrone.Core.Qualities;
using Readarr.Api.V1.Indexers;
using Readarr.Http;

namespace Readarr.Api.V1.Manga
{
    [V1ApiController]
    public class MangaSearchController : Controller
    {
        private readonly IBookService _bookService;
        private readonly IAuthorService _authorService;
        private readonly MangaDexConnector _mangaDexConnector;
        private readonly Logger _logger;

        public MangaSearchController(
            IBookService bookService,
            IAuthorService authorService,
            MangaDexConnector mangaDexConnector,
            Logger logger)
        {
            _bookService = bookService;
            _authorService = authorService;
            _mangaDexConnector = mangaDexConnector;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<List<ReleaseResource>>> GetMangaReleases([FromQuery] int bookId)
        {
            try
            {
                var book = _bookService.GetBook(bookId);
                if (book == null)
                {
                    return NotFound($"Book with ID {bookId} not found");
                }

                var author = _authorService.GetAuthor(book.AuthorId);
                if (author == null)
                {
                    return NotFound($"Author for book {bookId} not found");
                }

                var foreignMangaId = author.ForeignAuthorId;
                if (string.IsNullOrEmpty(foreignMangaId))
                {
                    return BadRequest("Author does not have a MangaDex foreign ID");
                }

                // Try to parse volume number from book title (e.g., "Vol. 1" or "Volume 1")
                var volumeNumber = ExtractVolumeNumber(book.Title);
                if (volumeNumber == null)
                {
                    // Try parsing from ForeignBookId if it contains volume info
                    volumeNumber = ExtractVolumeNumber(book.ForeignBookId);
                }

                if (volumeNumber == null)
                {
                    return BadRequest($"Could not determine volume number from book title: {book.Title}");
                }

                _logger.Info("Searching MangaDex for manga {0}, volume {1}", foreignMangaId, volumeNumber.Value);

                var chapters = await _mangaDexConnector.GetChapterNumbersForVolumeAsync(foreignMangaId, volumeNumber.Value);

                var releases = chapters.Select(chapter => new ReleaseResource
                {
                    Guid = $"mangadex-{chapter.Key}",
                    Title = $"Vol {volumeNumber.Value} Ch {chapter.Value} (MangaDex)",
                    Quality = new QualityModel(Quality.Unknown),
                    Size = 0,
                    IndexerId = -1,
                    Indexer = "MangaDex",
                    AuthorName = author.Name,
                    BookTitle = book.Title,
                    Approved = true,
                    Rejected = false,
                    Rejections = new List<string>(),
                    DownloadUrl = $"https://mangadex.org/chapter/{chapter.Key}",
                    InfoUrl = $"https://mangadex.org/chapter/{chapter.Key}",
                    DownloadAllowed = true,
                    Protocol = DownloadProtocol.Torrent,
                    CommentUrl = $"https://mangadex.org/chapter/{chapter.Key}"
                }).ToList();

                return Ok(releases);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "MangaDex search failed for book {0}", bookId);
                return Ok(new List<ReleaseResource>());
            }
        }

        private int? ExtractVolumeNumber(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return null;
            }

            // Try common patterns: "Vol. 1", "Volume 1", "v1", "V1"
            var patterns = new[]
            {
                @"(?:vol\.?|volume)\s*(\d+)",
                @"^v(\d+)$",
                @"^V(\d+)$"
            };

            foreach (var pattern in patterns)
            {
                var match = global::System.Text.RegularExpressions.Regex.Match(input, pattern, global::System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (match.Success && int.TryParse(match.Groups[1].Value, out var vol))
                {
                    return vol;
                }
            }

            // Try to parse the entire string as a number
            if (int.TryParse(input, out var directVol))
            {
                return directVol;
            }

            return null;
        }
    }
}
