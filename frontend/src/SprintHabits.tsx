import { useState, useEffect, useCallback } from 'react';
import type { HabitDto, SprintHabitDto } from './types';
import { api } from './api';

// The third start condition: the habits a sprint tracks (at least MIN_HABITS, paused ones not
// counted — SprintNetwork.MinHabits on the server). Picked here from the habit library as toggle
// chips; a habit new to the library can be added and tracked in one go. Tracking itself (daily
// checks, streaks, ±2 pts) is unchanged and lives in the day view and the Habits tab.

export const MIN_HABITS = 1;

export function useSprintHabits(roadmapId: string, sprintId: string, enabled = true) {
  const [library, setLibrary] = useState<HabitDto[] | null>(null);
  const [tracked, setTracked] = useState<SprintHabitDto[] | null>(null);
  const load = useCallback(async () => {
    const [lib, sh] = await Promise.all([api.getHabits(roadmapId), api.getSprintHabits(roadmapId, sprintId)]);
    setLibrary(lib); setTracked(sh);
  }, [roadmapId, sprintId]);
  useEffect(() => { if (enabled) load().catch(() => { setLibrary([]); setTracked([]); }); }, [load, enabled]);
  const count = (tracked ?? []).filter(h => !h.isPaused).length;
  return { library, tracked, load, count, ready: tracked !== null && count >= MIN_HABITS };
}

export function SprintHabitsChecklist({ roadmapId, sprintId, library, tracked, onChanged }: {
  roadmapId: string; sprintId: string; library: HabitDto[] | null; tracked: SprintHabitDto[] | null; onChanged: () => Promise<void>;
}) {
  const [name, setName] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const byHabit = new Map((tracked ?? []).map(t => [t.habitId, t]));

  const run = async (f: () => Promise<unknown>) => {
    setBusy(true); setError('');
    try { await f(); await onChanged(); }
    catch (e) { setError((e as Error).message.replace(/^API \d+: /, '')); }
    finally { setBusy(false); }
  };

  const toggle = (h: HabitDto) => run(async () => {
    const t = byHabit.get(h.id);
    if (t) await api.removeSprintHabit(roadmapId, sprintId, t.sprintHabitId);
    else await api.addSprintHabit(roadmapId, sprintId, h.id);
  });

  const addNew = () => {
    const n = name.trim();
    if (!n) return;
    run(async () => {
      const existing = (library ?? []).find(h => h.name.toLowerCase() === n.toLowerCase());
      const h = existing ?? await api.createHabit(roadmapId, n);
      if (!byHabit.has(h.id)) await api.addSprintHabit(roadmapId, sprintId, h.id);
      setName('');
    });
  };

  return (
    <div className="hb-box">
      {library === null ? <span className="sp-muted">Loading…</span> : (
        <>
          {library.length === 0
            ? <span className="sp-muted">Your habit library is empty — add the first habit below.</span>
            : (
              <div className="hb-chips" role="group" aria-label="Habits to track">
                {library.map(h => {
                  const t = byHabit.get(h.id);
                  return (
                    <button key={h.id} type="button" className={`hb-chip ${t ? 'on' : ''} ${t?.isPaused ? 'paused' : ''}`}
                      aria-pressed={!!t} disabled={busy} onClick={() => toggle(h)}
                      title={t ? (t.isPaused ? 'Tracked but paused — not counted' : 'Tracked — click to remove') : 'Click to track in this sprint'}>
                      <span aria-hidden="true">{t ? '✓' : '+'}</span>{h.name}
                    </button>
                  );
                })}
              </div>
            )}
          <div className="hb-add">
            <input type="text" value={name} onChange={e => setName(e.target.value)} placeholder="New habit…"
              aria-label="New habit" onKeyDown={e => { if (e.key === 'Enter') addNew(); }} />
            <button className="btn btn-sm" disabled={busy || !name.trim()} onClick={addNew}>Add & track</button>
          </div>
        </>
      )}
      {error && <div style={{ color: 'var(--danger)', fontSize: 13 }}>{error}</div>}
    </div>
  );
}
