using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Manga;
using NzbDrone.Core.Manga.Connectors;
using NzbDrone.Core.Manga.Monitoring;
using NzbDrone.Core.Manga.Repositories;

namespace NzbDrone.Core.Composition
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddMangaArr(this IServiceCollection services, IConfiguration configuration)
        {
            // Database
            services.AddDbContext<MangaDbContext>(options =>
            {
                var dbPath = configuration.GetValue<string>("Database:Path") ?? "mangaarr.db";
                options.UseSqlite($"Data Source={dbPath}");
            });

            // Repositories
            services.AddScoped<IMangaSeriesRepository, MangaSeriesRepository>();
            services.AddScoped<IVolumeRepository, VolumeRepository>();
            services.AddScoped<IChapterRepository, ChapterRepository>();
            services.AddScoped<IMangaFileRepository, MangaFileRepository>();

            // MangaDex connector
            services.AddSingleton<IMangaMetadataConnector, MangaDexConnector>();
            services.AddSingleton<MangaDexConnector>();

            // AniList connector
            services.AddSingleton<AniListConnector>();

            // Metadata aggregator
            services.AddSingleton<IMetadataAggregator, MetadataAggregator>();

            // Services
            services.AddSingleton<ICbzCreator, CbzCreator>();
            services.AddSingleton<IMangaDexDownloader, MangaDexDownloader>();
            services.AddSingleton<IKomgaIntegration, KomgaIntegration>();

            // Monitoring
            services.AddSingleton<MangaMonitoringService>();
            services.AddHostedService(provider => provider.GetRequiredService<MangaMonitoringService>());

            // Notifications
            services.AddSingleton<INotificationService, NotificationService>();
            services.AddSingleton<NotificationService>();

            // HTTP client
            services.AddHttpClient();

            return services;
        }

        public static IServiceCollection AddMangaArrConfiguration(this IServiceCollection services, IConfiguration configuration)
        {
            // Bind configuration sections
            services.Configure<KomgaSettings>(configuration.GetSection("Komga"));
            services.Configure<MangaDexSettings>(configuration.GetSection("MangaDex"));
            services.Configure<NotificationSettings>(configuration.GetSection("Notifications"));
            services.Configure<MonitoringSettings>(configuration.GetSection("Monitoring"));
            services.Configure<DatabaseSettings>(configuration.GetSection("Database"));

            return services;
        }
    }

    // Configuration classes
    public class KomgaSettings
    {
        public string BaseUrl { get; set; }
        public string ApiKey { get; set; }
        public bool AutoScan { get; set; } = true;
    }

    public class MangaDexSettings
    {
        public string BaseUrl { get; set; } = "https://api.mangadex.org";
        public int RateLimitMs { get; set; } = 200;
        public string Language { get; set; } = "en";
    }

    public class MonitoringSettings
    {
        public bool Enabled { get; set; } = true;
        public int CheckIntervalHours { get; set; } = 24;
        public bool AutoDownload { get; set; } = true;
    }

    public class DatabaseSettings
    {
        public string Path { get; set; } = "mangaarr.db";
    }
}
