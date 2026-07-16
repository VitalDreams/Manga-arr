using System.IO;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Organizer
{
    public class NamingConfig : ModelBase
    {
        public static NamingConfig Default => new NamingConfig
        {
            RenameBooks = false,
            ReplaceIllegalCharacters = true,
            ColonReplacementFormat = ColonReplacementFormat.Smart,
            StandardBookFormat = "{Series Title} Vol.{Volume:000} Ch.{Chapter:000}",
            AuthorFolderFormat = "{Series Title} ({Year})",
        };

        public bool RenameBooks { get; set; }
        public bool ReplaceIllegalCharacters { get; set; }
        public ColonReplacementFormat ColonReplacementFormat { get; set; }
        public string StandardBookFormat { get; set; }
        public string AuthorFolderFormat { get; set; }
    }
}
