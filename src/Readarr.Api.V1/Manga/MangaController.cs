using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Core.AuthorStats;
using NzbDrone.Core.Books;
using NzbDrone.Core.Datastore.Events;
using NzbDrone.Core.Manga;
using NzbDrone.Core.Manga.Connectors;
using NzbDrone.Core.Manga.Import;
using NzbDrone.Core.MediaCover;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Parser;
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
        private readonly IAuthorStatisticsService _authorStatisticsService;
        private readonly IMangaMetadataConnector _metadataConnector;
        private readonly IMapCoversToLocal _coverMapper;
        private readonly IHttpClient _httpClient;
        private readonly IMangaSearchService _mangaSearchService;
        private readonly IMangaSeriesService _mangaSeriesService;
        private readonly IMangaSeriesRepository _mangaSeriesRepository;
        private readonly IMangaMetadataRepository _mangaMetadataRepository;
        private readonly IMangaFileService _mangaFileService;
        private readonly IMediaFileService _mediaFileService;
        private readonly IVolumeRepository _volumeRepository;
        private readonly IMangaFileMigrationService _mangaFileMigrationService;
        private readonly IMangaImportService _importService;
        private readonly Logger _logger;

        public MangaController(
            IAuthorService authorService,
            IAuthorMetadataService authorMetadataService,
            IBookService bookService,
            IAuthorStatisticsService authorStatisticsService,
            IMangaMetadataConnector metadataConnector,
            IMapCoversToLocal coverMapper,
            IHttpClient httpClient,
            IMangaSearchService mangaSearchService,
            IMangaSeriesService mangaSeriesService,
            IMangaSeriesRepository mangaSeriesRepository,
            IMangaMetadataRepository mangaMetadataRepository,
            IMangaFileService mangaFileService,
            IMediaFileService mediaFileService,
            IVolumeRepository volumeRepository,
            IMangaFileMigrationService mangaFileMigrationService,
            IMangaImportService importService,
            IBroadcastSignalRMessage signalRBroadcaster,
            Logger logger)
            : base(signalRBroadcaster)
        {
            _authorService = authorService;
            _authorMetadataService = authorMetadataService;
            _bookService = bookService;
            _authorStatisticsService = authorStatisticsService;
            _metadataConnector = metadataConnector;
            _coverMapper = coverMapper;
            _httpClient = httpClient;
            _mangaSearchService = mangaSearchService;
            _mangaSeriesService = mangaSeriesService;
            _mangaSeriesRepository = mangaSeriesRepository;
            _mangaMetadataRepository = mangaMetadataRepository;
            _mangaFileService = mangaFileService;
            _mediaFileService = mediaFileService;
            _volumeRepository = volumeRepository;
            _mangaFileMigrationService = mangaFileMigrationService;
            _importService = importService;
            _logger = logger;

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

            EnsureNativeSeries(author, resource);
            var series = ResolveMangaSeries(author);
            if (series != null)
            {
                EnsureMangaFileMigration(author, series);
                _importService.ReconcileSeries(series);
                _coverMapper.EnsureMangaCovers(author.Id, series);
            }
            var nativeVolumes = series == null ? new List<Volume>() : _volumeRepository.FindByMangaSeriesId(series.Id);
            resource.Volumes = nativeVolumes
                .Select(v => v.ToResource(_mangaFileService.GetFilesByVolume(v.Id)))
                .ToList();

            var books = _bookService.GetBooksByAuthor(id);

            var stats = _authorStatisticsService.AuthorStatistics(id);
            resource.Statistics = new MangaStatisticsResource
            {
                TotalVolumes = resource.Volumes.Count,
                MonitoredVolumes = resource.Volumes.Count(v => v.Monitored),
                DownloadedVolumes = stats.BookFileCount,
                TotalChapters = 0,
                DownloadedChapters = 0,
                BookCount = stats.BookCount,
                TotalBookCount = stats.TotalBookCount,
                BookFileCount = stats.BookFileCount,
                AvailableBookCount = stats.AvailableBookCount,
                SizeOnDisk = stats.SizeOnDisk
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
            var allStats = _authorStatisticsService.AuthorStatistics().ToDictionary(x => x.AuthorId);
            var resources = new List<MangaResource>();

            foreach (var author in allAuthors)
            {
                var resource = author.ToMangaResource();

                EnsureNativeSeries(author, resource);
                var series = ResolveMangaSeries(author);
                if (series != null)
                {
                    EnsureMangaFileMigration(author, series);
                    _importService.ReconcileSeries(series);
                }
                var nativeVolumes = series == null ? new List<Volume>() : _volumeRepository.FindByMangaSeriesId(series.Id);
                resource.Volumes = nativeVolumes
                    .Select(v => v.ToResource(_mangaFileService.GetFilesByVolume(v.Id)))
                    .ToList();

                allStats.TryGetValue(author.Id, out var stats);
                resource.Statistics = new MangaStatisticsResource
                {
                    TotalVolumes = resource.Volumes.Count,
                    MonitoredVolumes = resource.Volumes.Count(v => v.Monitored),
                    DownloadedVolumes = stats?.BookFileCount ?? 0,
                    TotalChapters = 0,
                    DownloadedChapters = 0,
                    BookCount = stats?.BookCount ?? 0,
                    TotalBookCount = stats?.TotalBookCount ?? 0,
                    BookFileCount = stats?.BookFileCount ?? 0,
                    AvailableBookCount = stats?.AvailableBookCount ?? 0,
                    SizeOnDisk = stats?.SizeOnDisk ?? 0
                };

                MapCoverToLocal(resource);
                resources.Add(resource);
            }

            return resources;
        }

        [RestPostById]
        public async Task<ActionResult<MangaResource>> AddManga([FromBody] MangaResource mangaResource)
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

            // Fetch the volume/chapter map from MangaDex once and reuse it for both the Books
            // mirror (below) and the MangaSeries/Volume mirror, instead of fetching it twice.
            var volumeMap = await FetchVolumeChapterMapAsync(addedAuthor, mangaResource.ForeignMangaId);

            // Fetch volumes from MangaDex and store as Books
            StoreVolumesAsBooks(addedAuthor, mangaResource.ForeignMangaId, volumeMap);

            // Mirror into the manga-native tables so search/download can operate on it
            await EnsureMangaSeriesAsync(addedAuthor, mangaResource, volumeMap);

            var resource = addedAuthor.ToMangaResource();

            EnsureNativeSeries(addedAuthor, resource);
            var nativeSeries = ResolveMangaSeries(addedAuthor);
            if (nativeSeries != null)
            {
                EnsureMangaFileMigration(addedAuthor, nativeSeries);
            }

            var nativeVolumes = nativeSeries == null ? new List<Volume>() : _volumeRepository.FindByMangaSeriesId(nativeSeries.Id);
            resource.Volumes = nativeVolumes
                .Select(v => v.ToResource(_mangaFileService.GetFilesByVolume(v.Id)))
                .ToList();

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
            existing.CleanName = (mangaResource.Title ?? string.Empty).CleanAuthorName();
            existing.Path = mangaResource.Path;
            existing.QualityProfileId = mangaResource.QualityProfileId;
            existing.MetadataProfileId = mangaResource.MetadataProfileId;
            existing.Monitored = mangaResource.Monitored;
            existing.RootFolderPath = mangaResource.RootFolderPath;

            _authorService.UpdateAuthor(existing);

            // Keep the manga-native tables (MangaMetadata/MangaSeries) in sync with the
            // Author/AuthorMetadata mirror so search and download completion (which operate
            // on MangaSeries) see the updated root/path, profiles, monitored state and metadata.
            SyncMangaSeries(existing, mangaResource);

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

            var series = ResolveMangaSeries(author);
            if (series == null)
            {
                return Ok(new MangaSearchAndDownloadResult
                {
                    Success = false,
                    ErrorMessage = "No manga series is registered for this author. Try re-adding the manga."
                });
            }

            try
            {
                var result = await _mangaSearchService.SearchAndDownloadAsync(series.Id);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Manga search failed for series {0} (ID: {1})", author.Name, id);
                return Ok(new MangaSearchAndDownloadResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                });
            }
        }

        [HttpPost("{id}/search/{bookId}")]
        public async Task<ActionResult<MangaSearchAndDownloadResult>> SearchVolume(int id, int bookId)
        {
            var author = _authorService.GetAuthor(id);
            if (author == null)
            {
                return NotFound();
            }

            var book = _bookService.GetBook(bookId);
            if (book == null || book.AuthorId != id)
            {
                return NotFound($"Book {bookId} not found for manga {id}");
            }

            var volumeNumber = ExtractVolumeNumber(book.Title) ?? ExtractVolumeNumber(book.ForeignBookId);
            if (!volumeNumber.HasValue)
            {
                return BadRequest($"Could not determine volume number from book '{book.Title}'");
            }

            var series = ResolveMangaSeries(author);
            if (series == null)
            {
                return Ok(new MangaSearchAndDownloadResult
                {
                    Success = false,
                    ErrorMessage = "No manga series is registered for this author. Try re-adding the manga."
                });
            }

            try
            {
                var result = await _mangaSearchService.SearchAndDownloadAsync(series.Id, volumeNumber.Value);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Manga search failed for series {0} volume {1} (ID: {2})", author.Name, volumeNumber.Value, id);
                return Ok(new MangaSearchAndDownloadResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                });
            }
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
            var authorStats = _authorStatisticsService.AuthorStatistics(id);
            var bookStatsDict = authorStats?.BookStatistics?.ToDictionary(v => v.BookId);
            var series = ResolveMangaSeries(author);
            var nativeVolumes = series == null ? new List<Volume>() : _volumeRepository.FindByMangaSeriesId(series.Id);

            return books.Select(b =>
            {
                var resource = new BookResource
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
                };

                if (bookStatsDict != null && bookStatsDict.TryGetValue(b.Id, out var stats))
                {
                    resource.Statistics = stats.ToResource();
                }

                var volumeNumber = ExtractVolumeNumber(b.Title) ?? ExtractVolumeNumber(b.ForeignBookId);
                var nativeVolume = volumeNumber.HasValue
                    ? nativeVolumes.FirstOrDefault(v => v.VolumeNumber == volumeNumber.Value)
                    : null;
                if (nativeVolume != null)
                {
                    var mangaFiles = _mangaFileService.GetFilesByVolume(nativeVolume.Id);
                    resource.Statistics = new BookStatisticsResource
                    {
                        BookFileCount = mangaFiles.Count,
                        BookCount = 1,
                        TotalBookCount = 1,
                        SizeOnDisk = mangaFiles.Sum(f => f.Size)
                    };
                }

                return resource;
            }).ToList();
        }

        private async Task<VolumeChapterMap> FetchVolumeChapterMapAsync(NzbDrone.Core.Books.Author author, string foreignMangaId)
        {
            if (string.IsNullOrEmpty(foreignMangaId))
            {
                return null;
            }

            try
            {
                return await _metadataConnector.GetVolumeChapterMapAsync(foreignMangaId);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to fetch volume/chapter map for manga {0} (ForeignMangaId: {1})", author.Name, foreignMangaId);
                return null;
            }
        }

        private void StoreVolumesAsBooks(NzbDrone.Core.Books.Author author, string foreignMangaId, VolumeChapterMap volumeMap)
        {
            if (string.IsNullOrEmpty(foreignMangaId) || volumeMap?.VolumeChapters == null || volumeMap.VolumeChapters.Count == 0)
            {
                return;
            }

            try
            {
                foreach (var entry in volumeMap.VolumeChapters.OrderBy(v => v.Key))
                {
                    var volumeNumber = entry.Key;

                    var book = new Book
                    {
                        ForeignBookId = $"{foreignMangaId}_vol{volumeNumber}",
                        Title = $"Volume {volumeNumber}",
                        TitleSlug = $"{foreignMangaId}-vol{volumeNumber}",
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
                            TitleSlug = book.TitleSlug,
                            ForeignEditionId = book.ForeignBookId,
                            Monitored = true
                        }
                    };

                    _bookService.AddBook(book, false);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to fetch and store volumes for manga {0} (ForeignMangaId: {1})", author.Name, foreignMangaId);
            }
        }

        private void EnsureNativeSeries(NzbDrone.Core.Books.Author author, MangaResource resource)
        {
            if (ResolveMangaSeries(author) != null || string.IsNullOrEmpty(resource?.ForeignMangaId))
            {
                return;
            }

            try
            {
                var volumeMap = FetchVolumeChapterMapAsync(author, resource.ForeignMangaId).GetAwaiter().GetResult();
                EnsureMangaSeriesAsync(author, resource, volumeMap).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to lazily mirror manga {0} into native tables", author.Name);
            }
        }

        private void EnsureMangaFileMigration(NzbDrone.Core.Books.Author author, MangaSeries series)
        {
            try
            {
                var volumes = _volumeRepository.FindByMangaSeriesId(series.Id);
                var legacyFiles = _mediaFileService.GetFilesByAuthor(author.Id);
                var existingFilesByVolume = volumes.ToDictionary(
                    v => v.Id,
                    v => _mangaFileService.GetFilesByVolume(v.Id));

                _mangaFileMigrationService.MigrateLegacyBookFiles(author.Id, volumes, legacyFiles, existingFilesByVolume, series.Id);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to migrate legacy BookFiles to MangaFiles for manga {0}", author.Name);
            }
        }

        private MangaSeries ResolveMangaSeries(NzbDrone.Core.Books.Author author)
        {
            var foreignMangaId = author?.Metadata?.Value?.ForeignAuthorId;
            if (string.IsNullOrEmpty(foreignMangaId))
            {
                return null;
            }

            var metadata = _mangaMetadataRepository.FindByForeignMangaId(foreignMangaId);
            if (metadata == null)
            {
                return null;
            }

            return _mangaSeriesRepository.FindByMangaMetadataId(metadata.Id);
        }

        private async Task EnsureMangaSeriesAsync(NzbDrone.Core.Books.Author author, MangaResource mangaResource, VolumeChapterMap volumeMap)
        {
            var foreignMangaId = mangaResource?.ForeignMangaId;
            if (string.IsNullOrEmpty(foreignMangaId))
            {
                return;
            }

            try
            {
                var existingMetadata = _mangaMetadataRepository.FindByForeignMangaId(foreignMangaId);
                var existingSeries = existingMetadata == null ? null : _mangaSeriesRepository.FindByMangaMetadataId(existingMetadata.Id);
                if (existingSeries != null)
                {
                    await _mangaSeriesService.FetchAndStoreVolumesAsync(existingSeries, volumeMap);
                    return;
                }

                var mangaMetadata = new MangaMetadata
                {
                    ForeignMangaId = foreignMangaId,
                    Title = mangaResource.Title,
                    TitleSlug = mangaResource.TitleSlug,
                    Description = mangaResource.Overview,
                    Author = mangaResource.Author,
                    Artist = mangaResource.Artist,
                    Demographic = mangaResource.Demographic,
                    Status = mangaResource.Status,
                    Year = mangaResource.Year,
                    TotalVolumes = mangaResource.TotalVolumes,
                    TotalChapters = mangaResource.TotalChapters,
                    Genres = mangaResource.Genres ?? new List<string>(),
                    ContentType = ContentType.Manga,
                    CoverUrl = mangaResource.CoverUrl
                };

                var series = new MangaSeries
                {
                    Metadata = mangaMetadata,
                    Path = author.Path,
                    RootFolderPath = author.RootFolderPath,
                    QualityProfileId = author.QualityProfileId,
                    MetadataProfileId = author.MetadataProfileId,
                    Monitored = author.Monitored,
                    Added = author.Added,
                    CleanName = author.CleanName
                };

                var addedSeries = _mangaSeriesService.AddSeries(series);
                await _mangaSeriesService.FetchAndStoreVolumesAsync(addedSeries, volumeMap);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to mirror manga into MangaSeries for author {0} (ForeignMangaId: {1})", author.Name, foreignMangaId);
            }
        }

        private void SyncMangaSeries(NzbDrone.Core.Books.Author author, MangaResource mangaResource)
        {
            var foreignMangaId = author?.Metadata?.Value?.ForeignAuthorId;
            if (string.IsNullOrEmpty(foreignMangaId))
            {
                return;
            }

            try
            {
                var existingMetadata = _mangaMetadataRepository.FindByForeignMangaId(foreignMangaId);
                if (existingMetadata == null)
                {
                    return;
                }

                var series = _mangaSeriesRepository.FindByMangaMetadataId(existingMetadata.Id);
                if (series == null)
                {
                    return;
                }

                // Mirror the relevant metadata fields (leave fields not exposed on MangaResource,
                // e.g. DownloadMode, untouched so we don't clobber them with defaults).
                existingMetadata.Title = mangaResource.Title;
                existingMetadata.TitleSlug = mangaResource.TitleSlug;
                existingMetadata.Description = mangaResource.Overview;
                existingMetadata.Author = mangaResource.Author;
                existingMetadata.Artist = mangaResource.Artist;
                existingMetadata.Demographic = mangaResource.Demographic;
                existingMetadata.Status = mangaResource.Status;
                existingMetadata.Year = mangaResource.Year;
                existingMetadata.TotalVolumes = mangaResource.TotalVolumes;
                existingMetadata.TotalChapters = mangaResource.TotalChapters;
                existingMetadata.Genres = mangaResource.Genres ?? existingMetadata.Genres;

                if (mangaResource.CoverUrl.IsNotNullOrWhiteSpace())
                {
                    existingMetadata.CoverUrl = mangaResource.CoverUrl;
                }

                series.Metadata = existingMetadata;
                series.Path = author.Path;
                series.RootFolderPath = author.RootFolderPath;
                series.QualityProfileId = author.QualityProfileId;
                series.MetadataProfileId = author.MetadataProfileId;
                series.Monitored = author.Monitored;
                series.MonitorNewItems = author.MonitorNewItems;
                series.CleanName = author.CleanName;

                _mangaSeriesService.UpdateSeries(series);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to sync MangaSeries for author {0} (ForeignMangaId: {1})", author.Name, foreignMangaId);
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

        private void MapCoverToLocal(MangaResource resource)
        {
            if (resource.CoverUrl.IsNotNullOrWhiteSpace())
            {
                var covers = new List<MediaCover>
                {
                    new MediaCover(MediaCoverTypes.Cover, resource.CoverUrl)
                };
                _coverMapper.ConvertToLocalUrls(resource.Id, MediaCoverEntity.Manga, covers);
                resource.CoverUrl = covers[0].Url;
            }
        }
    }
}
