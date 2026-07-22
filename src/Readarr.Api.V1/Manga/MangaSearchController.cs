using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NLog;
using NzbDrone.Core.Books;
using NzbDrone.Core.Manga;
using Readarr.Http;

namespace Readarr.Api.V1.Manga
{
    [V1ApiController]
    public class MangaSearchController : Controller
    {
        private readonly IBookService _bookService;
        private readonly IAuthorService _authorService;
        private readonly IMangaMetadataRepository _mangaMetadataRepository;
        private readonly IMangaSeriesRepository _mangaSeriesRepository;
        private readonly IMangaSearchService _mangaSearchService;
        private readonly Logger _logger;

        public MangaSearchController(
            IBookService bookService,
            IAuthorService authorService,
            IMangaMetadataRepository mangaMetadataRepository,
            IMangaSeriesRepository mangaSeriesRepository,
            IMangaSearchService mangaSearchService,
            Logger logger)
        {
            _bookService = bookService;
            _authorService = authorService;
            _mangaMetadataRepository = mangaMetadataRepository;
            _mangaSeriesRepository = mangaSeriesRepository;
            _mangaSearchService = mangaSearchService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<MangaSearchAndDownloadResult>> Search([FromQuery] int? bookId = null, [FromQuery] string foreignMangaId = null, [FromQuery] int? volumeNumber = null)
        {
            int seriesId;
            var resolvedVolumeNumber = volumeNumber;

            if (!string.IsNullOrEmpty(foreignMangaId))
            {
                var metadata = _mangaMetadataRepository.FindByForeignMangaId(foreignMangaId);
                if (metadata == null)
                {
                    return NotFound($"No manga found with foreign ID {foreignMangaId}");
                }

                var series = _mangaSeriesRepository.FindByMangaMetadataId(metadata.Id);
                if (series == null)
                {
                    return NotFound($"No manga series registered for foreign ID {foreignMangaId}");
                }

                seriesId = series.Id;
            }
            else if (bookId.HasValue)
            {
                var book = _bookService.GetBook(bookId.Value);
                if (book == null)
                {
                    return NotFound($"Book with ID {bookId} not found");
                }

                var author = _authorService.GetAuthor(book.AuthorId);
                if (author == null)
                {
                    return NotFound($"Author for book {bookId} not found");
                }

                var foreignId = author.Metadata?.Value?.ForeignAuthorId;
                if (string.IsNullOrEmpty(foreignId))
                {
                    return BadRequest("Author does not have a MangaDex foreign ID");
                }

                var metadata = _mangaMetadataRepository.FindByForeignMangaId(foreignId);
                if (metadata == null)
                {
                    return NotFound($"No manga metadata for foreign ID {foreignId}");
                }

                var series = _mangaSeriesRepository.FindByMangaMetadataId(metadata.Id);
                if (series == null)
                {
                    return NotFound($"No manga series registered for foreign ID {foreignId}");
                }

                seriesId = series.Id;

                if (!resolvedVolumeNumber.HasValue)
                {
                    resolvedVolumeNumber = ExtractVolumeNumber(book.Title) ?? ExtractVolumeNumber(book.ForeignBookId);
                }
            }
            else
            {
                return BadRequest("Either bookId or foreignMangaId is required");
            }

            try
            {
                var result = await _mangaSearchService.SearchAndDownloadAsync(seriesId, resolvedVolumeNumber);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Manga search failed for series {0}", seriesId);
                return Ok(new MangaSearchAndDownloadResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                });
            }
        }

        private static int? ExtractVolumeNumber(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return null;
            }

            var patterns = new[]
            {
                @"(?:vol\.?|volume)\s*(\d+)",
                @"_vol(\d+)$",
                @"^v(\d+)$"
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(input, pattern, RegexOptions.IgnoreCase);
                if (match.Success && int.TryParse(match.Groups[1].Value, out var vol))
                {
                    return vol;
                }
            }

            if (int.TryParse(input, out var directVol))
            {
                return directVol;
            }

            return null;
        }
    }
}
