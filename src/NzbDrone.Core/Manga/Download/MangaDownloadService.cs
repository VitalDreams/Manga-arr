using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Http;
using NzbDrone.Core.Books;
using NzbDrone.Core.Download;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Manga.Download
{
    public interface IMangaDownloadService
    {
        Task<MangaDownloadResult> SendToDownloadClient(string title, string downloadUrl, DownloadProtocol protocol, MangaSeries series, Volume volume);
        Task<MangaDownloadStatus> GetDownloadStatus(string downloadId);
        Task<List<MangaDownloadStatus>> GetActiveDownloads();
    }

    public class MangaDownloadService : IMangaDownloadService
    {
        private readonly IProvideDownloadClient _downloadClientProvider;
        private readonly IHttpClient _httpClient;
        private readonly IDiskProvider _diskProvider;
        private readonly Logger _logger;

        public MangaDownloadService(
            IProvideDownloadClient downloadClientProvider,
            IHttpClient httpClient,
            IDiskProvider diskProvider,
            Logger logger)
        {
            _downloadClientProvider = downloadClientProvider;
            _httpClient = httpClient;
            _diskProvider = diskProvider;
            _logger = logger;
        }

        public async Task<MangaDownloadResult> SendToDownloadClient(
            string title,
            string downloadUrl,
            DownloadProtocol protocol,
            MangaSeries series,
            Volume volume)
        {
            _logger.Info("Sending manga download to client: {0} (protocol: {1})", title, protocol);

            try
            {
                var downloadClient = _downloadClientProvider.GetDownloadClient(protocol);

                if (downloadClient == null)
                {
                    _logger.Error("No download client available for protocol {0}", protocol);
                    return new MangaDownloadResult
                    {
                        Success = false,
                        ErrorMessage = $"No download client configured for {protocol}"
                    };
                }

                _logger.Info("Using download client: {0}", downloadClient.Name);

                var remoteBook = BuildRemoteBook(title, downloadUrl, protocol, series, volume);

                var downloadId = await downloadClient.Download(remoteBook, null);

                if (string.IsNullOrEmpty(downloadId))
                {
                    _logger.Warn("Download client returned empty download ID for {0}", title);
                    return new MangaDownloadResult
                    {
                        Success = false,
                        ErrorMessage = "Download client failed to add the download"
                    };
                }

                _logger.Info("Download added successfully. ID: {0}", downloadId);

                return new MangaDownloadResult
                {
                    Success = true,
                    DownloadId = downloadId,
                    Title = title,
                    Protocol = protocol,
                    ClientName = downloadClient.Name
                };
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to send manga download: {0}", title);
                return new MangaDownloadResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<MangaDownloadStatus> GetDownloadStatus(string downloadId)
        {
            try
            {
                var clients = _downloadClientProvider.GetDownloadClients();

                foreach (var client in clients)
                {
                    var items = client.GetItems();
                    var item = items.FirstOrDefault(i => i.DownloadId == downloadId);

                    if (item != null)
                    {
                        return new MangaDownloadStatus
                        {
                            DownloadId = item.DownloadId,
                            Title = item.Title,
                            Status = item.Status,
                            TotalSize = item.TotalSize,
                            RemainingSize = item.RemainingSize,
                            RemainingTime = item.RemainingTime,
                            OutputPath = item.OutputPath?.ToString(),
                            Message = item.Message,
                            ClientName = client.Name
                        };
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get download status for {0}", downloadId);
                return null;
            }
        }

        public async Task<List<MangaDownloadStatus>> GetActiveDownloads()
        {
            var allItems = new List<MangaDownloadStatus>();

            try
            {
                var clients = _downloadClientProvider.GetDownloadClients();

                foreach (var client in clients)
                {
                    var items = client.GetItems();

                    foreach (var item in items)
                    {
                        allItems.Add(new MangaDownloadStatus
                        {
                            DownloadId = item.DownloadId,
                            Title = item.Title,
                            Status = item.Status,
                            TotalSize = item.TotalSize,
                            RemainingSize = item.RemainingSize,
                            RemainingTime = item.RemainingTime,
                            OutputPath = item.OutputPath?.ToString(),
                            Message = item.Message,
                            ClientName = client.Name
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get active downloads");
            }

            return allItems;
        }

        private RemoteBook BuildRemoteBook(
            string title,
            string downloadUrl,
            DownloadProtocol protocol,
            MangaSeries series,
            Volume volume)
        {
            var releaseInfo = new ReleaseInfo
            {
                Guid = $"manga-arr-{series.ForeignMangaId}-vol{volume.VolumeNumber}-{Guid.NewGuid():N}",
                Title = title,
                DownloadUrl = downloadUrl,
                DownloadProtocol = protocol,
                Indexer = "MangaArr-Prowlarr",
                PublishDate = DateTime.UtcNow
            };

            var remoteBook = new RemoteBook
            {
                Release = releaseInfo,
                DownloadAllowed = true
            };

            // Add a minimal Book entry so the download client has context
            var book = new Book
            {
                Title = title,
                ForeignBookId = series.ForeignMangaId
            };

            // Add a minimal Author entry (required by Readarr's download pipeline)
            var author = new Author
            {
                Name = series.Name
            };

            book.Author = author;
            remoteBook.Books.Add(book);
            remoteBook.Author = author;

            return remoteBook;
        }
    }

    public class MangaDownloadResult
    {
        public bool Success { get; set; }
        public string DownloadId { get; set; }
        public string Title { get; set; }
        public DownloadProtocol Protocol { get; set; }
        public string ClientName { get; set; }
        public string ErrorMessage { get; set; }
    }

    public class MangaDownloadStatus
    {
        public string DownloadId { get; set; }
        public string Title { get; set; }
        public DownloadItemStatus Status { get; set; }
        public long TotalSize { get; set; }
        public long RemainingSize { get; set; }
        public TimeSpan? RemainingTime { get; set; }
        public string OutputPath { get; set; }
        public string Message { get; set; }
        public string ClientName { get; set; }
    }
}
