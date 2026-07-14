import React, { useState, useEffect } from 'react';
import axios from 'axios';

const MangaSettings = () => {
  const [mangadexStatus, setMangadexStatus] = useState(null);
  const [komgaStatus, setKomgaStatus] = useState(null);
  const [komgaSettings, setKomgaSettings] = useState({ baseUrl: '', apiKey: '' });
  const [namingTemplates, setNamingTemplates] = useState([]);
  const [testing, setTesting] = useState(false);
  const [testResult, setTestResult] = useState(null);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState(null);

  useEffect(() => {
    fetchSettings();
  }, []);

  const fetchSettings = async () => {
    try {
      const [mangadexRes, komgaRes, namingRes] = await Promise.all([
        axios.get('/api/v3/manga-settings/mangadex'),
        axios.get('/api/v3/manga-settings/komga'),
        axios.get('/api/v3/manga-settings/naming')
      ]);
      setMangadexStatus(mangadexRes.data);
      setKomgaStatus(komgaRes.data);
      setKomgaSettings({
        baseUrl: komgaRes.data.baseUrl !== 'not configured' ? komgaRes.data.baseUrl : '',
        apiKey: ''
      });
      setNamingTemplates(namingRes.data.templates);
    } catch (err) {
      setError('Failed to load settings');
    }
  };

  const handleTestKomga = async () => {
    setTesting(true);
    setTestResult(null);

    try {
      await axios.post('/api/v3/manga-settings/komga/test');
      setTestResult({ success: true, message: 'Connected successfully!' });
    } catch (err) {
      setTestResult({ success: false, message: 'Connection failed. Check URL and API key.' });
    } finally {
      setTesting(false);
    }
  };

  const handleSaveKomga = async () => {
    setSaving(true);
    setError(null);

    try {
      await axios.put('/api/v3/manga-settings/komga', komgaSettings);
      await fetchSettings();
      setTestResult({ success: true, message: 'Settings saved!' });
    } catch (err) {
      setError('Failed to save settings');
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="manga-settings">
      <h1>Settings</h1>

      {error && <div className="error">{error}</div>}

      {/* MangaDex Status */}
      <section className="settings-section">
        <h2>MangaDex</h2>
        <div className="status-card">
          <div className="status-row">
            <span className="label">Status:</span>
            <span className={`value ${mangadexStatus?.enabled ? 'text-success' : 'text-danger'}`}>
              {mangadexStatus?.enabled ? 'Connected' : 'Disabled'}
            </span>
          </div>
          <div className="status-row">
            <span className="label">API:</span>
            <span className="value">{mangadexStatus?.name} ({mangadexStatus?.baseUrl})</span>
          </div>
          <div className="status-row">
            <span className="label">API Key:</span>
            <span className="value">Not required</span>
          </div>
          <div className="status-row">
            <span className="label">Rate Limits:</span>
            <span className="value">
              {mangadexStatus?.rateLimits?.general} general, {mangadexStatus?.rateLimits?.images} images
            </span>
          </div>
        </div>
        <p className="help-text">
          MangaDex is the primary source for manga. No API key is required.
        </p>
      </section>

      {/* Komga Settings */}
      <section className="settings-section">
        <h2>Komga Integration</h2>
        <div className="form-group">
          <label htmlFor="komgaUrl">Komga URL</label>
          <input
            id="komgaUrl"
            type="text"
            value={komgaSettings.baseUrl}
            onChange={(e) => setKomgaSettings(prev => ({ ...prev, baseUrl: e.target.value }))}
            placeholder="http://localhost:25600"
            className="form-control"
          />
          <small className="help-text">The URL where Komga is running</small>
        </div>

        <div className="form-group">
          <label htmlFor="komgaApiKey">API Key (optional)</label>
          <input
            id="komgaApiKey"
            type="password"
            value={komgaSettings.apiKey}
            onChange={(e) => setKomgaSettings(prev => ({ ...prev, apiKey: e.target.value }))}
            placeholder="Your Komga API key"
            className="form-control"
          />
          <small className="help-text">Required if Komga has authentication enabled</small>
        </div>

        <div className="form-actions">
          <button
            className="btn btn-primary"
            onClick={handleSaveKomga}
            disabled={saving}
          >
            {saving ? 'Saving...' : 'Save Settings'}
          </button>

          <button
            className="btn btn-secondary"
            onClick={handleTestKomga}
            disabled={testing}
          >
            {testing ? 'Testing...' : 'Test Connection'}
          </button>
        </div>

        {testResult && (
          <div className={`test-result ${testResult.success ? 'success' : 'error'}`}>
            {testResult.message}
          </div>
        )}

        <div className="status-card">
          <div className="status-row">
            <span className="label">Current Status:</span>
            <span className={`value ${komgaStatus?.available ? 'text-success' : 'text-muted'}`}>
              {komgaStatus?.available ? 'Connected' : 'Not connected'}
            </span>
          </div>
        </div>
      </section>

      {/* Naming Templates */}
      <section className="settings-section">
        <h2>File Naming</h2>
        <p className="help-text">
          Choose how manga CBZ files are named. Files are saved to your manga library folder.
        </p>

        <div className="naming-templates">
          {namingTemplates.map((template, index) => (
            <div key={index} className="template-card">
              <div className="template-name">{template.name}</div>
              <div className="template-pattern">
                <code>{template.pattern}</code>
              </div>
              <div className="template-example">
                Example: <strong>{template.example}</strong>
              </div>
            </div>
          ))}
        </div>
      </section>

      {/* MangaDex Info */}
      <section className="settings-section">
        <h2>About MangaDex</h2>
        <p>
          MangaDex is a free, open-source manga reader and aggregator. It hosts
          scanlations (fan-translated manga) in multiple languages.
        </p>
        <ul>
          <li>Largest manga scanlation database</li>
          <li>Free API — no registration required</li>
          <li>Volume-level chapter organization</li>
          <li>Multiple language support</li>
        </ul>
        <p className="help-text">
          MangaArr uses MangaDex as the primary source for manga metadata and downloads.
          Torrent and Usenet indexers can also be configured via Prowlarr for additional sources.
        </p>
      </section>
    </div>
  );
};

export default MangaSettings;
