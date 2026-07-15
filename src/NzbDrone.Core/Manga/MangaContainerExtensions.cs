using DryIoc;
using NzbDrone.Core.Manga.Connectors;

namespace NzbDrone.Core.Manga
{
    public static class MangaContainerExtensions
    {
        public static IContainer AddMangaConnectors(this IContainer container)
        {
            // Register MangaDex as the default manga metadata connector
            container.Register<IMangaMetadataConnector, MangaDexConnector>(Reuse.Singleton);

            // Register volume pack tracker
            container.Register<IVolumePackTracker, VolumePackTracker>(Reuse.Singleton);

            // Register manga file service and repository
            container.Register<IMangaFileRepository, MangaFileRepository>(Reuse.Singleton);
            container.Register<IMangaFileService, MangaFileService>(Reuse.Singleton);

            return container;
        }
    }
}
