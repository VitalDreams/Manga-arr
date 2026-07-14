using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Manga.Repositories
{
    public interface IMangaSeriesRepository
    {
        Task<MangaSeries> GetByIdAsync(int id);
        Task<MangaSeries> GetByForeignIdAsync(string foreignMangaId);
        Task<List<MangaSeries>> GetAllAsync();
        Task<List<MangaSeries>> GetMonitoredAsync();
        Task<MangaSeries> AddAsync(MangaSeries series);
        Task UpdateAsync(MangaSeries series);
        Task DeleteAsync(int id);
        Task<bool> ExistsAsync(string foreignMangaId);
    }

    public class MangaSeriesRepository : IMangaSeriesRepository
    {
        private readonly MangaDbContext _context;

        public MangaSeriesRepository(MangaDbContext context)
        {
            _context = context;
        }

        public async Task<MangaSeries> GetByIdAsync(int id)
        {
            return await _context.MangaSeries
                .Include(s => s.Metadata)
                .FirstOrDefaultAsync(s => s.Id == id);
        }

        public async Task<MangaSeries> GetByForeignIdAsync(string foreignMangaId)
        {
            return await _context.MangaSeries
                .Include(s => s.Metadata)
                .FirstOrDefaultAsync(s => s.ForeignMangaId == foreignMangaId);
        }

        public async Task<List<MangaSeries>> GetAllAsync()
        {
            return await _context.MangaSeries
                .Include(s => s.Metadata)
                .OrderBy(s => s.Name)
                .ToListAsync();
        }

        public async Task<List<MangaSeries>> GetMonitoredAsync()
        {
            return await _context.MangaSeries
                .Include(s => s.Metadata)
                .Where(s => s.Monitored)
                .OrderBy(s => s.Name)
                .ToListAsync();
        }

        public async Task<MangaSeries> AddAsync(MangaSeries series)
        {
            _context.MangaSeries.Add(series);
            await _context.SaveChangesAsync();
            return series;
        }

        public async Task UpdateAsync(MangaSeries series)
        {
            _context.MangaSeries.Update(series);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var series = await _context.MangaSeries.FindAsync(id);
            if (series != null)
            {
                _context.MangaSeries.Remove(series);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<bool> ExistsAsync(string foreignMangaId)
        {
            return await _context.MangaSeries.AnyAsync(s => s.ForeignMangaId == foreignMangaId);
        }
    }

    public interface IVolumeRepository
    {
        Task<Volume> GetByIdAsync(int id);
        Task<List<Volume>> GetByMangaSeriesIdAsync(int mangaSeriesId);
        Task<Volume> GetByForeignIdAsync(string foreignVolumeId);
        Task<Volume> AddAsync(Volume volume);
        Task UpdateAsync(Volume volume);
        Task<List<Volume>> GetMissingVolumesAsync(int mangaSeriesId);
    }

    public class VolumeRepository : IVolumeRepository
    {
        private readonly MangaDbContext _context;

        public VolumeRepository(MangaDbContext context)
        {
            _context = context;
        }

        public async Task<Volume> GetByIdAsync(int id)
        {
            return await _context.Volumes
                .Include(v => v.Chapters)
                .Include(v => v.MangaFiles)
                .FirstOrDefaultAsync(v => v.Id == id);
        }

        public async Task<List<Volume>> GetByMangaSeriesIdAsync(int mangaSeriesId)
        {
            return await _context.Volumes
                .Include(v => v.Chapters)
                .Where(v => v.MangaSeriesId == mangaSeriesId)
                .OrderBy(v => v.VolumeNumber)
                .ToListAsync();
        }

        public async Task<Volume> GetByForeignIdAsync(string foreignVolumeId)
        {
            return await _context.Volumes
                .FirstOrDefaultAsync(v => v.ForeignVolumeId == foreignVolumeId);
        }

        public async Task<Volume> AddAsync(Volume volume)
        {
            _context.Volumes.Add(volume);
            await _context.SaveChangesAsync();
            return volume;
        }

        public async Task UpdateAsync(Volume volume)
        {
            _context.Volumes.Update(volume);
            await _context.SaveChangesAsync();
        }

        public async Task<List<Volume>> GetMissingVolumesAsync(int mangaSeriesId)
        {
            return await _context.Volumes
                .Where(v => v.MangaSeriesId == mangaSeriesId && v.Monitored)
                .Where(v => !_context.MangaFiles.Any(f => f.VolumeId == v.Id))
                .OrderBy(v => v.VolumeNumber)
                .ToListAsync();
        }
    }

    public interface IChapterRepository
    {
        Task<Chapter> GetByIdAsync(int id);
        Task<List<Chapter>> GetByVolumeIdAsync(int volumeId);
        Task<Chapter> AddAsync(Chapter chapter);
        Task AddRangeAsync(IEnumerable<Chapter> chapters);
    }

    public class ChapterRepository : IChapterRepository
    {
        private readonly MangaDbContext _context;

        public ChapterRepository(MangaDbContext context)
        {
            _context = context;
        }

        public async Task<Chapter> GetByIdAsync(int id)
        {
            return await _context.Chapters.FindAsync(id);
        }

        public async Task<List<Chapter>> GetByVolumeIdAsync(int volumeId)
        {
            return await _context.Chapters
                .Where(c => c.VolumeId == volumeId)
                .OrderBy(c => c.ChapterNumber)
                .ToListAsync();
        }

        public async Task<Chapter> AddAsync(Chapter chapter)
        {
            _context.Chapters.Add(chapter);
            await _context.SaveChangesAsync();
            return chapter;
        }

        public async Task AddRangeAsync(IEnumerable<Chapter> chapters)
        {
            _context.Chapters.AddRange(chapters);
            await _context.SaveChangesAsync();
        }
    }

    public interface IMangaFileRepository
    {
        Task<MangaFile> GetByIdAsync(int id);
        Task<List<MangaFile>> GetByVolumeIdAsync(int volumeId);
        Task<List<MangaFile>> GetByMangaSeriesIdAsync(int mangaSeriesId);
        Task<MangaFile> AddAsync(MangaFile file);
        Task DeleteAsync(int id);
    }

    public class MangaFileRepository : IMangaFileRepository
    {
        private readonly MangaDbContext _context;

        public MangaFileRepository(MangaDbContext context)
        {
            _context = context;
        }

        public async Task<MangaFile> GetByIdAsync(int id)
        {
            return await _context.MangaFiles.FindAsync(id);
        }

        public async Task<List<MangaFile>> GetByVolumeIdAsync(int volumeId)
        {
            return await _context.MangaFiles
                .Where(f => f.VolumeId == volumeId)
                .ToListAsync();
        }

        public async Task<List<MangaFile>> GetByMangaSeriesIdAsync(int mangaSeriesId)
        {
            return await _context.MangaFiles
                .Where(f => f.MangaSeriesId == mangaSeriesId)
                .ToListAsync();
        }

        public async Task<MangaFile> AddAsync(MangaFile file)
        {
            _context.MangaFiles.Add(file);
            await _context.SaveChangesAsync();
            return file;
        }

        public async Task DeleteAsync(int id)
        {
            var file = await _context.MangaFiles.FindAsync(id);
            if (file != null)
            {
                _context.MangaFiles.Remove(file);
                await _context.SaveChangesAsync();
            }
        }
    }
}
