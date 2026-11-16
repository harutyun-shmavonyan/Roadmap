import { useState } from 'react';
import type { ExperienceDto, SprintExperienceSchedule, SprintExperienceWeek } from './types';
import { EXPERIENCE_CATEGORIES } from './types';
import { api } from './api';

// The experiences schedule a sprint carries: at least 2 dated experiences in each Monday–Sunday
// week (a clipped week owes its share, rounded up). Start Sprint is refused until every week has
// them; Performance and the week view show how many were actually had. The rows themselves live
// in the Experiences tab — this only reads them, and the sprint modal can add a missing plan.

/** What a done experience earns its sprint — SprintExperiences.PointsPerDone on the server. */
const POINTS_PER_DONE = 5;

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
            {done && <span style={{ fontSize: 12, color: 'var(--accent)', fontFamily: 'var(--font-mono)', fontWeight: 600 }}>+{POINTS_PER_DONE}pt</span>}
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
        {schedule.earnedPoints > 0 && (
          <span style={{ marginLeft: 8, fontSize: 12, fontWeight: 600, color: 'var(--success)', fontFamily: 'var(--font-mono)' }}>
            +{schedule.earnedPoints}pt earned
          </span>
        )}
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
      <h2>Experiences <span style={{ marginLeft: 8 }}><WeekCount w={week} mode="done" /></span>
        {week.earnedPoints > 0 && (
          <span style={{ marginLeft: 8, fontSize: 12, fontWeight: 600, color: 'var(--success)', fontFamily: 'var(--font-mono)' }}>
            +{week.earnedPoints}pt
          </span>
        )}
      </h2>
      <ExperienceList w={week} />
    </div>
  );
}

/** Shift a yyyy-MM-dd date by whole days. */
const addDays = (iso: string, n: number) => {
  const d = new Date(iso + 'T00:00:00'); d.setDate(d.getDate() + n);
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
};
const dayDiff = (a: string, b: string) =>
  Math.round((new Date(b + 'T00:00:00').getTime() - new Date(a + 'T00:00:00').getTime()) / 86400000);

/**
 * Sprint modal, draft sprint: what Start needs, week by week. Each week lists the experiences
 * already dated in it; "+ Add" opens a picker of the planned experiences in the Experiences tab
 * (undated first) — picking one dates it into the week — or a one-line form for a new one.
 */
export function SprintExperiencesChecklist({ schedule, onAdded }: { schedule: SprintExperienceSchedule; onAdded: () => void }) {
  const [addingWeek, setAddingWeek] = useState<string | null>(null);
  const [planned, setPlanned] = useState<ExperienceDto[] | null>(null);
  const [query, setQuery] = useState('');
  const [title, setTitle] = useState('');
  const [date, setDate] = useState('');
  const [category, setCategory] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');

  const open = async (w: SprintExperienceWeek) => {
    setAddingWeek(w.weekStart); setTitle(''); setCategory(''); setQuery(''); setError('');
    const t = todayIso();
    setDate(t >= w.from && t <= w.to ? t : w.from);
    try { setPlanned(await api.getExperiences('planned')); }
    catch (e) { setError((e as Error).message); setPlanned([]); }
  };

  const addNew = async () => {
    if (!title.trim() || !date) return;
    setBusy(true); setError('');
    try {
      await api.createExperience({ title: title.trim(), category: category || null, tags: [], status: 'planned',
        location: null, startDate: date, endDate: null, descriptionMd: null });
      setAddingWeek(null); onAdded();
    } catch (e) { setError((e as Error).message); }
    finally { setBusy(false); }
  };

  // Date an existing experience into the week. PUT replaces, so every field is sent back as it
  // was; an end date moves with the start so a multi-day plan keeps its length.
  const schedule_ = async (x: ExperienceDto) => {
    if (!date) return;
    setBusy(true); setError('');
    try {
      const endDate = x.startDate && x.endDate ? addDays(date, dayDiff(x.startDate, x.endDate)) : x.endDate && !x.startDate ? null : x.endDate;
      await api.updateExperience(x.id, { title: x.title, category: x.category, tags: x.tags, status: x.status,
        location: x.location, startDate: date, endDate, descriptionMd: x.descriptionMd });
      setAddingWeek(null); onAdded();
    } catch (e) { setError((e as Error).message); }
    finally { setBusy(false); }
  };

  const week = schedule.weeks.find(w => w.weekStart === addingWeek);
  const candidates = (planned ?? [])
    .filter(x => !week || !(x.startDate && x.startDate >= week.from && x.startDate <= week.to))
    .filter(x => !query.trim() || `${x.title} ${x.category ?? ''} ${x.tags.join(' ')}`.toLowerCase().includes(query.trim().toLowerCase()))
    .sort((a, b) => (a.startDate ? 1 : 0) - (b.startDate ? 1 : 0) || (a.startDate ?? '').localeCompare(b.startDate ?? '') || a.title.localeCompare(b.title));

  return (
    <div style={{ width: '100%', padding: '8px 12px', background: 'var(--bg-secondary)', borderRadius: 'var(--radius-sm)', fontSize: 13 }}>
      <div style={{ marginBottom: 6, color: schedule.ready ? 'var(--success)' : 'var(--text-secondary, var(--text-muted))' }}>
        {schedule.ready
          ? '✓ Experiences scheduled — ready to start'
          : `Schedule at least ${schedule.perWeek} experiences a week to start this sprint`}
      </div>
      {schedule.weeks.map(w => (
        <div key={w.weekStart} style={{ padding: '6px 0', borderTop: '1px solid var(--border-subtle)' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
            <span style={{ fontWeight: 600 }}>{fmt(w.from)} – {fmt(w.to)}</span>
            <WeekCount w={w} mode="schedule" />
            <span style={{ flex: 1 }} />
            {addingWeek !== w.weekStart &&
              <button className="btn btn-sm" onClick={() => open(w)} disabled={busy}>+ Add</button>}
          </div>
          {w.experiences.length > 0 && (
            <div style={{ display: 'flex', flexDirection: 'column', gap: 2, marginTop: 4, paddingLeft: 4 }}>
              {w.experiences.map(e => (
                <div key={e.id} style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
                  <span style={{ width: 14, color: e.status === 'done' ? 'var(--success)' : 'var(--text-muted)' }}>{e.status === 'done' ? '✓' : '○'}</span>
                  <span style={{ flex: 1, minWidth: 0 }}>{e.title}</span>
                  {e.category && <span style={{ fontSize: 11, color: 'var(--text-muted)' }}>{e.category}</span>}
                  <span style={{ fontSize: 12, color: 'var(--text-muted)', whiteSpace: 'nowrap' }}>{fmtDay(e.date)}</span>
                </div>
              ))}
            </div>
          )}
          {addingWeek === w.weekStart && (
            <div style={{ marginTop: 8, display: 'flex', flexDirection: 'column', gap: 8 }}>
              <div style={{ display: 'flex', gap: 6, alignItems: 'center', flexWrap: 'wrap' }}>
                <span style={{ color: 'var(--text-muted)' }}>On</span>
                <input type="date" value={date} min={w.from} max={w.to} onChange={e => setDate(e.target.value)} />
                <span style={{ flex: 1 }} />
                <button className="btn btn-sm" onClick={() => setAddingWeek(null)} disabled={busy}>Cancel</button>
              </div>

              <div style={{ fontSize: 11, textTransform: 'uppercase', letterSpacing: '0.04em', color: 'var(--text-muted)' }}>
                From your planned experiences
              </div>
              <input type="text" value={query} onChange={e => setQuery(e.target.value)} placeholder="Filter…" />
              <div style={{ maxHeight: 220, overflowY: 'auto', display: 'flex', flexDirection: 'column', gap: 4 }}>
                {planned === null
                  ? <span style={{ color: 'var(--text-muted)' }}>Loading…</span>
                  : candidates.length === 0
                    ? <span style={{ color: 'var(--text-muted)' }}>No planned experiences{query ? ' match' : ' to pick'} — add a new one below.</span>
                    : candidates.map(x => (
                      <button key={x.id} type="button" onClick={() => schedule_(x)} disabled={busy || !date}
                        title={`Schedule on ${date}`}
                        style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '6px 10px', textAlign: 'left',
                          background: 'var(--bg-primary, transparent)', border: '1px solid var(--border-subtle)',
                          borderRadius: 'var(--radius-sm)', color: 'inherit', cursor: 'pointer', font: 'inherit' }}>
                        <span style={{ flex: 1, minWidth: 0 }}>{x.title}</span>
                        {x.category && <span style={{ fontSize: 11, color: 'var(--text-muted)' }}>{x.category}</span>}
                        <span style={{ fontSize: 11, color: 'var(--text-muted)', whiteSpace: 'nowrap' }}>
                          {x.startDate ? `now ${fmt(x.startDate)}` : 'undated'}
                        </span>
                      </button>
                    ))}
              </div>

              <div style={{ fontSize: 11, textTransform: 'uppercase', letterSpacing: '0.04em', color: 'var(--text-muted)' }}>
                Or a new one
              </div>
              <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
                <input type="text" value={title} onChange={e => setTitle(e.target.value)} placeholder="Experience…"
                  style={{ flex: 1, minWidth: 140 }} onKeyDown={e => { if (e.key === 'Enter') addNew(); }} />
                <select value={category} onChange={e => setCategory(e.target.value)}>
                  <option value="">Category…</option>
                  {EXPERIENCE_CATEGORIES.map(c => <option key={c} value={c}>{c}</option>)}
                </select>
                <button className="btn btn-sm btn-accent" onClick={addNew} disabled={busy || !title.trim() || !date}>Add</button>
              </div>
            </div>
          )}
        </div>
      ))}
      {error && <div style={{ color: 'var(--danger)', marginTop: 6 }}>{error}</div>}
    </div>
  );
}
