import { useState, useEffect, useCallback, useRef } from 'react';
import type { SprintDto, SprintExperienceSchedule } from './types';
import { api } from './api';
import { SprintExperiencesChecklist } from './SprintExperiences';
import { ScalesIcon } from './NavIcons';

// The Sprints window (redesign 2026-11-16). One job per area, in the order you need them:
//   Active   — the running sprint as a card: dates, a day-progress bar, how its experiences are going.
//   Drafts   — each draft as a card with a "Ready to start?" checklist and one big ▶ Start that
//              says why it is locked instead of hiding it in a tooltip.
//   History  — ended sprints folded away, one quiet line each.
// "+ New sprint" opens the form only when asked. Rare and destructive actions (edit, close,
// delete) sit in a ⋯ menu, and close/delete confirm in a bar inside the card, naming the sprint.

interface Props { roadmapId: string; onClose: () => void; }

type Kind = 'active' | 'draft' | 'ended';
const kindOf = (s: SprintDto): Kind => (!s.isOpen ? 'ended' : s.isStarted ? 'active' : 'draft');

const todayIso = () => {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
};
const dayNum = (iso: string) => Math.round(new Date(iso + 'T00:00:00').getTime() / 86400000);
const fmtDate = (iso: string, year = false) =>
  new Date(iso + 'T00:00:00').toLocaleDateString(undefined, { month: 'short', day: 'numeric', ...(year ? { year: 'numeric' } : {}) });
const plural = (n: number, w: string) => `${n} ${w}${n === 1 ? '' : 's'}`;

function Badge({ kind }: { kind: Kind }) {
  const label = kind === 'active' ? 'Active' : kind === 'draft' ? 'Draft' : 'Ended';
  return <span className={`sp-badge sp-badge-${kind}`}>{label}</span>;
}

/** A small ⋯ menu: closes on outside click or Escape. */
function Menu({ items }: { items: { label: string; danger?: boolean; onClick: () => void }[] }) {
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);
  useEffect(() => {
    if (!open) return;
    const down = (e: MouseEvent) => { if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false); };
    const key = (e: KeyboardEvent) => { if (e.key === 'Escape') { e.stopPropagation(); setOpen(false); } };
    document.addEventListener('mousedown', down);
    document.addEventListener('keydown', key, true);
    return () => { document.removeEventListener('mousedown', down); document.removeEventListener('keydown', key, true); };
  }, [open]);
  return (
    <div className="sp-menu" ref={ref}>
      <button className="btn btn-sm btn-ghost sp-menu-btn" aria-haspopup="menu" aria-expanded={open}
        aria-label="More actions" onClick={() => setOpen(o => !o)}>⋯</button>
      {open && (
        <div className="sp-menu-list" role="menu">
          {items.map(i => (
            <button key={i.label} role="menuitem" className={i.danger ? 'danger' : ''}
              onClick={() => { setOpen(false); i.onClick(); }}>{i.label}</button>
          ))}
        </div>
      )}
    </div>
  );
}

/** Name + dates form, used for a new sprint and for editing one. */
function SprintFields({ name, start, end, setName, setStart, setEnd, autoFocus }: {
  name: string; start: string; end: string;
  setName: (v: string) => void; setStart: (v: string) => void; setEnd: (v: string) => void; autoFocus?: boolean;
}) {
  const days = start && end && end >= start ? dayNum(end) - dayNum(start) + 1 : null;
  return (
    <>
      <div>
        <label htmlFor="sp-name">Name</label>
        <input id="sp-name" type="text" value={name} onChange={e => setName(e.target.value)} placeholder="Sprint 12" autoFocus={autoFocus} />
      </div>
      <div className="form-row">
        <div><label>Starts</label><input type="date" value={start} onChange={e => setStart(e.target.value)} /></div>
        <div><label>Ends</label><input type="date" value={end} min={start || undefined} onChange={e => setEnd(e.target.value)} /></div>
      </div>
      {days !== null && <div className="sp-hint">{plural(days, 'day')} · {plural(Math.ceil(days / 7), 'week')}</div>}
      {start && end && end < start && <div className="sp-hint" style={{ color: 'var(--danger)' }}>The end is before the start.</div>}
    </>
  );
}

function SprintCard({ s, exp, busy, roadmapId, onChanged, onError, setBusy }: {
  s: SprintDto; exp?: SprintExperienceSchedule; busy: boolean; roadmapId: string;
  onChanged: () => Promise<void>; onError: (m: string) => void; setBusy: (b: boolean) => void;
}) {
  const kind = kindOf(s);
  const [editing, setEditing] = useState(false);
  const [name, setName] = useState(s.name);
  const [start, setStart] = useState(s.startDate);
  const [end, setEnd] = useState(s.endDate);
  const [confirm, setConfirm] = useState<'close' | 'delete' | null>(null);

  const run = async (f: () => Promise<unknown>) => {
    onError(''); setBusy(true);
    try { await f(); await onChanged(); }
    catch (e) { onError((e as Error).message.replace(/^API \d+: /, '')); }
    finally { setBusy(false); }
  };

  const today = todayIso();
  const total = dayNum(s.endDate) - dayNum(s.startDate) + 1;
  const elapsed = Math.min(total, Math.max(0, dayNum(today) - dayNum(s.startDate) + 1));
  const startsIn = dayNum(s.startDate) - dayNum(today);

  const sub =
    kind === 'active' ? `Day ${elapsed} of ${total} · ${plural(total - elapsed, 'day')} left`
    : kind === 'draft' ? (startsIn > 0 ? `${plural(total, 'day')} · starts in ${plural(startsIn, 'day')}`
      : startsIn === 0 ? `${plural(total, 'day')} · starts today` : `${plural(total, 'day')} · start date has passed`)
    : `${plural(total, 'day')}`;

  const menu = [
    { label: 'Edit name & dates', onClick: () => { setEditing(true); setConfirm(null); setName(s.name); setStart(s.startDate); setEnd(s.endDate); } },
    ...(kind !== 'ended' ? [{ label: 'Close sprint', onClick: () => setConfirm('close') }] : []),
    { label: 'Delete', danger: true, onClick: () => setConfirm('delete') },
  ];

  if (kind === 'ended' && !editing) {
    return (
      <div className="sp-row">
        <span className="sp-row-name">{s.name}{s.scoringMode === 'Weighted' && <span className="sp-weighted" title="Weighted scoring"><ScalesIcon title="Weighted scoring" /></span>}</span>
        <span className="sp-row-dates">{fmtDate(s.startDate)} – {fmtDate(s.endDate, true)}</span>
        {confirm === 'delete'
          ? <span className="sp-row-confirm">
              <button className="btn btn-sm btn-danger" disabled={busy} onClick={() => run(() => api.deleteSprint(roadmapId, s.id))}>Delete</button>
              <button className="btn btn-sm" onClick={() => setConfirm(null)}>Cancel</button>
            </span>
          : <Menu items={menu} />}
      </div>
    );
  }

  const ready = !!exp?.ready;
  const readyWeeks = exp ? exp.weeks.filter(w => w.met).length : 0;

  return (
    <article className={`sp-card sp-card-${kind}`} aria-label={s.name}>
      {editing ? (
        <div className="sp-edit">
          <SprintFields name={name} start={start} end={end} setName={setName} setStart={setStart} setEnd={setEnd} autoFocus />
          <div className="sp-actions">
            <button className="btn btn-sm" onClick={() => setEditing(false)} disabled={busy}>Cancel</button>
            <button className="btn btn-sm btn-accent" disabled={busy || !name.trim() || !start || !end || end < start}
              onClick={() => run(async () => { await api.updateSprint(roadmapId, s.id, name.trim(), start, end); setEditing(false); })}>Save</button>
          </div>
        </div>
      ) : (
        <header className="sp-card-head">
          <div style={{ minWidth: 0, flex: 1 }}>
            <div className="sp-card-title">
              <Badge kind={kind} />
              <h3>{s.name}</h3>
              {s.scoringMode === 'Weighted' && (
                <span className="sp-chip" title="Weighted scoring: each day, overdone items give points to underdone ones; the day's total is unchanged"><ScalesIcon /> Weighted</span>
              )}
            </div>
            <div className="sp-card-dates"><span>{fmtDate(s.startDate)} – {fmtDate(s.endDate, true)}</span><span className="sp-sub">{sub}</span></div>
          </div>
          <Menu items={menu} />
        </header>
      )}

      {kind === 'active' && !editing && (
        <>
          <div className="sp-progress" role="progressbar" aria-label="Days elapsed" aria-valuemin={0} aria-valuemax={total} aria-valuenow={elapsed}>
            <div style={{ width: `${(elapsed / total) * 100}%` }} />
          </div>
          {exp && (
            <div className="sp-stat-line">
              <span>Experiences</span>
              <strong>{exp.done} of {exp.required} done</strong>
              <span className="sp-muted">· {exp.scheduled} scheduled{exp.earnedPoints > 0 ? ` · +${exp.earnedPoints} pts` : ''}</span>
            </div>
          )}
        </>
      )}

      {kind === 'draft' && !editing && (
        <section className="sp-ready" aria-label="Ready to start?">
          <h4>Ready to start?</h4>
          <div className={`sp-check ${ready ? 'ok' : ''}`}>
            <span className="sp-check-mark" aria-hidden="true">{ready ? '✓' : '○'}</span>
            <span>Experiences scheduled — at least {exp?.perWeek ?? 2} a week</span>
            <span className="sp-muted sp-check-count">{exp ? `${readyWeeks} of ${exp.weeks.length} weeks` : '…'}</span>
          </div>
          {exp && <SprintExperiencesChecklist schedule={exp} onAdded={onChanged} hideHeader />}
          <button className="btn btn-accent sp-start" disabled={busy || !ready}
            onClick={() => run(() => api.startSprint(roadmapId, s.id))}>
            ▶ Start sprint
          </button>
          {!ready && exp && <div className="sp-hint" style={{ textAlign: 'center' }}>Schedule the missing experiences above to unlock Start.</div>}
        </section>
      )}

      {confirm && (
        <div className={`sp-confirm ${confirm === 'delete' ? 'danger' : ''}`} role="alertdialog" aria-label={`Confirm ${confirm}`}>
          <span>
            {confirm === 'close'
              ? <>Close <strong>{s.name}</strong>? It ends today and moves to History.</>
              : <>Delete <strong>{s.name}</strong>{s.isStarted ? ' and its plan' : ''}? This can't be undone.</>}
          </span>
          <span className="sp-actions">
            <button className="btn btn-sm" onClick={() => setConfirm(null)} disabled={busy}>Cancel</button>
            <button className={`btn btn-sm ${confirm === 'delete' ? 'btn-danger' : 'btn-warning'}`} disabled={busy}
              onClick={() => run(async () => {
                if (confirm === 'close') await api.closeSprint(roadmapId, s.id); else await api.deleteSprint(roadmapId, s.id);
                setConfirm(null);
              })}>
              {confirm === 'close' ? 'Close sprint' : 'Delete'}
            </button>
          </span>
        </div>
      )}
    </article>
  );
}

export function SprintModal({ roadmapId, onClose }: Props) {
  const [sprints, setSprints] = useState<SprintDto[]>([]);
  const [exp, setExp] = useState<Record<string, SprintExperienceSchedule>>({});
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [creating, setCreating] = useState(false);
  const [showHistory, setShowHistory] = useState(false);
  const [name, setName] = useState('');
  const [startDate, setStartDate] = useState('');
  const [endDate, setEndDate] = useState('');
  const [scoringMode, setScoringMode] = useState<'Fixed' | 'Weighted'>('Fixed');

  const load = useCallback(async () => {
    const list = await api.getSprints(roadmapId);
    setSprints(list);
    // Experiences schedule for every sprint still open: drafts need it to start, the active
    // sprint shows how it is going.
    const open = list.filter(s => s.isOpen);
    const entries = await Promise.all(open.map(async s => [s.id, await api.getSprintExperiences(roadmapId, s.id)] as const));
    setExp(Object.fromEntries(entries));
  }, [roadmapId]);
  useEffect(() => { setLoading(true); load().catch(e => setError(String(e))).finally(() => setLoading(false)); }, [load]);

  // Escape closes the window (a menu open inside it handles its own Escape first).
  useEffect(() => {
    const key = (e: KeyboardEvent) => { if (e.key === 'Escape' && !document.querySelector('.xp-modal, .xp-tab-modal')) onClose(); };
    document.addEventListener('keydown', key);
    return () => document.removeEventListener('keydown', key);
  }, [onClose]);

  const active = sprints.filter(s => kindOf(s) === 'active');
  const drafts = sprints.filter(s => kindOf(s) === 'draft').sort((a, b) => a.startDate.localeCompare(b.startDate));
  const ended = sprints.filter(s => kindOf(s) === 'ended').sort((a, b) => b.endDate.localeCompare(a.endDate));

  // Default the new sprint to start the day after the last one ends, two weeks long.
  const openCreate = () => {
    const last = [...sprints].sort((a, b) => b.endDate.localeCompare(a.endDate))[0];
    const from = last && last.endDate >= todayIso()
      ? new Date(new Date(last.endDate + 'T00:00:00').getTime() + 86400000) : new Date();
    const iso = (d: Date) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
    const to = new Date(from.getTime() + 13 * 86400000);
    setName(''); setStartDate(iso(from)); setEndDate(iso(to)); setScoringMode('Fixed'); setError(''); setCreating(true);
  };

  const handleCreate = async () => {
    if (!name.trim() || !startDate || !endDate || endDate < startDate) return;
    setError(''); setBusy(true);
    try {
      await api.createSprint(roadmapId, name.trim(), startDate, endDate, scoringMode);
      setCreating(false); await load();
    } catch (e) { setError((e as Error).message.replace(/^API \d+: /, '')); }
    finally { setBusy(false); }
  };

  const cardProps = { busy, roadmapId, onChanged: load, onError: setError, setBusy };

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal sprint-modal sp-modal" onClick={e => e.stopPropagation()} role="dialog" aria-labelledby="sp-title">
        <div className="sp-top">
          <h2 id="sp-title">Sprints</h2>
          {!creating && <button className="btn btn-sm btn-accent" onClick={openCreate} disabled={busy}>+ New sprint</button>}
          <button className="btn btn-sm btn-ghost" onClick={onClose} aria-label="Close">✕</button>
        </div>

        {error && <div className="sp-error" role="alert">{error}</div>}

        {creating && (
          <section className="sp-card sp-new" aria-label="New sprint">
            <h3 style={{ margin: 0 }}>New sprint</h3>
            <SprintFields name={name} start={startDate} end={endDate} setName={setName} setStart={setStartDate} setEnd={setEndDate} autoFocus />
            <div>
              <label>Scoring</label>
              <div className="sp-seg" role="radiogroup" aria-label="Scoring">
                {([['Fixed', 'Fixed', 'A unit is always worth its points'], ['Weighted', <><ScalesIcon /> Weighted</>, 'Overdone items give points to underdone ones']] as const).map(([v, l, d]) => (
                  <button key={v} type="button" role="radio" aria-checked={scoringMode === v}
                    className={scoringMode === v ? 'on' : ''} onClick={() => setScoringMode(v)}>
                    <strong>{l}</strong><span>{d}</span>
                  </button>
                ))}
              </div>
            </div>
            <div className="sp-hint">Dates can't overlap another sprint. You'll schedule experiences before starting it.</div>
            <div className="sp-actions">
              <button className="btn btn-sm" onClick={() => setCreating(false)} disabled={busy}>Cancel</button>
              <button className="btn btn-sm btn-accent" onClick={handleCreate}
                disabled={busy || !name.trim() || !startDate || !endDate || endDate < startDate}>{busy ? 'Creating…' : 'Create sprint'}</button>
            </div>
          </section>
        )}

        {loading ? <p className="sp-muted">Loading…</p> : (
          <>
            {sprints.length === 0 && !creating && (
              <div className="sp-empty">
                <strong>No sprints yet</strong>
                <span>A sprint is a stretch of days your plan is scored against. Create one, schedule its experiences, then start it.</span>
                <button className="btn btn-accent" onClick={openCreate}>+ New sprint</button>
              </div>
            )}

            {active.length > 0 && (
              <section className="sp-section">
                <h4 className="sp-section-h">Active</h4>
                {active.map(s => <SprintCard key={s.id} s={s} exp={exp[s.id]} {...cardProps} />)}
              </section>
            )}

            {drafts.length > 0 && (
              <section className="sp-section">
                <h4 className="sp-section-h">{drafts.length === 1 ? 'Draft' : `Drafts · ${drafts.length}`}</h4>
                {drafts.map(s => <SprintCard key={s.id} s={s} exp={exp[s.id]} {...cardProps} />)}
              </section>
            )}

            {ended.length > 0 && (
              <section className="sp-section">
                <button className="sp-history-toggle" aria-expanded={showHistory} onClick={() => setShowHistory(h => !h)}>
                  <span aria-hidden="true">{showHistory ? '▾' : '▸'}</span> History · {ended.length}
                </button>
                {showHistory && <div className="sp-history">{ended.map(s => <SprintCard key={s.id} s={s} {...cardProps} />)}</div>}
              </section>
            )}
          </>
        )}
      </div>
    </div>
  );
}
