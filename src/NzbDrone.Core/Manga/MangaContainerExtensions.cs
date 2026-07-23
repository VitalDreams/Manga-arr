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

            // Register metadata aggregator (MangaDex + AniList enrichment)
            container.Register<MangaDexConnector, MangaDexConnector>(Reuse.Singleton);
            container.Register<AniListConnector, AniListConnector>(Reuse.Singleton);
            container.Register<IMetadataAggregator, MetadataAggregator>(Reuse.Singleton);

            // Register Prowlarr connector (fallback download source)
            container.Register<IProwlarrConnector, ProwlarrConnector>(Reuse.Singleton);

            // Register manga metadata and series repositories and service
            container.Register<IMangaMetadataRepository, MangaMetadataRepository>(Reuse.Singleton);
            container.Register<IMangaSeriesRepository, MangaSeriesRepository>(Reuse.Singleton);
            container.Register<IMangaSeriesService, MangaSeriesService>(Reuse.Singleton);

            // Register volume pack tracker
            container.Register<IVolumePackTracker, VolumePackTracker>(Reuse.Singleton);

            // Register manga file service and repository
            container.Register<IMangaFileRepository, MangaFileRepository>(Reuse.Singleton);
            container.Register<IMangaFileService, MangaFileService>(Reuse.Singleton);
            container.Register<IMangaFileMigrationService, MangaFileMigrationService>(Reuse.Singleton);

            // Register volume and chapter repositories
            container.Register<IVolumeRepository, VolumeRepository>(Reuse.Singleton);
            container.Register<IChapterRepository, ChapterRepository>(Reuse.Singleton);

            // Register naming and CBZ creation services
            container.Register<IMangaNamingService, MangaNamingService>(Reuse.Singleton);
            container.Register<IComicInfoGenerator, ComicInfoGenerator>(Reuse.Singleton);
            container.Register<ICbzCreator, CbzCreator>(Reuse.Singleton);
            container.Register<ISeriesMetadataGenerator, SeriesMetadataGenerator>(Reuse.Singleton);

            // Register MangaDex downloader (direct download)
            container.Register<IMangaDexDownloader, MangaDexDownloader>(Reuse.Singleton);

            // Register manga import services
            container.Register<IMangaFileScanner, MangaFileScanner>(Reuse.Singleton);
            container.Register<IMangaImportService, MangaImportService>(Reuse.Singleton);

            // Register story arc service and repository
            container.Register<IStoryArcRepository, StoryArcRepository>(Reuse.Singleton);
            container.Register<IStoryArcService, StoryArcService>(Reuse.Singleton);

            // Register manga download services (Phase 7)
            container.Register<IMangaDownloadService, MangaDownloadService>(Reuse.Singleton);
            // MangaDownloadCompletionHandler auto-registered by AutoAddServices as IHostedService

            // Register Komga integration
            container.Register<IKomgaIntegration, KomgaIntegration>(Reuse.Singleton);

            // Register notification service
            container.Register<INotificationService, NotificationService>(Reuse.Singleton);

            // Register manga search service (Phase 9 - orchestrates full search->download pipeline)
            container.Register<IMangaSearchService, MangaSearchService>(Reuse.Singleton);

            // Register monitoring service (Phase 9 - background monitoring with dual-source download)
            // MangaMonitoringService auto-registered by AutoAddServices as IHostedService

            return container;
        }
    }
}
