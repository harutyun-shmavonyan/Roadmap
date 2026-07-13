import { useState, useEffect, useCallback, useRef } from 'react';
import type { ScheduleBlock, ActionableItem, WorkLogDto, SprintDto, ScheduleHabitDto, ScheduleTaskDto, CustomLogDto, ScheduleSubPointDto } from './types';
import { api } from './api';
import { SprintModal } from './SprintModal';
import { fmtUnit } from './unitFormat';

interface Props { roadmapId: string; onBack: () => void; }

const HH = 80;
const GRID_PAD_TOP = 24;  // extra px above 12:00 AM
const GRID_PAD_BOT = 60;  // extra px below 12:00 AM (next day)
const fmt = (m: number) => { const h = Math.floor(m / 60), mm = m % 60, ap = h >= 12 ? 'PM' : 'AM', h12 = h === 0 ? 12 : h > 12 ? h - 12 : h; return `${h12}:${mm.toString().padStart(2, '0')} ${ap}`; };
const todayStr = () => { const d = new Date(); return `${d.getFullYear()}-${(d.getMonth() + 1).toString().padStart(2, '0')}-${d.getDate().toString().padStart(2, '0')}`; };
const fmtDate = (s: string) => new Date(s + 'T00:00:00').toLocaleDateString('en-US', { weekday: 'long', month: 'short', day: 'numeric' });
const fmtDateShort = (s: string) => new Date(s + 'T00:00:00').toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
const shift = (s: string, n: number) => { const d = new Date(s + 'T00:00:00'); d.setDate(d.getDate() + n); return `${d.getFullYear()}-${(d.getMonth() + 1).toString().padStart(2, '0')}-${d.getDate().toString().padStart(2, '0')}`; };

const COLORS = [
  { bg: '#1a2636', border: '#2a5a8a', text: '#6ab0f3' }, { bg: '#1f2a1a', border: '#4a7a2a', text: '#8aca6a' },
  { bg: '#2a1a2a', border: '#7a3a7a', text: '#c080c0' }, { bg: '#2a2418', border: '#8a6a2a', text: '#d4aa5a' },
  { bg: '#1a2a2a', border: '#2a7a7a', text: '#6acaca' }, { bg: '#2a1a1e', border: '#8a3a4a', text: '#d07080' },
];
const getC = (id: string) => { let h = 0; for (let i = 0; i < id.length; i++) h = ((h << 5) - h + id.charCodeAt(i)) | 0; return COLORS[Math.abs(h) % COLORS.length]; };

// Point-based color: gradient from muted to vivid for 0-5pts, gold for 5+
function getPointColor(block: ScheduleBlock): { bg: string; border: string; text: string } {
  const pts = (block.plannedUnits ?? 0) * (block.pointsPerUnit ?? 0);
  const dark = document.documentElement.getAttribute('data-theme') === 'dark';

  // Gold tier: 5+ points
  if (pts >= 5) return dark
    ? { bg: '#2a2410', border: '#b89530', text: '#dbb44a' }
    : { bg: '#fdf6e3', border: '#c4a035', text: '#9a7b1a' };

  // 0 points — neutral gray
  if (pts <= 0) return dark
    ? { bg: '#1a1a1e', border: '#333338', text: '#707078' }
    : { bg: '#f4f4f5', border: '#c0c0c6', text: '#8a8a90' };

  // Gradient 0-5: interpolate between soft cool (low) and warm muted (high)
  const t = Math.min(pts / 5, 1); // 0..1

  if (dark) {
    // Dark mode gradient: slate-blue -> muted teal -> warm sage
    const colors = [
      { bg: '#181c24', border: '#2e3a4e', text: '#6a7a94' }, // ~0 (cool slate)
      { bg: '#1a2028', border: '#2e4a56', text: '#6a9aaa' }, // ~1.25 (steel blue)
      { bg: '#1c2424', border: '#2e5650', text: '#6aaa98' }, // ~2.5 (teal)
      { bg: '#20261e', border: '#3e6040', text: '#7aaa78' }, // ~3.75 (sage)
      { bg: '#24261a', border: '#4e6838', text: '#90a868' }, // ~5 (warm sage)
    ];
    const idx = t * (colors.length - 1);
    const lo = Math.floor(idx);
    const hi = Math.min(lo + 1, colors.length - 1);
    const f = idx - lo;
    return {
      bg: lerpColor(colors[lo].bg, colors[hi].bg, f),
      border: lerpColor(colors[lo].border, colors[hi].border, f),
      text: lerpColor(colors[lo].text, colors[hi].text, f),
    };
  } else {
    // Light mode gradient: cool gray-blue -> muted teal -> warm sage
    const colors = [
      { bg: '#f0f2f6', border: '#a0aab8', text: '#6a7080' }, // ~0 (cool gray)
      { bg: '#edf2f6', border: '#88a4b8', text: '#5a7888' }, // ~1.25 (steel)
      { bg: '#eaf4f2', border: '#70a89a', text: '#4a7a70' }, // ~2.5 (teal)
      { bg: '#eff4ea', border: '#78a870', text: '#4a7a42' }, // ~3.75 (sage)
      { bg: '#f2f4e6', border: '#88a858', text: '#5a7830' }, // ~5 (warm sage)
    ];
    const idx = t * (colors.length - 1);
    const lo = Math.floor(idx);
    const hi = Math.min(lo + 1, colors.length - 1);
    const f = idx - lo;
    return {
      bg: lerpColor(colors[lo].bg, colors[hi].bg, f),
      border: lerpColor(colors[lo].border, colors[hi].border, f),
      text: lerpColor(colors[lo].text, colors[hi].text, f),
    };
  }
}

function lerpColor(a: string, b: string, t: number): string {
  const pa = [parseInt(a.slice(1,3),16), parseInt(a.slice(3,5),16), parseInt(a.slice(5,7),16)];
  const pb = [parseInt(b.slice(1,3),16), parseInt(b.slice(3,5),16), parseInt(b.slice(5,7),16)];
  const r = Math.round(pa[0] + (pb[0]-pa[0]) * t);
  const g = Math.round(pa[1] + (pb[1]-pa[1]) * t);
  const bl = Math.round(pa[2] + (pb[2]-pa[2]) * t);
  return `#${r.toString(16).padStart(2,'0')}${g.toString(16).padStart(2,'0')}${bl.toString(16).padStart(2,'0')}`;
}

function layoutBlocks(blocks: ScheduleBlock[]) {
  const sorted = [...blocks].sort((a, b) => a.startMinute - b.startMinute);
  const result: (ScheduleBlock & { col: number; totalCols: number })[] = [];
  const groups: ScheduleBlock[][] = [];
  for (const b of sorted) {
    let placed = false;
    for (const g of groups) {
      if (g.some(gb => b.startMinute < gb.startMinute + gb.durationMinutes && b.startMinute + b.durationMinutes > gb.startMinute)) { g.push(b); placed = true; break; }
    }
    if (!placed) groups.push([b]);
  }
  for (const g of groups) g.forEach((b, i) => result.push({ ...b, col: i, totalCols: g.length }));
  return result;
}

export function SchedulePage({ roadmapId, onBack }: Props) {
  const [date, setDate] = useState(todayStr);
  const [blocks, setBlocks] = useState<ScheduleBlock[]>([]);
  const [sprint, setSprint] = useState<SprintDto | null>(null);
  const [allItems, setAllItems] = useState<ActionableItem[]>([]);
  const [workLogs, setWorkLogs] = useState<WorkLogDto[]>([]);
  const [habits, setHabits] = useState<ScheduleHabitDto[]>([]);
  const [tasks, setTasks] = useState<ScheduleTaskDto[]>([]);
  const [customLogs, setCustomLogs] = useState<CustomLogDto[]>([]);
  const [showCustomLogForm, setShowCustomLogForm] = useState(false);
  const [isRelaxDay, setIsRelaxDay] = useState(false);
  const [showQuickLog, setShowQuickLog] = useState(false);
  const [quickLogSearch, setQuickLogSearch] = useState('');
  const [loading, setLoading] = useState(true);
  const [sidebarOpen, setSidebarOpen] = useState(false);
  const [showSprintModal, setShowSprintModal] = useState(false);
  const [logPopup, setLogPopup] = useState<{ block: ScheduleBlock; x: number; y: number } | null>(null);
  const [logAmount, setLogAmount] = useState('');
  const [logBusy, setLogBusy] = useState(false);
  const [nowMin, setNowMin] = useState(() => { const n = new Date(); return n.getHours() * 60 + n.getMinutes(); });
  const gridRef = useRef<HTMLDivElement>(null);
  const logInputRef = useRef<HTMLInputElement>(null);
  const logFormRef = useRef<HTMLDivElement>(null);

  const refresh = useCallback(async () => {
    const [sched, all, wl, hb, tk, cl] = await Promise.all([
      api.getSchedule(roadmapId, date), api.getActionables(roadmapId), api.getWorkLogs(roadmapId, date),
      api.getScheduleHabits(roadmapId, date), api.getScheduleTasks(roadmapId, date),
      api.getScheduleCustomLogs(roadmapId, date),
    ]);
    setBlocks(sched.blocks); setSprint(sched.activeSprint); setAllItems(all); setWorkLogs(wl); setHabits(hb); setTasks(tk); setCustomLogs(cl);
    setIsRelaxDay(sched.isRelaxDay ?? false);
  }, [roadmapId, date]);

  useEffect(() => { setLoading(true); refresh().finally(() => setLoading(false)); }, [refresh]);
  useEffect(() => { if (gridRef.current) gridRef.current.scrollTop = GRID_PAD_TOP + 7 * HH - 20; }, [loading]);
  useEffect(() => { const t = setInterval(() => { const n = new Date(); setNowMin(n.getHours() * 60 + n.getMinutes()); }, 30000); return () => clearInterval(t); }, []);
  useEffect(() => { if (logPopup && logInputRef.current) setTimeout(() => logInputRef.current?.focus(), 50); }, [logPopup]);

  // Outside-click handler for log popup — uses mousedown to fire before blur
  useEffect(() => {
    if (!logPopup) return;
    const handler = (e: MouseEvent) => {
      if (logFormRef.current && !logFormRef.current.contains(e.target as Node)) {
        setLogPopup(null);
      }
    };
    document.addEventListener('mousedown', handler);
    return () => document.removeEventListener('mousedown', handler);
  }, [logPopup]);

  const doLog = async () => {
    if (!logPopup || logBusy) return;
    const val = parseFloat(logAmount);
    if (isNaN(val) || val <= 0) return;
    setLogBusy(true);
    try {
      await api.logWork(roadmapId, logPopup.block.nodeId, date, val);
      setLogAmount('');
      await refresh();
      setLogPopup(null);
    } catch (e) {
      console.error('Log failed:', e);
    } finally {
      setLogBusy(false);
    }
  };

  const openLogPopup = (e: React.MouseEvent, block: ScheduleBlock) => {
    e.stopPropagation();
    e.preventDefault();
    const rect = (e.currentTarget as HTMLElement).getBoundingClientRect();
    const y = rect.bottom + 8 > window.innerHeight - 220 ? Math.max(8, rect.top - 200) : rect.bottom + 8;
    setLogPopup({ block, x: Math.min(rect.left, window.innerWidth - 320), y });
    setLogAmount('');
  };

  const isToday = date === todayStr();
  const nowTop = (nowMin / 60) * HH;
  const showNow = isToday;
  const noSprint = !sprint;
  const laid = layoutBlocks(blocks);

  const dayPoints = workLogs.reduce((s, w) => {
    const it = allItems.find(a => a.id === w.nodeId);
    return s + (it?.pointsPerUnit ? w.amount * it.pointsPerUnit : 0);
  }, 0) + customLogs.reduce((s, c) => s + c.points, 0);

  const dayPlannedPoints = blocks.reduce((s, b) => {
    return s + (b.plannedUnits ?? 0) * (b.pointsPerUnit ?? 0);
  }, 0);

  const dayPct = dayPlannedPoints > 0 ? Math.round(dayPoints / dayPlannedPoints * 100) : 0;

  return (
    <div className="sched-shell">
      <header className="sched-header">
        <div className="sched-header-left"><h1 className="sched-title">{fmtDate(date)}</h1></div>
        <div className="sched-nav">
          <button className="btn btn-sm" onClick={() => setDate(d => shift(d, -1))}>‹</button>
          <button className={`btn btn-sm ${isToday ? 'btn-accent' : ''}`} onClick={() => setDate(todayStr())}>Today</button>
          <input type="date" className="date-picker" value={date} onChange={e => { if (e.target.value) setDate(e.target.value); }} />
          <button className="btn btn-sm" onClick={() => setDate(d => shift(d, 1))}>›</button>
          {sprint && <span className={`sprint-badge ${sprint.isOpen && sprint.isStarted ? '' : 'closed'}`}>{sprint.name}{!sprint.isStarted ? ' (draft)' : ''}</span>}
          {noSprint && <span style={{ fontSize: 13, color: 'var(--text-muted)' }}>No sprint</span>}
          {sprint && (
            <button className={`btn btn-sm ${isRelaxDay ? 'btn-warning' : 'btn-ghost'}`}
              onClick={async () => { if (!sprint) return; await api.toggleRelaxDay(roadmapId, sprint.id, date); await refresh(); }}
              title={isRelaxDay ? 'Click to unmark relax day' : 'Mark as relax day'}>
              {isRelaxDay ? '🏖 Relax' : '🏖'}
            </button>
          )}
          <button className="btn btn-sm" onClick={() => setShowSprintModal(true)}>Sprints</button>
        </div>
      </header>

      {/* Habit chips */}
      {habits.length > 0 && sprint && (
        <div className="sched-habits-row">
          {habits.map(h => (
            <div key={h.sprintHabitId} className={`habit-chip ${h.isCheckedToday ? 'checked' : ''} ${h.isFormed ? 'formed' : ''}`}>
              <button className="habit-chip-toggle"
                onClick={() => {
                  const newVal = !h.isCheckedToday;
                  setHabits(prev => prev.map(hh => hh.sprintHabitId === h.sprintHabitId ? { ...hh, isCheckedToday: newVal, currentStreak: newVal ? hh.currentStreak + 1 : Math.max(0, hh.currentStreak - 1) } : hh));
                  api.toggleHabitCheck(roadmapId, sprint.id, h.sprintHabitId, date, newVal).then(() => refresh()).catch(() => refresh());
                }}>
                <span className="habit-chip-check">{h.isCheckedToday ? '✓' : '○'}</span>
                <span>{h.name}</span>
                {h.currentStreak > 0 && <span className="habit-chip-streak">{h.currentStreak}d</span>}
              </button>
              <button className="habit-chip-remove" title="Pause this habit"
                onClick={async (e) => {
                  e.stopPropagation();
                  await api.pauseSprintHabit(roadmapId, sprint.id, h.sprintHabitId);
                  await refresh();
                }}>✕</button>
            </div>
          ))}
        </div>
      )}

      {/* Tasks for the day */}
      {tasks.length > 0 && (
        <div className="sched-tasks-row">
          {tasks.map(t => {
            const priColor = t.priority === 'High' ? 'var(--danger)' : t.priority === 'Medium' ? '#e37400' : 'var(--text-muted)';
            return (
              <div key={t.id} className={`sched-task-card ${t.isCompleted ? 'completed' : ''}`}>
                <button className={`task-check ${t.isCompleted ? 'checked' : ''}`}
                  onClick={() => {
                    const wasCompleted = t.isCompleted;
                    setTasks(prev => prev.map(tt => tt.id === t.id ? { ...tt, isCompleted: !wasCompleted } : tt));
                    const p = wasCompleted ? api.uncompleteTask(roadmapId, t.id) : api.completeTask(roadmapId, t.id, date);
                    p.then(() => refresh()).catch(() => refresh());
                  }}>
                  {t.isCompleted ? '✓' : ''}
                </button>
                <span className="task-pri-dot" style={{ background: priColor }} />
                <span className="task-title">{t.title}</span>
                <span className="task-meta">{t.estimatedHours}h · {t.points}pt</span>
                {t.isOverdue && <span className="task-overdue">overdue</span>}
                {t.dueDate && !t.isOverdue && !t.isCompleted && <span className="task-due">due {t.dueDate.slice(5)}</span>}
                {!t.isCompleted && (
                  <button className="btn btn-ghost btn-sm" title="Delay 3 days"
                    onClick={async () => { await api.delayTask(roadmapId, t.id); await refresh(); }}>
                    ⏭
                  </button>
                )}
              </div>
            );
          })}
        </div>
      )}

      {noSprint ? (
        <div className="empty-state" style={{ flex: 1 }}>
          <p style={{ fontSize: 18, maxWidth: 400, textAlign: 'center', lineHeight: 1.6 }}>No sprint covers this date.</p>
          <button className="btn btn-accent" onClick={() => setShowSprintModal(true)}>Manage Sprints</button>
        </div>
      ) : (
        <div className="sched-body">
          {sidebarOpen && <div className="sidebar-backdrop" onClick={() => setSidebarOpen(false)} />}
          <aside className={`sched-sidebar ${sidebarOpen ? 'open' : ''}`}>
            <div className="today-summary">
              <div className="today-stat accent"><span className="today-label">Earned</span><span className="today-value">{Math.round(dayPoints * 10) / 10} pts</span></div>
              <div className="today-stat"><span className="today-label">Planned</span><span className="today-value">{Math.round(dayPlannedPoints * 10) / 10} pts</span></div>
              <div className="today-stat"><span className="today-label">Done</span><span className="today-value" style={dayPct >= 100 ? {color: 'var(--success)'} : dayPct >= 75 ? {color: '#e37400'} : {}}>{dayPct}%</span></div>
            </div>
            <div className="sched-sidebar-header"><h2>Log — {fmtDateShort(date)}</h2></div>
            <div className="sched-sidebar-list">
              {workLogs.length === 0 && customLogs.length === 0 && <p style={{ color: 'var(--text-muted)', fontSize: 14, padding: 8 }}>Click a block to log work.</p>}
              {workLogs.map(w => <WorkLogRow key={w.id} log={w} roadmapId={roadmapId} onChanged={refresh} />)}

              {/* Custom logs */}
              {customLogs.map(cl => (
                <div key={cl.id} className="worklog-entry">
                  <span style={{ flex: 1, fontSize: 14 }}>⭐ {cl.title}</span>
                  <span className="worklog-amount">+{cl.points}pt</span>
                  <button className="btn btn-ghost btn-sm" onClick={async () => { await api.deleteCustomLog(roadmapId, cl.id); await refresh(); }}>✕</button>
                </div>
              ))}

              {/* Add custom log */}
              {showCustomLogForm ? (
                <CustomLogForm date={date} roadmapId={roadmapId} items={allItems} onDone={() => { setShowCustomLogForm(false); refresh(); }} onCancel={() => setShowCustomLogForm(false)} />
              ) : (
                <button className="btn btn-sm btn-ghost" style={{ marginTop: 8, width: '100%', justifyContent: 'center' }}
                  onClick={() => setShowCustomLogForm(true)}>+ Log custom achievement</button>
              )}

              {/* Quick log to any item */}
              {showQuickLog ? (
                <QuickLogPicker items={allItems} date={date} roadmapId={roadmapId} search={quickLogSearch} setSearch={setQuickLogSearch}
                  onDone={() => { setShowQuickLog(false); setQuickLogSearch(''); refresh(); }} onCancel={() => { setShowQuickLog(false); setQuickLogSearch(''); }} />
              ) : (
                <button className="btn btn-sm btn-ghost" style={{ marginTop: 4, width: '100%', justifyContent: 'center' }}
                  onClick={() => setShowQuickLog(true)}>+ Log to any item</button>
              )}
            </div>
          </aside>

          <div className="sched-grid-wrapper" ref={gridRef}>
            <div className="sched-grid" style={{ height: 24 * HH + GRID_PAD_TOP + GRID_PAD_BOT }}>
              {Array.from({ length: 25 }, (_, i) => (
                <div key={i} className="hour-line" style={{ top: GRID_PAD_TOP + i * HH }}>
                  <span className="hour-label">{fmt(i * 60)}</span><div className="hour-rule" />
                </div>
              ))}
              {Array.from({ length: 24 }, (_, i) => (
                <div key={`h${i}`} className="half-hour-line" style={{ top: GRID_PAD_TOP + i * HH + HH / 2 }} />
              ))}
              {showNow && <div className="now-line" style={{ top: GRID_PAD_TOP + nowTop }}><div className="now-dot" /><div className="now-rule" /></div>}

              {laid.length === 0 && !loading && (
                <div style={{ position: 'absolute', top: '40%', left: '50%', transform: 'translate(-50%,-50%)', color: 'var(--text-muted)', fontSize: 16, textAlign: 'center' }}>
                  {isRelaxDay ? (
                    <div>
                      <div style={{ fontSize: 40, marginBottom: 8 }}>🏖</div>
                      <div style={{ fontWeight: 600, fontSize: 18 }}>Relax Day</div>
                      <div style={{ fontSize: 13, marginTop: 4 }}>No scheduled items. Habits and tasks still active.</div>
                      <div style={{ fontSize: 13, marginTop: 2 }}>Use the sidebar to log work to any item.</div>
                    </div>
                  ) : 'Nothing scheduled.'}
                </div>
              )}

              {laid.map((b, i) => {
                const top = GRID_PAD_TOP + (b.startMinute / 60) * HH;
                const rawH = (b.durationMinutes / 60) * HH;
                const minH = 22;
                const height = Math.max(rawH, minH);
                const isCompact = rawH < 30;
                const c = getPointColor(b);
                const wp = 100 / b.totalCols;
                const lp = b.col * wp;
                const dayLog = workLogs.filter(w => w.nodeId === b.nodeId).reduce((s, w) => s + w.amount, 0);
                const dayDone = b.plannedUnits > 0 && dayLog >= b.plannedUnits;
                const blockPts = Math.round((b.plannedUnits ?? 0) * (b.pointsPerUnit ?? 0) * 10) / 10;
                const durLabel = b.durationMinutes >= 60
                  ? `${Math.floor(b.durationMinutes/60)}h${b.durationMinutes%60 ? b.durationMinutes%60+'m' : ''}`
                  : `${b.durationMinutes}m`;

                return (
                  <div key={`${b.nodeId}-${i}`} className={`sched-entry ${isCompact ? 'compact' : ''}`}
                    style={{ top, height, backgroundColor: c.bg, borderLeftColor: c.border, color: c.text,
                      left: `calc(${lp}% + 4px)`, right: `calc(${100 - lp - wp}% + 4px)`, width: 'auto' }}
                    onClick={e => openLogPopup(e, b)}>
                    <div className="entry-row-top">
                      {dayDone && <span className="entry-done-check">✓</span>}
                      <span className="entry-title">{b.nodeTitle} <span className="entry-inline-meta">{blockPts > 0 ? `${blockPts}pt` : ''} {durLabel}</span></span>
                    </div>
                    {!isCompact && rawH >= 52 && (
                      <div className="entry-row-mid">
                        {b.plannedUnits > 0 && <span>{fmtUnit(dayLog, b.unit)}/{fmtUnit(b.plannedUnits, b.unit)}</span>}
                        <span>{b.completionPercent}%</span>
                      </div>
                    )}
                    {!isCompact && rawH >= 72 && (
                      <div className="entry-progress">
                        <div className="entry-progress-bar"><div className="entry-progress-fill" style={{ width: `${Math.min(b.completionPercent, 100)}%` }} /></div>
                      </div>
                    )}
                  </div>
                );
              })}
            </div>
          </div>
        </div>
      )}

      {/* Log popup — positioned fixed, outside-click handled via document listener */}
      {logPopup && (() => {
        const b = logPopup.block;
        const todayLogs = workLogs.filter(w => w.nodeId === b.nodeId);
        const todayTotal = todayLogs.reduce((s, w) => s + w.amount, 0);
        const dayDone = b.plannedUnits > 0 && todayTotal >= b.plannedUnits;
        return (
        <div className="entry-log-form" ref={logFormRef}
          style={{ position: 'fixed', left: logPopup.x, top: logPopup.y, zIndex: 200 }}>
          <h3 style={{ marginBottom: 6 }}>{b.nodeTitle}</h3>
          <div style={{ fontSize: 13, color: 'var(--text-muted)', marginBottom: 8, display: 'flex', flexDirection: 'column', gap: 4 }}>
            {b.plannedUnits > 0 && (
              <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                <span>Today's plan:</span>
                <span style={{ fontFamily: 'var(--font-mono)', fontWeight: 600, color: dayDone ? 'var(--success)' : 'var(--text-primary)' }}>
                  {fmtUnit(todayTotal, b.unit)} / {fmtUnit(b.plannedUnits, b.unit)} {dayDone ? '✅' : ''}
                </span>
              </div>
            )}
            {b.totalSize != null && (
              <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                <span>Overall:</span>
                <span style={{ fontFamily: 'var(--font-mono)' }}>{fmtUnit(b.totalLogged, b.unit)} / {fmtUnit(b.totalSize, b.unit)} ({b.completionPercent}%)</span>
              </div>
            )}
          </div>
          {todayLogs.length > 0 && (
            <div style={{ marginBottom: 10, borderTop: '1px solid var(--border-subtle)', paddingTop: 8 }}>
              <div style={{ fontSize: 11, color: 'var(--text-muted)', marginBottom: 4, textTransform: 'uppercase', letterSpacing: '0.05em' }}>Today's logs</div>
              {todayLogs.map(w => (
                <div key={w.id} style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 13, padding: '3px 0' }}>
                  <span style={{ flex: 1 }}>{fmtUnit(w.amount, b.unit)}</span>
                  <button className="btn btn-ghost btn-sm" style={{ fontSize: 10, padding: '0 4px', color: 'var(--danger)' }}
                    onClick={async (e) => { e.stopPropagation(); await api.deleteWorkLog(roadmapId, w.id); await refresh(); }}>✕</button>
                </div>
              ))}
            </div>
          )}
          {b.isChecklist && <ChecklistSubPointPanel roadmapId={roadmapId} date={date} nodeId={b.nodeId} onChange={refresh} />}
          {!b.isChecklist && <div className="log-form-row">
            <label>Add</label>
            <input ref={logInputRef} type="number" value={logAmount} step="any"
              onChange={e => setLogAmount(e.target.value)}
              onKeyDown={e => { if (e.key === 'Enter') { e.preventDefault(); doLog(); } if (e.key === 'Escape') setLogPopup(null); }}
              placeholder="0" />
            <span className="unit-hint">{b.unit ? b.unit + 's' : 'units'}</span>
          </div>}
          <div style={{ display: 'flex', gap: 8, marginTop: 6 }}>
            <button className="btn btn-sm btn-accent" disabled={logBusy}
              onClick={(e) => { e.stopPropagation(); doLog(); }}>
              {logBusy ? '...' : 'Log'}
            </button>
            <button className="btn btn-sm" onClick={() => setLogPopup(null)}>Cancel</button>
          </div>
          <div style={{ borderTop: '1px solid var(--border-subtle)', marginTop: 10, paddingTop: 8 }}>
            <button className="btn btn-sm btn-ghost" style={{ width: '100%', justifyContent: 'center', color: 'var(--success)' }}
              disabled={logBusy}
              onClick={async (e) => {
                e.stopPropagation();
                setLogBusy(true);
                try {
                  await api.updateNodeStatus(roadmapId, b.nodeId, 'Completed');
                  setLogPopup(null);
                  await refresh();
                } finally { setLogBusy(false); }
              }}>
              ✓ Mark complete
            </button>
          </div>
        </div>
        );
      })()}

      <button className="sidebar-toggle" onClick={() => setSidebarOpen(!sidebarOpen)}>☰</button>
      {showSprintModal && <SprintModal roadmapId={roadmapId} onClose={() => { setShowSprintModal(false); refresh(); }} />}
    </div>
  );
}

function WorkLogRow({ log, roadmapId, onChanged }: { log: WorkLogDto; roadmapId: string; onChanged: () => void }) {
  const [editing, setEditing] = useState(false);
  const [amount, setAmount] = useState(log.amount.toString());
  const [confirmDel, setConfirmDel] = useState(false);
  const ref = useRef<HTMLInputElement>(null);
  useEffect(() => { if (editing && ref.current) ref.current.focus(); }, [editing]);

  const save = async () => {
    const v = parseFloat(amount);
    if (!isNaN(v) && v > 0 && v !== log.amount) await api.updateWorkLog(roadmapId, log.id, v, log.note);
    setEditing(false); onChanged();
  };
  const del = async () => { if (!confirmDel) { setConfirmDel(true); return; } await api.deleteWorkLog(roadmapId, log.id); onChanged(); };

  return (
    <div className="worklog-entry">
      <span style={{ flex: 1, fontSize: 14 }}>{log.nodeTitle}</span>
      {editing ? (
        <input ref={ref} type="number" value={amount} step="any" onChange={e => setAmount(e.target.value)}
          onBlur={save} onKeyDown={e => { if (e.key === 'Enter') save(); if (e.key === 'Escape') { setAmount(log.amount.toString()); setEditing(false); } }}
          style={{ width: 70, background: 'var(--bg-primary)', border: '1px solid var(--accent)', borderRadius: 4, color: 'var(--text-primary)', fontFamily: 'var(--font-mono)', fontSize: 13, padding: '3px 6px', outline: 'none' }} />
      ) : (
        <span className="worklog-amount" style={{ cursor: 'pointer' }} onClick={() => setEditing(true)} title="Click to edit">
          {fmtUnit(log.amount, log.unit)}
        </span>
      )}
      <button className={`btn btn-ghost btn-sm ${confirmDel ? 'btn-danger' : ''}`} onClick={del}>{confirmDel ? '?' : '✕'}</button>
    </div>
  );
}

function CustomLogForm({ date, roadmapId, items, onDone, onCancel }: {
  date: string; roadmapId: string; items: ActionableItem[]; onDone: () => void; onCancel: () => void;
}) {
  const [title, setTitle] = useState('');
  const [points, setPoints] = useState('2');
  const [selectedItem, setSelectedItem] = useState<ActionableItem | null>(null);
  const [amount, setAmount] = useState('');
  const [busy, setBusy] = useState(false);
  const [showPicker, setShowPicker] = useState(false);
  const ref = useRef<HTMLInputElement>(null);
  useEffect(() => { ref.current?.focus(); }, []);

  const autoPoints = selectedItem?.pointsPerUnit != null
    ? Math.round((parseFloat(amount) || 0) * selectedItem.pointsPerUnit * 10) / 10
    : null;

  const submit = async () => {
    setBusy(true);
    try {
      if (selectedItem && selectedItem.pointsPerUnit != null) {
        const a = parseFloat(amount);
        if (isNaN(a) || a <= 0) return;
        const pts = a * selectedItem.pointsPerUnit;
        await api.createCustomLog(roadmapId, selectedItem.title, pts, date);
      } else {
        const t = title.trim(); const p = parseFloat(points);
        if (!t || isNaN(p) || p <= 0) return;
        await api.createCustomLog(roadmapId, t, p, date);
      }
      onDone();
    } finally { setBusy(false); }
  };

  const inputStyle = { background: 'var(--bg-primary)', border: '1px solid var(--border)', borderRadius: 'var(--radius-sm)',
    color: 'var(--text-primary)', fontFamily: 'var(--font-body)', fontSize: 14, padding: '8px 10px', outline: 'none', width: '100%' };

  return (
    <div style={{ padding: '10px 0', display: 'flex', flexDirection: 'column', gap: 8 }}>
      {!selectedItem ? (
        <>
          <input ref={ref} type="text" value={title} onChange={e => setTitle(e.target.value)}
            placeholder="What did you do?"
            onKeyDown={e => { if (e.key === 'Enter') submit(); if (e.key === 'Escape') onCancel(); }}
            style={inputStyle} />
          {showPicker && (
            <div style={{ maxHeight: 160, overflowY: 'auto', border: '1px solid var(--border)', borderRadius: 'var(--radius-sm)' }}>
              {items.filter(i => i.status === 'Active' || i.status === 'NotStarted').map(i => (
                <div key={i.id} onClick={() => { setSelectedItem(i); setShowPicker(false); }}
                  style={{ padding: '6px 10px', fontSize: 13, cursor: 'pointer', borderBottom: '1px solid var(--border-subtle)' }}
                  onMouseEnter={e => (e.currentTarget.style.background = 'var(--bg-secondary)')}
                  onMouseLeave={e => (e.currentTarget.style.background = '')}>
                  <span>{i.title}</span>
                  {i.pointsPerUnit != null && <span style={{ float: 'right', fontSize: 11, color: 'var(--text-muted)' }}>{i.pointsPerUnit} pts/{i.unit ?? 'unit'}</span>}
                </div>
              ))}
            </div>
          )}
          <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
            <span style={{ fontSize: 12, color: 'var(--text-muted)' }}>Points:</span>
            <input type="number" value={points} onChange={e => setPoints(e.target.value)} step="1" min="1"
              onKeyDown={e => { if (e.key === 'Enter') submit(); if (e.key === 'Escape') onCancel(); }}
              style={{ width: 60, background: 'var(--bg-primary)', border: '1px solid var(--border)', borderRadius: 'var(--radius-sm)',
                color: 'var(--text-primary)', fontFamily: 'var(--font-mono)', fontSize: 14, padding: '6px 8px', outline: 'none' }} />
            <button className="btn btn-sm btn-ghost" onClick={() => setShowPicker(p => !p)} title="Pick from existing items">from list</button>
            <div style={{ flex: 1 }} />
            <button className="btn btn-sm" onClick={onCancel}>Cancel</button>
            <button className="btn btn-sm btn-accent" onClick={submit} disabled={busy || !title.trim()}>Log</button>
          </div>
        </>
      ) : (
        <>
          <div style={{ fontSize: 13, fontWeight: 600, color: 'var(--text-primary)' }}>{selectedItem.title}</div>
          {selectedItem.pointsPerUnit != null && (
            <div style={{ fontSize: 11, color: 'var(--text-muted)' }}>{selectedItem.pointsPerUnit} pts / {selectedItem.unit ?? 'unit'}</div>
          )}
          <input type="number" value={amount} onChange={e => setAmount(e.target.value)} step="any" placeholder={`Amount (${selectedItem.unit ?? 'units'})`}
            onKeyDown={e => { if (e.key === 'Enter') submit(); if (e.key === 'Escape') onCancel(); }}
            style={{ ...inputStyle, fontFamily: 'var(--font-mono)' }} />
          {autoPoints !== null && autoPoints > 0 && (
            <div style={{ fontSize: 12, color: 'var(--accent)' }}>= {autoPoints} pts</div>
          )}
          <div style={{ display: 'flex', gap: 8 }}>
            <button className="btn btn-sm" onClick={() => { setSelectedItem(null); setAmount(''); }}>Back</button>
            <button className="btn btn-sm btn-accent" onClick={submit} disabled={busy || !amount}>Log</button>
          </div>
        </>
      )}
    </div>
  );
}

function QuickLogPicker({ items, date, roadmapId, search, setSearch, onDone, onCancel }: {
  items: ActionableItem[]; date: string; roadmapId: string; search: string;
  setSearch: (s: string) => void; onDone: () => void; onCancel: () => void;
}) {
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [amount, setAmount] = useState('');
  const [busy, setBusy] = useState(false);
  const searchRef = useRef<HTMLInputElement>(null);
  const amountRef = useRef<HTMLInputElement>(null);

  useEffect(() => { searchRef.current?.focus(); }, []);
  useEffect(() => { if (selectedId) setTimeout(() => amountRef.current?.focus(), 50); }, [selectedId]);

  const active = items.filter(i => i.status === 'Active' || i.status === 'NotStarted');
  const filtered = search.trim()
    ? active.filter(i => i.title.toLowerCase().includes(search.toLowerCase()))
    : active;
  const selected = items.find(i => i.id === selectedId);
  const previewPts = selected?.pointsPerUnit != null
    ? Math.round((parseFloat(amount) || 0) * selected.pointsPerUnit * 10) / 10
    : null;

  const submit = async () => {
    if (!selectedId) return;
    const v = parseFloat(amount);
    if (isNaN(v) || v <= 0) return;
    setBusy(true);
    try { await api.logWork(roadmapId, selectedId, date, v); onDone(); }
    finally { setBusy(false); }
  };

  if (selectedId && selected) {
    return (
      <div style={{ padding: '10px 0', display: 'flex', flexDirection: 'column', gap: 8 }}>
        <div style={{ fontSize: 13, fontWeight: 600 }}>{selected.title}</div>
        <div style={{ fontSize: 11, color: 'var(--text-muted)' }}>
          {selected.unit ? `Unit: ${selected.unit}` : ''}
          {selected.pointsPerUnit != null ? `  ·  ${selected.pointsPerUnit} pts/${selected.unit ?? 'unit'}` : ''}
        </div>
        <input ref={amountRef} type="number" value={amount} step="any" placeholder="Amount..."
          onChange={e => setAmount(e.target.value)}
          onKeyDown={e => { if (e.key === 'Enter') submit(); if (e.key === 'Escape') { setSelectedId(null); setAmount(''); } }}
          style={{ background: 'var(--bg-primary)', border: '1px solid var(--border)', borderRadius: 'var(--radius-sm)',
            color: 'var(--text-primary)', fontFamily: 'var(--font-mono)', fontSize: 14, padding: '8px 10px', outline: 'none', width: '100%' }} />
        {previewPts !== null && previewPts > 0 && (
          <div style={{ fontSize: 12, color: 'var(--accent)' }}>= {previewPts} pts</div>
        )}
        <div style={{ display: 'flex', gap: 8 }}>
          <button className="btn btn-sm" onClick={() => { setSelectedId(null); setAmount(''); }}>Back</button>
          <button className="btn btn-sm btn-accent" onClick={submit} disabled={busy || !amount}>Log</button>
        </div>
      </div>
    );
  }

  return (
    <div style={{ padding: '10px 0', display: 'flex', flexDirection: 'column', gap: 6 }}>
      <input ref={searchRef} type="text" value={search} onChange={e => setSearch(e.target.value)}
        placeholder="Search items..."
        onKeyDown={e => { if (e.key === 'Escape') onCancel(); }}
        style={{ background: 'var(--bg-primary)', border: '1px solid var(--border)', borderRadius: 'var(--radius-sm)',
          color: 'var(--text-primary)', fontFamily: 'var(--font-body)', fontSize: 13, padding: '7px 10px', outline: 'none', width: '100%' }} />
      <div style={{ maxHeight: 200, overflowY: 'auto' }}>
        {filtered.slice(0, 20).map(item => (
          <div key={item.id} onClick={() => setSelectedId(item.id)}
            style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '6px 8px', cursor: 'pointer',
              borderRadius: 'var(--radius-sm)', fontSize: 13 }}
            onMouseEnter={e => (e.currentTarget.style.background = 'var(--bg-secondary)')}
            onMouseLeave={e => (e.currentTarget.style.background = 'transparent')}>
            <span style={{ flex: 1 }}>{item.title}</span>
            {item.pointsPerUnit != null
              ? <span style={{ fontSize: 11, color: 'var(--accent)' }}>{item.pointsPerUnit} pts/{item.unit ?? 'unit'}</span>
              : <span style={{ fontSize: 11, color: 'var(--text-muted)' }}>{item.unit || ''}</span>}
          </div>
        ))}
        {filtered.length === 0 && <div style={{ padding: 8, color: 'var(--text-muted)', fontSize: 13 }}>No matching items.</div>}
        {filtered.length > 20 && <div style={{ padding: 8, color: 'var(--text-muted)', fontSize: 11 }}>Showing first 20. Type to narrow down.</div>}
      </div>
      <button className="btn btn-sm" onClick={onCancel} style={{ alignSelf: 'flex-start' }}>Cancel</button>
    </div>
  );
}


function ChecklistSubPointPanel({ roadmapId, date, nodeId, onChange }: { roadmapId: string; date: string; nodeId: string; onChange: () => Promise<void> | void; }) {
  const [items, setItems] = useState<ScheduleSubPointDto[]>([]);
  const [loading, setLoading] = useState(true);
  const load = useCallback(async () => { setItems(await api.getScheduleSubPoints(roadmapId, date, nodeId)); }, [roadmapId, date, nodeId]);
  useEffect(() => { setLoading(true); load().finally(() => setLoading(false)); }, [load]);
  const toggle = async (spid: string, isChecked: boolean) => {
    setItems(prev => prev.map(it => it.id === spid ? { ...it, isChecked } : it));
    await api.toggleScheduleSubPoint(roadmapId, date, nodeId, spid, isChecked);
    await load();
    await onChange();
  };
  if (loading) return <div style={{ fontSize: 12, color: "var(--text-muted)" }}>Loading subpoints…</div>;
  if (items.length === 0) return <div style={{ fontSize: 12, color: "var(--text-muted)" }}>No subpoints defined. Edit this node to add some.</div>;
  const done = items.filter(i => i.isChecked).length;
  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 4 }}>
      <div style={{ fontSize: 11, color: "var(--text-muted)", textTransform: "uppercase", letterSpacing: "0.05em", marginBottom: 2 }}>Subpoints ({done}/{items.length})</div>
      {items.map(sp => (
        <label key={sp.id} style={{ display: "flex", alignItems: "center", gap: 8, fontSize: 13, cursor: "pointer" }}>
          <input type="checkbox" checked={sp.isChecked} onChange={e => toggle(sp.id, e.target.checked)} onClick={e => e.stopPropagation()} />
          <span style={{ flex: 1, textDecoration: sp.isChecked ? "line-through" : "none", opacity: sp.isChecked ? 0.6 : 1 }}>{sp.title}</span>
        </label>
      ))}
    </div>
  );
}
