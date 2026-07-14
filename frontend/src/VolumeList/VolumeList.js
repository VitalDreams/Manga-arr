import React, { useState, useEffect } from 'react';
import { useParams, Link } from 'react-router-dom';
import axios from 'axios';

const VolumeList = () => {
  const { id } = useParams();
  const [series, setSeries] = useState(null);
  const [volumes, setVolumes] = useState([]);
  const [loading, setLoading] = useState(true);
  const [downloading, setDownloading] = useState({});
  const [downloadStatus, setDownloadStatus] = useState({});

  useEffect(() => {
    fetchData();
  }, [id]);

  const fetchData = async () => {
    try {
      const [seriesRes, volumesRes] = await Promise.all([
        axios.get(`/api/v3/series/${id}`),
        axios.get(`/api/v3/series/${id}/volumes`)
      ]);
      setSeries(seriesRes.data);
      setVolumes(volumesRes.data);
      setLoading(false);
    } catch (err) {
      console.error('Failed to load series data:', err);
      setLoading(false);
    }
  };

  const handleDownload = async (volumeNumber) => {
    setDownloading(prev => ({ ...prev, [volumeNumber]: true }));
    setDownloadStatus(prev => ({ ...prev, [volumeNumber]: 'starting' }));

    try {
      const response = await axios.post(`/api/v3/series/${id}/download/${volumeNumber}`);
      setDownloadStatus(prev => ({
        ...prev,
        [volumeNumber]: response.data.status === 'completed' ? 'success' : 'failed'
      }));
    } catch (err) {
      setDownloadStatus(prev => ({ ...prev, [volumeNumber]: 'failed' }));
    } finally {
      setDownloading(prev => ({ ...prev, [volumeNumber]: false }));
    }
  };

  const handleDownloadAll = async () => {
    for (const volume of volumes) {
      await handleDownload(volume.volumeNumber);
    }
  };

  if (loading) return <div className="loading">Loading volumes...</div>;
  if (!series) return <div className="error">Series not found</div>;

  return (
    <div className="volume-list">
      <div className="header">
        <div className="header-info">
          <Link to="/" className="back-link">&larr; Back to Library</Link>
          <h1>{series.name}</h1>
          <div className="series-meta">
            <span className="status">{series.metadata?.value?.status}</span>
            <span className="year">{series.metadata?.value?.year}</span>
            <span className="volumes">{volumes.length} volumes</span>
          </div>
        </div>

        <div className="header-actions">
          <button
            className="btn btn-primary"
            onClick={handleDownloadAll}
            disabled={Object.values(downloading).some(d => d)}
          >
            Download All Volumes
          </button>
        </div>
      </div>

      {series.metadata?.value?.description && (
        <div className="series-description">
          <p>{series.metadata.value.description}</p>
        </div>
      )}

      <div className="volumes-table">
        <table>
          <thead>
            <tr>
              <th>Volume</th>
              <th>Chapters</th>
              <th>Status</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {volumes.map(volume => (
              <tr key={volume.volumeNumber} className="volume-row">
                <td className="volume-number">
                  Vol. {volume.volumeNumber.toString().padStart(3, '0')}
                </td>
                <td className="chapter-count">
                  {volume.chapterCount} chapters
                </td>
                <td className="volume-status">
                  {downloadStatus[volume.volumeNumber] === 'success' && (
                    <span className="badge badge-success">Downloaded</span>
                  )}
                  {downloadStatus[volume.volumeNumber] === 'failed' && (
                    <span className="badge badge-danger">Failed</span>
                  )}
                  {downloading[volume.volumeNumber] && (
                    <span className="badge badge-info">Downloading...</span>
                  )}
                  {!downloadStatus[volume.volumeNumber] && !downloading[volume.volumeNumber] && (
                    <span className="badge badge-default">Not Downloaded</span>
                  )}
                </td>
                <td className="volume-actions">
                  <button
                    className={`btn btn-sm ${downloadStatus[volume.volumeNumber] === 'success' ? 'btn-success' : 'btn-primary'}`}
                    onClick={() => handleDownload(volume.volumeNumber)}
                    disabled={downloading[volume.volumeNumber]}
                  >
                    {downloading[volume.volumeNumber]
                      ? 'Downloading...'
                      : downloadStatus[volume.volumeNumber] === 'success'
                        ? 'Re-download'
                        : 'Download'
                    }
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
};

export default VolumeList;
