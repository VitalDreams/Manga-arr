using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Books;
using NzbDrone.Core.MediaCover;
using Readarr.Http.REST;

namespace Readarr.Api.V1.Manga
{
    public class MangaResource : RestResource
    {
        [JsonIgnore]
        public int MangaMetadataId { get; set; }

        public string ForeignMangaId { get; set; }
        public string Title { get; set; }
        public string TitleSlug { get; set; }
        public string Overview { get; set; }
        public string Author { get; set; }
        public string Artist { get; set; }
        public string Status { get; set; }
        public string Demographic { get; set; }
        public int Year { get; set; }
        public int TotalVolumes { get; set; }
        public int TotalChapters { get; set; }
        public List<string> Genres { get; set; }
        public List<Links> Links { get; set; }
        public string CoverUrl { get; set; }
        public Ratings Ratings { get; set; }

        // View & Edit
        public string Path { get; set; }
        public int QualityProfileId { get; set; }
        public int MetadataProfileId { get; set; }

        // Editing Only
        public bool Monitored { get; set; }
        public NewItemMonitorTypes MonitorNewItems { get; set; }

        public string RootFolderPath { get; set; }
        public string CleanName { get; set; }
        public string SortName { get; set; }

        public HashSet<int> Tags { get; set; }
        public DateTime Added { get; set; }

        public MangaStatisticsResource Statistics { get; set; }
        public List<VolumeResource> Volumes { get; set; }
    }

    public class MangaStatisticsResource
    {
        public int TotalVolumes { get; set; }
        public int DownloadedVolumes { get; set; }
        public int MonitoredVolumes { get; set; }
        public int TotalChapters { get; set; }
        public int DownloadedChapters { get; set; }
    }

    public static class MangaResourceMapper
    {
        // Prefix for structured manga metadata stored in Overview
        private const string MangaMetaPrefix = "\n\n[MANGA_META:";

        public static MangaResource ToMangaResource(this NzbDrone.Core.Books.Author model)
        {
            if (model == null)
            {
                return null;
            }

            var metadata = model.Metadata?.Value;
            var (overview, mangaAuthor, artist, demographic, year, totalVolumes, totalChapters) = ParseOverview(metadata?.Overview);

            return new MangaResource
            {
                Id = model.Id,
                MangaMetadataId = model.AuthorMetadataId,

                ForeignMangaId = metadata?.ForeignAuthorId,
                Title = metadata?.Name,
                TitleSlug = model.CleanName,
                Overview = overview,
                Author = mangaAuthor,
                Artist = artist,
                Status = metadata?.Status == AuthorStatusType.Continuing ? "continuing" : "ended",
                Demographic = demographic,
                Year = year,
                TotalVolumes = totalVolumes,
                TotalChapters = totalChapters,
                Genres = metadata?.Genres ?? new List<string>(),
                Links = metadata?.Links ?? new List<Links>(),
                CoverUrl = metadata?.Images?.FirstOrDefault(i => i.CoverType == MediaCoverTypes.Poster)?.Url,
                Ratings = metadata?.Ratings ?? new Ratings(),

                Path = model.Path,
                QualityProfileId = model.QualityProfileId,
                MetadataProfileId = model.MetadataProfileId,
                Monitored = model.Monitored,
                MonitorNewItems = model.MonitorNewItems,
                RootFolderPath = model.RootFolderPath,
                CleanName = model.CleanName,

                Tags = model.Tags,
                Added = model.Added,
                Statistics = new MangaStatisticsResource()
            };
        }

        public static NzbDrone.Core.Books.Author ToAuthorModel(this MangaResource resource)
        {
            if (resource == null)
            {
                return null;
            }

            return new NzbDrone.Core.Books.Author
            {
                Id = resource.Id,
                CleanName = (resource.Title ?? string.Empty).ToLowerInvariant().Replace(" ", string.Empty),
                Path = resource.Path,
                QualityProfileId = resource.QualityProfileId,
                MetadataProfileId = resource.MetadataProfileId,
                Monitored = resource.Monitored,
                MonitorNewItems = resource.MonitorNewItems,
                RootFolderPath = resource.RootFolderPath,
                Tags = resource.Tags ?? new HashSet<int>(),
                ContentType = ContentType.Manga,
                Added = resource.Added
            };
        }

        public static AuthorMetadata ToAuthorMetadata(this MangaResource resource)
        {
            if (resource == null)
            {
                return null;
            }

            var title = resource.Title ?? string.Empty;

            var metadata = new AuthorMetadata
            {
                ForeignAuthorId = resource.ForeignMangaId,
                Name = resource.Title,
                SortName = title.ToLower(),
                NameLastFirst = resource.Title,
                SortNameLastFirst = title.ToLower(),
                TitleSlug = resource.TitleSlug ?? title.ToLowerInvariant().Replace(" ", "-"),
                Overview = BuildOverview(resource.Overview, resource.Author, resource.Artist, resource.Demographic, resource.Year, resource.TotalVolumes, resource.TotalChapters),
                Genres = resource.Genres ?? new List<string>(),
                Status = AuthorStatusType.Ended
            };

            // Map manga status to AuthorStatusType
            if (resource.Status != null)
            {
                metadata.Status = resource.Status.ToLowerInvariant() switch
                {
                    "ongoing" => AuthorStatusType.Continuing,
                    "continuing" => AuthorStatusType.Continuing,
                    "completed" => AuthorStatusType.Ended,
                    "ended" => AuthorStatusType.Ended,
                    _ => AuthorStatusType.Ended
                };
            }

            // Map cover URL to Images
            if (resource.CoverUrl.IsNotNullOrWhiteSpace())
            {
                metadata.Images = new List<MediaCover>
                {
                    new MediaCover(MediaCoverTypes.Poster, resource.CoverUrl)
                };
            }

            return metadata;
        }

        public static VolumeResource ToVolumeResource(this Book model, int authorId)
        {
            if (model == null)
            {
                return null;
            }

            return new VolumeResource
            {
                Id = model.Id,
                AuthorId = authorId,
                ForeignVolumeId = model.ForeignBookId,
                Title = model.Title,
                ReleaseDate = model.ReleaseDate,
                Monitored = model.Monitored,
                Added = model.Added
            };
        }

        public static List<MangaResource> ToMangaResource(this IEnumerable<NzbDrone.Core.Books.Author> models)
        {
            return models?.Select(ToMangaResource).ToList();
        }

        /// <summary>
        /// Builds the Overview field by combining the user-provided overview with structured manga metadata.
        /// Format: "User overview\n\n[MANGA_META:Author=X|Artist=Y|Demographic=Z|Year=W|TotalVolumes=V|TotalChapters=C]"
        /// </summary>
        private static string BuildOverview(string overview, string author, string artist, string demographic, int year, int totalVolumes, int totalChapters)
        {
            var parts = new List<string>();
            if (author.IsNotNullOrWhiteSpace()) parts.Add($"Author={Escape(author)}");
            if (artist.IsNotNullOrWhiteSpace()) parts.Add($"Artist={Escape(artist)}");
            if (demographic.IsNotNullOrWhiteSpace()) parts.Add($"Demographic={Escape(demographic)}");
            if (year > 0) parts.Add($"Year={year}");
            if (totalVolumes > 0) parts.Add($"TotalVolumes={totalVolumes}");
            if (totalChapters > 0) parts.Add($"TotalChapters={totalChapters}");

            var baseOverview = overview ?? string.Empty;

            if (parts.Count == 0)
            {
                return baseOverview;
            }

            return $"{baseOverview}{MangaMetaPrefix}{string.Join("|", parts)}]";
        }

        /// <summary>
        /// Parses the Overview field to extract the user overview and structured manga metadata.
        /// </summary>
        private static (string overview, string author, string artist, string demographic, int year, int totalVolumes, int totalChapters) ParseOverview(string rawOverview)
        {
            if (rawOverview == null)
            {
                return (null, null, null, null, 0, 0, 0);
            }

            var metaIndex = rawOverview.IndexOf(MangaMetaPrefix, StringComparison.Ordinal);
            if (metaIndex < 0)
            {
                return (rawOverview, null, null, null, 0, 0, 0);
            }

            var overview = rawOverview.Substring(0, metaIndex).TrimEnd();
            var metaSection = rawOverview.Substring(metaIndex);

            // Parse [MANGA_META:key=value|key=value]
            var match = Regex.Match(metaSection, @"\[MANGA_META:(.+?)\]");
            if (!match.Success)
            {
                return (overview, null, null, null, 0, 0, 0);
            }

            var pairs = match.Groups[1].Value.Split('|');
            string author = null, artist = null, demographic = null;
            int year = 0, totalVolumes = 0, totalChapters = 0;

            foreach (var pair in pairs)
            {
                var eqIndex = pair.IndexOf('=');
                if (eqIndex < 0) continue;

                var key = pair.Substring(0, eqIndex);
                var value = Unescape(pair.Substring(eqIndex + 1));

                switch (key)
                {
                    case "Author": author = value; break;
                    case "Artist": artist = value; break;
                    case "Demographic": demographic = value; break;
                    case "Year": int.TryParse(value, out year); break;
                    case "TotalVolumes": int.TryParse(value, out totalVolumes); break;
                    case "TotalChapters": int.TryParse(value, out totalChapters); break;
                }
            }

            return (overview, author, artist, demographic, year, totalVolumes, totalChapters);
        }

        private static string Escape(string value)
        {
            return value?.Replace("|", "\\|").Replace("=", "\\=").Replace("]", "\\]") ?? string.Empty;
        }

        private static string Unescape(string value)
        {
            return value?.Replace("\\|", "|").Replace("\\=", "=").Replace("\\]", "]") ?? string.Empty;
        }
    }
}
