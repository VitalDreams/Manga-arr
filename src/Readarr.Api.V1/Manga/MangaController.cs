using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Core.Books;
using NzbDrone.Core.Datastore.Events;
using NzbDrone.Core.MediaCover;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Manga.Connectors;
using NzbDrone.Http.REST.Attributes;
using NzbDrone.SignalR;
using Readarr.Api.V1.Books;
using Readarr.Http;
using Readarr.Http.REST;

namespace Readarr.Api.V1.Manga
{
    [V1ApiController]
    public class MangaController : RestControllerWithSignalR<MangaResource, NzbDrone.Core.Books.Author>
    {
        private readonly IAuthorService _authorService;
        private readonly IAuthorMetadataService _authorMetadataService;
        private readonly IBookService _bookService;
        private readonly IMangaMetadataConnector _metadataConnector;
        private readonly IMapCoversToLocal _coverMapper;
        private readonly IHttpClient _httpClient;

        public MangaController(
            IAuthorService authorService,
            IAuthorMetadataService authorMetadataService,
            IBookService bookService,
            IMangaMetadataConnector metadataConnector,
            IMapCoversToLocal coverMapper,
            IHttpClient httpClient,
            IBroadcastSignalRMessage signalRBroadcaster)
            : base(signalRBroadcaster)
        {
            _authorService = authorService;
            _authorMetadataService = authorMetadataService;
            _bookService = bookService;
            _metadataConnector = metadataConnector;
            _coverMapper = coverMapper;
            _httpClient = httpClient;

            SharedValidator.RuleFor(s => s.Title).NotEmpty();
            SharedValidator.RuleFor(s => s.Path).NotEmpty();
            PostValidator.RuleFor(s => s.ForeignMangaId).NotEmpty();
        }

        protected override MangaResource GetResourceById(int id)
        {
            var author = _authorService.GetAuthor(id);
            if (author == null)
            {
                return null;
            }

            var resource = author.ToMangaResource();

            // Load books (volumes) for this manga
            var books = _bookService.GetBooksByAuthor(id);
            resource.Volumes = books.Select(b => b.ToVolumeResource(id)).ToList();

            resource.Statistics = new MangaStatisticsResource
            {
                TotalVolumes = books.Count,
                MonitoredVolumes = books.Count(b => b.Monitored),
                DownloadedVolumes = 0,
                TotalChapters = 0,
                DownloadedChapters = 0
            };

            // Map cover URL to local path
            MapCoverToLocal(resource);

            return resource;
        }

        [HttpGet]
        [Produces("application/json")]
        public List<MangaResource> AllManga()
        {
            var allAuthors = _authorService.GetAllAuthors();
            var resources = new List<MangaResource>();

            foreach (var author in allAuthors)
            {
                var resource = author.ToMangaResource();

                var books = _bookService.GetBooksByAuthor(author.Id);
                resource.Volumes = books.Select(b => b.ToVolumeResource(author.Id)).ToList();

                resource.Statistics = new MangaStatisticsResource
                {
                    TotalVolumes = books.Count,
                    MonitoredVolumes = books.Count(b => b.Monitored),
                    DownloadedVolumes = 0,
                    TotalChapters = 0,
                    DownloadedChapters = 0
                };

                MapCoverToLocal(resource);
                resources.Add(resource);
            }

            return resources;
        }

        [RestPostById]
        public ActionResult<MangaResource> AddManga([FromBody] MangaResource mangaResource)
        {
            // Create AuthorMetadata from manga resource
            var metadata = mangaResource.ToAuthorMetadata();
            _authorMetadataService.Upsert(metadata);

            // Create Author record
            var author = mangaResource.ToAuthorModel();
            author.AuthorMetadataId = metadata.Id;
            author.Metadata = metadata;
            author.Added = DateTime.UtcNow;

            var addedAuthor = _authorService.AddAuthor(author, false);

            // Fetch volumes from MangaDex and store as Books
            FetchAndStoreVolumes(addedAuthor, mangaResource.ForeignMangaId);

            var resource = addedAuthor.ToMangaResource();

            // Load the books we just created
            var books = _bookService.GetBooksByAuthor(addedAuthor.Id);
            resource.Volumes = books.Select(b => b.ToVolumeResource(addedAuthor.Id)).ToList();

            MapCoverToLocal(resource);

            return Created(addedAuthor.Id);
        }

        [RestPutById]
        public ActionResult<MangaResource> UpdateManga([FromBody] MangaResource mangaResource)
        {
            var existing = _authorService.GetAuthor(mangaResource.Id);
            if (existing == null)
            {
                return NotFound();
            }

            // Update metadata using ToAuthorMetadata which preserves structured manga data in Overview
            var newMetadata = mangaResource.ToAuthorMetadata();
            var metadata = existing.Metadata.Value;
            metadata.Name = newMetadata.Name;
            metadata.Overview = newMetadata.Overview;
            metadata.Genres = newMetadata.Genres;
            metadata.Status = newMetadata.Status;
            if (newMetadata.Images?.Count > 0)
            {
                metadata.Images = newMetadata.Images;
            }
            _authorMetadataService.Upsert(metadata);

            // Update author
            existing.CleanName = (mangaResource.Title ?? string.Empty).ToLowerInvariant().Replace(" ", string.Empty);
            existing.Path = mangaResource.Path;
            existing.QualityProfileId = mangaResource.QualityProfileId;
            existing.MetadataProfileId = mangaResource.MetadataProfileId;
            existing.Monitored = mangaResource.Monitored;
            existing.RootFolderPath = mangaResource.RootFolderPath;

            _authorService.UpdateAuthor(existing);

            BroadcastResourceChange(ModelAction.Updated, mangaResource);
            return Accepted(mangaResource.Id);
        }

        [RestDeleteById]
        public void DeleteManga(int id, bool deleteFiles = false)
        {
            _authorService.DeleteAuthor(id, deleteFiles);
        }

        [AllowAnonymous]
        [HttpGet("cover")]
        public IActionResult GetCover([FromQuery] string url)
        {
            if (string.IsNullOrEmpty(url) || !url.StartsWith("https://uploads.mangadex.org/"))
            {
                return BadRequest("Invalid cover URL");
            }

            try
            {
                var request = new HttpRequest(url);
                var response = _httpClient.Get(request);

                var contentType = response.Headers.ContentType ?? "image/jpeg";

                Response.Headers["Cache-Control"] = "public, max-age=86400";

                return File(response.ResponseData, contentType);
            }
            catch (Exception)
            {
                return StatusCode(502, "Failed to fetch cover image");
            }
        }

        [HttpPost("{id}/search")]
        public async Task<ActionResult<MangaSearchAndDownloadResult>> SearchAllVolumes(int id)
        {
            var author = _authorService.GetAuthor(id);
            if (author == null)
            {
                return NotFound();
            }

            // Search is not yet implemented in this backend-only refactor
            return Ok(new MangaSearchAndDownloadResult());
        }

        [HttpPost("{id}/search/{volumeId}")]
        public async Task<ActionResult<MangaSearchAndDownloadResult>> SearchVolume(int id, int volumeId)
        {
            var author = _authorService.GetAuthor(id);
            if (author == null)
            {
                return NotFound();
            }

            // Search is not yet implemented in this backend-only refactor
            return Ok(new MangaSearchAndDownloadResult());
        }

        [HttpGet("{id}/books")]
        public List<BookResource> GetMangaBooks(int id)
        {
            var author = _authorService.GetAuthor(id);
            if (author == null)
            {
                return new List<BookResource>();
            }

            var books = _bookService.GetBooksByAuthor(id);

            return books.Select(b => new BookResource
            {
                Id = b.Id,
                Title = b.Title,
                AuthorId = id,
                ForeignBookId = b.ForeignBookId,
                TitleSlug = b.TitleSlug,
                Monitored = b.Monitored,
                AnyEditionOk = b.AnyEditionOk,
                ReleaseDate = b.ReleaseDate,
                Genres = b.Genres,
                Ratings = b.Ratings ?? new Ratings(),
                Added = b.Added,
                LastSearchTime = b.LastSearchTime
            }).ToList();
        }

        private void FetchAndStoreVolumes(NzbDrone.Core.Books.Author author, string foreignMangaId)
        {
            if (string.IsNullOrEmpty(foreignMangaId))
            {
                return;
            }

            try
            {
                var volumeMap = _metadataConnector.GetVolumeChapterMapAsync(foreignMangaId).GetAwaiter().GetResult();
                if (volumeMap?.VolumeChapters == null || volumeMap.VolumeChapters.Count == 0)
                {
                    return;
                }

                foreach (var entry in volumeMap.VolumeChapters.OrderBy(v => v.Key))
                {
                    var volumeNumber = entry.Key;

                    var book = new Book
                    {
                        ForeignBookId = $"{foreignMangaId}_vol{volumeNumber}",
                        Title = $"Volume {volumeNumber}",
                        TitleSlug = $"volume-{volumeNumber}",
                        CleanTitle = $"volume {volumeNumber}",
                        Monitored = true,
                        AnyEditionOk = true,
                        ReleaseDate = null,
                        Added = DateTime.UtcNow,
                        AuthorMetadataId = author.AuthorMetadataId,
                        Author = new NzbDrone.Core.Books.Author { Id = author.Id },
                        AuthorMetadata = new AuthorMetadata { Id = author.AuthorMetadataId }
                    };

                    book.Editions = new List<Edition>
                    {
                        new Edition
                        {
                            Title = book.Title,
                            ForeignEditionId = book.ForeignBookId,
                            Monitored = true
                        }
                    };

                    _bookService.AddBook(book, false);
                }
            }
            catch (Exception ex)
            {
                // Log but don't fail the add operation
            }
        }

        private void MapCoverToLocal(MangaResource resource)
        {
            if (resource.CoverUrl.IsNotNullOrWhiteSpace())
            {
                var covers = new List<MediaCover>
                {
                    new MediaCover(MediaCoverTypes.Poster, resource.CoverUrl)
                };
                _coverMapper.ConvertToLocalUrls(resource.Id, MediaCoverEntity.Author, covers);
                resource.CoverUrl = covers[0].Url;
            }
        }
    }
}
