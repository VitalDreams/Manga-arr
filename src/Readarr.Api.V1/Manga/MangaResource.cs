using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using NzbDrone.Core.Books;
using NzbDrone.Core.Manga;
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
        public static MangaResource ToResource(this MangaSeries model)
        {
            if (model == null)
            {
                return null;
            }

            return new MangaResource
            {
                Id = model.Id,
                MangaMetadataId = model.MangaMetadataId,

                ForeignMangaId = model.ForeignMangaId,
                Title = model.Name,
                TitleSlug = model.CleanName,
                Overview = model.Metadata?.Value?.Description,
                Author = model.Metadata?.Value?.Author,
                Artist = model.Metadata?.Value?.Artist,
                Status = model.Metadata?.Value?.Status,
                Demographic = model.Metadata?.Value?.Demographic,
                Year = model.Metadata?.Value?.Year ?? 0,
                TotalVolumes = model.Metadata?.Value?.TotalVolumes ?? 0,
                TotalChapters = model.Metadata?.Value?.TotalChapters ?? 0,
                Genres = model.Metadata?.Value?.Genres ?? new List<string>(),
                Tags = model.Metadata?.Value?.Tags ?? new List<string>(),
                CoverUrl = model.Metadata?.Value?.CoverUrl,

                Path = model.Path,
                QualityProfileId = model.QualityProfileId,
                MetadataProfileId = model.MetadataProfileId,
                Monitored = model.Monitored,
                MonitorNewItems = model.MonitorNewItems,
                RootFolderPath = model.RootFolderPath,
                CleanName = model.CleanName,
                SortName = model.Metadata?.Value?.SortTitle,

                TagIds = model.Tags,
                Added = model.Added,
                Statistics = new MangaStatisticsResource()
            };
        }

        public static MangaSeries ToModel(this MangaResource resource)
        {
            if (resource == null)
            {
                return null;
            }

            return new MangaSeries
            {
                Id = resource.Id,
                MangaMetadataId = resource.MangaMetadataId,
                ForeignMangaId = resource.ForeignMangaId,
                CleanName = resource.CleanName,
                Path = resource.Path,
                QualityProfileId = resource.QualityProfileId,
                MetadataProfileId = resource.MetadataProfileId,
                Monitored = resource.Monitored,
                MonitorNewItems = resource.MonitorNewItems,
                RootFolderPath = resource.RootFolderPath,
                Tags = resource.TagIds,
                Added = resource.Added
            };
        }

        public static List<MangaResource> ToResource(this IEnumerable<MangaSeries> models)
        {
            return models?.Select(ToResource).ToList();
        }
    }
}
