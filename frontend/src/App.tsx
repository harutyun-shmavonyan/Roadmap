import { useState, useEffect, useCallback, useRef } from 'react';
import type { RoadmapSummary } from './types';
import { api, isLoggedIn, login, checkAuth, clearToken } from './api';
import { RoadmapPage } from './RoadmapPage';
import { SchedulePage } from './SchedulePage';
import { WeekPlanPage } from './WeekPlanPage';
import { PerformancePage } from './PerformancePage';
import { HabitsPage } from './HabitsPage';
import { TasksPage } from './TasksPage';
import { NotesPage } from './NotesPage';
import { ArticlesPage } from './ArticlesPage';
import { NewsletterPage } from './NewsletterPage';
import { NAV_ICON, IconDefs } from './NavIcons';
import { JobsPage } from './JobsPage';
import { EnglishPage } from './EnglishPage';
import { NutritionPage } from './NutritionPage';

/* ─── Navigation model ───
   Scoped pages belong to the selected roadmap and take a roadmapId; global pages
   stand on their own. The rail groups them by that split, and the hash mirrors it:
   #/r/{roadmapId}/{page} for scoped, #/{page} for global, #/roadmaps for the picker. */

type ScopedPage = 'schedule' | 'weekplan' | 'roadmap' | 'tasks' | 'performance' | 'habits';
type GlobalPage = 'articles' | 'newsletter' | 'english' | 'notes' | 'nutrition' | 'jobs';
type PageId = ScopedPage | GlobalPage;

const SCOPED_PAGES: ScopedPage[] = ['schedule', 'weekplan', 'roadmap', 'tasks', 'performance', 'habits'];
const GLOBAL_PAGES: GlobalPage[] = ['articles', 'newsletter', 'english', 'notes', 'nutrition', 'jobs'];

const isScoped = (id: PageId): id is ScopedPage => (SCOPED_PAGES as string[]).includes(id);

interface NavItem { id: PageId; label: string; }
interface NavGroup { title: string | null; scoped: boolean; items: NavItem[]; }

const NAV_GROUPS: NavGroup[] = [
  {
    title: null, scoped: true, items: [
      { id: 'schedule', label: 'Schedule' },
      { id: 'weekplan', label: 'Week' },
      { id: 'roadmap', label: 'Roadmap' },
      { id: 'tasks', label: 'Tasks' },
      { id: 'performance', label: 'Performance' },
      { id: 'habits', label: 'Habits' },
    ],
  },
  {
    title: 'Learn', scoped: false, items: [
      { id: 'articles', label: 'Articles' },
      { id: 'newsletter', label: 'Newsletter' },
      { id: 'english', label: 'English' },
    ],
  },
  {
    title: 'Track', scoped: false, items: [
      { id: 'notes', label: 'Notes' },
      { id: 'nutrition', label: 'Nutrition' },
      { id: 'jobs', label: 'Jobs' },
    ],
  },
];

const NAV_INDEX: Record<string, NavItem> = Object.fromEntries(
  NAV_GROUPS.flatMap(g => g.items).map(i => [i.id, i])
);

/* Phone bottom bar: everything else lives one tap away behind "More". */
// The phone's quick row: the two places you steer the day from, then the two you read.
const BOTTOM_BAR: PageId[] = ['schedule', 'weekplan', 'newsletter', 'articles'];

/* ─── Hash routing ─── */

type Route =
  | { kind: 'picker' }
  | { kind: 'scoped'; roadmapId: string; page: ScopedPage }
  | { kind: 'global'; page: GlobalPage };

function parseHash(): Route {
  const parts = window.location.hash.replace(/^#\/?/, '').split('/').filter(Boolean);
  if (parts[0] === 'r' && parts[1]) {
    const page = parts[2] as ScopedPage;
    return { kind: 'scoped', roadmapId: parts[1], page: SCOPED_PAGES.includes(page) ? page : 'schedule' };
  }
  if (parts.length === 1 && GLOBAL_PAGES.includes(parts[0] as GlobalPage)) {
    return { kind: 'global', page: parts[0] as GlobalPage };
  }
  return { kind: 'picker' };
}

function hashFor(id: PageId, roadmapId: string | null): string {
  if (isScoped(id)) return roadmapId ? `#/r/${roadmapId}/${id}` : '#/roadmaps';
  return `#/${id}`;
}

function useRoute(): [Route, (hash: string, replace?: boolean) => void] {
  const [route, setRoute] = useState<Route>(parseHash);
  useEffect(() => {
    const onChange = () => setRoute(parseHash());
    window.addEventListener('hashchange', onChange);
    return () => window.removeEventListener('hashchange', onChange);
  }, []);
  // replaceState does not fire hashchange, and neither does re-assigning the same
  // hash — both branches re-read the location so the route state stays authoritative.
  const go = useCallback((hash: string, replace = false) => {
    if (replace) {
      window.history.replaceState(null, '', hash);
      setRoute(parseHash());
    } else if (window.location.hash === hash) {
      setRoute(parseHash());
    } else {
      window.location.hash = hash;
    }
  }, []);
  return [route, go];
}

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
  const [route, go] = useRoute();
  const [roadmaps, setRoadmaps] = useState<RoadmapSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [creating, setCreating] = useState(false);
  const [newName, setNewName] = useState('');
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [menuOpen, setMenuOpen] = useState(false);
  const [railCollapsed, setRailCollapsed] = useState(() => localStorage.getItem('railCollapsed') === '1');
  // Global pages carry no roadmap, so the rail remembers the last one to stay usable there.
  const [lastRoadmapId, setLastRoadmapId] = useState<string | null>(() => localStorage.getItem('lastRoadmapId'));

  const load = useCallback(async () => {
    setLoading(true);
    try { setRoadmaps(await api.listRoadmaps()); }
    finally { setLoading(false); }
  }, []);
  useEffect(() => { load(); }, [load]);

  useEffect(() => { localStorage.setItem('railCollapsed', railCollapsed ? '1' : '0'); }, [railCollapsed]);

  useEffect(() => {
    if (route.kind !== 'scoped') return;
    setLastRoadmapId(route.roadmapId);
    localStorage.setItem('lastRoadmapId', route.roadmapId);
  }, [route]);

  useEffect(() => { setDrawerOpen(false); setMenuOpen(false); }, [route]);

  // Land on the only roadmap when there is one, but only on first load — otherwise
  // the picker would bounce you straight back out and a second roadmap could never be made.
  const autoLanded = useRef(false);
  useEffect(() => {
    if (loading) return;
    if (route.kind === 'scoped' && roadmaps.length > 0 && !roadmaps.some(r => r.id === route.roadmapId)) {
      go('#/roadmaps', true); return;
    }
    if (autoLanded.current) return;
    autoLanded.current = true;
    if (route.kind === 'picker' && roadmaps.length === 1) go(`#/r/${roadmaps[0].id}/schedule`, true);
  }, [loading, roadmaps, route, go]);

  const create = async () => {
    const t = newName.trim(); if (!t) return;
    const c = await api.createRoadmap(t);
    setNewName(''); setCreating(false);
    await load();
    go(`#/r/${c.id}/schedule`);
  };

  const back = () => go('#/roadmaps');

  const railRoadmapId = route.kind === 'scoped'
    ? route.roadmapId
    : (lastRoadmapId && roadmaps.some(r => r.id === lastRoadmapId) ? lastRoadmapId : (roadmaps[0]?.id ?? null));
  const railRoadmap = roadmaps.find(r => r.id === railRoadmapId) ?? null;
  const activePage: PageId | null = route.kind === 'picker' ? null : route.page;

  const rail = (collapsed: boolean, showCollapseToggle: boolean) => (
    <>
      <div className="rail-top">
        <button className="rail-brand" onClick={() => setMenuOpen(o => !o)}
          title={railRoadmap?.name ?? 'Choose roadmap'}>
          <span className="rail-brand-mark">◆</span>
          {!collapsed && <>
            <span className="rail-brand-name">{railRoadmap?.name ?? 'Choose roadmap'}</span>
            <span className="rail-brand-caret">▾</span>
          </>}
        </button>
        {menuOpen && <>
          <div className="rail-menu-backdrop" onClick={() => setMenuOpen(false)} />
          <div className="rail-menu">
            {roadmaps.map(r => (
              <button key={r.id} className={`rail-menu-item ${r.id === railRoadmapId ? 'active' : ''}`}
                onClick={() => go(`#/r/${r.id}/${route.kind === 'scoped' ? route.page : 'schedule'}`)}>
                {r.name}
              </button>
            ))}
            {roadmaps.length > 0 && <div className="rail-menu-sep" />}
            <button className="rail-menu-item" onClick={() => go('#/roadmaps')}>All roadmaps…</button>
          </div>
        </>}
      </div>

      <div className="rail-groups">
        {NAV_GROUPS.map((g, gi) => (
          <div className="rail-group" key={gi}>
            {g.title && (collapsed ? <div className="rail-group-rule" /> : <div className="rail-group-title">{g.title}</div>)}
            {g.items.map(item => (
              <button key={item.id} disabled={g.scoped && !railRoadmapId}
                className={`rail-item ${activePage === item.id ? 'active' : ''}`}
                title={collapsed ? item.label : undefined}
                onClick={() => go(hashFor(item.id, railRoadmapId))}>
                <span className="rail-item-icon">{NAV_ICON[item.id]}</span>
                {!collapsed && <span className="rail-item-label">{item.label}</span>}
              </button>
            ))}
          </div>
        ))}
      </div>

      <div className="rail-foot">
        <button className="rail-foot-btn" onClick={toggleTheme}
          title={`Switch to ${theme === 'light' ? 'dark' : 'light'} mode`}>{theme === 'light' ? NAV_ICON.moon : NAV_ICON.sun}</button>
        <button className="rail-foot-btn" onClick={onLogout} title="Logout">{NAV_ICON.logout}</button>
        {showCollapseToggle && (
          <button className="rail-foot-btn rail-collapse" onClick={() => setRailCollapsed(c => !c)}
            title={collapsed ? 'Expand sidebar' : 'Collapse sidebar'}>{collapsed ? '»' : '«'}</button>
        )}
      </div>
    </>
  );

  const picker = (
    <div className="picker-screen">
      <h1><span>◆</span> Roadmap</h1>
      {loading ? <p style={{ color: 'var(--text-muted)' }}>Loading...</p> : <>
        {roadmaps.length > 0 && <div className="picker-list">
          {roadmaps.map(r => (
            <div key={r.id} className="picker-item" onClick={() => go(`#/r/${r.id}/schedule`)}>
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

  const renderRoute = () => {
    if (route.kind === 'global') {
      switch (route.page) {
        case 'notes': return <NotesPage />;
        case 'articles': return <ArticlesPage />;
        case 'newsletter': return <NewsletterPage />;
        case 'jobs': return <JobsPage />;
        case 'english': return <EnglishPage />;
        case 'nutrition': return <NutritionPage />;
      }
    }
    if (route.kind === 'scoped') {
      const rid = route.roadmapId;
      switch (route.page) {
        case 'schedule': return <SchedulePage roadmapId={rid} onBack={back} />;
        case 'roadmap': return <RoadmapPage roadmapId={rid} onBack={back} />;
        case 'weekplan': return <WeekPlanPage roadmapId={rid} onBack={back} />;
        case 'performance': return <PerformancePage roadmapId={rid} onBack={back} />;
        case 'habits': return <HabitsPage roadmapId={rid} onBack={back} />;
        case 'tasks': return <TasksPage roadmapId={rid} onBack={back} />;
      }
    }
    return picker;
  };

  return (
    <div className="app-frame">
      {/* The icons' gradients, defined once outside anything that can be hidden. */}
      <IconDefs />
      <nav className={`app-rail ${railCollapsed ? 'collapsed' : ''}`}>{rail(railCollapsed, true)}</nav>

      {drawerOpen && <>
        <div className="rail-backdrop" onClick={() => setDrawerOpen(false)} />
        <nav className="app-rail app-rail-drawer">{rail(false, false)}</nav>
      </>}

      <div className="app-main">
        <div className="app-page">{renderRoute()}</div>
        <nav className="app-bottombar">
          {BOTTOM_BAR.map(id => {
            const item = NAV_INDEX[id];
            return (
              <button key={id} disabled={isScoped(id) && !railRoadmapId}
                className={`bar-item ${activePage === id ? 'active' : ''}`}
                onClick={() => go(hashFor(id, railRoadmapId))}>
                <span className="bar-item-icon">{NAV_ICON[item.id]}</span>
                <span className="bar-item-label">{item.label}</span>
              </button>
            );
          })}
          <button className={`bar-item ${drawerOpen ? 'active' : ''}`} onClick={() => setDrawerOpen(true)}>
            <span className="bar-item-icon">{NAV_ICON.more}</span>
            <span className="bar-item-label">More</span>
          </button>
        </nav>
      </div>
    </div>
  );
}
