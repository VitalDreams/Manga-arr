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
            StandardBookFormat = "$Series $IssueN ($Year)",
            AuthorFolderFormat = "$Series ($Year)",
        };

        public bool RenameBooks { get; set; }
        public bool ReplaceIllegalCharacters { get; set; }
        public ColonReplacementFormat ColonReplacementFormat { get; set; }
        public string StandardBookFormat { get; set; }
        public string AuthorFolderFormat { get; set; }
    }
}
