import React from 'react';
import { BrowserRouter as Router, Routes, Route, Link } from 'react-router-dom';
import MangaSeriesList from './MangaSeries/MangaSeriesList';
import MangaSearch from './MangaSearch/MangaSearch';
import VolumeList from './VolumeList/VolumeList';
import MangaSettings from './MangaSettings/MangaSettings';

const App = () => {
  return (
    <Router>
      <div className="app">
        {/* Navigation */}
        <nav className="navbar">
          <div className="navbar-brand">
            <Link to="/">MangaArr</Link>
          </div>
          <div className="navbar-menu">
            <Link to="/" className="navbar-item">Library</Link>
            <Link to="/add" className="navbar-item">Add Manga</Link>
            <Link to="/settings" className="navbar-item">Settings</Link>
          </div>
        </nav>

        {/* Main Content */}
        <main className="main-content">
          <Routes>
            <Route path="/" element={<MangaSeriesList />} />
            <Route path="/add" element={<MangaSearch />} />
            <Route path="/series/:id" element={<VolumeList />} />
            <Route path="/settings" element={<MangaSettings />} />
          </Routes>
        </main>

        {/* Footer */}
        <footer className="footer">
          <p>MangaArr v0.1.0 — Manga Download Manager</p>
        </footer>
      </div>
    </Router>
  );
};

export default App;
