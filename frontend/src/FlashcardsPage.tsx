import { useState, useEffect, useCallback, type CSSProperties } from 'react';
import { marked } from 'marked';
import type { FlashcardDto, FlashcardPromptDto, FlashcardStatsDto, FlashcardPromptState } from './types';
import { api } from './api';
import { FlashcardsReview } from './FlashcardsReview';
import { FlashcardsDashboard } from './FlashcardsDashboard';

// Notes v2. A card is one note in one book; its date is a property and a filter, and its prompts are
// what get scheduled. Cards and prompts are mostly written by the skills in chat; here they are read,
// fixed and reviewed. Nothing on this page touches the v1 Notes tab or its data.

type Book = 'red' | 'green';
type BookFilter = 'all' | Book;

/** A card's title: its first line without the bullet marker. */
function cardTitle(content: string): string {
  const first = content.split('\n').find(l => l.trim()) ?? '';
  return first.replace(/^\s*[-*]\s+/, '').trim() || '(empty)';
}

marked.setOptions({ gfm: true, breaks: true });

function fmtDate(iso: string): string {
  const [y, m, d] = iso.slice(0, 10).split('-');
  return d && m && y ? `${d}.${m}.${y}` : iso;
}

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

function fmtDays(days: number): string {
  if (days < 14) return `${Math.round(days)}d`;
  if (days < 60) return `${Math.round(days / 7)}w`;
  if (days < 540) return `${Math.round(days / 30)}mo`;
  return `${(days / 365).toFixed(1)}y`;
}

const STATE_COLOR: Record<FlashcardPromptState, string> = { Active: '#30a46c', Parked: '#f5a623', Suspended: '#8b8b8b' };
const GRADE_COLOR: Record<string, string> = { Again: '#e5484d', Hard: '#f5a623', Good: '#30a46c', Easy: '#5b8def' };
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
      borderRadius: 'var(--radius-md)', background: 'var(--bg-secondary)', border: '1px solid var(--border-subtle)', position: 'relative',
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
    <textarea value={props.value} onChange={e => props.onChange(e.target.value)} placeholder={props.placeholder} rows={props.rows ?? 2}
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
function PromptCard({ p, onChanged }: { p: FlashcardPromptDto; onChanged: () => void }) {
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
      await api.updateFlashcardPrompt(p.id, { question: q, answer: a, reset: resetToo || undefined, state: resetToo ? 'Active' : undefined });
      setEditing(false); setResetToo(false); onChanged();
    } finally { setBusy(false); }
  };
  const setState = async (state: FlashcardPromptState) => {
    setBusy(true);
    try { await api.updateFlashcardPrompt(p.id, { state }); onChanged(); } finally { setBusy(false); }
  };
  const remove = async () => {
    if (!confirm('Delete this prompt and its review history?')) return;
    setBusy(true);
    try { await api.deleteFlashcardPrompt(p.id); onChanged(); } finally { setBusy(false); }
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
            padding: revealed ? 0 : '6px 10px', border: revealed ? 'none' : '1px dashed var(--border)', whiteSpace: 'pre-wrap',
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

function AddPromptForm({ cardId, onAdded }: { cardId: string; onAdded: () => void }) {
  const [open, setOpen] = useState(false);
  const [q, setQ] = useState('');
  const [a, setA] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (!open) return <button className="btn btn-sm" onClick={() => setOpen(true)}>+ Add prompt</button>;

  const submit = async () => {
    setBusy(true); setError(null);
    try {
      await api.addFlashcardPrompts(cardId, [{ question: q, answer: a }]);
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
        <span style={{ fontSize: 12, color: 'var(--text-muted)' }}>First review in 4 days — writing the card counts as the first exposure.</span>
      </div>
    </div>
  );
}

/** Create a card by hand: book, date (today by default) and the day's bullets. Prompts come after, per bullet. */
function NewCardForm({ defaultBook, onCreated }: { defaultBook: Book; onCreated: (card: FlashcardDto) => void }) {
  const [book, setBook] = useState<Book>(defaultBook);
  const [open, setOpen] = useState(false);
  const [date, setDate] = useState('');
  const [content, setContent] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (!open) return <button className="btn btn-sm" style={{ width: '100%', justifyContent: 'center' }} onClick={() => setOpen(true)}>+ New card</button>;

  const submit = async () => {
    setBusy(true); setError(null);
    try {
      const card = await api.createFlashcard(book, content, date || undefined);
      setContent(''); setDate(''); setOpen(false); onCreated(card);
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    } finally { setBusy(false); }
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 8, padding: 12, borderRadius: 'var(--radius-md)', border: '1px dashed var(--border)', marginBottom: 12 }}>
      <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
        <select value={book} onChange={e => setBook(e.target.value as Book)}
          style={{ padding: '5px 8px', fontSize: 13, borderRadius: 'var(--radius-sm)', border: '1px solid var(--border)', background: 'var(--bg-primary)', color: 'var(--text-primary)' }}>
          <option value="red">red</option><option value="green">green</option>
        </select>
        <span style={{ fontSize: 12, color: 'var(--text-muted)' }}>date</span>
        <input type="date" value={date} onChange={e => setDate(e.target.value)}
          style={{ padding: '5px 8px', fontSize: 13, borderRadius: 'var(--radius-sm)', border: '1px solid var(--border)', background: 'var(--bg-primary)', color: 'var(--text-primary)' }} />
        <span style={{ fontSize: 12, color: 'var(--text-muted)' }}>(empty = today)</span>
      </div>
      <TextArea value={content} onChange={setContent} placeholder={'- One note: a single bullet, subpoints indented two spaces'} rows={4} />
      {error && <div style={{ fontSize: 12, color: '#e5484d' }}>{error}</div>}
      <div style={{ display: 'flex', gap: 8 }}>
        <button className="btn btn-accent btn-sm" disabled={busy || !content.trim()} onClick={submit}>Create</button>
        <button className="btn btn-sm" disabled={busy} onClick={() => setOpen(false)}>Cancel</button>
      </div>
    </div>
  );
}

export function FlashcardsPage() {
  const [mode, setMode] = useState<'cards' | 'review' | 'dashboard'>('cards');
  const [book, setBook] = useState<BookFilter>('all');
  const [date, setDate] = useState('');
  const [search, setSearch] = useState('');
  const [query, setQuery] = useState('');
  const [cards, setCards] = useState<FlashcardDto[]>([]);
  const [stats, setStats] = useState<FlashcardStatsDto | null>(null);
  const [selected, setSelected] = useState<string | null>(null);
  const [card, setCard] = useState<FlashcardDto | null>(null);
  const [editingContent, setEditingContent] = useState(false);
  const [draft, setDraft] = useState('');
  const [loading, setLoading] = useState(true);

  // Debounce the search box so typing does not fire a request per key.
  useEffect(() => { const t = setTimeout(() => setQuery(search.trim()), 250); return () => clearTimeout(t); }, [search]);

  const loadStats = useCallback(() => { api.getFlashcardStats().then(setStats).catch(() => setStats(null)); }, []);
  const loadCards = useCallback((keepSelection: boolean) => {
    return api.getFlashcards({ book: book === 'all' ? undefined : book, date: date || undefined, search: query || undefined }).then(list => {
      setCards(list);
      setSelected(prev => (keepSelection && prev && list.some(c => c.id === prev)) ? prev : (list[0]?.id ?? null));
    });
  }, [book, date, query]);

  useEffect(() => {
    setLoading(true);
    loadCards(false).finally(() => setLoading(false));
  }, [loadCards]);
  useEffect(loadStats, [loadStats]);

  useEffect(() => {
    if (!selected) { setCard(null); return; }
    let cancelled = false;
    setEditingContent(false);
    api.getFlashcard(selected).then(c => { if (!cancelled) setCard(c); }).catch(() => { if (!cancelled) setCard(null); });
    return () => { cancelled = true; };
  }, [selected]);

  // Back from a review: due dates moved, so the badges, the selected card and the totals re-fetch.
  useEffect(() => {
    if (mode !== 'cards') return;
    loadCards(true);
    loadStats();
    if (selected) api.getFlashcard(selected).then(setCard).catch(() => undefined);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [mode]);

  // After any prompt write: the card, the list badges and the totals all move.
  const refresh = () => {
    if (selected) api.getFlashcard(selected).then(setCard).catch(() => undefined);
    loadCards(true);
    loadStats();
  };

  // The list grouped under its dates, newest first (the server already orders it).
  const groups: { date: string; cards: FlashcardDto[] }[] = [];
  for (const c of cards) {
    const last = groups[groups.length - 1];
    if (last && last.date === c.entryDate) last.cards.push(c); else groups.push({ date: c.entryDate, cards: [c] });
  }

  const saveContent = async () => {
    if (!card) return;
    const updated = await api.updateFlashcard(card.id, draft);
    setCard({ ...updated, prompts: card.prompts });
    setEditingContent(false);
    loadCards(true);
  };

  const removeCard = async () => {
    if (!card || !confirm(`Delete this card (${fmtDate(card.entryDate)}) with its ${card.promptCount} prompts?`)) return;
    await api.deleteFlashcard(card.id);
    setSelected(null);
    loadCards(false);
    loadStats();
  };

  const bookColor = (b: string) => b === 'red' ? '#e5484d' : '#30a46c';
  const tr = stats?.trueRetention30d;

  return (
    <div style={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
      {/* Mode switch, Red / Green, and the totals (global — the cap is shared by both books) */}
      <div style={{ display: 'flex', gap: 8, padding: '12px 16px', borderBottom: '1px solid var(--border-subtle)', flexShrink: 0, flexWrap: 'wrap', alignItems: 'center' }}>
        <div style={{ display: 'inline-flex', border: '1px solid var(--border)', borderRadius: 'var(--radius-md)', overflow: 'hidden', marginRight: 6 }}>
          {(['cards', 'review', 'dashboard'] as const).map(m => {
            const active = mode === m;
            return (
              <button key={m} onClick={() => setMode(m)} style={{
                padding: '7px 14px', fontSize: 13, fontWeight: 600, cursor: 'pointer', border: 'none', textTransform: 'capitalize',
                background: active ? 'var(--accent, #5b8def)' : 'var(--bg-secondary)', color: active ? '#fff' : 'var(--text-secondary)',
              }}>{m === 'review' ? `Review${stats && stats.dueToday > 0 ? ` · ${Math.min(stats.dueToday, stats.remaining)}` : ''}` : m === 'dashboard' ? 'Dashboard' : 'Cards'}</button>
            );
          })}
        </div>
        {mode === 'cards' && (['all', 'red', 'green'] as BookFilter[]).map(b => {
          const active = book === b;
          const c = b === 'all' ? 'var(--accent, #5b8def)' : bookColor(b);
          return (
            <button key={b} onClick={() => setBook(b)}
              style={{
                display: 'flex', alignItems: 'center', gap: 7, padding: '7px 14px',
                borderRadius: 'var(--radius-md)', cursor: 'pointer', fontSize: 14, fontWeight: 600, textTransform: 'capitalize',
                border: `1px solid ${active ? c : 'var(--border)'}`,
                background: active ? c : 'var(--bg-secondary)',
                color: active ? '#fff' : 'var(--text-secondary)',
              }}>
              {b !== 'all' && <span style={{ width: 10, height: 10, borderRadius: '50%', background: active ? '#fff' : c, display: 'inline-block' }} />}
              {b}
            </button>
          );
        })}
        {stats && (
          <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', marginLeft: 'auto', alignItems: 'stretch' }}>
            <Stat label="cards" value={stats.flashcards} title={`${stats.flashcardsWithPrompts} with prompts, ${stats.flashcardsWithoutPrompts} without`} />
            <Stat label="prompts" value={stats.prompts} title={`${stats.active} active`} />
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
        <FlashcardsReview onStatsChanged={loadStats} />
      ) : mode === 'dashboard' ? (
        <FlashcardsDashboard />
      ) : loading ? (
        <div style={{ padding: 24, color: 'var(--text-muted)' }}>Loading...</div>
      ) : (
        <div style={{ display: 'flex', flex: 1, minHeight: 0 }}>
          {/* Card list, grouped by date; filters on top */}
          <div style={{ width: 300, flexShrink: 0, overflowY: 'auto', borderRight: '1px solid var(--border-subtle)', padding: 8 }}>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 6, marginBottom: 10 }}>
              <div style={{ display: 'flex', gap: 6, alignItems: 'center' }}>
                <input type="date" value={date} onChange={e => setDate(e.target.value)} title="Show only the cards of this day"
                  style={{ flex: 1, padding: '5px 8px', fontSize: 13, borderRadius: 'var(--radius-sm)', border: '1px solid var(--border)', background: 'var(--bg-primary)', color: 'var(--text-primary)' }} />
                {date && <button style={smallBtn} onClick={() => setDate('')}>all days</button>}
              </div>
              <input value={search} onChange={e => setSearch(e.target.value)} placeholder="Search cards and questions…"
                style={{ padding: '6px 10px', fontSize: 13, borderRadius: 'var(--radius-sm)', border: '1px solid var(--border)', background: 'var(--bg-primary)', color: 'var(--text-primary)' }} />
              <NewCardForm defaultBook={book === 'green' ? 'green' : 'red'} onCreated={c => { loadCards(false).then(() => setSelected(c.id)); loadStats(); }} />
              <div style={{ fontSize: 11, color: 'var(--text-muted)' }}>{cards.length} card{cards.length === 1 ? '' : 's'}{date ? ` on ${fmtDate(date)}` : ''}</div>
            </div>
            {cards.length === 0 && <div style={{ padding: '12px 10px', fontSize: 13, color: 'var(--text-muted)' }}>No cards match.</div>}
            {groups.map(g => (
              <div key={g.date} style={{ marginBottom: 8 }}>
                <button onClick={() => setDate(g.date)} title="Show only this day"
                  style={{ display: 'block', width: '100%', textAlign: 'left', border: 'none', background: 'transparent', cursor: 'pointer',
                    fontSize: 11, fontWeight: 700, letterSpacing: '0.04em', textTransform: 'uppercase', color: 'var(--text-muted)', padding: '6px 10px 4px' }}>
                  {fmtDate(g.date)} · {g.cards.length}
                </button>
                {g.cards.map(c => {
                  const active = c.id === selected;
                  return (
                    <button key={c.id} onClick={() => setSelected(c.id)}
                      style={{
                        display: 'flex', alignItems: 'center', gap: 8, width: '100%', textAlign: 'left', padding: '7px 10px', marginBottom: 3,
                        borderRadius: 'var(--radius-sm)', cursor: 'pointer', border: 'none',
                        borderLeft: `3px solid ${active ? bookColor(c.book) : 'transparent'}`,
                        background: active ? 'var(--bg-secondary)' : 'transparent',
                        color: active ? 'var(--text-primary)' : 'var(--text-secondary)',
                      }}>
                      <span title={c.book} style={{ width: 8, height: 8, borderRadius: 4, background: bookColor(c.book), flexShrink: 0 }} />
                      <span style={{ flex: 1, minWidth: 0, fontSize: 13, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{cardTitle(c.content)}</span>
                      {c.promptCount > 0 ? (
                        <span title={`${c.promptCount} prompt${c.promptCount === 1 ? '' : 's'}${c.due ? `, ${c.due} due` : ''}${c.parked ? `, ${c.parked} parked` : ''}${c.suspended ? `, ${c.suspended} suspended` : ''}`}
                          style={{
                            fontSize: 11, fontWeight: 700, minWidth: 18, height: 18, padding: '0 5px', borderRadius: 9, flexShrink: 0,
                            display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
                            background: c.due > 0 ? '#e5484d' : 'var(--bg-primary)',
                            color: c.due > 0 ? '#fff' : 'var(--text-muted)',
                            border: c.due > 0 ? 'none' : '1px solid var(--border)',
                          }}>{c.due > 0 ? c.due : c.promptCount}</span>
                      ) : (
                        <span title="No prompts yet" style={{ width: 8, height: 8, borderRadius: 4, border: '1px solid var(--border)', flexShrink: 0 }} />
                      )}
                    </button>
                  );
                })}
              </div>
            ))}
          </div>

          {/* Card content + prompts */}
          <div style={{ flex: 1, overflowY: 'auto', padding: '20px 28px' }}>
            {card && (
              <>
                <div style={{ display: 'flex', alignItems: 'baseline', gap: 12, marginBottom: 16 }}>
                  <Pill text={card.book} color={bookColor(card.book)} />
                  <button onClick={() => setDate(card.entryDate)} title="Show all cards of this day"
                    style={{ border: 'none', background: 'transparent', cursor: 'pointer', fontSize: 14, color: 'var(--text-muted)', padding: 0 }}>{fmtDate(card.entryDate)}</button>
                  <span style={{ flex: 1 }} />
                  {editingContent ? (
                    <>
                      <button className="btn btn-accent btn-sm" onClick={saveContent}>Save</button>
                      <button className="btn btn-sm" onClick={() => setEditingContent(false)}>Cancel</button>
                    </>
                  ) : (
                    <>
                      <button style={smallBtn} onClick={() => { setDraft(card.content); setEditingContent(true); }}>edit content</button>
                      <button style={{ ...smallBtn, color: '#e5484d', borderColor: '#e5484d55' }} onClick={removeCard}>delete card</button>
                    </>
                  )}
                </div>
                {editingContent
                  ? <TextArea value={draft} onChange={setDraft} placeholder="The day's bullets" rows={8} />
                  : <div className="md-body" dangerouslySetInnerHTML={{ __html: marked.parse(card.content) as string }} />}

                <div style={{ marginTop: 28, paddingTop: 16, borderTop: '1px solid var(--border-subtle)' }}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 10 }}>
                    <h3 style={{ margin: 0, fontSize: 14, fontWeight: 700, color: 'var(--text-secondary)', textTransform: 'uppercase', letterSpacing: '0.04em' }}>
                      Prompts {card.prompts.length > 0 && <span style={{ color: 'var(--text-muted)', fontWeight: 600 }}>· {card.prompts.length}</span>}
                    </h3>
                    <span style={{ flex: 1 }} />
                    <AddPromptForm cardId={card.id} onAdded={refresh} />
                  </div>
                  {card.prompts.length === 0 ? (
                    <div style={{ fontSize: 13, color: 'var(--text-muted)', padding: '10px 0' }}>
                      No prompts yet. The note-taking skill writes them with the card; the quiz's backfill adds them to older cards; or add one here, one fact each.
                    </div>
                  ) : card.prompts.map(p => <PromptCard key={p.id} p={p} onChanged={refresh} />)}
                </div>
              </>
            )}
          </div>
        </div>
      )}
    </div>
  );
}
