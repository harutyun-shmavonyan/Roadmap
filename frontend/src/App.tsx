import { useState, useEffect, useCallback } from 'react';
import type { RoadmapSummary } from './types';
import { api, isLoggedIn, login, checkAuth, clearToken } from './api';
import { RoadmapPage } from './RoadmapPage';
import { SchedulePage } from './SchedulePage';
import { WeekPlanPage } from './WeekPlanPage';
import { PerformancePage } from './PerformancePage';
import { HabitsPage } from './HabitsPage';
import { TasksPage } from './TasksPage';
import { NotesPage } from './NotesPage';
import { JobsPage } from './JobsPage';
import { EnglishPage } from './EnglishPage';

type Page = 'picker' | 'schedule' | 'roadmap' | 'weekplan' | 'performance' | 'habits' | 'tasks' | 'notes' | 'jobs' | 'english';

function useTheme() {
  const [theme, setTheme] = useState<'light' | 'dark'>(() => {
    const saved = localStorage.getItem('theme');
    if (saved === 'dark' || saved === 'light') return saved;
    return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
  });
  useEffect(() => {
    document.documentElement.setAttribute('data-theme', theme);
    localStorage.setItem('theme', theme);
  }, [theme]);
  const toggle = useCallback(() => setTheme(t => t === 'light' ? 'dark' : 'light'), []);
  return { theme, toggle };
}

export function App() {
  const { theme, toggle: toggleTheme } = useTheme();
  const [authed, setAuthed] = useState<boolean | null>(null); // null = checking
  const [password, setPassword] = useState('');
  const [loginError, setLoginError] = useState(false);
  const [loginBusy, setLoginBusy] = useState(false);

  // Check auth on mount
  useEffect(() => {
    if (!isLoggedIn()) { setAuthed(false); return; }
    checkAuth().then(ok => setAuthed(ok));
  }, []);

  const handleLogin = async () => {
    setLoginBusy(true); setLoginError(false);
    const ok = await login(password);
    setLoginBusy(false);
    if (ok) { setAuthed(true); setPassword(''); }
    else setLoginError(true);
  };

  // Show login screen
  if (authed === null) return <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', height: '100vh', color: 'var(--text-muted)' }}>Loading...</div>;
  if (!authed) return <LoginScreen password={password} setPassword={setPassword} error={loginError} busy={loginBusy} onLogin={handleLogin} theme={theme} toggleTheme={toggleTheme} />;

  return <AuthedApp theme={theme} toggleTheme={toggleTheme} onLogout={() => { clearToken(); setAuthed(false); }} />;
}

function LoginScreen({ password, setPassword, error, busy, onLogin, theme, toggleTheme }: {
  password: string; setPassword: (s: string) => void; error: boolean; busy: boolean; onLogin: () => void; theme: string; toggleTheme: () => void;
}) {
  return (
    <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', height: '100vh', background: 'var(--bg-primary)' }}>
      <div style={{ width: 320, padding: 32, background: 'var(--bg-secondary)', borderRadius: 'var(--radius-lg)', border: '1px solid var(--border)' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 24 }}>
          <h1 style={{ fontSize: 24, fontWeight: 700, color: 'var(--text-primary)', margin: 0 }}>Roadmap</h1>
          <button className="btn btn-ghost btn-sm" onClick={toggleTheme}>{theme === 'dark' ? '☀️' : '🌙'}</button>
        </div>
        <input type="password" value={password} onChange={e => { setPassword(e.target.value); if (error) {} }}
          placeholder="Enter password"
          autoFocus
          onKeyDown={e => { if (e.key === 'Enter') onLogin(); }}
          style={{ width: '100%', fontSize: 16, padding: '12px 14px', background: 'var(--bg-primary)', border: `1px solid ${error ? 'var(--danger)' : 'var(--border)'}`,
            borderRadius: 'var(--radius-sm)', color: 'var(--text-primary)', outline: 'none', marginBottom: 12 }} />
        {error && <div style={{ color: 'var(--danger)', fontSize: 13, marginBottom: 8 }}>Wrong password</div>}
        <button className="btn btn-accent" style={{ width: '100%', padding: '12px', fontSize: 15 }}
          onClick={onLogin} disabled={busy || !password}>
          {busy ? 'Logging in...' : 'Login'}
        </button>
      </div>
    </div>
  );
}

function AuthedApp({ theme, toggleTheme, onLogout }: { theme: string; toggleTheme: () => void; onLogout: () => void }) {
  const [roadmaps, setRoadmaps] = useState<RoadmapSummary[]>([]);
  const [selId, setSelId] = useState<string | null>(null);
  const [page, setPage] = useState<Page>('picker');
  const [loading, setLoading] = useState(true);
  const [creating, setCreating] = useState(false);
  const [newName, setNewName] = useState('');

  const load = async () => {
    setLoading(true);
    try {
      const d = await api.listRoadmaps(); setRoadmaps(d);
      if (d.length === 1 && !selId) { setSelId(d[0].id); setPage('schedule'); }
    } finally { setLoading(false); }
  };
  useEffect(() => { load(); }, []);

  const create = async () => {
    const t = newName.trim(); if (!t) return;
    const c = await api.createRoadmap(t);
    setNewName(''); setCreating(false); setSelId(c.id); setPage('schedule'); load();
  };

  const back = () => { setSelId(null); setPage('picker'); load(); };

  const themeBtn = (
    <button className="btn btn-ghost btn-sm" onClick={toggleTheme} title={`Switch to ${theme === 'light' ? 'dark' : 'light'} mode`}
      style={{ fontSize: 18, padding: '4px 8px' }}>
      {theme === 'light' ? '🌙' : '☀️'}
    </button>
  );

  if (selId && page !== 'picker') {
    const nav = (
      <div className="nav-tabs">
        <button className={`nav-tab ${page === 'schedule' ? 'active' : ''}`} onClick={() => setPage('schedule')}>Schedule</button>
        <button className={`nav-tab ${page === 'weekplan' ? 'active' : ''}`} onClick={() => setPage('weekplan')}>Week</button>
        <button className={`nav-tab ${page === 'roadmap' ? 'active' : ''}`} onClick={() => setPage('roadmap')}>Roadmap</button>
        <button className={`nav-tab ${page === 'performance' ? 'active' : ''}`} onClick={() => setPage('performance')}>Performance</button>
        <button className={`nav-tab ${page === 'habits' ? 'active' : ''}`} onClick={() => setPage('habits')}>Habits</button>
        <button className={`nav-tab ${page === 'tasks' ? 'active' : ''}`} onClick={() => setPage('tasks')}>Tasks</button>
        <button className={`nav-tab ${page === 'notes' ? 'active' : ''}`} onClick={() => setPage('notes')}>Notes</button>
        <button className={`nav-tab ${page === 'jobs' ? 'active' : ''}`} onClick={() => setPage('jobs')}>Jobs</button>
        <button className={`nav-tab ${page === 'english' ? 'active' : ''}`} onClick={() => setPage('english')}>English</button>
      </div>
    );

    const renderPage = () => {
      switch (page) {
        case 'schedule': return <SchedulePage roadmapId={selId} onBack={back} />;
        case 'roadmap': return <RoadmapPage roadmapId={selId} onBack={back} />;
        case 'weekplan': return <WeekPlanPage roadmapId={selId} onBack={back} />;
        case 'performance': return <PerformancePage roadmapId={selId} onBack={back} />;
        case 'habits': return <HabitsPage roadmapId={selId} onBack={back} />;
        case 'tasks': return <TasksPage roadmapId={selId} onBack={back} />;
        case 'notes': return <NotesPage />;
        case 'jobs': return <JobsPage />;
        case 'english': return <EnglishPage />;
      }
    };

    return (
      <div style={{ display: 'flex', flexDirection: 'column', height: '100dvh' }}>
        <div className="app-header" style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '8px 14px', borderBottom: '1px solid var(--border-subtle)', background: 'var(--bg-primary)', flexShrink: 0, gap: 8, flexWrap: 'wrap' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap', flex: 1, minWidth: 0 }}>
            <span style={{ fontSize: 15, fontWeight: 500, color: 'var(--text-secondary)', whiteSpace: 'nowrap' }}><span style={{ color: 'var(--accent)', fontWeight: 600 }}>◆</span> {roadmaps.find(r => r.id === selId)?.name}</span>
            {nav}
          </div>
          <div style={{ display: 'flex', gap: 6, alignItems: 'center', flexShrink: 0 }}>
            {themeBtn}
            <button className="btn btn-sm" onClick={back}>←</button>
            <button className="btn btn-sm btn-ghost" onClick={onLogout} title="Logout" style={{ fontSize: 14 }}>🚪</button>
          </div>
        </div>
        <div style={{ flex: 1, overflow: 'hidden' }}>
          {renderPage()}
        </div>
      </div>
    );
  }

  // Picker screen
  return (
    <div className="picker-screen">
      <div style={{ position: 'absolute', top: 16, right: 16 }}>{themeBtn}</div>
      <h1><span>◆</span> Roadmap</h1>
      {loading ? <p style={{ color: 'var(--text-muted)' }}>Loading...</p> : <>
        {roadmaps.length > 0 && <div className="picker-list">
          {roadmaps.map(r => (
            <div key={r.id} className="picker-item" onClick={() => { setSelId(r.id); setPage('schedule'); }}>
              <div>
                <div className="picker-item-name">{r.name}</div>
                {r.description && <div className="picker-item-desc">{r.description}</div>}
              </div>
              <span className="picker-item-arrow">→</span>
            </div>
          ))}
        </div>}
        {creating ? (
          <div style={{ display: 'flex', gap: 10, alignItems: 'center' }}>
            <input className="inline-input" placeholder="Roadmap name..." value={newName} onChange={e => setNewName(e.target.value)} autoFocus
              onKeyDown={e => { if (e.key === 'Enter') create(); if (e.key === 'Escape') setCreating(false); }} />
            <button className="btn btn-accent" onClick={create}>Create</button>
            <button className="btn" onClick={() => setCreating(false)}>Cancel</button>
          </div>
        ) : <button className="btn btn-accent" onClick={() => setCreating(true)}>+ New Roadmap</button>}
      </>}
    </div>
  );
}
