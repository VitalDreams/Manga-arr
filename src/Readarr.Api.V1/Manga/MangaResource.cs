using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
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
        public List<string> Tags { get; set; }
        public string CoverUrl { get; set; }

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

        public HashSet<int> TagIds { get; set; }
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

    public class VolumeResource
    {
        public int Id { get; set; }
        public int AuthorId { get; set; }
        public string ForeignVolumeId { get; set; }
        public string Title { get; set; }
        public DateTime? ReleaseDate { get; set; }
        public bool Monitored { get; set; }
        public DateTime Added { get; set; }
    }

    public static class MangaResourceMapper
    {
        public static MangaResource ToMangaResource(this NzbDrone.Core.Books.Author model)
        {
            if (model == null)
            {
                return null;
            }

            var metadata = model.Metadata?.Value;

            return new MangaResource
            {
                Id = model.Id,
                MangaMetadataId = model.AuthorMetadataId,

                ForeignMangaId = metadata?.ForeignAuthorId,
                Title = metadata?.Name,
                TitleSlug = model.CleanName,
                Overview = metadata?.Overview,
                Genres = metadata?.Genres ?? new List<string>(),
                CoverUrl = metadata?.Images?.FirstOrDefault(i => i.CoverType == MediaCoverTypes.Poster)?.Url,

                Path = model.Path,
                QualityProfileId = model.QualityProfileId,
                MetadataProfileId = model.MetadataProfileId,
                Monitored = model.Monitored,
                MonitorNewItems = model.MonitorNewItems,
                RootFolderPath = model.RootFolderPath,
                CleanName = model.CleanName,

                TagIds = model.Tags,
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
                Tags = resource.TagIds ?? new HashSet<int>(),
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

            var metadata = new AuthorMetadata
            {
                ForeignAuthorId = resource.ForeignMangaId,
                Name = resource.Title,
                TitleSlug = resource.TitleSlug ?? (resource.Title ?? string.Empty).ToLowerInvariant().Replace(" ", "-"),
                Overview = resource.Overview,
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
    }
}
