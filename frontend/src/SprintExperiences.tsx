import { useState, useEffect, useCallback, useMemo } from 'react';
import { createPortal } from 'react-dom';
import { ExperienceForm, ExperiencesPage, buildTagsByCategory } from './ExperiencesPage';
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
/** "Nov 24 – 30" within a month, "Nov 30 – Dec 6" across one, "Dec 7" for a single day. */
const fmtRange = (from: string, to: string) => {
  if (from === to) return fmt(from);
  const [a, b] = [new Date(from + 'T00:00:00'), new Date(to + 'T00:00:00')];
  return a.getMonth() === b.getMonth() ? `${fmt(from)} – ${b.getDate()}` : `${fmt(from)} – ${fmt(to)}`;
};
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
 * dated in it (click one to edit it in the Experiences tab's own form). "+ Add" opens the whole
 * Experiences tab over the sprint modal in schedule mode for that week — the same cards, filters,
 * detail view and form, plus a day picker and "Schedule" (calendar icon) on every planned card.
 */
export function SprintExperiencesChecklist({ schedule, onAdded, hideHeader }: { schedule: SprintExperienceSchedule; onAdded: () => void; hideHeader?: boolean }) {
  const [all, setAll] = useState<ExperienceDto[] | null>(null);
  const [tabWeek, setTabWeek] = useState<SprintExperienceWeek | null>(null);
  const [editing, setEditing] = useState<ExperienceDto | null>(null);
  const [error, setError] = useState('');

  const loadAll = useCallback(async () => {
    try { setAll(await api.getExperiences()); }
    catch (e) { setError((e as Error).message); setAll([]); }
  }, []);
  useEffect(() => { loadAll(); }, [loadAll]);

  const tagsByCategory = useMemo(() => buildTagsByCategory(all ?? []), [all]);
  const changed = useCallback(() => { loadAll(); onAdded(); }, [loadAll, onAdded]);

  return (
    <div style={{ width: '100%', padding: '8px 12px', background: 'var(--bg-secondary)', borderRadius: 'var(--radius-sm)', fontSize: 13 }}>
      {!hideHeader && (
        <div style={{ marginBottom: 6, color: schedule.ready ? 'var(--success)' : 'var(--text-secondary, var(--text-muted))' }}>
          {schedule.ready
            ? '✓ Experiences scheduled — ready to start'
            : `Schedule at least ${schedule.perWeek} experiences a week to start this sprint`}
        </div>
      )}
      {schedule.weeks.map((w, i) => (
        <div key={w.weekStart} style={{ padding: '6px 0', borderTop: hideHeader && i === 0 ? 'none' : '1px solid var(--border-subtle)' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <span style={{ fontWeight: 600, whiteSpace: 'nowrap' }}>{fmtRange(w.from, w.to)}</span>
            <WeekCount w={w} mode="schedule" />
            <span style={{ flex: 1 }} />
            <button className="btn btn-sm" style={{ flexShrink: 0 }} onClick={() => setTabWeek(w)}>+ Add</button>
          </div>
          {w.experiences.length > 0 && (
            <div style={{ display: 'flex', flexDirection: 'column', gap: 2, marginTop: 4 }}>
              {w.experiences.map(e => (
                <button key={e.id} type="button" title="Open in the experience form"
                  onClick={() => { const x = (all ?? []).find(a => a.id === e.id); if (x) setEditing(x); }}
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
        </div>
      ))}
      {error && <div style={{ color: 'var(--danger)', marginTop: 6 }}>{error}</div>}

      {/* Portals so the sprint modal's scrolling box never clips them; React events still bubble
          to the sprint modal's card, which stops them, so a click inside never closes it. */}
      {tabWeek && createPortal(
        <div className="modal-overlay" onClick={() => setTabWeek(null)}>
          <div className="modal xp-tab-modal" onClick={e => e.stopPropagation()} role="dialog" aria-label="Experiences">
            <ExperiencesPage schedule={{ from: tabWeek.from, to: tabWeek.to, onChanged: changed, onClose: () => setTabWeek(null) }} />
          </div>
        </div>,
        document.body)}
      {editing && createPortal(
        <ExperienceForm x={editing} tagsByCategory={tagsByCategory}
          onCancel={() => setEditing(null)}
          onSaved={() => { setEditing(null); changed(); }} />,
        document.body)}
    </div>
  );
}
