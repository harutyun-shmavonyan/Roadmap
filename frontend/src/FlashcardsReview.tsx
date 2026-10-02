import { useState, useEffect, useCallback } from 'react';
import type { FlashcardPromptDto, FlashcardSessionDto, FlashcardGrade } from './types';
import { api } from './api';

// The Notes v2 flashcard review. Pulls today's queue — already triaged under the daily cap by the
// server — and walks it one card at a time: question, reveal, self-grade. Every grade is recorded at
// once, so a session begun here and finished in chat (or the other way round) is one session.
// Failures are shown once more at the end, unrecorded: the real retest is tomorrow.

type Scope = 'both' | 'red' | 'green';
type Phase = 'loading' | 'review' | 'relearn' | 'done' | 'error';
interface Outcome { grade: FlashcardGrade; intervalDays: number; leech: boolean; }

const GRADES: FlashcardGrade[] = ['again', 'hard', 'good', 'easy'];
const GRADE_COLOR: Record<FlashcardGrade, string> = { again: '#e5484d', hard: '#f5a623', good: '#30a46c', easy: '#5b8def' };
const GRADE_LABEL: Record<FlashcardGrade, string> = { again: 'Again', hard: 'Hard', good: 'Good', easy: 'Easy' };
const GRADE_HINT: Record<FlashcardGrade, string> = {
  again: 'wrong or blank · back tomorrow',
  hard: 'right in part, or a real effort',
  good: 'recalled it',
  easy: 'instant and complete',
};
const MARK: Record<FlashcardGrade, string> = { again: '✗', hard: '~', good: '✓', easy: '✓✓' };
const BOOK_COLOR: Record<string, string> = { red: '#e5484d', green: '#30a46c' };

function fmtDate(iso: string): string {
  const [y, m, d] = iso.slice(0, 10).split('-');
  return d && m && y ? `${d}.${m}.${y}` : iso;
}

function fmtInterval(days: number): string {
  if (days < 1) return 'tomorrow';
  if (days === 1) return '1 day';
  if (days < 14) return `${days} days`;
  if (days < 60) return `${Math.round(days / 7)} weeks`;
  if (days < 540) return `${Math.round(days / 30)} months`;
  return `${(days / 365).toFixed(1)} years`;
}

function Pill({ text, color }: { text: string; color: string }) {
  return (
    <span style={{
      fontSize: 11, fontWeight: 600, padding: '2px 8px', borderRadius: 999,
      background: `${color}1a`, color, border: `1px solid ${color}55`, whiteSpace: 'nowrap',
    }}>{text}</span>
  );
}

function Chip({ active, onClick, children }: { active: boolean; onClick: () => void; children: React.ReactNode }) {
  return (
    <button onClick={onClick} style={{
      padding: '5px 12px', borderRadius: 'var(--radius-md)', cursor: 'pointer', fontSize: 12, fontWeight: 600,
      textTransform: 'capitalize', border: `1px solid ${active ? 'var(--accent, #5b8def)' : 'var(--border)'}`,
      background: active ? 'var(--accent, #5b8def)' : 'var(--bg-secondary)', color: active ? '#fff' : 'var(--text-secondary)',
    }}>{children}</button>
  );
}

function Card({ p, children }: { p: FlashcardPromptDto; children: React.ReactNode }) {
  return (
    <div style={{
      maxWidth: 720, width: '100%', margin: '0 auto', border: '1px solid var(--border-subtle)',
      borderRadius: 'var(--radius-lg, 12px)', background: 'var(--bg-secondary)', padding: '22px 26px',
    }}>
      <div style={{ display: 'flex', gap: 6, alignItems: 'center', flexWrap: 'wrap', marginBottom: 14 }}>
        <Pill text={p.book} color={BOOK_COLOR[p.book] ?? '#8b8b8b'} />
        <span style={{ fontSize: 12, color: 'var(--text-muted)' }}>{fmtDate(p.entryDate)}</span>
        {p.relearning && <Pill text="again from yesterday" color="#e5484d" />}
        {p.state === 'Parked' && <Pill text="back from parked" color="#f5a623" />}
      </div>
      <div style={{ fontSize: 20, fontWeight: 600, lineHeight: 1.4, color: 'var(--text-primary)' }}>{p.question}</div>
      {children}
    </div>
  );
}

const answerBox: React.CSSProperties = {
  marginTop: 18, padding: '12px 14px', borderRadius: 'var(--radius-md)', background: 'var(--bg-primary)',
  border: '1px solid var(--border-subtle)', fontSize: 15, lineHeight: 1.5, color: 'var(--text-secondary)', whiteSpace: 'pre-wrap',
};

export function FlashcardsReview({ onStatsChanged }: { onStatsChanged: () => void }) {
  const [scope, setScope] = useState<Scope>('both');
  const [session, setSession] = useState<FlashcardSessionDto | null>(null);
  const [phase, setPhase] = useState<Phase>('loading');
  const [error, setError] = useState<string | null>(null);
  const [index, setIndex] = useState(0);
  const [revealed, setRevealed] = useState(false);
  const [outcomes, setOutcomes] = useState<Record<string, Outcome>>({});
  const [last, setLast] = useState<{ text: string; color: string } | null>(null);
  const [relearn, setRelearn] = useState<FlashcardPromptDto[]>([]);
  const [busy, setBusy] = useState(false);
  const [askedToday, setAskedToday] = useState(0);
  const [cap, setCap] = useState(25);

  const start = useCallback(async (s: Scope) => {
    setPhase('loading'); setError(null); setOutcomes({}); setLast(null); setIndex(0); setRevealed(false); setRelearn([]);
    try {
      const sess = await api.startFlashcardReview(s === 'both' ? undefined : s);
      setSession(sess); setAskedToday(sess.askedToday); setCap(sess.dailyCap);
      setPhase(sess.prompts.length ? 'review' : 'done');
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e)); setPhase('error');
    }
  }, []);

  useEffect(() => { void start(scope); }, [scope, start]);

  const queue = session?.prompts ?? [];
  const current: FlashcardPromptDto | undefined = phase === 'review' ? queue[index] : phase === 'relearn' ? relearn[index] : undefined;

  const grade = async (g: FlashcardGrade, answer?: string) => {
    if (!current || busy || phase !== 'review') return;
    setBusy(true);
    try {
      const r = await api.recordFlashcardReview(current.id, g, answer, answer === 'O' ? 'declared known' : undefined);
      const next = { ...outcomes, [current.id]: { grade: g, intervalDays: r.intervalDays, leech: r.leech } };
      setOutcomes(next);
      setAskedToday(r.askedToday); setCap(r.dailyCap);
      setLast({
        color: GRADE_COLOR[g],
        text: g === 'again'
          ? `${MARK.again} back tomorrow${r.leech ? ' · failed 8 times, suspended as a leech — rewrite it on its card' : ''}`
          : `${MARK[g]} next in ${fmtInterval(r.intervalDays)}`,
      });
      onStatsChanged();
      if (index + 1 < queue.length) { setIndex(index + 1); setRevealed(false); }
      else {
        const failed = queue.filter(p => next[p.id]?.grade === 'again' && !next[p.id]?.leech);
        if (failed.length) { setRelearn(failed); setIndex(0); setRevealed(false); setPhase('relearn'); }
        else setPhase('done');
      }
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    } finally { setBusy(false); }
  };

  const relearnNext = () => {
    if (index + 1 < relearn.length) { setIndex(index + 1); setRevealed(false); }
    else setPhase('done');
  };

  // Keyboard: Space/Enter reveals (and advances in the relearn pass), 1-4 grade, O = "I know this".
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.target instanceof HTMLInputElement || e.target instanceof HTMLTextAreaElement) return;
      if (phase === 'review') {
        if (e.key === ' ' || e.key === 'Enter') { e.preventDefault(); setRevealed(true); }
        else if (e.key === 'o' || e.key === 'O') void grade('easy', 'O');
        else if (revealed && GRADES[Number(e.key) - 1]) void grade(GRADES[Number(e.key) - 1]);
      } else if (phase === 'relearn') {
        if (e.key === ' ' || e.key === 'Enter') { e.preventDefault(); if (revealed) relearnNext(); else setRevealed(true); }
      }
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  });

  const tally = GRADES.map(g => [g, Object.values(outcomes).filter(o => o.grade === g).length] as const);
  const total = queue.length;
  const position = phase === 'review' ? index + 1 : total;

  return (
    <div style={{ display: 'flex', flexDirection: 'column', flex: 1, minHeight: 0, overflowY: 'auto', padding: '16px 24px 32px' }}>
      <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap', maxWidth: 720, width: '100%', margin: '0 auto 14px' }}>
        {(['both', 'red', 'green'] as Scope[]).map(s => <Chip key={s} active={scope === s} onClick={() => setScope(s)}>{s}</Chip>)}
        <span style={{ flex: 1 }} />
        {session && phase !== 'loading' && (
          <span style={{ fontSize: 12, color: 'var(--text-muted)' }}>
            {total > 0 && phase !== 'done' && <>card {position} / {total} · </>}
            asked {askedToday} / {cap} today
          </span>
        )}
      </div>

      {last && (phase === 'review' || phase === 'relearn') && (
        <div style={{ maxWidth: 720, width: '100%', margin: '0 auto 10px', fontSize: 13, fontWeight: 600, color: last.color }}>{last.text}</div>
      )}

      {phase === 'loading' && <div style={{ color: 'var(--text-muted)', textAlign: 'center', padding: 40 }}>Building today's queue…</div>}

      {phase === 'error' && (
        <div style={{ maxWidth: 720, margin: '0 auto', color: '#e5484d', fontSize: 13 }}>
          {error}
          <div><button className="btn btn-sm" style={{ marginTop: 10 }} onClick={() => start(scope)}>Retry</button></div>
        </div>
      )}

      {phase === 'review' && current && (
        <Card p={current}>
          {!revealed ? (
            <div style={{ display: 'flex', gap: 10, alignItems: 'center', marginTop: 22, flexWrap: 'wrap' }}>
              <button className="btn btn-accent" onClick={() => setRevealed(true)}>Show answer <span className="kbd" style={{ marginLeft: 6, opacity: 0.8 }}>space</span></button>
              <button className="btn" disabled={busy} onClick={() => grade('easy', 'O')} title="You know this perfectly — recorded as Easy without revealing">
                O · I know this
              </button>
            </div>
          ) : (
            <>
              <div style={answerBox}>{current.answer}</div>
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 8, marginTop: 16 }}>
                {GRADES.map((g, i) => (
                  <button key={g} disabled={busy} onClick={() => grade(g)} style={{
                    padding: '10px 8px', borderRadius: 'var(--radius-md)', cursor: 'pointer', textAlign: 'center',
                    border: `1px solid ${GRADE_COLOR[g]}66`, background: `${GRADE_COLOR[g]}14`, color: GRADE_COLOR[g],
                  }}>
                    <div style={{ fontSize: 14, fontWeight: 700 }}>{GRADE_LABEL[g]} <span style={{ opacity: 0.6, fontWeight: 500, fontSize: 11 }}>{i + 1}</span></div>
                    <div style={{ fontSize: 11, marginTop: 3, color: 'var(--text-muted)' }}>{GRADE_HINT[g]}</div>
                  </button>
                ))}
              </div>
              <div style={{ marginTop: 10, fontSize: 11, color: 'var(--text-muted)' }}>
                stability {current.stability.toFixed(1)}d · predicted recall {Math.round(current.retrievability * 100)}% · {current.reviews} reviews · {current.lapses} lapses
              </div>
            </>
          )}
        </Card>
      )}

      {phase === 'relearn' && current && (
        <>
          <div style={{ maxWidth: 720, width: '100%', margin: '0 auto 10px', fontSize: 12, color: 'var(--text-muted)' }}>
            Once more · {index + 1} / {relearn.length} — not recorded; the real retest is tomorrow
          </div>
          <Card p={current}>
            {!revealed ? (
              <div style={{ marginTop: 22 }}><button className="btn btn-accent" onClick={() => setRevealed(true)}>Show answer <span className="kbd" style={{ marginLeft: 6, opacity: 0.8 }}>space</span></button></div>
            ) : (
              <>
                <div style={answerBox}>{current.answer}</div>
                <div style={{ marginTop: 16 }}><button className="btn" onClick={relearnNext}>Next <span className="kbd" style={{ marginLeft: 6, opacity: 0.8 }}>space</span></button></div>
              </>
            )}
          </Card>
        </>
      )}

      {phase === 'done' && session && (
        <div style={{ maxWidth: 720, width: '100%', margin: '0 auto', border: '1px solid var(--border-subtle)', borderRadius: 'var(--radius-lg, 12px)', background: 'var(--bg-secondary)', padding: '22px 26px' }}>
          {total === 0 ? (
            <>
              <div style={{ fontSize: 18, fontWeight: 700, color: 'var(--text-primary)' }}>
                {session.remaining === 0 ? `Daily cap of ${session.dailyCap} reached` : 'Nothing due today'}
              </div>
              <div style={{ fontSize: 13, color: 'var(--text-muted)', marginTop: 8, lineHeight: 1.6 }}>
                {session.remaining === 0
                  ? `${session.dueTotal} more are due but wait for tomorrow — the cap is what keeps the load steady.`
                  : 'New prompts come due four days after their card is written. Write a note in chat, or say "quiz me" and pick v2.'}
                {session.parkedTotal > 0 && <div>{session.parkedTotal} parked prompts are waiting for a lighter day.</div>}
              </div>
            </>
          ) : (
            <>
              <div style={{ fontSize: 18, fontWeight: 700, color: 'var(--text-primary)', marginBottom: 12 }}>Session done</div>
              <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap', marginBottom: 14 }}>
                {tally.map(([g, n]) => (
                  <div key={g} style={{ padding: '8px 14px', borderRadius: 'var(--radius-md)', background: `${GRADE_COLOR[g]}14`, border: `1px solid ${GRADE_COLOR[g]}55`, color: GRADE_COLOR[g], minWidth: 80 }}>
                    <div style={{ fontSize: 20, fontWeight: 700 }}>{n}</div>
                    <div style={{ fontSize: 11 }}>{MARK[g]} {GRADE_LABEL[g].toLowerCase()}</div>
                  </div>
                ))}
              </div>
              <div style={{ fontSize: 13, color: 'var(--text-secondary)', lineHeight: 1.7 }}>
                <div>Asked {askedToday} / {cap} today.</div>
                {session.overflow > 0 && <div>{session.overflow} due prompts did not fit the cap{session.parkedNow > 0 ? `; ${session.parkedNow} of them were below 50% recall and were parked` : ''}.</div>}
                {session.unparked > 0 && <div>{session.unparked} parked prompts were pulled back in today.</div>}
                {Object.values(outcomes).some(o => o.leech) && <div style={{ color: '#e5484d' }}>A prompt was suspended as a leech — rewrite it on its card and restart its schedule.</div>}
              </div>
              <div style={{ display: 'flex', gap: 8, marginTop: 16 }}>
                <button className="btn btn-sm" onClick={() => start(scope)}>Check for more</button>
              </div>
            </>
          )}
        </div>
      )}

      <div style={{ maxWidth: 720, width: '100%', margin: '18px auto 0', fontSize: 11, color: 'var(--text-muted)', textAlign: 'center' }}>
        space · reveal &nbsp; 1–4 · grade &nbsp; O · I know this
      </div>
    </div>
  );
}
