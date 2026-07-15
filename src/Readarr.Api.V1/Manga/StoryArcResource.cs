using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using NzbDrone.Core.Manga;
using Readarr.Http.REST;

namespace Readarr.Api.V1.Manga
{
    public class StoryArcResource : RestResource
    {
        [JsonIgnore]
        public int MangaMetadataId { get; set; }

        public string ForeignArcId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int ArcOrder { get; set; }
        public string ChapterRange { get; set; }
    }

    public static class StoryArcResourceMapper
    {
        public static StoryArcResource ToResource(this StoryArc model)
        {
            if (model == null)
            {
                return null;
            }

            return new StoryArcResource
            {
                Id = model.Id,
                MangaMetadataId = model.MangaMetadataId,
                ForeignArcId = model.ForeignArcId,
                Name = model.Name,
                Description = model.Description,
                ArcOrder = model.ArcOrder,
                ChapterRange = model.ChapterRange
            };
        }

        public static StoryArc ToModel(this StoryArcResource resource)
        {
            if (resource == null)
            {
                return null;
            }

            return new StoryArc
            {
                Id = resource.Id,
                MangaMetadataId = resource.MangaMetadataId,
                ForeignArcId = resource.ForeignArcId,
                Name = resource.Name,
                Description = resource.Description,
                ArcOrder = resource.ArcOrder,
                ChapterRange = resource.ChapterRange
            };
        }

        public static List<StoryArcResource> ToResource(this IEnumerable<StoryArc> models)
        {
            return models?.Select(ToResource).ToList();
        }
    }
}
