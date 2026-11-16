import { useState, useEffect, useCallback } from 'react';
import { createPortal } from 'react-dom';
import type { SingleTaskDto, SprintExperienceSchedule, SprintExperienceWeek, TaskProjectDto } from './types';
import { api } from './api';
import { WeekCount, fmt, fmtDay, fmtRange, todayIso } from './SprintExperiences';
import { CalendarIcon } from './NavIcons';

// The network schedule a sprint carries: a new person to meet every week. The people are the tasks
// of the Tasks tab's "Network" project (matched by name, any case); a task's due date is the
// meeting, and completing the task means you met them. Start Sprint is refused until every week has
// one. Server side: SprintNetwork.cs.

export const NETWORK_PROJECT = 'Network';

const findNetwork = (projects: TaskProjectDto[]) =>
  projects.find(p => p.name.toLowerCase() === NETWORK_PROJECT.toLowerCase()) ?? null;

/**
 * Your Network list, opened from the sprint modal for one week: pick a day, then choose who to meet
 * — that person's task gets the day as its due date (and starts no later than it, so it shows on the
 * day). Someone new can be added to the list right here; the Network project is created on first use.
 */
function NetworkPicker({ roadmapId, week, onChanged, onClose }: {
  roadmapId: string; week: SprintExperienceWeek; onChanged: () => void; onClose: () => void;
}) {
  const [tasks, setTasks] = useState<SingleTaskDto[] | null>(null);
  const [project, setProject] = useState<TaskProjectDto | null>(null);
  const [day, setDay] = useState(() => { const t = todayIso(); return t >= week.from && t <= week.to ? t : week.from; });
  const [query, setQuery] = useState('');
  const [showDone, setShowDone] = useState(false);
  const [newName, setNewName] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');

  const load = useCallback(async () => {
    try {
      const p = findNetwork(await api.getTaskProjects(roadmapId));
      setProject(p);
      setTasks(p ? await api.getTasks(roadmapId, { projectId: p.id }) : []);
    } catch (e) { setError((e as Error).message); setTasks([]); }
  }, [roadmapId]);
  useEffect(() => { load(); }, [load]);

  useEffect(() => {
    const key = (e: KeyboardEvent) => { if (e.key === 'Escape') { e.stopPropagation(); onClose(); } };
    document.addEventListener('keydown', key, true);
    return () => document.removeEventListener('keydown', key, true);
  }, [onClose]);

  const inWeek = (t: SingleTaskDto) => !!t.dueDate && t.dueDate >= week.from && t.dueDate <= week.to;

  const meet = async (t: SingleTaskDto) => {
    setBusy(true); setError('');
    try {
      await api.updateTask(roadmapId, t.id, {
        title: t.title, priority: t.priority, estimatedHours: t.estimatedHours, weekdays: t.weekdays,
        startDate: t.startDate > day ? day : t.startDate, dueDate: day, projectId: t.project?.id ?? project?.id ?? null,
      });
      await load(); onChanged();
    } catch (e) { setError((e as Error).message.replace(/^API \d+: /, '')); }
    finally { setBusy(false); }
  };

  const addPerson = async () => {
    const title = newName.trim();
    if (!title) return;
    setBusy(true); setError('');
    try {
      const p = project ?? await api.createTaskProject(roadmapId, { name: NETWORK_PROJECT });
      await api.createTask(roadmapId, { title, priority: 'Medium', estimatedHours: 1, weekdays: null,
        startDate: day, dueDate: day, projectId: p.id });
      setNewName(''); await load(); onChanged();
    } catch (e) { setError((e as Error).message.replace(/^API \d+: /, '')); }
    finally { setBusy(false); }
  };

  const q = query.trim().toLowerCase();
  const list = (tasks ?? [])
    .filter(t => showDone || !t.isCompleted)
    .filter(t => !q || t.title.toLowerCase().includes(q))
    .sort((a, b) => Number(a.isCompleted) - Number(b.isCompleted)
      || Number(inWeek(a)) - Number(inWeek(b))
      || (a.dueDate ? 1 : 0) - (b.dueDate ? 1 : 0)
      || (a.dueDate ?? '').localeCompare(b.dueDate ?? '') || a.title.localeCompare(b.title));
  const thisWeek = (tasks ?? []).filter(inWeek);
  const doneCount = (tasks ?? []).filter(t => t.isCompleted).length;

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal nw-modal" onClick={e => e.stopPropagation()} role="dialog" aria-labelledby="nw-title">
        <div className="sp-top">
          <h2 id="nw-title">Network — who will you meet?</h2>
          <button className="btn btn-sm btn-ghost" onClick={onClose} aria-label="Close">✕</button>
        </div>

        <div className="xp-sched-bar">
          <div style={{ minWidth: 0 }}>
            <div>Week of <strong>{fmtRange(week.from, week.to)}</strong></div>
            <div style={{ color: 'var(--text-secondary)', fontSize: 13 }}>
              {thisWeek.length === 0 ? 'Nobody scheduled yet' : `Meeting ${thisWeek.map(t => t.title).join(', ')}`}
            </div>
          </div>
          <span style={{ flex: 1 }} />
          <label className="nw-day">Day
            <input type="date" value={day} min={week.from} max={week.to} onChange={e => e.target.value && setDay(e.target.value)} />
          </label>
        </div>

        {error && <div className="sp-error" role="alert">{error}</div>}

        <div className="nw-tools">
          <input type="text" value={query} onChange={e => setQuery(e.target.value)} placeholder="Search people…" aria-label="Search people" />
          {doneCount > 0 && (
            <label className="nw-toggle"><input type="checkbox" checked={showDone} onChange={e => setShowDone(e.target.checked)} /> Show met ({doneCount})</label>
          )}
        </div>

        <div className="nw-list" role="list">
          {tasks === null ? <p className="sp-muted">Loading…</p>
            : list.length === 0
              ? <p className="sp-muted">{(tasks ?? []).length === 0
                  ? `Your ${NETWORK_PROJECT} list is empty — add the first person below.`
                  : 'Nobody matches.'}</p>
              : list.map(t => {
                const here = inWeek(t);
                return (
                  <div key={t.id} className={`nw-person ${here ? 'here' : ''} ${t.isCompleted ? 'met' : ''}`} role="listitem">
                    <span className="nw-avatar" aria-hidden="true">{t.title.trim().slice(0, 1).toUpperCase()}</span>
                    <span className="nw-name">
                      <span>{t.title}</span>
                      <span className="nw-meta">
                        {t.isCompleted ? `Met ${t.completedDate ? fmt(t.completedDate) : ''}`
                          : here ? `This week · ${fmtDay(t.dueDate!)}`
                          : t.dueDate ? `Planned ${fmt(t.dueDate)}` : 'Not scheduled'}
                      </span>
                    </span>
                    {t.isCompleted ? <span className="nw-badge">✓ Met</span>
                      : here && t.dueDate === day ? <span className="nw-badge on">✓ {fmtDay(day)}</span>
                      : (
                        <button className="btn btn-sm btn-accent" disabled={busy} onClick={() => meet(t)}>
                          <CalendarIcon ink="var(--accent)" tone="#fff" /> {here ? `Move to ${fmtDay(day)}` : `Meet ${fmtDay(day)}`}
                        </button>
                      )}
                  </div>
                );
              })}
        </div>

        <div className="nw-add">
          <input type="text" value={newName} onChange={e => setNewName(e.target.value)} placeholder="Someone new…"
            aria-label="Add a person" onKeyDown={e => { if (e.key === 'Enter') addPerson(); }} />
          <button className="btn btn-sm" disabled={busy || !newName.trim()} onClick={addPerson}>Add · meet {fmtDay(day)}</button>
        </div>
        <div className="sp-hint">People are the tasks of the Tasks tab's “{NETWORK_PROJECT}” project; the due date is the meeting, completing the task means you met.</div>
      </div>
    </div>
  );
}

/** Sprint modal, draft sprint: who you meet, week by week, and "+ Add" to open your Network list. */
export function SprintNetworkChecklist({ roadmapId, schedule, onChanged }: {
  roadmapId: string; schedule: SprintExperienceSchedule; onChanged: () => void;
}) {
  const [week, setWeek] = useState<SprintExperienceWeek | null>(null);
  return (
    <div style={{ width: '100%', padding: '8px 12px', background: 'var(--bg-secondary)', borderRadius: 'var(--radius-sm)', fontSize: 13 }}>
      {schedule.weeks.map((w, i) => (
        <div key={w.weekStart} style={{ padding: '6px 0', borderTop: i === 0 ? 'none' : '1px solid var(--border-subtle)' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <span style={{ fontWeight: 600, whiteSpace: 'nowrap' }}>{fmtRange(w.from, w.to)}</span>
            <WeekCount w={w} mode="schedule" />
            <span style={{ flex: 1 }} />
            <button className="btn btn-sm" style={{ flexShrink: 0 }} onClick={() => setWeek(w)}>+ Add</button>
          </div>
          {w.experiences.length > 0 && (
            <div style={{ display: 'flex', flexDirection: 'column', gap: 2, marginTop: 4 }}>
              {w.experiences.map(p => (
                <div key={p.id} style={{ display: 'flex', gap: 8, alignItems: 'center', padding: '3px 4px' }}>
                  <span style={{ width: 14, color: p.status === 'done' ? 'var(--success)' : 'var(--text-muted)' }}>{p.status === 'done' ? '✓' : '○'}</span>
                  <span style={{ flex: 1, minWidth: 0 }}>{p.title}</span>
                  <span style={{ fontSize: 12, color: 'var(--text-muted)', whiteSpace: 'nowrap' }}>{fmtDay(p.date)}</span>
                </div>
              ))}
            </div>
          )}
        </div>
      ))}
      {week && createPortal(
        <NetworkPicker roadmapId={roadmapId} week={week} onChanged={onChanged} onClose={() => setWeek(null)} />,
        document.body)}
    </div>
  );
}
