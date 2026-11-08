import { useEffect, useMemo, useState } from 'react';
import { marked } from 'marked';
import type { SignalDto, SignalEvidence } from './types';
import { api } from './api';

/* The weekly thematic pipeline's card (screen T), drawn for a phone first.
   Everything is read from what Screener publishes; older runs without `context.lateness` fall back to parsing. */

type Kind = 'entry' | 'post_event_watch' | 'exit' | 'trim' | 're_underwrite';
type Verdict = 'early' | 'building' | 'late';

const md = (s: string) => marked.parse(s) as string;
const isNum = (x: unknown): x is number => typeof x === 'number' && Number.isFinite(x);
const tint = (c: string, p = 14) => `color-mix(in srgb, ${c} ${p}%, transparent)`;

const VERDICT: Record<Verdict, { color: string; label: string; hint: string }> = {
  early: { color: 'var(--success)', label: 'Early', hint: 'little of the theme is in the price yet' },
  building: { color: 'var(--warning)', label: 'Building', hint: 'part of the theme is priced in — smaller size' },
  late: { color: 'var(--danger)', label: 'Late', hint: 'the crowd is in — no entry' },
};
const KIND: Record<Kind, { label: string; color: string }> = {
  entry: { label: 'ENTRY', color: 'var(--accent)' },
  post_event_watch: { label: 'WATCH', color: 'var(--text-muted)' },
  exit: { label: 'EXIT', color: 'var(--danger)' },
  trim: { label: 'TRIM', color: 'var(--warning)' },
  re_underwrite: { label: 'RE-UNDERWRITE', color: 'var(--warning)' },
};
const STATUS: Record<string, Record<string, string>> = {
  entry: { new: 'Undecided', acted: 'Entered', reviewed: 'Watching', dismissed: 'Discarded' },
  post_event_watch: { new: 'Undecided', acted: 'Noted', reviewed: 'Watching', dismissed: 'Dismissed' },
  exit: { new: 'Undecided', acted: 'Sold', reviewed: 'Seen', dismissed: 'Holding (override)' },
  trim: { new: 'Undecided', acted: 'Trimmed', reviewed: 'Seen', dismissed: 'Holding (override)' },
  re_underwrite: { new: 'Undecided', acted: 'Re-underwritten', reviewed: 'Seen', dismissed: 'Held without review' },
};
const zone = (v: number) => (v < 35 ? VERDICT.early.color : v > 65 ? VERDICT.late.color : VERDICT.building.color);
const fmtDate = (iso?: string | null) => (iso && /^\d{4}-\d{2}-\d{2}/.test(iso) ? `${iso.slice(8, 10)}.${iso.slice(5, 7)}.${iso.slice(0, 4)}` : iso ?? '');

function kindOf(s: SignalDto): Kind {
  const m = s.eventId.match(/_(entry|post_event_watch|exit|trim|re_underwrite)$/);
  return (m?.[1] as Kind) ?? 'entry';
}

type Lateness = {
  verdict?: Verdict; routine?: number; baseline?: number; components: Record<string, { value: number; baseline?: number }>;
  rel6m?: number; stock6m?: number; etf6m?: number; etf?: string; pePct?: number; peMetric?: string;
  revisions?: string; change90d?: number; attention?: number | null; reasoning?: string;
};

function readLateness(s: SignalDto, thesisLateness: string): Lateness {
  const ctx = (s.context ?? {}) as Record<string, any>;
  const L = (ctx.lateness ?? {}) as Record<string, any>;
  const raw = (L.raw ?? {}) as Record<string, any>;
  const comps: Lateness['components'] = {};
  for (const e of s.evidence) {
    const k = String(e.label);
    if (['run-up', 'valuation', 'revisions', 'attention'].includes(k)) {
      const b = Number(String(e.note ?? '').replace(/[^\d.]/g, ''));
      comps[k] = { value: Number(e.value), baseline: Number.isFinite(b) && e.note ? b : undefined };
    }
  }
  const h = s.headline ?? '';
  const num = (re: RegExp) => { const m = h.match(re); return m ? Number(m[1].replace('−', '-')) : undefined; };
  const verdict = (L.verdict ?? s.title.match(/\b(early|building|late)\b/)?.[1]) as Verdict | undefined;
  return {
    verdict, components: comps,
    routine: L.routine ?? ctx.routine_composite, baseline: L.baseline ?? ctx.baseline_composite,
    rel6m: isNum(raw.rel_6m) ? raw.rel_6m * 100 : num(/([+\-−]?\d+)% 6m rel/),
    stock6m: isNum(raw.stock_6m) ? raw.stock_6m * 100 : undefined, etf6m: isNum(raw.etf_6m) ? raw.etf_6m * 100 : undefined,
    etf: raw.sector_etf, pePct: raw.valuation_pct ?? num(/pct ([\d.]+)/), peMetric: raw.valuation_metric,
    revisions: raw.revisions ?? h.match(/revisions ([a-z]+:\d+)/)?.[1], change90d: raw.change_90d,
    attention: raw.attention_ratio ?? null, reasoning: L.reasoning ?? thesisLateness,
  };
}

// Plain-language lines for the four lateness inputs.
function sentences(l: Lateness): { key: string; text: string; value?: number }[] {
  const out: { key: string; text: string; value?: number }[] = [];
  if (isNum(l.rel6m)) {
    const r = Math.round(l.rel6m), vs = l.etf ? `its sector fund (${l.etf})` : 'its sector fund';
    const detail = isNum(l.stock6m) && isNum(l.etf6m) ? ` — stock ${l.stock6m > 0 ? '+' : ''}${Math.round(l.stock6m)}%, fund ${l.etf6m > 0 ? '+' : ''}${Math.round(l.etf6m)}%` : '';
    out.push({ key: 'run-up', value: l.components['run-up']?.value,
      text: r < 0 ? `Lagged ${vs} by ${-r}% over 6 months${detail}` : `Beat ${vs} by ${r}% over 6 months${detail} — part of the move may be priced in` });
  }
  if (isNum(l.pePct)) {
    const p = Math.round(l.pePct), metric = l.peMetric?.startsWith('price_to_sales') ? 'price/sales' : 'P/E';
    out.push({ key: 'valuation', value: l.components.valuation?.value,
      text: p <= 50 ? `Cheaper than ${100 - p}% of its own history (${metric})` : `More expensive than ${p}% of its own history (${metric})` });
  }
  if (l.revisions) {
    const [d, days] = l.revisions.split(':'); const n = Number(days);
    const chg = isNum(l.change90d) ? ` (${l.change90d > 0 ? '+' : ''}${Math.round(l.change90d * 100)}% in 90 days)` : '';
    out.push({ key: 'revisions', value: l.components.revisions?.value,
      text: d === 'up' ? (n > 0 ? `Analysts raising estimates for ~${n} days${chg}` : `Estimates edging up${chg}`)
        : d === 'down' ? `Analysts cutting estimates for ~${n} days${chg}` : `Estimates flat${chg}` });
  }
  out.push({ key: 'attention', value: isNum(l.attention) ? l.components.attention?.value : undefined,
    text: isNum(l.attention) ? `News buzz ${l.attention.toFixed(1)}× its yearly average` : 'News buzz unknown this week (GDELT unavailable)' });
  return out;
}

function parseThesis(t: string) {
  const [body, rest = ''] = t.split('\n\n**Causal chain:**');
  const [chain, lateness = ''] = rest.split('\n\n**Lateness:**');
  return { body: body.trim(), chain: chain.trim(), lateness: lateness.trim() };
}

function parseKills(text: string | null) {
  return (text ?? '').split('\n').filter(Boolean).map(line => {
    const m = line.match(/^\d+\.\s*(.*?) — metric (.*?), threshold (.*?) for (\d+) period\(s\); source (.*?); checked (\w+)/);
    return m ? { statement: m[1], metric: m[2], threshold: m[3], periods: Number(m[4]), source: m[5], cadence: m[6] }
      : { statement: line.replace(/^\d+\.\s*/, '') };
  });
}

const CONF: Record<string, string> = { high: 'var(--success)', medium: 'var(--warning)', low: 'var(--danger)' };

/* ─── small pieces ─── */

function EntryPrices({ pd, stop }: { pd: Record<string, any>; stop?: string }) {
  const ref = isNum(pd.reference_close) ? pd.reference_close : undefined;
  const cur = pd.currency === 'EUR' ? '€' : '$';
  const pct = (x: number) => (ref ? `${x >= ref ? '+' : '−'}${Math.abs((x / ref - 1) * 100).toFixed(1)}% vs last close` : '');
  const row = (label: string, value: number | undefined, note: string, strong = false) => value === undefined ? null : (
    <div style={{ display: 'flex', justifyContent: 'space-between', gap: 10, padding: '4px 0', borderTop: '1px solid var(--border-subtle)' }}>
      <span style={{ color: 'var(--text-secondary)' }}>{label}</span>
      <span style={{ textAlign: 'right' }}><b style={{ fontSize: strong ? 16 : 14, color: 'var(--text-primary)' }}>{cur}{value.toFixed(2)}</b>
        <span style={{ display: 'block', fontSize: 11.5, color: 'var(--text-muted)' }}>{note}</span></span>
    </div>
  );
  return (
    <div style={{ marginTop: 10, padding: '8px 12px', borderRadius: 'var(--radius-md)', background: tint('var(--accent)', 8),
      border: `1px solid ${tint('var(--accent)', 25)}`, fontSize: 13.5 }}>
      <div style={{ fontSize: 12, fontWeight: 700, letterSpacing: 0.4, textTransform: 'uppercase', color: 'var(--text-muted)', marginBottom: 4 }}>Entry prices</div>
      {row('Last close', ref, pd.reference_date ? `on ${fmtDate(pd.reference_date)}` : '')}
      {row('Suggested limit', pd.suggested_limit ?? pd.range_low, `${pct(pd.suggested_limit ?? pd.range_low)} · GTC limit order`, true)}
      {row('Highest price allowed', pd.range_high, `${pct(pd.range_high)} · never above this`)}
      <div style={{ paddingTop: 6, borderTop: '1px solid var(--border-subtle)', color: 'var(--text-primary)' }}>
        Size <b>{pd.size_pct}%</b> of portfolio · hold up to {pd.horizon_quarters ?? 3} quarters{stop ? ` (time stop ${fmtDate(stop)})` : ''}
      </div>
    </div>
  );
}

function Chip({ text, color, solid, title }: { text: string; color: string; solid?: boolean; title?: string }) {
  return <span title={title} style={{ fontSize: 11.5, fontWeight: 700, padding: '2px 8px', borderRadius: 999, whiteSpace: 'nowrap',
    color: solid ? '#fff' : color, background: solid ? color : tint(color), border: `1px solid ${tint(color, 35)}` }}>{text}</span>;
}

function Block({ title, children, open = false, hint }: { title: string; children: React.ReactNode; open?: boolean; hint?: string }) {
  return (
    <details open={open} style={{ marginTop: 12, borderTop: '1px solid var(--border-subtle)', paddingTop: 10 }}>
      <summary style={{ cursor: 'pointer', fontSize: 12, fontWeight: 700, letterSpacing: 0.4, textTransform: 'uppercase',
        color: 'var(--text-muted)', listStyle: 'revert' }}>{title}{hint && <span style={{ textTransform: 'none', fontWeight: 400,
          letterSpacing: 0, marginLeft: 6 }}>— {hint}</span>}</summary>
      <div style={{ marginTop: 8 }}>{children}</div>
    </details>
  );
}

function LatenessBars({ l }: { l: Lateness }) {
  const names: Record<string, string> = { 'run-up': 'Run-up', valuation: 'Valuation', revisions: 'Revisions', attention: 'Attention' };
  return (
    <div style={{ display: 'grid', gap: 8 }}>
      {Object.entries(names).map(([k, label]) => {
        const c = l.components[k]; if (!c) return null;
        const unknown = k === 'attention' && !isNum(l.attention);
        return (
          <div key={k}>
            <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 12.5 }}>
              <span style={{ color: 'var(--text-primary)', fontWeight: 600 }}>{label}</span>
              <span style={{ color: 'var(--text-muted)' }}>{unknown ? 'unknown — scored neutral 50' : `${Math.round(c.value)}${isNum(c.baseline) && c.baseline !== c.value ? ` · formula ${Math.round(c.baseline)}` : ''}`}</span>
            </div>
            <div style={{ position: 'relative', height: 8, borderRadius: 4, marginTop: 3,
              background: 'linear-gradient(90deg, var(--success) 0 35%, var(--warning) 35% 65%, var(--danger) 65% 100%)', opacity: 0.25 }} />
            <div style={{ position: 'relative', height: 0 }}>
              <div style={{ position: 'absolute', top: -8, left: 0, width: `${Math.max(2, Math.min(100, c.value))}%`, height: 8, borderRadius: 4,
                background: unknown ? 'var(--border)' : zone(c.value) }} />
              {isNum(c.baseline) && c.baseline !== c.value && <div title="formula (baseline)" style={{ position: 'absolute', top: -11,
                left: `calc(${Math.min(100, c.baseline)}% - 1px)`, width: 2, height: 14, background: 'var(--text-primary)' }} />}
            </div>
          </div>
        );
      })}
      <div style={{ fontSize: 11.5, color: 'var(--text-muted)' }}>0 = nothing priced in · green &lt; 35 early · amber 35–65 building · red &gt; 65 late · black tick = the fixed formula</div>
    </div>
  );
}

type Fact = SignalEvidence & { date?: string; direction?: string; entity?: string; magnitude?: string; confidence?: number;
  actor_type?: string; horizon?: string };

function Facts({ rows }: { rows: Fact[] }) {
  const [all, setAll] = useState(false);
  const shown = all ? rows : rows.slice(0, 5);
  const arrow = (d?: string) => d === 'up' ? { t: '▲', c: 'var(--success)' } : d === 'down' ? { t: '▼', c: 'var(--danger)' } : { t: '●', c: 'var(--text-muted)' };
  return (
    <div style={{ display: 'grid', gap: 12 }}>
      {shown.map((e, i) => {
        const [actor, claim] = String(e.label).split(' — ');
        const host = e.note && /^https?:\/\//.test(e.note) ? new URL(e.note).hostname.replace(/^www\./, '') : null;
        const primary = e.threshold === 'Tier A';
        const a = arrow(e.direction);
        return (
          <div key={i} style={{ borderLeft: `3px solid ${primary ? 'var(--accent)' : 'var(--border)'}`, paddingLeft: 10 }}>
            <div style={{ display: 'flex', gap: 6, alignItems: 'center', flexWrap: 'wrap' }}>
              <span title={e.direction ?? ''} style={{ color: a.c, fontSize: 12 }}>{a.t}</span>
              <b style={{ fontSize: 13.5, color: 'var(--text-primary)' }}>{actor}</b>
              {claim && <Chip text={claim} color="var(--accent)" />}
              <Chip text={primary ? 'primary source' : 'news / secondary'} color={primary ? 'var(--success)' : 'var(--text-muted)'}
                title={primary ? 'Tier A: the company, regulator or industry body itself said it' : 'Tier B: a journalist or aggregator'} />
              {e.date && <span style={{ fontSize: 12, color: 'var(--text-muted)' }}>{fmtDate(e.date)}</span>}
            </div>
            {(e.magnitude || e.entity) && <div style={{ marginTop: 3, fontSize: 13.5, color: 'var(--text-primary)' }}>
              {e.magnitude && <b>{e.magnitude}</b>}{e.magnitude && e.entity ? ' · ' : ''}{e.entity && <span style={{ color: 'var(--text-secondary)' }}>{e.entity}</span>}
              {e.horizon && <span style={{ color: 'var(--text-muted)' }}> · {e.horizon}</span>}</div>}
            <div style={{ fontSize: 13.5, lineHeight: 1.5, color: 'var(--text-secondary)', marginTop: 3, fontStyle: 'italic' }}>“{e.value}”</div>
            <div style={{ display: 'flex', gap: 10, alignItems: 'center', marginTop: 3, flexWrap: 'wrap' }}>
              {host && <a href={e.note!} target="_blank" rel="noreferrer" style={{ fontSize: 12, color: 'var(--text-muted)' }}>{host} ↗</a>}
              {isNum(e.confidence) && <span title="How sure the extraction is that this is a fact, not speculation" style={{ fontSize: 12, color: 'var(--text-muted)', display: 'inline-flex', alignItems: 'center', gap: 4 }}>
                fact confidence
                <span style={{ width: 40, height: 5, borderRadius: 3, background: 'var(--border)', display: 'inline-block', position: 'relative' }}>
                  <span style={{ position: 'absolute', inset: 0, width: `${e.confidence * 100}%`, borderRadius: 3, background: e.confidence >= 0.7 ? 'var(--success)' : 'var(--warning)' }} /></span>
                {Math.round(e.confidence * 100)}%</span>}
            </div>
          </div>
        );
      })}
      {rows.length > 5 && <button className="btn btn-ghost btn-sm" onClick={() => setAll(v => !v)} style={{ justifySelf: 'start' }}>
        {all ? 'Show fewer' : `Show all ${rows.length}`}</button>}
    </div>
  );
}

function CompanyDocs({ f }: { f: Record<string, any> }) {
  if (!f.available) return <div style={{ fontSize: 13, color: 'var(--text-muted)' }}>{f.why}</div>;
  const label: Record<string, string> = { annual: 'Annual report', quarterly: 'Quarterly report', results_release: 'Results release' };
  return (
    <div style={{ display: 'grid', gap: 8, fontSize: 13.5, color: 'var(--text-primary)', lineHeight: 1.5 }}>
      {f.exposure_note && <div><b>Exposure:</b> {f.exposure_note}</div>}
      {f.balance_sheet && <div><b>Balance sheet:</b> {f.balance_sheet}</div>}
      {f.outlook_quote && <div style={{ fontStyle: 'italic', color: 'var(--text-secondary)' }}>“{f.outlook_quote}”</div>}
      {f.risk_note && <div style={{ padding: '6px 8px', borderRadius: 'var(--radius-sm)', background: tint('var(--warning)', 12) }}><b>Risk:</b> {f.risk_note}</div>}
      {Array.isArray(f.documents) && <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap' }}>
        {f.documents.map((d: any, i: number) => d.url && <a key={i} href={d.url} target="_blank" rel="noreferrer" style={{ fontSize: 12.5 }}>
          {label[d.kind] ?? d.kind} · {d.form} {fmtDate(d.date)} ↗</a>)}</div>}
    </div>
  );
}

type Step = { step: string; detail: string; lag?: string | null; facts?: { actor: string; date?: string; magnitude?: string; url?: string; tier?: string }[] };

// The causal chain as a timeline: each link, the facts behind it, and the lag to the next one.
function ChainSteps({ steps, summary }: { steps: Step[]; summary?: string }) {
  return (
    <div style={{ marginTop: 10, padding: '10px 12px', borderRadius: 'var(--radius-md)', background: 'var(--bg-secondary)' }}>
      <div style={{ fontSize: 12, fontWeight: 700, letterSpacing: 0.4, textTransform: 'uppercase', color: 'var(--text-muted)' }}>Causal chain</div>
      {summary && <div style={{ fontSize: 12.5, color: 'var(--text-muted)', margin: '3px 0 8px', lineHeight: 1.45 }}>{summary}</div>}
      <ol style={{ listStyle: 'none', margin: 0, padding: 0 }}>
        {steps.map((st, i) => {
          const last = i === steps.length - 1;
          const isBreak = /break/i.test(st.step);
          const dot = isBreak ? 'var(--danger)' : st.facts && st.facts.length ? 'var(--accent)' : 'var(--warning)';
          return (
            <li key={i} style={{ display: 'grid', gridTemplateColumns: '22px 1fr', columnGap: 8 }}>
              <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center' }}>
                <span style={{ width: 20, height: 20, borderRadius: 999, background: dot, color: '#fff', fontSize: 11, fontWeight: 700,
                  display: 'flex', alignItems: 'center', justifyContent: 'center', flexShrink: 0 }}>{isBreak ? '!' : i + 1}</span>
                {!last && <span style={{ flex: 1, width: 2, background: 'var(--border)', minHeight: 12 }} />}
              </div>
              <div style={{ paddingBottom: last ? 0 : 10 }}>
                <div style={{ fontSize: 14, fontWeight: 700, color: isBreak ? 'var(--danger)' : 'var(--text-primary)' }}>{st.step}</div>
                <div style={{ fontSize: 13.5, lineHeight: 1.5, color: 'var(--text-secondary)', marginTop: 2 }}>{st.detail}</div>
                {st.facts && st.facts.length > 0 && <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap', marginTop: 5 }}>
                  {st.facts.map((f, j) => <a key={j} href={f.url} target="_blank" rel="noreferrer" title={f.magnitude ?? ''}
                    style={{ fontSize: 11.5, padding: '2px 7px', borderRadius: 999, textDecoration: 'none', border: '1px solid var(--border)',
                      background: 'var(--bg-elevated)', color: 'var(--text-secondary)' }}>
                    {f.tier === 'A' ? '● ' : '○ '}{f.actor}{f.date ? ` · ${fmtDate(f.date)}` : ''} ↗</a>)}
                </div>}
                {(!st.facts || st.facts.length === 0) && !isBreak && <div style={{ fontSize: 11.5, color: 'var(--warning)', marginTop: 4 }}>
                  the step this card is betting on — not yet in the data</div>}
                {st.lag && !last && <div style={{ display: 'inline-block', marginTop: 6, fontSize: 11.5, padding: '1px 8px', borderRadius: 999,
                  background: tint('var(--accent)', 12), color: 'var(--text-secondary)' }}>⏱ next step: {st.lag}</div>}
              </div>
            </li>
          );
        })}
      </ol>
      <div style={{ fontSize: 11.5, color: 'var(--text-muted)', marginTop: 6 }}>● primary source · ○ news · amber = the step the card bets on · ! = what would break it</div>
    </div>
  );
}

const QUESTIONS = [
  'Can I state the causal chain in one sentence without looking?',
  'Which actor said the key thing, and would that actor actually know?',
  'What has to be true in two quarters for this to work — and is that already the consensus number?',
  'What kills it, and will I genuinely see that signal in time?',
  'If it drops 30% next month with no kill condition hit, do I hold? If no, the size is too big or the thesis too weak.',
];

/* ─── the decision panel: plain actions that write the status and the notes the routine parses ─── */

function Decision({ s, kind, onSaved }: { s: SignalDto; kind: Kind; onSaved: (n: SignalDto) => void }) {
  const pd = (s.proposalDetail ?? {}) as Record<string, any>;
  const lo = isNum(pd.range_low) ? pd.range_low : undefined, hi = isNum(pd.range_high) ? pd.range_high : undefined;
  const size = isNum(pd.size_pct) ? pd.size_pct : undefined;
  const [busy, setBusy] = useState(false);
  const [mode, setMode] = useState<null | 'enter' | 'discard' | 'fill' | 'sold' | 'hold'>(null);
  const [price, setPrice] = useState(lo ? String(lo) : '');
  const [shares, setShares] = useState('');
  const [date, setDate] = useState(new Date().toISOString().slice(0, 10));
  const [reason, setReason] = useState('');
  const [notes, setNotes] = useState(s.notes ?? '');
  const [portfolio, setPortfolio] = useState(() => { try { return localStorage.getItem('themes.portfolio') ?? ''; } catch { return ''; } });
  useEffect(() => setNotes(s.notes ?? ''), [s.id, s.notes]);
  const savePortfolio = (v: string) => { setPortfolio(v); try { localStorage.setItem('themes.portfolio', v); } catch { /* convenience */ } };
  const p = Number(price), pv = Number(portfolio);
  const suggested = size && pv > 0 && p > 0 ? Math.floor((pv * size) / 100 / p) : null;
  const priceErr = kind === 'entry' && price && hi && p > hi ? `Above the card's range (max ${hi}). The card never chases — choose Watch instead.` : '';

  const save = async (patch: { status?: string; notes?: string }) => {
    setBusy(true);
    try { onSaved(await api.updateSignal(s.id, patch)); setMode(null); } finally { setBusy(false); }
  };
  const addLine = (line: string) => [notes.trim(), line].filter(Boolean).join('\n');
  const lab = STATUS[kind] ?? STATUS.entry;
  const input = { fontSize: 14, padding: '6px 8px', borderRadius: 'var(--radius-sm)', border: '1px solid var(--border)',
    background: 'var(--bg-primary)', color: 'var(--text-primary)', width: 120 } as const;
  const big = (label: string, sub: string, color: string, onClick: () => void, active = false) => (
    <button disabled={busy} onClick={onClick} style={{ flex: '1 1 150px', textAlign: 'left', padding: '10px 12px', cursor: 'pointer',
      borderRadius: 'var(--radius-md)', border: `1.5px solid ${active ? color : 'var(--border)'}`, background: active ? tint(color) : 'var(--bg-secondary)' }}>
      <div style={{ fontWeight: 700, fontSize: 14, color }}>{label}</div>
      <div style={{ fontSize: 12, color: 'var(--text-muted)', marginTop: 2 }}>{sub}</div>
    </button>
  );

  return (
    <div style={{ marginTop: 14, padding: 12, borderRadius: 'var(--radius-md)', background: 'var(--bg-secondary)', border: '1px solid var(--border-subtle)' }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 10 }}>
        <span style={{ fontSize: 12, fontWeight: 700, letterSpacing: 0.4, textTransform: 'uppercase', color: 'var(--text-muted)' }}>Your decision</span>
        <Chip text={lab[s.status] ?? s.status} color={s.status === 'acted' ? 'var(--success)' : s.status === 'dismissed' ? 'var(--text-muted)' : 'var(--accent)'} />
      </div>

      {kind === 'entry' && (s.status === 'acted' ? (
        <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
          {big('＋ Record a fill', 'price × shares, date — one per partial fill', 'var(--success)', () => setMode('fill'), mode === 'fill')}
          {big('Change the limit', 'note a new limit inside the range', 'var(--accent)', () => setMode('enter'), mode === 'enter')}
          {big('Undo', 'back to undecided', 'var(--text-muted)', () => save({ status: 'new' }))}
        </div>
      ) : (
        <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
          {big('✓ Enter', 'I placed (or will place) a GTC limit order', 'var(--success)', () => setMode('enter'), mode === 'enter')}
          {big('👁 Watch', 're-score it next week', 'var(--accent)', () => save({ status: 'reviewed' }), s.status === 'reviewed')}
          {big('✕ Discard', 'one-line reason, tracked against what it does', 'var(--danger)', () => setMode('discard'), mode === 'discard')}
        </div>
      ))}
      {kind === 'post_event_watch' && <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
        {big('👁 Keep watching', 'stays in view until the re-score date', 'var(--accent)', () => save({ status: 'reviewed' }), s.status === 'reviewed')}
        {big('✕ Dismiss', 'not interested', 'var(--text-muted)', () => setMode('discard'), mode === 'discard')}
      </div>}
      {(kind === 'exit' || kind === 'trim') && <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
        {big(kind === 'exit' ? '✓ Sold' : '✓ Trimmed', 'record each sale: price × shares, date', 'var(--success)', () => setMode('sold'), mode === 'sold')}
        {big('Hold (override)', 'reason required — overrides are evaluated', 'var(--warning)', () => setMode('hold'), mode === 'hold')}
      </div>}
      {kind === 're_underwrite' && <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
        {big('✓ Re-underwritten', 'I re-checked it; record a sale below if I sold', 'var(--success)', () => setMode('sold'), mode === 'sold')}
        {big('Hold without review', 'reason required', 'var(--warning)', () => setMode('hold'), mode === 'hold')}
      </div>}

      {(mode === 'enter') && (
        <div style={{ marginTop: 10, display: 'grid', gap: 8 }}>
          <label style={{ fontSize: 13 }}>Limit price {lo && hi && <span style={{ color: 'var(--text-muted)' }}>(range {lo}–{hi}; suggested {lo})</span>}<br />
            <input style={input} inputMode="decimal" value={price} onChange={e => setPrice(e.target.value)} /></label>
          {size && <label style={{ fontSize: 13 }}>Portfolio value (to size it, remembered on this device)<br />
            <input style={input} inputMode="decimal" value={portfolio} onChange={e => savePortfolio(e.target.value)} placeholder="e.g. 50000" />
            {suggested !== null && <span style={{ marginLeft: 8, color: 'var(--text-primary)' }}>→ <b>{suggested} shares</b> ({size}% of portfolio)</span>}</label>}
          {priceErr && <div style={{ color: 'var(--danger)', fontSize: 13 }}>{priceErr}</div>}
          <div><button className="btn btn-sm btn-accent" disabled={busy || !p || !!priceErr}
            onClick={() => save({ status: 'acted', notes: addLine(`limit ${p.toFixed(2)}`) })}>Save — order placed</button>
            <span style={{ marginLeft: 8, fontSize: 12, color: 'var(--text-muted)' }}>GTC limit in MyAmeria. Never a market order.</span></div>
        </div>
      )}
      {(mode === 'fill' || mode === 'sold') && (
        <div style={{ marginTop: 10, display: 'flex', gap: 8, flexWrap: 'wrap', alignItems: 'flex-end' }}>
          <label style={{ fontSize: 13 }}>Price<br /><input style={input} inputMode="decimal" value={price} onChange={e => setPrice(e.target.value)} /></label>
          <label style={{ fontSize: 13 }}>Shares<br /><input style={input} inputMode="decimal" value={shares} onChange={e => setShares(e.target.value)} /></label>
          <label style={{ fontSize: 13 }}>Date<br /><input style={input} type="date" value={date} onChange={e => setDate(e.target.value)} /></label>
          <button className="btn btn-sm btn-accent" disabled={busy || !p || !Number(shares)}
            onClick={() => save({ status: 'acted', notes: addLine(`${mode === 'fill' ? 'fill' : 'sold'} ${p.toFixed(2)} x ${Number(shares)} ${date}`) })}>
            Save {mode === 'fill' ? 'fill' : 'sale'}</button>
        </div>
      )}
      {(mode === 'discard' || mode === 'hold') && (
        <div style={{ marginTop: 10, display: 'flex', gap: 8, flexWrap: 'wrap', alignItems: 'flex-end' }}>
          <label style={{ fontSize: 13, flex: '1 1 240px' }}>Reason (one line)<br />
            <input style={{ ...input, width: '100%' }} value={reason} onChange={e => setReason(e.target.value)}
              placeholder={mode === 'discard' ? 'e.g. theme already in the price' : 'e.g. kill condition data looks like a one-off'} /></label>
          <button className="btn btn-sm" disabled={busy || reason.trim().length < 3}
            onClick={() => save({ status: 'dismissed', notes: addLine(reason.trim()) })}>Save</button>
        </div>
      )}

      <details style={{ marginTop: 10 }}>
        <summary style={{ cursor: 'pointer', fontSize: 12, color: 'var(--text-muted)' }}>Notes as the routine reads them</summary>
        <textarea value={notes} onChange={e => setNotes(e.target.value)} rows={3}
          style={{ width: '100%', marginTop: 6, fontSize: 13, padding: 8, borderRadius: 'var(--radius-sm)', border: '1px solid var(--border)',
            background: 'var(--bg-primary)', color: 'var(--text-primary)', boxSizing: 'border-box', fontFamily: 'var(--font-mono)' }} />
        <div style={{ fontSize: 11.5, color: 'var(--text-muted)' }}>One per line: <code>limit 77.88</code> · <code>fill 77.85 x 19 2026-11-10</code> · <code>sold 88.10 x 19 2026-12-02</code> · or a reason.</div>
        {(s.notes ?? '') !== notes && <button className="btn btn-sm btn-accent" style={{ marginTop: 6 }} disabled={busy} onClick={() => save({ notes })}>Save notes</button>}
      </details>
    </div>
  );
}

/* ─── the card ─── */

export function ThematicCard({ s, onSaved, onHelp }: { s: SignalDto; onSaved: (n: SignalDto) => void; onHelp: () => void }) {
  const kind = kindOf(s);
  const th = useMemo(() => parseThesis(s.thesis ?? ''), [s.thesis]);
  const l = useMemo(() => readLateness(s, th.lateness), [s, th.lateness]);
  const lines = useMemo(() => sentences(l), [l]);
  const kills = useMemo(() => parseKills(s.invalidation), [s.invalidation]);
  const facts = s.evidence.filter(e => !['run-up', 'valuation', 'revisions', 'attention'].includes(String(e.label)));
  const ctx = (s.context ?? {}) as Record<string, any>;
  const pd = (s.proposalDetail ?? {}) as Record<string, any>;
  const ticker = s.title.split(' — ')[1]?.split(' (')[0] ?? s.key;
  const ring = s.title.match(/ring (\d)/)?.[1];
  const v = l.verdict ? VERDICT[l.verdict] : null;
  const stop = s.horizon?.match(/(\d{4}-\d{2}-\d{2})/)?.[1];
  const [checked, setChecked] = useState<boolean[]>([false, false, false, false, false]);
  const conf = ctx.confidence as { score: number; band: string; parts: { name: string; points: number; max: number; note: string }[] } | undefined;

  return (
    <div style={{ border: '1px solid var(--border)', borderRadius: 'var(--radius-md)', background: 'var(--bg-elevated)', padding: '14px 16px',
      marginBottom: 16, borderLeft: `4px solid ${v?.color ?? KIND[kind].color}` }}>
      {/* who, what, how late */}
      <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
        <Chip text={KIND[kind].label} color={KIND[kind].color} solid />
        <span style={{ fontSize: 19, fontWeight: 800, color: 'var(--text-primary)' }}>{ticker}</span>
        {ring && <Chip text={ring === '2' ? 'Ring 2 · supplier' : 'Ring 1 · core business'} color="var(--text-secondary)"
          title={ring === '2' ? 'Second-order beneficiary: the market usually reacts later' : 'The theme is the company\'s main business'} />}
        {v && <Chip text={`${v.label}${isNum(l.routine) ? ` · ${Math.round(l.routine)}` : ''}`} color={v.color} solid title={`Lateness: ${v.hint}`} />}
        {conf && <Chip text={`Confidence ${conf.band} · ${conf.score}`} color={CONF[conf.band] ?? 'var(--text-muted)'}
          title="The routine's confidence that the thesis is well founded — not a probability of profit" />}
        <button onClick={onHelp} title="How to read a card" style={{ marginLeft: 'auto', border: '1px solid var(--border)', borderRadius: 999,
          width: 24, height: 24, cursor: 'pointer', background: 'var(--bg-secondary)', color: 'var(--text-muted)', fontWeight: 700 }}>?</button>
      </div>
      {ctx.theme && <div style={{ marginTop: 6, fontSize: 13.5, color: 'var(--text-secondary)' }}>
        {ctx.theme} {ctx.maturity && <Chip text={String(ctx.maturity)} color={ctx.maturity === 'consensus' || ctx.maturity === 'fading' ? 'var(--danger)' : ctx.maturity === 'building' ? 'var(--warning)' : 'var(--success)'}
          title="Theme maturity: emerging → building → consensus → fading" />}
      </div>}

      {/* the plan, in one line */}
      {kind === 'entry' && isNum(pd.range_low) && <EntryPrices pd={pd} stop={stop} />}
      {kind !== 'entry' && s.proposal && <div style={{ marginTop: 10, padding: '8px 10px', borderRadius: 'var(--radius-sm)',
        background: tint(KIND[kind].color, 10), fontSize: 14, color: 'var(--text-primary)' }}>{s.proposal}</div>}

      {/* the four lateness inputs in words */}
      <ul style={{ margin: '10px 0 0', paddingLeft: 0, listStyle: 'none', display: 'grid', gap: 4 }}>
        {lines.map(x => <li key={x.key} style={{ fontSize: 13.5, color: 'var(--text-secondary)', display: 'flex', gap: 8, alignItems: 'baseline' }}>
          <span style={{ width: 8, height: 8, borderRadius: 999, flexShrink: 0, background: isNum(x.value) ? zone(x.value) : 'var(--border)', display: 'inline-block' }} />
          {x.text}</li>)}
      </ul>

      {conf && <Block title="How confident?" hint={`${conf.score}/100 · ${conf.band} — confidence in the thesis, not a probability of profit`}>
        <div style={{ display: 'grid', gap: 7 }}>
          {conf.parts.map(p => <div key={p.name}>
            <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 12.5 }}>
              <span style={{ fontWeight: 600, color: 'var(--text-primary)' }}>{p.name}</span>
              <span style={{ color: 'var(--text-muted)' }}>{p.points} / {p.max}</span></div>
            <div style={{ height: 6, borderRadius: 3, background: 'var(--border)', marginTop: 3, position: 'relative' }}>
              <div style={{ position: 'absolute', inset: 0, width: `${(p.points / p.max) * 100}%`, borderRadius: 3,
                background: p.points / p.max >= 0.7 ? 'var(--success)' : p.points / p.max >= 0.4 ? 'var(--warning)' : 'var(--danger)' }} /></div>
            <div style={{ fontSize: 12, color: 'var(--text-muted)', marginTop: 2 }}>{p.note}</div>
          </div>)}
          <div style={{ fontSize: 11.5, color: 'var(--text-muted)' }}>High ≥ 80 · Medium 60–79 · Low &lt; 60. It does not change the size — sizes follow the risk rules.</div>
        </div>
      </Block>}

      <Block title="Why" open>
        <div className="md-body" style={{ fontSize: 14, lineHeight: 1.6, color: 'var(--text-primary)' }} dangerouslySetInnerHTML={{ __html: md(th.body) }} />
        {Array.isArray(ctx.chain_steps) && ctx.chain_steps.length > 0
          ? <ChainSteps steps={ctx.chain_steps} summary={th.chain} />
          : th.chain && <div style={{ marginTop: 8, padding: '8px 10px', borderRadius: 'var(--radius-sm)', background: 'var(--bg-secondary)', fontSize: 13.5, lineHeight: 1.55 }}>
            <b>Causal chain</b><br />{th.chain.split(' → ').map((step, i, a) => <span key={i}>{step}{i < a.length - 1 && <b style={{ color: 'var(--accent)' }}> → </b>}</span>)}</div>}
      </Block>

      <Block title="How late is it?" hint={isNum(l.routine) ? `${Math.round(l.routine)} by the routine, ${isNum(l.baseline) ? Math.round(l.baseline) : '–'} by the formula${ctx.disagreement ? ' — they disagree' : ''}` : undefined}>
        <LatenessBars l={l} />
        {l.reasoning && <div style={{ marginTop: 8, fontSize: 12.5, lineHeight: 1.5, color: 'var(--text-muted)' }}>{l.reasoning}</div>}
      </Block>

      {ctx.filings && <Block title="Company documents" hint="what the company's own filings say" open>
        <CompanyDocs f={ctx.filings} />
      </Block>}

      {facts.length > 0 && <Block title={`Evidence · ${facts.length}`} hint="the facts behind the theme — newest primary sources first">
        <Facts rows={facts as Fact[]} />
      </Block>}

      {kills.length > 0 && <Block title={`What would prove it wrong · ${kills.length}`} hint="checked every Saturday; one met → EXIT card">
        <ol style={{ margin: 0, paddingLeft: 18, display: 'grid', gap: 8 }}>
          {kills.map((k, i) => <li key={i} style={{ fontSize: 13.5, color: 'var(--text-primary)', lineHeight: 1.45 }}>{k.statement}
            {'source' in k && <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap', marginTop: 3 }}>
              <Chip text={`${k.threshold}${k.periods && k.periods > 1 ? ` · ${k.periods}× in a row` : ''}`} color="var(--danger)" />
              <Chip text={k.source!} color="var(--text-secondary)" />
              <Chip text={k.cadence!} color="var(--text-secondary)" /></div>}</li>)}
        </ol>
      </Block>}

      {s.candidates.length > 0 && <Block title={`Same theme · ${s.candidates.length}`} hint="the other companies and why they are not this card">
        <div style={{ display: 'grid', gap: 6 }}>
          {s.candidates.map((c: any, i) => <div key={i} style={{ fontSize: 13, display: 'flex', gap: 6, flexWrap: 'wrap', alignItems: 'baseline' }}>
            <b style={{ color: 'var(--text-primary)' }}>{c.ticker}</b>
            <Chip text={c.tradable === false ? 'not buyable' : c.verdict === 'verified' ? 'verified' : String(c.verdict)} color={c.tradable === false ? 'var(--text-muted)' : c.verdict === 'verified' ? 'var(--success)' : 'var(--warning)'} />
            {isNum(c.exposure) && <span style={{ color: 'var(--text-muted)' }}>{Math.round(c.exposure * 100)}% exposed</span>}
            {c.exposure_source && <span style={{ color: 'var(--text-muted)' }}>({c.exposure_source})</span>}
            <span style={{ color: 'var(--text-secondary)' }}>{c.reason}</span></div>)}
        </div>
      </Block>}

      <Block title="Before you decide" hint="answer without looking">
        <div style={{ display: 'grid', gap: 6 }}>
          {QUESTIONS.map((q, i) => <label key={i} style={{ display: 'flex', gap: 8, fontSize: 13.5, lineHeight: 1.45, color: 'var(--text-primary)', cursor: 'pointer' }}>
            <input type="checkbox" checked={checked[i]} onChange={() => setChecked(c => c.map((x, j) => (j === i ? !x : x)))} />{i + 1}. {q}</label>)}
        </div>
      </Block>

      <Decision s={s} kind={kind} onSaved={onSaved} />
    </div>
  );
}

/* ─── the glossary, one per Themes page ─── */

const TERMS: [string, string][] = [
  ['Card', 'One investment proposal, written in full before you decide: thesis, evidence, how late it is, entry range, size, what would prove it wrong, and a time stop. Its exit rules are frozen once you enter.'],
  ['ENTRY · WATCH · EXIT · TRIM · RE-UNDERWRITE', 'Buy proposal · good theme but already priced in, re-checked later · a kill condition was met or the time stop came, sell · the idea played out, sell half · down 25% with no kill condition met, look again (not a sell signal).'],
  ['Theme', 'A causal story backed by ≥ 3 facts from ≥ 2 sources, at least 2 of them primary (the company or regulator itself).'],
  ['Maturity', 'Emerging (only primary sources talk about it) → Building (trade press covers it; entries still allowed) → Consensus (mainstream headlines; no new entries) → Fading (newest facts point the other way).'],
  ['Ring 1 · Ring 2', 'Ring 1: the theme is the company\'s main business. Ring 2: a supplier, equipment, test, logistics or software link, or a competitor of whoever the theme hurts — the market usually notices these later.'],
  ['Lateness (0–100)', 'How much of the theme is already in the price: < 35 Early (full size), 35–65 Building (smaller size), > 65 Late (no entry, a WATCH card instead). Two numbers: the routine\'s judgement and a fixed formula kept as a yardstick.'],
  ['Run-up', 'The stock\'s last 6 months compared with its sector fund. Lagging the fund = less priced in.'],
  ['Valuation', 'Where today\'s P/E (or price/sales when there are no profits) sits in the company\'s own history. Low = cheap versus itself.'],
  ['Revisions', 'Whether analysts are raising or cutting earnings estimates, and since when. A fresh rise is early; months of rises are already noticed.'],
  ['Attention', 'How much more the company is in the news than its yearly average. Above 3× = crowded.'],
  ['Entry range', 'The prices you may buy at with a GTC limit order. Never above the top; the low end is suggested. Unfilled after 10 trading days → the card goes back to Watch.'],
  ['Size', 'Share of your whole portfolio: Early 3%, Building 2%, Ring 1 Building 1.5%. Max 6 open cards, max 2 per theme, total ≤ 23.75%.'],
  ['Kill condition', 'A published number that would prove the thesis wrong, with its source and how often it is checked. Checked every Saturday; one met → EXIT card. Price drops alone never close a card.'],
  ['Time stop', 'The date an unproven card closes regardless: 3 quarters after the fill.'],
  ['Primary source', 'Tier A: the company, regulator or industry body itself (a filing, a notice, a release) — not a journalist\'s guess.'],
  ['Your decision', 'Enter = order placed (add the limit, then each fill) · Watch = re-score next week · Discard = not for you (one-line reason; discards are tracked against what the stock then does) · undecided cards expire after 4 weeks.'],
];

export function ThemesGlossary({ open, onToggle }: { open: boolean; onToggle: () => void }) {
  return (
    <div id="themes-glossary" style={{ marginBottom: 14, border: '1px solid var(--border-subtle)', borderRadius: 'var(--radius-md)', background: 'var(--bg-elevated)' }}>
      <button onClick={onToggle} style={{ width: '100%', textAlign: 'left', padding: '10px 14px', background: 'none', border: 'none', cursor: 'pointer',
        fontSize: 14, fontWeight: 700, color: 'var(--text-primary)' }}>📖 How to read a card {open ? '▾' : '▸'}</button>
      {open && <dl style={{ margin: 0, padding: '0 14px 12px', display: 'grid', gap: 8 }}>
        {TERMS.map(([t, d]) => <div key={t}><dt style={{ fontWeight: 700, fontSize: 13.5, color: 'var(--text-primary)' }}>{t}</dt>
          <dd style={{ margin: '2px 0 0', fontSize: 13.5, lineHeight: 1.5, color: 'var(--text-secondary)' }}>{d}</dd></div>)}
      </dl>}
    </div>
  );
}
