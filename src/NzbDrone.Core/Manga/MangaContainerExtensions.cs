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

            return container;
        }
    }
}
