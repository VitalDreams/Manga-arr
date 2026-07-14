using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Manga.Connectors;
using Readarr.Http;

namespace Readarr.Api.V1.Manga
{
    [V1ApiController("manga/lookup")]
    public class MangaLookupController : Controller
    {
        private readonly IMangaMetadataConnector _mangaDexConnector;

        public MangaLookupController(IMangaMetadataConnector mangaDexConnector)
        {
            _mangaDexConnector = mangaDexConnector;
        }

        [HttpGet]
        public object Search([FromQuery] string term)
        {
            var searchResults = _mangaDexConnector.SearchAsync(term).GetAwaiter().GetResult();
            return searchResults.Select(r => new MangaLookupResource
            {
                ForeignMangaId = r.ForeignMangaId,
                Title = r.Title,
                Overview = r.Description,
                Author = r.Author,
                Status = r.Status,
                Year = r.Year,
                CoverUrl = r.CoverUrl,
                Genres = r.Genres
            }).ToList();
        }
    }

    public class MangaLookupResource
    {
        public string ForeignMangaId { get; set; }
        public string Title { get; set; }
        public string Overview { get; set; }
        public string Author { get; set; }
        public string Status { get; set; }
        public int Year { get; set; }
        public string CoverUrl { get; set; }
        public List<string> Genres { get; set; }
    }
}
