import { useState, useEffect, useCallback, useMemo } from 'react';
import { createPortal } from 'react-dom';
import { ExperienceForm, buildTagsByCategory } from './ExperiencesPage';
import type { ExperienceDto, SprintExperienceSchedule, SprintExperienceWeek } from './types';
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

/**
 * Sprint modal, draft sprint: what Start needs, week by week. Each week lists the experiences
 * dated in it (click one to edit it in the Experiences tab's own form). "+ Add" lets you pick one of
 * your planned experiences — it opens in that same form with the date set into the week — or start
 * a new one there, so scheduling has the tab's full functionality: category, tags, location,
 * description, photos.
 */
export function SprintExperiencesChecklist({ schedule, onAdded }: { schedule: SprintExperienceSchedule; onAdded: () => void }) {
  const [all, setAll] = useState<ExperienceDto[] | null>(null);
  const [addingWeek, setAddingWeek] = useState<string | null>(null);
  const [query, setQuery] = useState('');
  const [date, setDate] = useState('');
  const [form, setForm] = useState<{ x: ExperienceDto | null; initialStart?: string } | null>(null);
  const [error, setError] = useState('');

  const loadAll = useCallback(async () => {
    try { setAll(await api.getExperiences()); }
    catch (e) { setError((e as Error).message); setAll([]); }
  }, []);
  useEffect(() => { loadAll(); }, [loadAll]);

  const tagsByCategory = useMemo(() => buildTagsByCategory(all ?? []), [all]);

  const open = (w: SprintExperienceWeek) => {
    setAddingWeek(w.weekStart); setQuery(''); setError('');
    const t = todayIso();
    setDate(t >= w.from && t <= w.to ? t : w.from);
  };

  const week = schedule.weeks.find(w => w.weekStart === addingWeek);
  const candidates = (all ?? [])
    .filter(x => x.status === 'planned')
    .filter(x => !week || !(x.startDate && x.startDate >= week.from && x.startDate <= week.to))
    .filter(x => !query.trim() || `${x.title} ${x.category ?? ''} ${x.tags.join(' ')}`.toLowerCase().includes(query.trim().toLowerCase()))
    .sort((a, b) => (a.startDate ? 1 : 0) - (b.startDate ? 1 : 0) || (a.startDate ?? '').localeCompare(b.startDate ?? '') || a.title.localeCompare(b.title));

  const editExisting = (id: string) => {
    const x = (all ?? []).find(e => e.id === id);
    if (x) setForm({ x });
  };

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
              <button className="btn btn-sm" onClick={() => open(w)}>+ Add</button>}
          </div>
          {w.experiences.length > 0 && (
            <div style={{ display: 'flex', flexDirection: 'column', gap: 2, marginTop: 4 }}>
              {w.experiences.map(e => (
                <button key={e.id} type="button" onClick={() => editExisting(e.id)} title="Open in the experience form"
                  style={{ display: 'flex', gap: 8, alignItems: 'center', padding: '3px 4px', background: 'none', border: 'none',
                    color: 'inherit', font: 'inherit', textAlign: 'left', cursor: 'pointer', borderRadius: 4 }}>
                  <span style={{ width: 14, color: e.status === 'done' ? 'var(--success)' : 'var(--text-muted)' }}>{e.status === 'done' ? '✓' : '○'}</span>
                  <span style={{ flex: 1, minWidth: 0 }}>{e.title}</span>
                  {e.category && <span style={{ fontSize: 11, color: 'var(--text-muted)' }}>{e.category}</span>}
                  <span style={{ fontSize: 12, color: 'var(--text-muted)', whiteSpace: 'nowrap' }}>{fmtDay(e.date)}</span>
                </button>
              ))}
            </div>
          )}
          {addingWeek === w.weekStart && (
            <div style={{ marginTop: 8, display: 'flex', flexDirection: 'column', gap: 8 }}>
              <div style={{ display: 'flex', gap: 6, alignItems: 'center', flexWrap: 'wrap' }}>
                <span style={{ color: 'var(--text-muted)' }}>On</span>
                <input type="date" value={date} min={w.from} max={w.to} onChange={e => setDate(e.target.value)} style={{ width: 'auto' }} />
                <span style={{ flex: 1 }} />
                <button className="btn btn-sm btn-accent" onClick={() => setForm({ x: null, initialStart: date })} disabled={!date}>＋ New experience</button>
                <button className="btn btn-sm" onClick={() => setAddingWeek(null)}>Cancel</button>
              </div>
              <div style={{ fontSize: 11, textTransform: 'uppercase', letterSpacing: '0.04em', color: 'var(--text-muted)' }}>
                Or pick one of your planned experiences
              </div>
              <input type="text" value={query} onChange={e => setQuery(e.target.value)} placeholder="Filter…" />
              <div style={{ maxHeight: 220, overflowY: 'auto', display: 'flex', flexDirection: 'column', gap: 4 }}>
                {all === null
                  ? <span style={{ color: 'var(--text-muted)' }}>Loading…</span>
                  : candidates.length === 0
                    ? <span style={{ color: 'var(--text-muted)' }}>No planned experiences{query ? ' match' : ' to pick'}.</span>
                    : candidates.map(x => (
                      <button key={x.id} type="button" onClick={() => setForm({ x, initialStart: date })} disabled={!date}
                        title={`Open it with the date set to ${date}`}
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
            </div>
          )}
        </div>
      ))}
      {error && <div style={{ color: 'var(--danger)', marginTop: 6 }}>{error}</div>}

      {/* The Experiences tab's own form, over the sprint modal. A portal so the sprint modal's
          scrolling box never clips it; React events still bubble to the sprint modal, whose card
          stops them, so a click here never closes it. */}
      {form && createPortal(
        <ExperienceForm x={form.x} initialStart={form.initialStart} tagsByCategory={tagsByCategory}
          onCancel={() => setForm(null)}
          onSaved={() => { setForm(null); setAddingWeek(null); loadAll(); onAdded(); }} />,
        document.body)}
    </div>
  );
}
