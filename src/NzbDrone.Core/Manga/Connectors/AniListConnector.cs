using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace NzbDrone.Core.Manga.Connectors
{
    public class AniListConnector : IMangaMetadataConnector
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private const string AniListApiUrl = "https://graphql.anilist.co";
        private const int RateLimitDelayMs = 600;

        public string Name => "AniList";
        public string BaseUrl => AniListApiUrl;
        public bool Enabled { get; set; } = true;

        public async Task<List<MangaSearchResult>> SearchAsync(string query, int limit = 10)
        {
            var graphqlQuery = @"
                query ($search: String, $type: MediaType) {
                    Page(page: 1, perPage: " + limit + @") {
                        media(search: $search, type: $type, format: MANGA) {
                            id
                            title { romaji english native }
                            description
                            status
                            format
                            startDate { year month day }
                            coverImage { large medium }
                            genres
                            tags { name }
                            meanScore
                            popularity
                            chapters
                            volumes
                            staff(edges: { role: WRITER }) {
                                node { name { full } }
                            }
                        }
                    }
                }";

            var variables = new { search = query, type = "MANGA" };
            var response = await PostGraphQLAsync<AniListPageResponse>(graphqlQuery, variables);

            return response?.Data?.Page?.Media?.Select(m => new MangaSearchResult
            {
                ForeignMangaId = $"anilist-{m.Id}",
                Title = m.Title?.English ?? m.Title?.Romaji ?? m.Title?.Native,
                Description = CleanDescription(m.Description),
                Author = m.Staff?.Edges?.FirstOrDefault()?.Node?.Name?.Full,
                Artist = null,
                Status = MapStatus(m.Status),
                Demographic = null,
                Year = m.StartDate?.Year ?? 0,
                CoverUrl = m.CoverImage?.Large ?? m.CoverImage?.Medium,
                Genres = m.Genres?.ToList() ?? new List<string>(),
                ContentRating = null
            }).ToList() ?? new List<MangaSearchResult>();
        }

        public async Task<MangaMetadata> GetMangaMetadataAsync(string foreignMangaId)
        {
            var anilistId = foreignMangaId.Replace("anilist-", "");
            if (!int.TryParse(anilistId, out var id))
            {
                return null;
            }

            var graphqlQuery = @"
                query ($id: Int) {
                    Media(id: $id, type: MANGA) {
                        id
                        title { romaji english native }
                        description(asHtml: false)
                        status
                        format
                        startDate { year month day }
                        endDate { year month day }
                        coverImage { large medium extraLarge }
                        bannerImage
                        genres
                        tags { name isGeneralSpoiler }
                        meanScore
                        popularity
                        favourites
                        chapters
                        volumes
                        source
                        staff(edges: { role: WRITER }) {
                            node { name { full } }
                        }
                    }
                }";

            var variables = new { id };
            var response = await PostGraphQLAsync<AniListMediaResponse>(graphqlQuery, variables);
            var m = response?.Data?.Media;

            if (m == null) return null;

            return new MangaMetadata
            {
                ForeignMangaId = $"anilist-{m.Id}",
                Title = m.Title?.English ?? m.Title?.Romaji ?? m.Title?.Native,
                Description = CleanDescription(m.Description),
                Author = m.Staff?.Edges?.FirstOrDefault()?.Node?.Name?.Full,
                Artist = null,
                Status = MapStatus(m.Status),
                ContentRating = null,
                Year = m.StartDate?.Year ?? 0,
                TotalVolumes = m.Volumes ?? 0,
                TotalChapters = m.Chapters ?? 0,
                Genres = m.Genres?.ToList() ?? new List<string>(),
                Tags = m.Tags?.Where(t => !t.IsGeneralSpoiler).Select(t => t.Name).ToList() ?? new List<string>(),
                CoverUrl = m.CoverImage?.ExtraLarge ?? m.CoverImage?.Large ?? m.CoverImage?.Medium,
                LastInfoSync = DateTime.UtcNow
            };
        }

        public Task<VolumeChapterMap> GetVolumeChapterMapAsync(string foreignMangaId)
        {
            return Task.FromResult(new VolumeChapterMap
            {
                ForeignMangaId = foreignMangaId,
                VolumeChapters = new Dictionary<int, List<string>>()
            });
        }

        public Task<List<ChapterInfo>> GetChaptersForVolumeAsync(string foreignMangaId, int volumeNumber)
        {
            return Task.FromResult(new List<ChapterInfo>());
        }

        public Task<ChapterPages> GetChapterPagesAsync(string foreignChapterId)
        {
            return Task.FromResult(new ChapterPages());
        }

        public async Task<string> GetCoverUrlAsync(string foreignMangaId)
        {
            var metadata = await GetMangaMetadataAsync(foreignMangaId);
            return metadata?.CoverUrl;
        }

        private async Task<T> PostGraphQLAsync<T>(string query, object variables)
        {
            await Task.Delay(RateLimitDelayMs);

            var body = JsonSerializer.Serialize(new { query, variables });
            var content = new StringContent(body, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(AniListApiUrl, content);
            var responseString = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<T>(responseString);
        }

        private string CleanDescription(string description)
        {
            if (string.IsNullOrEmpty(description)) return description;
            var clean = System.Text.RegularExpressions.Regex.Replace(description, "<[^>]+>", "");
            clean = System.Text.RegularExpressions.Regex.Replace(clean, "\\(spoiler\\)", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return clean.Trim();
        }

        private string MapStatus(string anilistStatus)
        {
            return anilistStatus?.ToUpper() switch
            {
                "FINISHED" => "completed",
                "RELEASING" => "ongoing",
                "NOT_YET_RELEASED" => "not_yet_published",
                "CANCELLED" => "cancelled",
                "HIATUS" => "hiatus",
                _ => "ongoing"
            };
        }
    }

    // Response models
    public class AniListPageResponse
    {
        [JsonPropertyName("data")]
        public AniListPageData Data { get; set; }
    }

    public class AniListPageData
    {
        [JsonPropertyName("Page")]
        public AniListPage Page { get; set; }
    }

    public class AniListPage
    {
        [JsonPropertyName("media")]
        public List<AniListMedia> Media { get; set; }
    }

    public class AniListMediaResponse
    {
        [JsonPropertyName("data")]
        public AniListMediaData Data { get; set; }
    }

    public class AniListMediaData
    {
        [JsonPropertyName("Media")]
        public AniListMedia Media { get; set; }
    }

    public class AniListMedia
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }
        [JsonPropertyName("title")]
        public AniListTitle Title { get; set; }
        [JsonPropertyName("description")]
        public string Description { get; set; }
        [JsonPropertyName("status")]
        public string Status { get; set; }
        [JsonPropertyName("format")]
        public string Format { get; set; }
        [JsonPropertyName("startDate")]
        public AniListDate StartDate { get; set; }
        [JsonPropertyName("endDate")]
        public AniListDate EndDate { get; set; }
        [JsonPropertyName("coverImage")]
        public AniListCoverImage CoverImage { get; set; }
        [JsonPropertyName("bannerImage")]
        public string BannerImage { get; set; }
        [JsonPropertyName("genres")]
        public List<string> Genres { get; set; }
        [JsonPropertyName("tags")]
        public List<AniListTag> Tags { get; set; }
        [JsonPropertyName("meanScore")]
        public int? MeanScore { get; set; }
        [JsonPropertyName("popularity")]
        public int? Popularity { get; set; }
        [JsonPropertyName("favourites")]
        public int? Favourites { get; set; }
        [JsonPropertyName("chapters")]
        public int? Chapters { get; set; }
        [JsonPropertyName("volumes")]
        public int? Volumes { get; set; }
        [JsonPropertyName("source")]
        public string Source { get; set; }
        [JsonPropertyName("staff")]
        public AniListStaffConnection Staff { get; set; }
    }

    public class AniListTitle
    {
        [JsonPropertyName("romaji")]
        public string Romaji { get; set; }
        [JsonPropertyName("english")]
        public string English { get; set; }
        [JsonPropertyName("native")]
        public string Native { get; set; }
    }

    public class AniListDate
    {
        [JsonPropertyName("year")]
        public int? Year { get; set; }
        [JsonPropertyName("month")]
        public int? Month { get; set; }
        [JsonPropertyName("day")]
        public int? Day { get; set; }
    }

    public class AniListCoverImage
    {
        [JsonPropertyName("large")]
        public string Large { get; set; }
        [JsonPropertyName("medium")]
        public string Medium { get; set; }
        [JsonPropertyName("extraLarge")]
        public string ExtraLarge { get; set; }
    }

    public class AniListTag
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }
        [JsonPropertyName("isGeneralSpoiler")]
        public bool IsGeneralSpoiler { get; set; }
    }

    public class AniListStaffConnection
    {
        [JsonPropertyName("edges")]
        public List<AniListStaffEdge> Edges { get; set; }
    }

    public class AniListStaffEdge
    {
        [JsonPropertyName("role")]
        public string Role { get; set; }
        [JsonPropertyName("node")]
        public AniListStaff Node { get; set; }
    }

    public class AniListStaff
    {
        [JsonPropertyName("name")]
        public AniListStaffName Name { get; set; }
    }

    public class AniListStaffName
    {
        [JsonPropertyName("full")]
        public string Full { get; set; }
    }
}
