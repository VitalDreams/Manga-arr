using System.Collections.Generic;
using System.Data;
using System.Linq;
using Dapper;
using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(045)]
    public class rename_quality_profiles : NzbDroneMigrationBase
    {
        // Old Readarr profile names -> manga-native names
        private static readonly Dictionary<string, string> RenameMap = new Dictionary<string, string>
        {
            { "eBook", "Manga" },
            { "Audiobook", "Archive" },
            { "Default", "Manga" }
        };

        protected override void MainDbUpgrade()
        {
            Execute.WithConnection(RenameProfiles);
        }

        private void RenameProfiles(IDbConnection conn, IDbTransaction tran)
        {
            var profiles = conn.Query<ProfileRow>(
                @"SELECT ""Id"", ""Name"" FROM ""QualityProfiles""",
                transaction: tran).ToList();

            var existingNames = profiles.Select(p => p.Name).ToHashSet();

            foreach (var profile in profiles)
            {
                if (!RenameMap.TryGetValue(profile.Name, out var newName))
                {
                    continue;
                }

                // If the target name already exists (e.g. user already has "Manga"), skip
                // to avoid a unique-constraint violation. The profile already has the
                // manga-native name or a colliding name exists.
                if (existingNames.Contains(newName))
                {
                    continue;
                }

                conn.Execute(
                    @"UPDATE ""QualityProfiles"" SET ""Name"" = @Name WHERE ""Id"" = @Id",
                    new { Name = newName, Id = profile.Id },
                    transaction: tran);

                // Track the new name so subsequent profiles in this migration
                // don't collide with the name we just assigned.
                existingNames.Add(newName);
            }
        }

        private class ProfileRow
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }
    }
}
