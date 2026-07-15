using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Manga.Connectors;
using Readarr.Api.V1.Manga;
using Readarr.Http;

namespace Readarr.Api.V1.Search
{
    [V1ApiController]
    public class SearchController : Controller
    {
        private readonly IMetadataAggregator _metadataAggregator;

        public SearchController(IMetadataAggregator metadataAggregator)
        {
            _metadataAggregator = metadataAggregator;
        }

        [HttpGet]
        public object Search([FromQuery] string term)
        {
            var searchResults = _metadataAggregator.SearchAsync(term).GetAwaiter().GetResult();
            if (searchResults == null)
            {
                return new List<MangaLookupResource>();
            }

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
}
