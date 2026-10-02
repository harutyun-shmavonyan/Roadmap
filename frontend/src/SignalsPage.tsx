import { useState, useEffect, useCallback, useMemo } from 'react';
import { marked } from 'marked';
import type { SignalRunDto, SignalRunSummaryDto, SignalDto, SignalEvidence, SignalMarket, SignalWatchItem } from './types';
import { SIGNAL_STATUSES } from './types';
import { api } from './api';

// "2026-09-30" -> "30.09.2026"
function fmtDate(iso: string): string {
  const [y, m, d] = iso.split('-');
  return d && m && y ? `${d}.${m}.${y}` : iso;
}
function weekday(iso: string): string {
  const t = Date.parse(`${iso}T12:00:00Z`);
  return Number.isNaN(t) ? '' : new Date(t).toLocaleDateString(undefined, { weekday: 'short' });
}
const isNum = (x: unknown): x is number => typeof x === 'number' && Number.isFinite(x);
const pct = (x: unknown, d = 1): string => isNum(x) ? `${x > 0 ? '+' : x < 0 ? '−' : ''}${Math.abs(x * 100).toFixed(d)}%` : '–';
const num = (x: unknown, d = 1): string => isNum(x) ? x.toFixed(d) : '–';
const md = (src: string): string => marked.parse(src) as string;

const SCREEN_NAME: Record<string, string> = { A: 'Sector panic', B: 'Index panic', C: 'Cyclical upturn' };
const SCREEN_COLOR: Record<string, string> = { A: 'var(--danger)', B: 'var(--warning)', C: 'var(--success)' };
const STATUS_LABEL: Record<string, string> = { new: 'New', reviewed: 'Reviewed', acted: 'Acted on', dismissed: 'Dismissed' };

/* ─── small pieces ─── */

// The dot in the day strip: red = something fired, amber = a degraded or failed run, green = quiet.
function dotColor(r: SignalRunSummaryDto): string {
  if (r.signalCount > 0) return 'var(--danger)';
  if (r.status !== 'OK' || !r.isTradingDay) return 'var(--warning)';
  return 'var(--success)';
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div style={{ marginTop: 14 }}>
      <div style={{ fontSize: 11.5, fontWeight: 700, letterSpacing: 0.4, textTransform: 'uppercase', color: 'var(--text-muted)', marginBottom: 6 }}>{title}</div>
      {children}
    </div>
  );
}

function Prose({ src }: { src: string }) {
  return <div className="md-body" style={{ fontSize: 14, lineHeight: 1.6, color: 'var(--text-primary)' }}
    dangerouslySetInnerHTML={{ __html: md(src) }} />;
}

// One line per market: the index, the day, the distance from the high, the fear gauge, breadth, credit.
function MarketLine({ markets }: { markets: Record<string, SignalMarket> | null }) {
  if (!markets) return null;
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 4, fontSize: 13, color: 'var(--text-secondary)' }}>
      {Object.entries(markets).map(([mk, m]) => (
        <div key={mk}>
          <b style={{ color: 'var(--text-primary)' }}>{mk}</b>{' '}
          {m.benchmark} {num(m.close, 2)} ({pct(m.ret_1d)} today · {pct(m.dd_252d)} from 52w high)
          {' · '}{m.vol_name ?? 'VIX'} {num(m.vol)}
          {isNum(m.breadth) && <> · breadth {pct(m.breadth, 0).replace('+', '')} above 200d</>}
          {isNum(m.hy_oas) && <> · HY spread {num(m.hy_oas, 2)} ({isNum(m.hy_oas_20d_change) ? `${m.hy_oas_20d_change > 0 ? '+' : ''}${m.hy_oas_20d_change.toFixed(2)}` : '–'} 20d)</>}
        </div>
      ))}
    </div>
  );
}

function EvidenceTable({ rows }: { rows: SignalEvidence[] }) {
  if (!rows.length) return null;
  return (
    <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 13 }}>
      <thead>
        <tr style={{ color: 'var(--text-muted)', textAlign: 'left' }}>
          <th style={{ padding: '4px 6px', fontWeight: 600 }}>Metric</th>
          <th style={{ padding: '4px 6px', fontWeight: 600 }}>Value</th>
          <th style={{ padding: '4px 6px', fontWeight: 600 }}>Rule</th>
          <th style={{ padding: '4px 6px' }} />
        </tr>
      </thead>
      <tbody>
        {rows.map((e, i) => (
          <tr key={i} style={{ borderTop: '1px solid var(--border-subtle)' }}>
            <td style={{ padding: '5px 6px' }}>{e.label}{e.note && <div style={{ fontSize: 11.5, color: 'var(--text-muted)' }}>{e.note}</div>}</td>
            <td style={{ padding: '5px 6px', fontFamily: 'var(--font-mono)', whiteSpace: 'nowrap' }}>{e.value}</td>
            <td style={{ padding: '5px 6px', color: 'var(--text-secondary)', whiteSpace: 'nowrap' }}>{e.threshold ?? ''}</td>
            <td style={{ padding: '5px 6px', textAlign: 'center' }}>
              {e.passes === true ? <span style={{ color: 'var(--success)' }}>✓</span>
                : e.passes === false ? <span style={{ color: 'var(--danger)' }}>✗</span> : ''}
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

// Which candidate columns matter depends on the screen; anything else the screener sent is left out.
type Col = { key: string; label: string; fmt: (v: unknown, row: Record<string, unknown>) => string };
const COLS: Record<string, Col[]> = {
  A: [
    { key: 'ticker', label: 'Ticker', fmt: v => String(v ?? '') },
    { key: 'dd_20d', label: 'dd 20d', fmt: v => pct(v, 0) },
    { key: 'z', label: 'z', fmt: v => num(v) },
    { key: 'rev_20d', label: 'rev 20d', fmt: v => pct(v) },
    { key: 'up_30d', label: 'up/down 30d', fmt: (v, r) => `${v ?? '–'}/${r.down_30d ?? '–'}` },
    { key: 'net_cash_to_mcap', label: 'net cash / mcap', fmt: v => pct(v, 0) },
    { key: 'interest_coverage', label: 'coverage', fmt: v => isNum(v) ? `${v.toFixed(0)}×` : '–' },
    { key: 'bystander_score', label: 'score', fmt: v => num(v) },
  ],
  C: [
    { key: 'ticker', label: 'Ticker', fmt: v => String(v ?? '') },
    { key: 'group_name', label: 'Group', fmt: v => String(v ?? '') },
    { key: 'fwd_pe', label: 'fwd P/E', fmt: v => num(v) },
    { key: 'rev_63d', label: 'rev 3m', fmt: v => pct(v) },
    { key: 'rev_21d', label: 'rev 1m', fmt: v => pct(v) },
    { key: 'rev_breadth_30d', label: 'breadth', fmt: v => pct(v, 0).replace('+', '') },
    { key: 'mom_12_1', label: 'mom 12-1', fmt: v => pct(v, 0) },
    { key: 'pct_of_high', label: '% of 52w high', fmt: v => pct(v, 0).replace('+', '') },
  ],
};

function CandidatesTable({ rows, screen }: { rows: Record<string, unknown>[]; screen: string }) {
  const [all, setAll] = useState(false);
  const cols = COLS[screen] ?? COLS.A;
  if (!rows.length) return null;
  const shown = all ? rows : rows.slice(0, 10);
  return (
    <div style={{ overflowX: 'auto' }}>
      <table style={{ borderCollapse: 'collapse', fontSize: 12.5, minWidth: '100%' }}>
        <thead>
          <tr style={{ color: 'var(--text-muted)', textAlign: 'left' }}>
            {cols.map(c => <th key={c.key} style={{ padding: '4px 6px', fontWeight: 600, whiteSpace: 'nowrap' }}>{c.label}</th>)}
          </tr>
        </thead>
        <tbody>
          {shown.map((r, i) => (
            <tr key={i} style={{ borderTop: '1px solid var(--border-subtle)' }}>
              {cols.map(c => <td key={c.key} style={{ padding: '4px 6px', whiteSpace: 'nowrap',
                fontFamily: c.key === 'ticker' || c.key === 'group_name' ? undefined : 'var(--font-mono)',
                fontWeight: c.key === 'ticker' ? 600 : undefined }}>{c.fmt(r[c.key], r)}</td>)}
            </tr>
          ))}
        </tbody>
      </table>
      {rows.length > 10 && (
        <button className="btn btn-ghost btn-sm" onClick={() => setAll(a => !a)} style={{ marginTop: 4 }}>
          {all ? 'Show fewer' : `Show all ${rows.length}`}
        </button>
      )}
    </div>
  );
}

/* ─── one signal ─── */

function SignalCard({ s, runDate, onSaved }: { s: SignalDto; runDate: string; onSaved: (next: SignalDto) => void }) {
  const [notes, setNotes] = useState(s.notes ?? '');
  const [busy, setBusy] = useState(false);
  useEffect(() => { setNotes(s.notes ?? ''); }, [s.id, s.notes]);

  const save = async (patch: { status?: string; notes?: string }) => {
    setBusy(true);
    try { onSaved(await api.updateSignal(s.id, patch)); } finally { setBusy(false); }
  };
  const notesDirty = (s.notes ?? '') !== notes;

  return (
    <div style={{ border: '1px solid var(--border)', borderRadius: 'var(--radius-md)', background: 'var(--bg-elevated)',
      padding: '14px 16px', marginBottom: 14, borderLeft: `4px solid ${SCREEN_COLOR[s.screen] ?? 'var(--accent)'}` }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
        <span style={{ fontSize: 11, fontWeight: 700, padding: '2px 7px', borderRadius: 999, color: '#fff',
          background: SCREEN_COLOR[s.screen] ?? 'var(--accent)' }}>{s.screen} · {SCREEN_NAME[s.screen] ?? s.screen}</span>
        <span style={{ fontSize: 15.5, fontWeight: 700, color: 'var(--text-primary)' }}>{s.title}</span>
        {s.regime && <span style={{ fontSize: 11.5, color: 'var(--text-muted)' }}>regime {s.regime}</span>}
        <span style={{ marginLeft: 'auto', fontSize: 11.5, fontWeight: 600, padding: '2px 8px', borderRadius: 999,
          background: s.status === 'acted' ? 'var(--success-light)' : s.status === 'dismissed' ? 'var(--bg-hover)' : 'var(--accent-light)',
          color: s.status === 'acted' ? 'var(--success)' : s.status === 'dismissed' ? 'var(--text-muted)' : 'var(--action-text)' }}>
          {STATUS_LABEL[s.status] ?? s.status}
        </span>
      </div>

      {s.headline && <div style={{ marginTop: 10, fontSize: 14.5, fontWeight: 600, lineHeight: 1.5, color: 'var(--text-primary)' }}>{s.headline}</div>}

      <Section title="Evidence"><EvidenceTable rows={s.evidence} /></Section>

      {s.thesis && <Section title="Why this makes sense"><Prose src={s.thesis} /></Section>}

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(260px, 1fr))', gap: 14 }}>
        {s.horizon && <Section title={`Horizon${s.horizonDays ? ` · up to ${s.horizonDays} trading days` : ''}`}>
          <div style={{ fontSize: 13.5, lineHeight: 1.55, color: 'var(--text-primary)' }}>{s.horizon}</div>
        </Section>}
        {s.proposal && <Section title="Proposal">
          <div style={{ fontSize: 13.5, lineHeight: 1.55, color: 'var(--text-primary)' }}>{s.proposal}</div>
        </Section>}
      </div>

      {s.invalidation && <Section title="What would prove it wrong">
        <div style={{ fontSize: 13.5, lineHeight: 1.55, color: 'var(--text-primary)' }}>{s.invalidation}</div>
      </Section>}

      {s.candidates.length > 0 && <Section title={`Candidates · ${s.candidates.length}`}>
        <CandidatesTable rows={s.candidates} screen={s.screen} />
      </Section>}

      <Section title="Triage">
        {s.triageSummary ? (
          <>
            <Prose src={s.triageSummary} />
            <div style={{ fontSize: 11.5, color: 'var(--text-muted)', marginTop: 4 }}>
              Read of the public record{s.triageModel ? ` by ${s.triageModel}` : ''}{s.triagedAt ? ` · ${new Date(s.triagedAt).toLocaleString()}` : ''}
            </div>
          </>
        ) : (
          <div style={{ fontSize: 13, color: 'var(--text-muted)', lineHeight: 1.55 }}>
            Awaiting triage. Run the <code>screener-alert-triage</code> skill on <code>signals/{runDate}/{s.eventId}.json</code>;
            its verdict — contagion or impairment, the cleanest bystanders, what to leave alone — appears here.
          </div>
        )}
      </Section>

      <Section title="Your decision">
        <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
          {SIGNAL_STATUSES.map(st => (
            <button key={st} className={`btn btn-sm ${s.status === st ? 'btn-accent' : ''}`} disabled={busy || s.status === st}
              onClick={() => save({ status: st })}>{STATUS_LABEL[st]}</button>
          ))}
        </div>
        <textarea value={notes} onChange={e => setNotes(e.target.value)} rows={2} placeholder="Notes — what you did, what you are waiting for"
          style={{ width: '100%', marginTop: 8, fontSize: 13, padding: 8, borderRadius: 'var(--radius-sm)', border: '1px solid var(--border)',
            background: 'var(--bg-primary)', color: 'var(--text-primary)', resize: 'vertical', boxSizing: 'border-box' }} />
        {notesDirty && <button className="btn btn-sm btn-accent" disabled={busy} style={{ marginTop: 6 }}
          onClick={() => save({ notes })}>Save notes</button>}
      </Section>
    </div>
  );
}

/* ─── a quiet day ─── */

function QuietPanel({ run }: { run: SignalRunDto }) {
  const degraded = run.status !== 'OK';
  return (
    <div style={{ border: '1px solid var(--border)', borderRadius: 'var(--radius-md)', background: 'var(--bg-elevated)', padding: '22px 20px',
      borderLeft: `4px solid ${run.isTradingDay && !degraded ? 'var(--success)' : 'var(--warning)'}` }}>
      <div style={{ fontSize: 20, fontWeight: 700, color: 'var(--text-primary)' }}>
        {!run.isTradingDay ? 'Not a trading day' : 'No important signal'}
      </div>
      <div style={{ fontSize: 13.5, color: 'var(--text-secondary)', marginTop: 6, lineHeight: 1.55 }}>
        {!run.isTradingDay
          ? 'No market had prices for this date; the screens were skipped.'
          : degraded
            ? `Nothing crossed a trigger, but the run was ${run.status.toLowerCase()} — see Health below for what was missing.`
            : 'Every screen ran and nothing crossed a trigger. Nothing to do today.'}
      </div>
    </div>
  );
}

function WatchList({ items }: { items: SignalWatchItem[] }) {
  if (!items.length) return null;
  return (
    <Section title="Within reach of a trigger">
      <div style={{ fontSize: 13, color: 'var(--text-secondary)', lineHeight: 1.6 }}>
        {items.map((w, i) => (
          <div key={i}>
            [{w.market ?? 'US'}] <b style={{ color: 'var(--text-primary)' }}>{w.group}</b>
            {' · '}z {num(w.z)} · dd {pct(w.dd_20d, 0)}{isNum(w.rev) && <> · rev {pct(w.rev)}</>}{isNum(w.cutting) && <> · cutting {pct(w.cutting, 0).replace('+', '')}</>}
            {w.why_not ? <span style={{ color: 'var(--text-muted)' }}> — not a signal because {w.why_not}</span>
              : w.note && <span style={{ color: 'var(--text-muted)' }}> · {w.note}</span>}
          </div>
        ))}
      </div>
    </Section>
  );
}

/* ─── the page ─── */

export function SignalsPage() {
  const [runs, setRuns] = useState<SignalRunSummaryDto[]>([]);
  const [run, setRun] = useState<SignalRunDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [showReport, setShowReport] = useState(false);

  // Opening a day is reading it: the tick is set here, and the list row loses its unread mark.
  const markRead = useCallback(async (r: SignalRunDto) => {
    if (r.isRead) return;
    try {
      const s = await api.markSignalRunRead(r.id);
      setRuns(list => list.map(x => x.id === s.id ? s : x));
      setRun(cur => cur && cur.id === r.id ? { ...cur, isRead: true, readOn: s.readOn } : cur);
    } catch { /* the tick is a convenience; the day still shows */ }
  }, []);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const [list, latest] = await Promise.all([api.getSignalRuns(120), api.getLatestSignalRun().catch(() => null)]);
        if (cancelled) return;
        setRuns(list);
        setRun(latest);
        if (latest) void markRead(latest);
      } catch {
        if (!cancelled) setError('Could not load the signal runs.');
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => { cancelled = true; };
  }, [markRead]);

  const selectDay = useCallback(async (date: string) => {
    if (date === run?.runDate) return;
    setLoading(true);
    try {
      const r = await api.getSignalRun(date);
      setRun(r);
      setShowReport(false);
      void markRead(r);
    } catch {
      setError(`Could not load ${date}.`);
    } finally {
      setLoading(false);
    }
  }, [run?.runDate, markRead]);

  const patchSignal = useCallback((next: SignalDto) => {
    setRun(r => r && ({ ...r, signals: r.signals.map(s => s.id === next.id ? next : s) }));
  }, []);

  const strip = useMemo(() => runs.slice(0, 14), [runs]);
  const unread = runs.filter(r => !r.isRead).length;

  if (loading && !run) return <div style={{ padding: 24, color: 'var(--text-muted)' }}>Loading...</div>;
  if (error) return <div style={{ padding: 24, color: 'var(--danger)' }}>{error}</div>;

  if (!run) return (
    <div style={{ padding: 32, maxWidth: 560, color: 'var(--text-muted)', fontSize: 14, lineHeight: 1.7 }}>
      <div style={{ fontSize: 16, fontWeight: 600, color: 'var(--text-primary)', marginBottom: 8 }}>📈 No runs yet</div>
      The market screener publishes every run here after the US close — the quiet days too, so a day with nothing to
      do says so. Signals arrive with the rule's evidence and reasoning; the triage skill adds its reading afterwards.
    </div>
  );

  return (
    <div style={{ height: '100%', overflowY: 'auto', WebkitOverflowScrolling: 'touch' }}>
      {/* Day picker: a strip of recent days (dot = fired / degraded / quiet) and a select for the rest. */}
      <div style={{ position: 'sticky', top: 0, zIndex: 5, background: 'var(--bg-primary)', borderBottom: '1px solid var(--border-subtle)',
        padding: '10px 16px', display: 'flex', alignItems: 'center', gap: 10, flexWrap: 'wrap' }}>
        <select className="sprint-select" value={run.runDate} onChange={e => selectDay(e.target.value)} style={{ fontSize: 14, fontWeight: 600 }}>
          {runs.map(r => (
            <option key={r.id} value={r.runDate}>
              {fmtDate(r.runDate)} — {r.signalCount > 0 ? `${r.signalCount} signal${r.signalCount === 1 ? '' : 's'}` : r.isTradingDay ? 'quiet' : 'closed'}{r.isRead ? '' : ' •'}
            </option>
          ))}
        </select>
        <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
          {strip.map(r => (
            <button key={r.id} onClick={() => selectDay(r.runDate)} title={`${fmtDate(r.runDate)} — ${r.summary}`}
              style={{ display: 'flex', alignItems: 'center', gap: 5, padding: '3px 8px', borderRadius: 999, fontSize: 12, cursor: 'pointer',
                border: `1px solid ${r.runDate === run.runDate ? 'var(--accent)' : 'var(--border)'}`,
                background: r.runDate === run.runDate ? 'var(--accent-light)' : 'var(--bg-elevated)',
                color: 'var(--text-primary)', fontWeight: r.isRead ? 400 : 700 }}>
              <span style={{ width: 8, height: 8, borderRadius: 999, background: dotColor(r), display: 'inline-block' }} />
              {weekday(r.runDate)} {r.runDate.slice(8)}
            </button>
          ))}
        </div>
        {unread > 0 && <span style={{ marginLeft: 'auto', fontSize: 12, color: 'var(--text-muted)' }}>{unread} unread</span>}
      </div>

      <div style={{ padding: '16px 16px 32px', maxWidth: 960 }}>
        <div style={{ display: 'flex', alignItems: 'baseline', gap: 10, flexWrap: 'wrap', marginBottom: 10 }}>
          <div style={{ fontSize: 18, fontWeight: 700, color: 'var(--text-primary)' }}>{weekday(run.runDate)} {fmtDate(run.runDate)}</div>
          <div style={{ fontSize: 12.5, color: 'var(--text-muted)' }}>
            run {run.status.toLowerCase()}{run.gitSha ? ` · ${run.gitSha.slice(0, 7)}` : ''} · published {new Date(run.publishedAt).toLocaleString()}
          </div>
        </div>

        {run.signals.length === 0 && <QuietPanel run={run} />}

        {/* The day in words comes first; the numbers it was written from sit below it for anyone who wants them. */}
        {run.briefing && (
          <div style={{ marginTop: 14, padding: '14px 16px', border: '1px solid var(--border-subtle)', borderRadius: 'var(--radius-md)',
            background: 'var(--bg-elevated)' }}>
            <Prose src={run.briefing} />
          </div>
        )}

        {run.signals.length > 0 && <div style={{ marginTop: 14 }}>
          {run.signals.map(s => <SignalCard key={s.id} s={s} runDate={run.runDate} onSaved={patchSignal} />)}
        </div>}

        {!run.briefing && <WatchList items={run.watch ?? []} />}

        <Section title="The numbers"><MarketLine markets={run.markets} /></Section>

        {run.warnings.length > 0 && (
          <Section title="Health">
            <ul style={{ margin: 0, paddingLeft: 18, fontSize: 12.5, color: 'var(--text-muted)', lineHeight: 1.6 }}>
              {run.warnings.map((w, i) => <li key={i}>{w}</li>)}
            </ul>
          </Section>
        )}

        {run.reportMarkdown && (
          <div style={{ marginTop: 16 }}>
            <button className="btn btn-ghost btn-sm" onClick={() => setShowReport(v => !v)}>{showReport ? 'Hide technical report' : 'Technical report'}</button>
            {showReport && <div style={{ marginTop: 8, padding: '12px 14px', border: '1px solid var(--border-subtle)', borderRadius: 'var(--radius-md)',
              background: 'var(--bg-secondary)', overflowX: 'auto' }}><Prose src={run.reportMarkdown} /></div>}
          </div>
        )}
      </div>
    </div>
  );
}
