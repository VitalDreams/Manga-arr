using DryIoc;
using Microsoft.Extensions.Hosting;
using NzbDrone.Core.Manga.Connectors;
using NzbDrone.Core.Manga.Download;
using NzbDrone.Core.Manga.Import;
using NzbDrone.Core.Manga.Monitoring;

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

            // Register volume repository
            container.Register<IVolumeRepository, VolumeRepository>(Reuse.Singleton);

            // Register manga import services
            container.Register<IMangaFileScanner, MangaFileScanner>(Reuse.Singleton);
            container.Register<IMangaImportService, MangaImportService>(Reuse.Singleton);

            // Register story arc service and repository
            container.Register<IStoryArcRepository, StoryArcRepository>(Reuse.Singleton);
            container.Register<IStoryArcService, StoryArcService>(Reuse.Singleton);

            // Register manga download services (Phase 7)
            container.Register<IMangaDownloadService, MangaDownloadService>(Reuse.Singleton);
            container.Register<IHostedService, MangaDownloadCompletionHandler>(Reuse.Singleton, serviceKey: "MangaDownloadCompletionHandler");

            return container;
        }
    }
}
