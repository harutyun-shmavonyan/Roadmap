import { useState } from 'react';
import type { SprintExperienceSchedule, SprintExperienceWeek } from './types';
import { EXPERIENCE_CATEGORIES } from './types';
import { api } from './api';

// The experiences schedule a sprint carries: at least 2 dated experiences in each Monday–Sunday
// week (a clipped week owes its share, rounded up). Start Sprint is refused until every week has
// them; Performance and the week view show how many were actually had. The rows themselves live
// in the Experiences tab — this only reads them, and the sprint modal can add a missing plan.

const todayIso = () => {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
};

const fmt = (iso: string) =>
  new Date(iso + 'T00:00:00').toLocaleDateString(undefined, { month: 'short', day: 'numeric' });
const fmtDay = (iso: string) =>
  new Date(iso + 'T00:00:00').toLocaleDateString(undefined, { weekday: 'short', day: 'numeric' });

/** done of required, with how many are scheduled — the colour says whether the week's minimum is met. */
function WeekCount({ w, mode }: { w: SprintExperienceWeek; mode: 'schedule' | 'done' }) {
  const ended = w.to < todayIso();
  const metDone = w.done >= w.required;
  const color = mode === 'schedule'
    ? (w.met ? 'var(--success)' : 'var(--danger)')
    : metDone ? 'var(--success)' : ended ? 'var(--danger)' : w.met ? 'var(--text-muted)' : 'var(--warning)';
  return (
    <span style={{ fontFamily: 'var(--font-mono)', fontSize: 12, fontWeight: 600, color }}>
      {mode === 'schedule'
        ? `${w.scheduled}/${w.required} scheduled`
        : `${w.done}/${w.required} done · ${w.scheduled} scheduled`}
    </span>
  );
}

function ExperienceList({ w }: { w: SprintExperienceWeek }) {
  const today = todayIso();
  if (w.experiences.length === 0)
    return <div style={{ fontSize: 13, color: 'var(--text-muted)', padding: '4px 0' }}>No experiences scheduled this week.</div>;
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
      {w.experiences.map(e => {
        const done = e.status === 'done';
        const missed = !done && e.date < today;
        return (
          <div key={e.id} style={{ display: 'flex', alignItems: 'center', gap: 10, padding: '8px 12px',
            background: 'var(--bg-secondary)', borderRadius: 'var(--radius-sm)', fontSize: 14 }}>
            <span style={{ width: 16, textAlign: 'center', color: done ? 'var(--success)' : missed ? 'var(--danger)' : 'var(--text-muted)', fontWeight: 600 }}
              title={done ? 'Done' : missed ? 'Date passed — not marked done' : 'Planned'}>
              {done ? '✓' : missed ? '✕' : '○'}
            </span>
            <span style={{ flex: 1, minWidth: 0, color: missed ? 'var(--text-muted)' : undefined }}>{e.title}</span>
            {e.category && <span style={{ fontSize: 11, color: 'var(--text-muted)', border: '1px solid var(--border-subtle)', borderRadius: 4, padding: '1px 6px' }}>{e.category}</span>}
            <span style={{ fontSize: 12, color: 'var(--text-muted)', whiteSpace: 'nowrap' }}>{fmtDay(e.date)}</span>
          </div>
        );
      })}
    </div>
  );
}

/** Performance: the whole sprint, a summary line and one block per week. */
export function SprintExperiencesSection({ schedule }: { schedule: SprintExperienceSchedule }) {
  return (
    <div className="perf-section">
      <h2>Experiences
        <span style={{ marginLeft: 8, fontSize: 12, fontWeight: 600, fontFamily: 'var(--font-mono)',
          color: schedule.done >= schedule.required ? 'var(--success)' : 'var(--text-muted)' }}>
          {schedule.done}/{schedule.required} done · {schedule.scheduled} scheduled · min {schedule.perWeek}/week
        </span>
      </h2>
      <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
        {schedule.weeks.map(w => (
          <div key={w.weekStart}>
            <div style={{ display: 'flex', alignItems: 'baseline', gap: 10, marginBottom: 6 }}>
              <span style={{ fontSize: 13, fontWeight: 600 }}>{fmt(w.from)} – {fmt(w.to)}</span>
              <WeekCount w={w} mode="done" />
            </div>
            <ExperienceList w={w} />
          </div>
        ))}
      </div>
    </div>
  );
}

/** Week view: just this week. */
export function WeekExperiencesSection({ week }: { week: SprintExperienceWeek }) {
  return (
    <div className="perf-section">
      <h2>Experiences <span style={{ marginLeft: 8 }}><WeekCount w={week} mode="done" /></span></h2>
      <ExperienceList w={week} />
    </div>
  );
}

/**
 * Sprint modal, draft sprint: what Start needs, week by week, with a one-line form to add a
 * planned experience to a short week without leaving the modal.
 */
export function SprintExperiencesChecklist({ schedule, onAdded }: { schedule: SprintExperienceSchedule; onAdded: () => void }) {
  const [addingWeek, setAddingWeek] = useState<string | null>(null);
  const [title, setTitle] = useState('');
  const [date, setDate] = useState('');
  const [category, setCategory] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');

  const open = (w: SprintExperienceWeek) => {
    setAddingWeek(w.weekStart); setTitle(''); setCategory(''); setError('');
    const t = todayIso();
    setDate(t >= w.from && t <= w.to ? t : w.from);
  };

  const add = async () => {
    if (!title.trim() || !date) return;
    setBusy(true); setError('');
    try {
      await api.createExperience({ title: title.trim(), category: category || null, tags: [], status: 'planned',
        location: null, startDate: date, endDate: null, descriptionMd: null });
      setAddingWeek(null); onAdded();
    } catch (e) { setError((e as Error).message); }
    finally { setBusy(false); }
  };

  return (
    <div style={{ width: '100%', padding: '8px 12px', background: 'var(--bg-secondary)', borderRadius: 'var(--radius-sm)', fontSize: 13 }}>
      <div style={{ marginBottom: 6, color: schedule.ready ? 'var(--success)' : 'var(--text-secondary, var(--text-muted))' }}>
        {schedule.ready
          ? '✓ Experiences scheduled — ready to start'
          : `Schedule at least ${schedule.perWeek} experiences a week to start this sprint`}
      </div>
      {schedule.weeks.map(w => (
        <div key={w.weekStart} style={{ padding: '4px 0', borderTop: '1px solid var(--border-subtle)' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
            <span style={{ minWidth: 110 }}>{fmt(w.from)} – {fmt(w.to)}</span>
            <WeekCount w={w} mode="schedule" />
            <span style={{ flex: 1, minWidth: 0, color: 'var(--text-muted)', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
              {w.experiences.map(e => e.title).join(' · ')}
            </span>
            {addingWeek !== w.weekStart &&
              <button className="btn btn-sm" onClick={() => open(w)} disabled={busy}>+ Add</button>}
          </div>
          {addingWeek === w.weekStart && (
            <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap', marginTop: 6 }}>
              <input type="text" value={title} onChange={e => setTitle(e.target.value)} placeholder="Experience…"
                style={{ flex: 1, minWidth: 140 }} autoFocus onKeyDown={e => { if (e.key === 'Enter') add(); }} />
              <input type="date" value={date} min={w.from} max={w.to} onChange={e => setDate(e.target.value)} />
              <select value={category} onChange={e => setCategory(e.target.value)}>
                <option value="">Category…</option>
                {EXPERIENCE_CATEGORIES.map(c => <option key={c} value={c}>{c}</option>)}
              </select>
              <button className="btn btn-sm btn-accent" onClick={add} disabled={busy || !title.trim() || !date}>Add</button>
              <button className="btn btn-sm" onClick={() => setAddingWeek(null)} disabled={busy}>Cancel</button>
            </div>
          )}
        </div>
      ))}
      {error && <div style={{ color: 'var(--danger)', marginTop: 6 }}>{error}</div>}
    </div>
  );
}
