import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import axios from 'axios';

const MangaSearch = () => {
  const navigate = useNavigate();
  const [query, setQuery] = useState('');
  const [results, setResults] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);
  const [adding, setAdding] = useState({});

  const handleSearch = async (e) => {
    e.preventDefault();
    if (!query.trim()) return;

    setLoading(true);
    setError(null);

    try {
      const response = await axios.get(`/api/v3/manga/search?query=${encodeURIComponent(query)}`);
      setResults(response.data);
    } catch (err) {
      setError('Search failed. Please try again.');
    } finally {
      setLoading(false);
    }
  };

  const handleAdd = async (manga) => {
    setAdding(prev => ({ ...prev, [manga.foreignMangaId]: true }));

    try {
      await axios.post('/api/v3/series', {
        foreignMangaId: manga.foreignMangaId,
        rootFolderPath: '/manga',
        monitored: true
      });
      navigate('/');
    } catch (err) {
      if (err.response?.status === 409) {
        setError('This manga is already in your library');
      } else {
        setError('Failed to add manga');
      }
    } finally {
      setAdding(prev => ({ ...prev, [manga.foreignMangaId]: false }));
    }
  };

  return (
    <div className="manga-search">
      <div className="header">
        <h1>Add Manga</h1>
        <p>Search for manga on MangaDex and add them to your library.</p>
      </div>

      <form onSubmit={handleSearch} className="search-form">
        <div className="search-input-group">
          <input
            type="text"
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            placeholder="Search manga titles (e.g., Berserk, One Piece, Solo Leveling)"
            className="search-input"
          />
          <button type="submit" className="btn btn-primary" disabled={loading}>
            {loading ? 'Searching...' : 'Search'}
          </button>
        </div>
      </form>

      {error && <div className="error">{error}</div>}

      {results.length > 0 && (
        <div className="search-results">
          <h2>Results ({results.length})</h2>

          {results.map(manga => (
            <div key={manga.foreignMangaId} className="search-result-card">
              <div className="result-cover">
                {manga.coverUrl ? (
                  <img src={manga.coverUrl} alt={manga.title} loading="lazy" />
                ) : (
                  <div className="no-cover">No Cover</div>
                )}
              </div>

              <div className="result-info">
                <h3>{manga.title}</h3>

                <div className="result-meta">
                  {manga.author && <span className="author">by {manga.author}</span>}
                  {manga.year > 0 && <span className="year">{manga.year}</span>}
                  <span className="status">{manga.status}</span>
                  {manga.demographic && <span className="demographic">{manga.demographic}</span>}
                </div>

                {manga.genres?.length > 0 && (
                  <div className="result-genres">
                    {manga.genres.map(genre => (
                      <span key={genre} className="genre-tag">{genre}</span>
                    ))}
                  </div>
                )}

                <p className="result-description">
                  {manga.description?.substring(0, 200)}
                  {manga.description?.length > 200 && '...'}
                </p>

                <button
                  className="btn btn-primary"
                  onClick={() => handleAdd(manga)}
                  disabled={adding[manga.foreignMangaId]}
                >
                  {adding[manga.foreignMangaId] ? 'Adding...' : '+ Add to Library'}
                </button>
              </div>
            </div>
          ))}
        </div>
      )}

      {results.length === 0 && !loading && query && (
        <div className="no-results">
          <p>No manga found for "{query}". Try a different search term.</p>
        </div>
      )}
    </div>
  );
};

export default MangaSearch;
