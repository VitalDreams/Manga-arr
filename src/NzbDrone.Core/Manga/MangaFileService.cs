using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Manga
{
    public interface IMangaFileService
    {
        MangaFile GetById(int id);
        List<MangaFile> GetFilesBySeries(int seriesId);
        List<MangaFile> GetFilesByVolume(int volumeId);
        List<MangaFile> GetVolumePacksBySeries(int seriesId);
        MangaFile Add(MangaFile file);
        void Update(MangaFile file);
        void Delete(int id);
        void DeleteByVolume(int volumeId);
        void DeleteBySeries(int seriesId);
    }

    public class MangaFileService : IMangaFileService
    {
        private readonly IMangaFileRepository _repository;

        public MangaFileService(IMangaFileRepository repository)
        {
            _repository = repository;
        }

        public MangaFile GetById(int id)
        {
            return _repository.Get(id);
        }

        public List<MangaFile> GetFilesBySeries(int seriesId)
        {
            return _repository.GetFilesBySeries(seriesId);
        }

        public List<MangaFile> GetFilesByVolume(int volumeId)
        {
            return _repository.GetFilesByVolume(volumeId);
        }

        public List<MangaFile> GetVolumePacksBySeries(int seriesId)
        {
            return _repository.GetFilesBySeries(seriesId)
                .Where(f => f.IsVolumePack)
                .ToList();
        }

        public MangaFile Add(MangaFile file)
        {
            _repository.Insert(file);
            return file;
        }

        public void Update(MangaFile file)
        {
            _repository.Update(file);
        }

        public void Delete(int id)
        {
            _repository.Delete(id);
        }

        public void DeleteByVolume(int volumeId)
        {
            var files = _repository.GetFilesByVolume(volumeId);
            foreach (var file in files)
            {
                _repository.Delete(file.Id);
            }
        }

        public void DeleteBySeries(int seriesId)
        {
            var files = _repository.GetFilesBySeries(seriesId);
            foreach (var file in files)
            {
                _repository.Delete(file.Id);
            }
        }
    }

    public interface IMangaFileRepository : IBasicRepository<MangaFile>
    {
        List<MangaFile> GetFilesBySeries(int seriesId);
        List<MangaFile> GetFilesByVolume(int volumeId);
    }

    public class MangaFileRepository : BasicRepository<MangaFile>, IMangaFileRepository
    {
        public MangaFileRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public List<MangaFile> GetFilesBySeries(int seriesId)
        {
            return Query(f => f.MangaSeriesId == seriesId).ToList();
        }

        public List<MangaFile> GetFilesByVolume(int volumeId)
        {
            return Query(f => f.VolumeId == volumeId).ToList();
        }
    }
}
