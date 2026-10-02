import { useState, useEffect, useCallback, type CSSProperties } from 'react';
import { marked } from 'marked';
import type { NoteDto, NotePromptDto, NotePromptOverviewDto, NoteSrsStatsDto, NotePromptState } from './types';
import { api } from './api';
import { NotesReview } from './NotesReview';

type Book = 'red' | 'green';

marked.setOptions({ gfm: true, breaks: true });

// "2018-08-20" -> "20.08.2018"
function fmtDate(iso: string): string {
  const [y, m, d] = iso.slice(0, 10).split('-');
  return d && m && y ? `${d}.${m}.${y}` : iso;
}

/** "due in 3d" / "due today" / "overdue by 2d" — the interval matters more than the date. */
function dueLabel(dueOn: string): string {
  const today = new Date();
  today.setHours(0, 0, 0, 0);
  const due = new Date(dueOn + 'T00:00:00');
  const days = Math.round((due.getTime() - today.getTime()) / 86_400_000);
  if (days === 0) return 'due today';
  if (days < 0) return `overdue by ${-days}d`;
  if (days === 1) return 'due tomorrow';
  return `due in ${days}d`;
}

/** Stability as a human interval: "4d", "3w", "5mo", "2y". */
function fmtDays(days: number): string {
  if (days < 14) return `${Math.round(days)}d`;
  if (days < 60) return `${Math.round(days / 7)}w`;
  if (days < 540) return `${Math.round(days / 30)}mo`;
  return `${(days / 365).toFixed(1)}y`;
}

const STATE_COLOR: Record<NotePromptState, string> = {
  Active: '#30a46c',
  Parked: '#f5a623',
  Suspended: '#8b8b8b',
};

const GRADE_COLOR: Record<string, string> = {
  Again: '#e5484d',
  Hard: '#f5a623',
  Good: '#30a46c',
  Easy: '#5b8def',
};

const LEECH_LAPSES = 8;

function Pill({ text, color, title }: { text: string; color: string; title?: string }) {
  return (
    <span title={title} style={{
      fontSize: 11, fontWeight: 600, padding: '2px 8px', borderRadius: 999,
      background: `${color}1a`, color, border: `1px solid ${color}55`, whiteSpace: 'nowrap',
    }}>{text}</span>
  );
}

function Stat({ label, value, color, title }: { label: string; value: number | string; color?: string; title?: string }) {
  return (
    <div title={title} style={{
      padding: '8px 12px', borderRadius: 'var(--radius-md)', background: 'var(--bg-secondary)',
      border: '1px solid var(--border-subtle)', minWidth: 84,
    }}>
      <div style={{ fontSize: 18, fontWeight: 700, color: color ?? 'var(--text-primary)' }}>{value}</div>
      <div style={{ fontSize: 11, color: 'var(--text-muted)', marginTop: 2 }}>{label}</div>
    </div>
  );
}

/** The next seven days' due counts as a tiny bar row — whether tomorrow is heavy or light. */
function LoadBars({ load, cap }: { load: { date: string; due: number }[]; cap: number }) {
  const max = Math.max(cap, ...load.map(l => l.due));
  return (
    <div title="Due per day, next 7 days (today first). The line is the daily cap." style={{
      display: 'flex', alignItems: 'flex-end', gap: 3, height: 40, padding: '0 12px',
      borderRadius: 'var(--radius-md)', background: 'var(--bg-secondary)', border: '1px solid var(--border-subtle)',
      position: 'relative',
    }}>
      <div style={{ position: 'absolute', left: 0, right: 0, bottom: `${(cap / max) * 100}%`, borderTop: '1px dashed var(--text-muted)', opacity: 0.5 }} />
      {load.map(l => (
        <div key={l.date} title={`${fmtDate(l.date)}: ${l.due} due`} style={{
          width: 9, height: `${Math.max(6, (l.due / max) * 100)}%`, borderRadius: 2,
          background: l.due > cap ? '#e5484d' : 'var(--accent, #5b8def)', opacity: l.due === 0 ? 0.25 : 0.9,
        }} />
      ))}
    </div>
  );
}

/** Oldest → newest grade squares: the shape of the learning curve at a glance. */
function GradeTrail({ grades }: { grades: string[] }) {
  if (!grades.length) return <span style={{ fontSize: 12, color: 'var(--text-muted)' }}>never asked yet</span>;
  return (
    <div style={{ display: 'flex', gap: 3, flexWrap: 'wrap' }}>
      {grades.map((g, i) => (
        <span key={i} title={g} style={{
          width: 14, height: 14, borderRadius: 3, fontSize: 9, fontWeight: 700, color: '#fff',
          display: 'flex', alignItems: 'center', justifyContent: 'center', background: GRADE_COLOR[g] ?? '#8b8b8b',
        }}>{g[0]}</span>
      ))}
    </div>
  );
}

function TextArea(props: { value: string; onChange: (v: string) => void; placeholder: string; rows?: number }) {
  return (
    <textarea value={props.value} onChange={e => props.onChange(e.target.value)} placeholder={props.placeholder}
      rows={props.rows ?? 2}
      style={{
        width: '100%', boxSizing: 'border-box', resize: 'vertical', padding: '8px 10px', fontSize: 13,
        fontFamily: 'var(--font-body)', borderRadius: 'var(--radius-sm)', border: '1px solid var(--border)',
        background: 'var(--bg-primary)', color: 'var(--text-primary)',
      }} />
  );
}

const smallBtn: CSSProperties = {
  padding: '5px 10px', fontSize: 12, fontWeight: 600, cursor: 'pointer', borderRadius: 'var(--radius-sm)',
  border: '1px solid var(--border)', background: 'var(--bg-primary)', color: 'var(--text-secondary)',
};

/** One prompt: question, hidden answer, schedule pills, and the edit / suspend / delete actions. */
function PromptCard({ p, onChanged }: { p: NotePromptDto; onChanged: () => void }) {
  const [revealed, setRevealed] = useState(false);
  const [editing, setEditing] = useState(false);
  const [q, setQ] = useState(p.question);
  const [a, setA] = useState(p.answer);
  const [resetToo, setResetToo] = useState(false);
  const [busy, setBusy] = useState(false);
  const [history, setHistory] = useState(false);

  const isLeech = p.state === 'Suspended' && p.lapses >= LEECH_LAPSES;

  const save = async () => {
    setBusy(true);
    try {
      await api.updateNotePrompt(p.id, { question: q, answer: a, reset: resetToo || undefined, state: resetToo ? 'Active' : undefined });
      setEditing(false); setResetToo(false); onChanged();
    } finally { setBusy(false); }
  };
  const setState = async (state: NotePromptState) => {
    setBusy(true);
    try { await api.updateNotePrompt(p.id, { state }); onChanged(); } finally { setBusy(false); }
  };
  const remove = async () => {
    if (!confirm('Delete this prompt and its review history?')) return;
    setBusy(true);
    try { await api.deleteNotePrompt(p.id); onChanged(); } finally { setBusy(false); }
  };

  return (
    <div style={{
      border: '1px solid var(--border-subtle)', borderRadius: 'var(--radius-md)', background: 'var(--bg-secondary)',
      padding: '12px 14px', marginBottom: 8, opacity: p.state === 'Suspended' ? 0.75 : 1,
    }}>
      {editing ? (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
          <TextArea value={q} onChange={setQ} placeholder="Question — specific, no hint of the answer" />
          <TextArea value={a} onChange={setA} placeholder="Answer — what a correct reply must contain" />
          <label style={{ fontSize: 12, color: 'var(--text-secondary)', display: 'flex', gap: 6, alignItems: 'center' }}>
            <input type="checkbox" checked={resetToo} onChange={e => setResetToo(e.target.checked)} />
            Restart the schedule (a rewritten prompt starts fresh: due in 4 days, lapses cleared)
          </label>
          <div style={{ display: 'flex', gap: 8 }}>
            <button className="btn btn-accent btn-sm" disabled={busy || !q.trim() || !a.trim()} onClick={save}>Save</button>
            <button className="btn btn-sm" disabled={busy} onClick={() => { setEditing(false); setQ(p.question); setA(p.answer); }}>Cancel</button>
          </div>
        </div>
      ) : (
        <>
          <div style={{ fontSize: 14, fontWeight: 600, color: 'var(--text-primary)', lineHeight: 1.45 }}>{p.question}</div>
          <div onClick={() => setRevealed(r => !r)} style={{
            marginTop: 6, fontSize: 13, lineHeight: 1.5, cursor: 'pointer',
            color: revealed ? 'var(--text-secondary)' : 'var(--text-muted)',
            background: revealed ? 'transparent' : 'var(--bg-primary)', borderRadius: 'var(--radius-sm)',
            padding: revealed ? 0 : '6px 10px', border: revealed ? 'none' : '1px dashed var(--border)',
          }}>
            {revealed ? p.answer : 'show answer'}
          </div>
          <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap', alignItems: 'center', marginTop: 10 }}>
            <Pill text={isLeech ? 'leech' : p.state.toLowerCase()} color={isLeech ? '#e5484d' : STATE_COLOR[p.state]}
              title={isLeech ? `Failed ${p.lapses} times — rewrite the prompt and restart its schedule` : undefined} />
            {p.relearning && <Pill text="relearning" color="#e5484d" title="Failed last time; asked first next session" />}
            {p.state === 'Active' && <Pill text={dueLabel(p.dueOn)} color={p.isDue ? '#e5484d' : '#8b8b8b'} />}
            <Pill text={`stability ${fmtDays(p.stability)}`} color="#5b8def" title="Days until predicted recall falls to 90%" />
            <Pill text={`recall ${Math.round(p.retrievability * 100)}%`} color={p.retrievability >= 0.9 ? '#30a46c' : p.retrievability >= 0.7 ? '#f5a623' : '#e5484d'} title="Predicted recall today" />
            <span style={{ fontSize: 11, color: 'var(--text-muted)' }}>
              {p.reviews} review{p.reviews === 1 ? '' : 's'} · {p.lapses} lapse{p.lapses === 1 ? '' : 's'} · difficulty {p.difficulty.toFixed(1)}
            </span>
            <span style={{ flex: 1 }} />
            <button style={smallBtn} disabled={busy} onClick={() => setHistory(h => !h)}>{history ? 'hide history' : 'history'}</button>
            <button style={smallBtn} disabled={busy} onClick={() => setEditing(true)}>edit</button>
            {p.state === 'Suspended'
              ? <button style={smallBtn} disabled={busy} onClick={() => setState('Active')}>activate</button>
              : <button style={smallBtn} disabled={busy} onClick={() => setState('Suspended')}>suspend</button>}
            <button style={{ ...smallBtn, color: '#e5484d', borderColor: '#e5484d55' }} disabled={busy} onClick={remove}>delete</button>
          </div>
          {history && (
            <div style={{ marginTop: 10, paddingTop: 10, borderTop: '1px solid var(--border-subtle)' }}>
              <GradeTrail grades={[...p.reviewHistory].reverse().map(r => r.grade)} />
              {p.reviewHistory.length > 0 && (
                <div style={{ marginTop: 8, fontSize: 12, color: 'var(--text-muted)', maxHeight: 160, overflowY: 'auto' }}>
                  {p.reviewHistory.map((r, i) => (
                    <div key={i} style={{ marginBottom: 5, paddingBottom: 5, borderBottom: '1px solid var(--border-subtle)' }}>
                      <strong style={{ color: GRADE_COLOR[r.grade] }}>{r.grade}</strong>
                      {' · '}{fmtDate(r.reviewedAt)}
                      {' · '}after {r.elapsedDays}d at {Math.round(r.retrievability * 100)}% predicted
                      {' · '}stability {fmtDays(r.stabilityBefore)} → {fmtDays(r.stabilityAfter)}
                      {r.wasRelearning && ' · relearning'}
                      {r.answer && <div style={{ marginTop: 2 }}>answered: <em>{r.answer}</em></div>}
                      {r.note && <div style={{ fontStyle: 'italic', marginTop: 2 }}>{r.note}</div>}
                    </div>
                  ))}
                </div>
              )}
            </div>
          )}
        </>
      )}
    </div>
  );
}

function AddPromptForm({ book, dayNumber, onAdded }: { book: Book; dayNumber: number; onAdded: () => void }) {
  const [open, setOpen] = useState(false);
  const [q, setQ] = useState('');
  const [a, setA] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (!open) return <button className="btn btn-sm" onClick={() => setOpen(true)}>+ Add prompt</button>;

  const submit = async () => {
    setBusy(true); setError(null);
    try {
      await api.createNotePrompts(book, dayNumber, [{ question: q, answer: a }]);
      setQ(''); setA(''); setOpen(false); onAdded();
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    } finally { setBusy(false); }
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 8, padding: 12, borderRadius: 'var(--radius-md)', border: '1px dashed var(--border)' }}>
      <TextArea value={q} onChange={setQ} placeholder="Question — one fact, specific, no hint of the answer" />
      <TextArea value={a} onChange={setA} placeholder="Answer — what a correct reply must contain" />
      {error && <div style={{ fontSize: 12, color: '#e5484d' }}>{error}</div>}
      <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
        <button className="btn btn-accent btn-sm" disabled={busy || !q.trim() || !a.trim()} onClick={submit}>Add</button>
        <button className="btn btn-sm" disabled={busy} onClick={() => setOpen(false)}>Cancel</button>
        <span style={{ fontSize: 12, color: 'var(--text-muted)' }}>First review in 4 days — writing the note counts as the first exposure.</span>
      </div>
    </div>
  );
}

export function NotesPage() {
  const [mode, setMode] = useState<'notes' | 'review'>('notes');
  const [book, setBook] = useState<Book>('red');
  const [notes, setNotes] = useState<NoteDto[]>([]);
  const [overview, setOverview] = useState<Record<number, NotePromptOverviewDto>>({});
  const [stats, setStats] = useState<NoteSrsStatsDto | null>(null);
  const [selected, setSelected] = useState<number | null>(null);
  const [prompts, setPrompts] = useState<NotePromptDto[]>([]);
  const [loading, setLoading] = useState(true);

  const loadStats = useCallback(() => { api.getNoteSrsStats().then(setStats).catch(() => setStats(null)); }, []);
  const loadOverview = useCallback((b: Book) => {
    api.getNotePromptOverview(b)
      .then(list => setOverview(Object.fromEntries(list.map(o => [o.dayNumber, o]))))
      .catch(() => setOverview({}));
  }, []);

  useEffect(() => {
    setLoading(true);
    Promise.all([api.getNotes(book), api.getNotePromptOverview(book).catch(() => [] as NotePromptOverviewDto[])])
      .then(([list, ov]) => {
        setNotes(list);
        setOverview(Object.fromEntries(ov.map(o => [o.dayNumber, o])));
        setSelected(list.length ? list[0].dayNumber : null);
      })
      .finally(() => setLoading(false));
    loadStats();
  }, [book, loadStats]);

  useEffect(() => {
    if (selected === null) { setPrompts([]); return; }
    let cancelled = false;
    api.getNotePrompts(book, selected).then(list => { if (!cancelled) setPrompts(list); }).catch(() => { if (!cancelled) setPrompts([]); });
    return () => { cancelled = true; };
  }, [book, selected]);

  // Back from a review: due dates moved, so the badges, the selected note's prompts and the totals re-fetch.
  useEffect(() => {
    if (mode !== 'notes') return;
    loadOverview(book);
    loadStats();
    if (selected !== null) api.getNotePrompts(book, selected).then(setPrompts).catch(() => undefined);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [mode]);

  // After any prompt write: the note's prompts, the day-list badges and the totals all move.
  const refreshPrompts = () => {
    if (selected !== null) api.getNotePrompts(book, selected).then(setPrompts).catch(() => undefined);
    loadOverview(book);
    loadStats();
  };

  const current = notes.find(n => n.dayNumber === selected) ?? null;
  const accent = book === 'red' ? '#e5484d' : '#30a46c';
  const tr = stats?.trueRetention30d;

  return (
    <div style={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
      {/* Mode switch, Red / Green subtabs, and the spaced-repetition totals (global — the cap is shared by both books) */}
      <div style={{ display: 'flex', gap: 8, padding: '12px 16px', borderBottom: '1px solid var(--border-subtle)', flexShrink: 0, flexWrap: 'wrap', alignItems: 'center' }}>
        <div style={{ display: 'inline-flex', border: '1px solid var(--border)', borderRadius: 'var(--radius-md)', overflow: 'hidden', marginRight: 6 }}>
          {(['notes', 'review'] as const).map(m => {
            const active = mode === m;
            return (
              <button key={m} onClick={() => setMode(m)} style={{
                padding: '7px 14px', fontSize: 13, fontWeight: 600, cursor: 'pointer', border: 'none', textTransform: 'capitalize',
                background: active ? 'var(--accent, #5b8def)' : 'var(--bg-secondary)', color: active ? '#fff' : 'var(--text-secondary)',
              }}>{m === 'review' ? `Review${stats && stats.dueToday > 0 ? ` · ${Math.min(stats.dueToday, stats.remaining)}` : ''}` : 'Notes'}</button>
            );
          })}
        </div>
        {mode === 'notes' && (['red', 'green'] as Book[]).map(b => {
          const active = book === b;
          const c = b === 'red' ? '#e5484d' : '#30a46c';
          return (
            <button key={b} onClick={() => setBook(b)}
              style={{
                display: 'flex', alignItems: 'center', gap: 7, padding: '7px 16px',
                borderRadius: 'var(--radius-md)', cursor: 'pointer', fontSize: 14, fontWeight: 600,
                textTransform: 'capitalize',
                border: `1px solid ${active ? c : 'var(--border)'}`,
                background: active ? c : 'var(--bg-secondary)',
                color: active ? '#fff' : 'var(--text-secondary)',
              }}>
              <span style={{ width: 10, height: 10, borderRadius: '50%', background: active ? '#fff' : c, display: 'inline-block' }} />
              {b}
            </button>
          );
        })}
        {stats && (
          <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', marginLeft: 'auto', alignItems: 'stretch' }}>
            <Stat label="prompts" value={stats.prompts} title={`${stats.active} active · ${stats.notesWithPrompts} notes covered, ${stats.notesWithoutPrompts} not yet`} />
            <Stat label="due today" value={stats.dueToday} color={stats.dueToday > 0 ? '#e5484d' : undefined} />
            <Stat label="asked / cap" value={`${stats.askedToday} / ${stats.dailyCap}`} title={`${stats.remaining} questions left today`} />
            <Stat label="parked" value={stats.parked} color={stats.parked > 0 ? '#f5a623' : undefined}
              title="Fell below 50% recall while the cap was full; comes back on a day with spare slots. A growing number means the cap carries less than you write." />
            <Stat label="leeches" value={stats.leeches} color={stats.leeches > 0 ? '#e5484d' : undefined} title={`Suspended after ${LEECH_LAPSES} lapses — rewrite them`} />
            <Stat label="retention 30d" value={tr == null ? '—' : `${Math.round(tr * 100)}%`}
              color={tr == null ? undefined : tr >= 0.85 ? '#30a46c' : '#f5a623'}
              title={`Share of scheduled reviews recalled. Target ${Math.round(stats.desiredRetention * 100)}%`} />
            <Stat label="carries / day" value={stats.carryCapacityPerDay} title="New prompts per day the cap can sustain at steady state (about 7 questions per carried prompt)" />
            <LoadBars load={stats.upcomingLoad} cap={stats.dailyCap} />
          </div>
        )}
      </div>

      {mode === 'review' ? (
        <NotesReview onStatsChanged={loadStats} />
      ) : loading ? (
        <div style={{ padding: 24, color: 'var(--text-muted)' }}>Loading...</div>
      ) : notes.length === 0 ? (
        <div style={{ padding: 24, color: 'var(--text-muted)' }}>No notes in the {book} book yet.</div>
      ) : (
        <div style={{ display: 'flex', flex: 1, minHeight: 0 }}>
          {/* Day list */}
          <div style={{ width: 230, flexShrink: 0, overflowY: 'auto', borderRight: '1px solid var(--border-subtle)', padding: 8 }}>
            {notes.map(n => {
              const active = n.dayNumber === selected;
              const o = overview[n.dayNumber];
              return (
                <button key={n.dayNumber} onClick={() => setSelected(n.dayNumber)}
                  style={{
                    display: 'flex', alignItems: 'center', gap: 8, width: '100%', textAlign: 'left', padding: '8px 10px', marginBottom: 4,
                    borderRadius: 'var(--radius-sm)', cursor: 'pointer', border: 'none',
                    borderLeft: `3px solid ${active ? accent : 'transparent'}`,
                    background: active ? 'var(--bg-secondary)' : 'transparent',
                    color: active ? 'var(--text-primary)' : 'var(--text-secondary)',
                  }}>
                  <div style={{ flex: 1, minWidth: 0 }}>
                    <div style={{ fontSize: 13, fontWeight: 600 }}>Day {n.dayNumber}</div>
                    <div style={{ fontSize: 12, color: 'var(--text-muted)' }}>{fmtDate(n.entryDate)}</div>
                  </div>
                  {o && o.promptCount > 0 && (
                    <span title={`${o.promptCount} prompt${o.promptCount === 1 ? '' : 's'}${o.due ? `, ${o.due} due` : ''}${o.parked ? `, ${o.parked} parked` : ''}${o.suspended ? `, ${o.suspended} suspended` : ''}`}
                      style={{
                        fontSize: 11, fontWeight: 700, minWidth: 18, height: 18, padding: '0 5px', borderRadius: 9,
                        display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
                        background: o.due > 0 ? '#e5484d' : 'var(--bg-primary)',
                        color: o.due > 0 ? '#fff' : 'var(--text-muted)',
                        border: o.due > 0 ? 'none' : '1px solid var(--border)',
                      }}>{o.due > 0 ? o.due : o.promptCount}</span>
                  )}
                </button>
              );
            })}
          </div>

          {/* Visualized markdown + prompts */}
          <div style={{ flex: 1, overflowY: 'auto', padding: '20px 28px' }}>
            {current && (
              <>
                <div style={{ display: 'flex', alignItems: 'baseline', gap: 12, marginBottom: 16 }}>
                  <h2 style={{ margin: 0, fontSize: 20, color: 'var(--text-primary)' }}>Day {current.dayNumber}</h2>
                  <span style={{ fontSize: 14, color: 'var(--text-muted)' }}>{fmtDate(current.entryDate)}</span>
                </div>
                <div className="md-body" dangerouslySetInnerHTML={{ __html: marked.parse(current.content) as string }} />

                <div style={{ marginTop: 28, paddingTop: 16, borderTop: '1px solid var(--border-subtle)' }}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 10 }}>
                    <h3 style={{ margin: 0, fontSize: 14, fontWeight: 700, color: 'var(--text-secondary)', textTransform: 'uppercase', letterSpacing: '0.04em' }}>
                      Prompts {prompts.length > 0 && <span style={{ color: 'var(--text-muted)', fontWeight: 600 }}>· {prompts.length}</span>}
                    </h3>
                    <span style={{ flex: 1 }} />
                    <AddPromptForm book={book} dayNumber={current.dayNumber} onAdded={refreshPrompts} />
                  </div>
                  {prompts.length === 0 ? (
                    <div style={{ fontSize: 13, color: 'var(--text-muted)', padding: '10px 0' }}>
                      No prompts yet. The note-taking skill extracts them when a note is written; older notes get theirs through the quiz's backfill, or add one here.
                    </div>
                  ) : prompts.map(p => <PromptCard key={p.id} p={p} onChanged={refreshPrompts} />)}
                </div>
              </>
            )}
          </div>
        </div>
      )}
    </div>
  );
}
