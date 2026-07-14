using Microsoft.EntityFrameworkCore;
using NzbDrone.Core.Manga;

namespace NzbDrone.Core.Datastore
{
    public class MangaDbContext : DbContext
    {
        public MangaDbContext(DbContextOptions<MangaDbContext> options) : base(options) { }

        public DbSet<MangaSeries> MangaSeries { get; set; }
        public DbSet<MangaMetadata> MangaMetadata { get; set; }
        public DbSet<Volume> Volumes { get; set; }
        public DbSet<Chapter> Chapters { get; set; }
        public DbSet<MangaFile> MangaFiles { get; set; }
        public DbSet<MonitoredManga> MonitoredManga { get; set; }
        public DbSet<DownloadHistory> DownloadHistory { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // MangaSeries
            modelBuilder.Entity<MangaSeries>(entity =>
            {
                entity.ToTable("MangaSeries");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ForeignMangaId).HasMaxLength(100).IsRequired();
                entity.Property(e => e.Name).HasMaxLength(500).IsRequired();
                entity.Property(e => e.CleanName).HasMaxLength(500);
                entity.Property(e => e.Path).HasMaxLength(1000);
                entity.Property(e => e.RootFolderPath).HasMaxLength(1000);
                entity.HasIndex(e => e.ForeignMangaId).IsUnique();
                entity.HasIndex(e => e.CleanName);
            });

            // MangaMetadata
            modelBuilder.Entity<MangaMetadata>(entity =>
            {
                entity.ToTable("MangaMetadata");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ForeignMangaId).HasMaxLength(100).IsRequired();
                entity.Property(e => e.Title).HasMaxLength(500).IsRequired();
                entity.Property(e => e.Description).HasMaxLength(5000);
                entity.Property(e => e.Author).HasMaxLength(200);
                entity.Property(e => e.Artist).HasMaxLength(200);
                entity.Property(e => e.Publisher).HasMaxLength(200);
                entity.Property(e => e.OriginalLanguage).HasMaxLength(10);
                entity.Property(e => e.Demographic).HasMaxLength(50);
                entity.Property(e => e.Status).HasMaxLength(50);
                entity.Property(e => e.ContentRating).HasMaxLength(50);
                entity.Property(e => e.CoverUrl).HasMaxLength(1000);
                entity.Property(e => e.LocalCoverPath).HasMaxLength(1000);
                entity.HasIndex(e => e.ForeignMangaId).IsUnique();
            });

            // Volume
            modelBuilder.Entity<Volume>(entity =>
            {
                entity.ToTable("Volumes");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ForeignVolumeId).HasMaxLength(100).IsRequired();
                entity.Property(e => e.Title).HasMaxLength(500).IsRequired();
                entity.Property(e => e.TitleSlug).HasMaxLength(500);
                entity.Property(e => e.CleanTitle).HasMaxLength(500);
                entity.HasOne(e => e.MangaSeries).WithMany().HasForeignKey("MangaSeriesId");
                entity.HasIndex(e => e.ForeignVolumeId).IsUnique();
                entity.HasIndex("MangaSeriesId");
            });

            // Chapter
            modelBuilder.Entity<Chapter>(entity =>
            {
                entity.ToTable("Chapters");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ForeignChapterId).HasMaxLength(100).IsRequired();
                entity.Property(e => e.Title).HasMaxLength(500);
                entity.Property(e => e.TitleSlug).HasMaxLength(500);
                entity.Property(e => e.Language).HasMaxLength(10);
                entity.Property(e => e.Overview).HasMaxLength(5000);
                entity.Property(e => e.ScanlationGroup).HasMaxLength(200);
                entity.HasOne(e => e.Volume).WithMany().HasForeignKey("VolumeId");
                entity.HasIndex(e => e.ForeignChapterId).IsUnique();
                entity.HasIndex("VolumeId");
            });

            // MangaFile
            modelBuilder.Entity<MangaFile>(entity =>
            {
                entity.ToTable("MangaFiles");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Path).HasMaxLength(1000).IsRequired();
                entity.Property(e => e.FileName).HasMaxLength(500).IsRequired();
                entity.Property(e => e.RelativePath).HasMaxLength(1000);
                entity.HasIndex("VolumeId");
                entity.HasIndex("MangaSeriesId");
            });

            // MonitoredManga
            modelBuilder.Entity<MonitoredManga>(entity =>
            {
                entity.ToTable("MonitoredManga");
                entity.HasKey(e => e.MangaDexId);
                entity.Property(e => e.MangaDexId).HasMaxLength(100).IsRequired();
                entity.Property(e => e.Title).HasMaxLength(500).IsRequired();
                entity.Property(e => e.OutputPath).HasMaxLength(1000);
            });

            // DownloadHistory
            modelBuilder.Entity<DownloadHistory>(entity =>
            {
                entity.ToTable("DownloadHistory");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.MangaDexId).HasMaxLength(100);
                entity.Property(e => e.Title).HasMaxLength(500);
                entity.Property(e => e.FilePath).HasMaxLength(1000);
                entity.Property(e => e.Status).HasMaxLength(50);
                entity.HasIndex(e => e.MangaDexId);
                entity.HasIndex(e => e.DownloadedAt);
            });
        }
    }

    // Extend DownloadHistory with Id
    public partial class DownloadHistory
    {
        public int Id { get; set; }
    }

    // MangaFile entity
    public class MangaFile
    {
        public int Id { get; set; }
        public int VolumeId { get; set; }
        public int MangaSeriesId { get; set; }
        public string Path { get; set; }
        public string FileName { get; set; }
        public string RelativePath { get; set; }
        public long Size { get; set; }
        public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    }
}
