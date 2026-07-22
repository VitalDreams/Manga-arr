using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Manga;
using Readarr.Http.REST;

namespace Readarr.Api.V1.Manga
{
    public class VolumeResource : RestResource
    {
        public int AuthorId { get; set; }
        public int MangaSeriesId { get; set; }
        public int MangaMetadataId { get; set; }
        public string ForeignVolumeId { get; set; }
        public string Title { get; set; }
        public int VolumeNumber { get; set; }
        public DateTime? ReleaseDate { get; set; }
        public bool Monitored { get; set; }
        public DateTime Added { get; set; }
        public int ChapterCount { get; set; }
        public int MangaFileCount { get; set; }
        public long SizeOnDisk { get; set; }
        public bool Available { get; set; }
    }

    public static class VolumeResourceMapper
    {
        public static VolumeResource ToResource(this Volume model)
        {
            if (model == null)
            {
                return null;
            }

            return new VolumeResource
            {
                Id = model.Id,
                MangaSeriesId = model.MangaSeriesId,
                MangaMetadataId = model.MangaMetadataId,
                ForeignVolumeId = model.ForeignVolumeId,
                Title = model.Title,
                VolumeNumber = model.VolumeNumber,
                ReleaseDate = model.ReleaseDate,
                Monitored = model.Monitored,
                Added = model.Added,
                ChapterCount = model.Chapters?.Value?.Count ?? 0
            };
        }

        public static VolumeResource ToResource(this Volume model, IEnumerable<MangaFile> files)
        {
            var resource = model.ToResource();
            var fileList = files?.ToList() ?? new List<MangaFile>();
            resource.MangaFileCount = fileList.Count;
            resource.SizeOnDisk = fileList.Sum(f => f.Size);
            resource.Available = fileList.Count > 0;
            return resource;
        }

        public static List<VolumeResource> ToResource(this IEnumerable<Volume> models)
        {
            return models?.Select(ToResource).ToList();
        }
    }
}
