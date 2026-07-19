using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.Books;

namespace NzbDrone.Core.Manga.Connectors
{
    public class MangaDexConnector : IMangaMetadataConnector
    {
        private readonly IHttpClient _httpClient;
        private const string MangaDexApiUrl = "https://api.mangadex.org";
        private const int RateLimitDelayMs = 200; // 5 req/s

        public string Name => "MangaDex";
        public string BaseUrl => MangaDexApiUrl;
        public bool Enabled { get; set; } = true;

        public MangaDexConnector(IHttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<List<MangaSearchResult>> SearchAsync(string query, int limit = 10)
        {
            var url = $"{MangaDexApiUrl}/manga?title={Uri.EscapeDataString(query)}&limit={limit}&includes[]=author&includes[]=artist&includes[]=cover_art";

            var response = await GetAsync<MangaDexResponse<MangaDexManga>>(url);

            return response?.Data?.Select(m => new MangaSearchResult
            {
                ForeignMangaId = m.Id,
                Title = m.Attributes?.Title?.GetValueOrDefault("en") ?? m.Attributes?.Title?.Values.FirstOrDefault(),
                Description = m.Attributes?.Description?.GetValueOrDefault("en") ?? m.Attributes?.Description?.Values.FirstOrDefault(),
                Author = m.Relationships?.FirstOrDefault(r => r.Type == "author")?.Attributes?.Name,
                Artist = m.Relationships?.FirstOrDefault(r => r.Type == "artist")?.Attributes?.Name,
                Status = m.Attributes?.Status,
                Demographic = m.Attributes?.PublicationDemographic,
                Year = m.Attributes?.Year ?? 0,
                CoverUrl = GetCoverUrl(m),
                Genres = m.Attributes?.Tags?.Where(t => t.Attributes?.Group == "genre").Select(t => t.Attributes?.Name?.GetValueOrDefault("en")).ToList() ?? new List<string>(),
                ContentRating = m.Attributes?.ContentRating,
                ContentType = m.Attributes?.OriginalLanguage switch
                {
                    "ja" => ContentType.Manga,
                    "ko" => ContentType.Manhwa,
                    "zh" => ContentType.Manhua,
                    _ => ContentType.Other
                }
            }).ToList();
        }

        public async Task<MangaMetadata> GetMangaMetadataAsync(string foreignMangaId)
        {
            var url = $"{MangaDexApiUrl}/manga/{foreignMangaId}?includes[]=author&includes[]=artist&includes[]=cover_art";
            var response = await GetAsync<MangaDexResponseSingle<MangaDexManga>>(url);
            var m = response?.Data;

            if (m == null)
            {
                return null;
            }

            var primaryTitle = m.Attributes?.Title?.GetValueOrDefault("en") ?? m.Attributes?.Title?.Values.FirstOrDefault();
            var alternateTitles = m.Attributes?.Title?
                .Where(kvp => kvp.Value != primaryTitle)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
                ?? new Dictionary<string, string>();

            return new MangaMetadata
            {
                ForeignMangaId = m.Id,
                Title = primaryTitle,
                Description = m.Attributes?.Description?.GetValueOrDefault("en") ?? m.Attributes?.Description?.Values.FirstOrDefault(),
                Author = m.Relationships?.FirstOrDefault(r => r.Type == "author")?.Attributes?.Name,
                Artist = m.Relationships?.FirstOrDefault(r => r.Type == "artist")?.Attributes?.Name,
                OriginalLanguage = m.Attributes?.OriginalLanguage,
                Demographic = m.Attributes?.PublicationDemographic,
                Status = m.Attributes?.Status,
                ContentRating = m.Attributes?.ContentRating,
                ContentType = m.Attributes?.OriginalLanguage switch
                {
                    "ja" => ContentType.Manga,
                    "ko" => ContentType.Manhwa,
                    "zh" => ContentType.Manhua,
                    _ => ContentType.Other
                },
                Year = m.Attributes?.Year ?? 0,
                Genres = m.Attributes?.Tags?.Where(t => t.Attributes?.Group == "genre").Select(t => t.Attributes?.Name?.GetValueOrDefault("en")).ToList() ?? new List<string>(),
                Tags = m.Attributes?.Tags?.Where(t => t.Attributes?.Group == "theme").Select(t => t.Attributes?.Name?.GetValueOrDefault("en")).ToList() ?? new List<string>(),
                AlternateTitles = alternateTitles,
                CoverUrl = GetCoverUrl(m),
                LastInfoSync = DateTime.UtcNow
            };
        }

        public async Task<VolumeChapterMap> GetVolumeChapterMapAsync(string foreignMangaId)
        {
            var url = $"{MangaDexApiUrl}/manga/{foreignMangaId}/aggregate";
            var response = await GetAsync<MangaDexAggregate>(url);

            var volumeChapters = new Dictionary<int, List<string>>();

            if (response?.Volumes != null)
            {
                foreach (var volume in response.Volumes)
                {
                    if (int.TryParse(volume.Key, out var volumeNumber))
                    {
                        var chapterIds = volume.Value.Chapters?.Select(c => c.Value.Id).ToList() ?? new List<string>();
                        volumeChapters[volumeNumber] = chapterIds;
                    }
                }
            }

            return new VolumeChapterMap
            {
                ForeignMangaId = foreignMangaId,
                VolumeChapters = volumeChapters
            };
        }

        public async Task<List<ChapterInfo>> GetChaptersForVolumeAsync(string foreignMangaId, int volumeNumber)
        {
            var url = $"{MangaDexApiUrl}/manga/{foreignMangaId}/feed?volume[]={volumeNumber}&translatedLanguage[]=en&order[chapter]=asc&limit=100";

            var response = await GetAsync<MangaDexResponse<MangaDexChapter>>(url);

            return response?.Data?.Select(c => new ChapterInfo
            {
                ForeignChapterId = c.Id,
                Title = c.Attributes?.Title ?? $"Chapter {c.Attributes?.Chapter}",
                ChapterNumber = decimal.TryParse(c.Attributes?.Chapter, out var num) ? num : 0,
                VolumeNumber = volumeNumber,
                Language = c.Attributes?.TranslatedLanguage,
                ScanlationGroup = c.Relationships?.FirstOrDefault(r => r.Type == "scanlation_group")?.Attributes?.Name,
                PageCount = c.Attributes?.Pages ?? 0,
                ReleaseDate = c.Attributes?.PublishAt
            }).ToList();
        }

        public async Task<ChapterPages> GetChapterPagesAsync(string foreignChapterId)
        {
            var url = $"{MangaDexApiUrl}/at-home/server/{foreignChapterId}";
            var response = await GetAsync<MangaDexAtHome>(url);

            var baseUrl = response?.BaseUrl;
            return new ChapterPages
            {
                ForeignChapterId = foreignChapterId,
                BaseUrl = baseUrl,
                PageUrls = response?.Chapter?.Data?.Select(f => $"{baseUrl}/data/{response.Chapter.Hash}/{f}").ToList() ?? new List<string>(),
                Hash = response?.Chapter?.Hash
            };
        }

        public async Task<string> GetCoverUrlAsync(string foreignMangaId)
        {
            var url = $"{MangaDexApiUrl}/manga/{foreignMangaId}?includes[]=cover_art";
            var response = await GetAsync<MangaDexResponseSingle<MangaDexManga>>(url);
            return response?.Data != null ? GetCoverUrl(response.Data) : null;
        }

        public Task<List<StoryArcInfo>> GetStoryArcsAsync(string foreignMangaId)
        {
            // MangaDex does not yet expose a direct story arc API.
            // This stub returns an empty list; arcs can be created manually via the API
            // or detected from volume groupings in a future implementation.
            return Task.FromResult(new List<StoryArcInfo>());
        }

        private string GetCoverUrl(MangaDexManga manga)
        {
            var coverRel = manga.Relationships?.FirstOrDefault(r => r.Type == "cover_art");
            var fileName = coverRel?.Attributes?.FileName;
            return fileName != null ? $"https://uploads.mangadex.org/covers/{manga.Id}/{fileName}" : null;
        }

        private async Task<T> GetAsync<T>(string url) where T : new()
        {
            await Task.Delay(RateLimitDelayMs); // Rate limiting
            var request = new HttpRequestBuilder(url).Build();
            var response = await _httpClient.GetAsync(request);
            return Json.Deserialize<T>(response.Content);
        }
    }

    // MangaDex API response models
    public class MangaDexResponse<T>
    {
        public List<T> Data { get; set; }
    }

    public class MangaDexResponseSingle<T>
    {
        public T Data { get; set; }
    }

    public class MangaDexManga
    {
        public string Id { get; set; }
        public MangaDexMangaAttributes Attributes { get; set; }
        public List<MangaDexRelationship> Relationships { get; set; }
    }

    public class MangaDexMangaAttributes
    {
        public Dictionary<string, string> Title { get; set; }
        public Dictionary<string, string> Description { get; set; }
        public string OriginalLanguage { get; set; }
        public string PublicationDemographic { get; set; }
        public string Status { get; set; }
        public string ContentRating { get; set; }
        public int? Year { get; set; }
        public List<MangaDexTag> Tags { get; set; }
    }

    public class MangaDexTag
    {
        public string Id { get; set; }
        public MangaDexTagAttributes Attributes { get; set; }
    }

    public class MangaDexTagAttributes
    {
        public Dictionary<string, string> Name { get; set; }
        public string Group { get; set; }
    }

    public class MangaDexRelationship
    {
        public string Id { get; set; }
        public string Type { get; set; }
        public MangaDexRelationshipAttributes Attributes { get; set; }
    }

    public class MangaDexRelationshipAttributes
    {
        public string Name { get; set; }
        public string FileName { get; set; }
    }

    public class MangaDexChapter
    {
        public string Id { get; set; }
        public MangaDexChapterAttributes Attributes { get; set; }
        public List<MangaDexRelationship> Relationships { get; set; }
    }

    public class MangaDexChapterAttributes
    {
        public string Title { get; set; }
        public string Chapter { get; set; }
        public string TranslatedLanguage { get; set; }
        public int Pages { get; set; }
        public DateTime? PublishAt { get; set; }
    }

    public class MangaDexAggregate
    {
        public Dictionary<string, MangaDexVolume> Volumes { get; set; }
    }

    public class MangaDexVolume
    {
        public string Volume { get; set; }
        public Dictionary<string, MangaDexChapterRef> Chapters { get; set; }
    }

    public class MangaDexChapterRef
    {
        public string Id { get; set; }
        public string Chapter { get; set; }
    }

    public class MangaDexAtHome
    {
        public string BaseUrl { get; set; }
        public MangaDexAtHomeChapter Chapter { get; set; }
    }

    public class MangaDexAtHomeChapter
    {
        public string Hash { get; set; }
        public List<string> Data { get; set; }
    }
}
