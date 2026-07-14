using System;
using System.Collections.Generic;
using NzbDrone.Core.Books;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Manga
{
    public class MangaMetadata : Entity<MangaMetadata>
    {
        public MangaMetadata()
        {
            Links = new List<Links>();
            Genres = new List<string>();
            Tags = new List<string>();
            Ratings = new Ratings();
        }

        // MangaDex metadata
        public string ForeignMangaId { get; set; }
        public string Title { get; set; }
        public string TitleSlug { get; set; }
        public string SortTitle { get; set; }
        public string Description { get; set; }
        public string Author { get; set; }
        public string Artist { get; set; }
        public string Publisher { get; set; }
        public string OriginalLanguage { get; set; }
        public string Demographic { get; set; }  // shonen, shoujo, seinen, josei
        public string Status { get; set; }  // ongoing, completed, hiatus, cancelled
        public string ContentRating { get; set; }  // safe, suggestive, erotica
        public int Year { get; set; }
        public int TotalVolumes { get; set; }
        public int TotalChapters { get; set; }
        public List<string> Genres { get; set; }
        public List<string> Tags { get; set; }
        public List<Links> Links { get; set; }
        public Ratings Ratings { get; set; }
        public DateTime? LastInfoSync { get; set; }

        // Cover image
        public string CoverUrl { get; set; }
        public string LocalCoverPath { get; set; }

        public override string ToString()
        {
            return string.Format("[{0}][{1}]", ForeignMangaId, Title);
        }
    }
}
