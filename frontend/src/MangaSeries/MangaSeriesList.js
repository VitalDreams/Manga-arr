import React, { useState, useEffect } from 'react';
import { Link } from 'react-router-dom';
import axios from 'axios';

const MangaSeriesList = () => {
  const [series, setSeries] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  useEffect(() => {
    fetchSeries();
  }, []);

  const fetchSeries = async () => {
    try {
      const response = await axios.get('/api/v3/series');
      setSeries(response.data);
      setLoading(false);
    } catch (err) {
      setError('Failed to load manga library');
      setLoading(false);
    }
  };

  const handleDelete = async (id) => {
    if (!window.confirm('Remove this manga from your library?')) return;
    try {
      await axios.delete(`/api/v3/series/${id}`);
      setSeries(series.filter(s => s.id !== id));
    } catch (err) {
      setError('Failed to delete series');
    }
  };

  const toggleMonitoring = async (id, currentStatus) => {
    try {
      await axios.put(`/api/v3/series/${id}/monitor`, {
        monitored: !currentStatus
      });
      setSeries(series.map(s =>
        s.id === id ? { ...s, monitored: !s.monitored } : s
      ));
    } catch (err) {
      setError('Failed to update monitoring');
    }
  };

  if (loading) return <div className="loading">Loading manga library...</div>;
  if (error) return <div className="error">{error}</div>;

  return (
    <div className="manga-series-list">
      <div className="header">
        <h1>Manga Library</h1>
        <Link to="/add" className="btn btn-primary">
          + Add Manga
        </Link>
      </div>

      {series.length === 0 ? (
        <div className="empty-state">
          <h2>No manga in your library</h2>
          <p>Search for manga on MangaDex and add them to your library.</p>
          <Link to="/add" className="btn btn-primary">
            Add Your First Manga
          </Link>
        </div>
      ) : (
        <div className="series-grid">
          {series.map(s => (
            <div key={s.id} className="series-card">
              <div className="series-cover">
                {s.metadata?.value?.coverUrl ? (
                  <img
                    src={s.metadata.value.coverUrl}
                    alt={s.name}
                    loading="lazy"
                  />
                ) : (
                  <div className="no-cover">No Cover</div>
                )}
              </div>

              <div className="series-info">
                <h3>
                  <Link to={`/series/${s.id}`}>
                    {s.name}
                  </Link>
                </h3>

                <div className="series-meta">
                  <span className="status">{s.metadata?.value?.status}</span>
                  <span className="year">{s.metadata?.value?.year}</span>
                </div>

                <div className="series-actions">
                  <button
                    className={`btn btn-sm ${s.monitored ? 'btn-success' : 'btn-default'}`}
                    onClick={() => toggleMonitoring(s.id, s.monitored)}
                  >
                    {s.monitored ? 'Monitoring' : 'Not Monitoring'}
                  </button>

                  <Link
                    to={`/series/${s.id}`}
                    className="btn btn-sm btn-info"
                  >
                    View
                  </Link>

                  <button
                    className="btn btn-sm btn-danger"
                    onClick={() => handleDelete(s.id)}
                  >
                    Remove
                  </button>
                </div>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
};

export default MangaSeriesList;
