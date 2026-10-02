import { useEffect, useMemo, useRef, useState } from 'react';
import type { FlashcardMemoryDto } from './types';
import { api } from './api';

// Notes v2 dashboard. Panel 1: how your prompts' predicted recall was spread on any day of the past year,
// today by default. Each prompt's state on that day is rebuilt from its own history (first exposure, then
// every review up to that day); prompts from notes written after that day did not exist yet and are left out. Recall comes from the same forgetting curve the server schedules with
// (Fsrs.Retrievability): R(t) = (1 + 19/81 · t / S)^-0.5, so R equals 90% when t equals the stability S.


const FACTOR = 19 / 81;
const DECAY = -0.5;
const BINS = 10;
const MAX_DAYS_BACK = 365;
const DAY_MS = 86_400_000;

/** yyyy-mm-dd → whole days since the epoch, timezone-free (the server sends dates in Asia/Yerevan). */
function dayIndex(iso: string): number {
  const [y, m, d] = iso.split('-').map(Number);
  return Math.round(Date.UTC(y, m - 1, d) / DAY_MS);
}

function fromDayIndex(i: number): Date {
  const d = new Date(i * DAY_MS);
  return new Date(d.getUTCFullYear(), d.getUTCMonth(), d.getUTCDate());
}

function recall(stability: number, days: number): number {
  if (days <= 0) return 1;
  return Math.pow(1 + FACTOR * days / Math.max(stability, 0.1), DECAY);
}

function fmtDate(d: Date): string {
  return `${String(d.getDate()).padStart(2, '0')}.${String(d.getMonth() + 1).padStart(2, '0')}.${d.getFullYear()}`;
}

/** A clean tick step for a 0..max percent axis: 5, 10, 20 or 25. */
function niceStep(max: number): number {
  for (const s of [5, 10, 20, 25]) if (max / s <= 5) return s;
  return 25;
}


function RecallHistogram({ bins, total }: { bins: number[]; total: number }) {
  const [hover, setHover] = useState<number | null>(null);
  const wrap = useRef<HTMLDivElement>(null);
  const [width, setWidth] = useState(640);

  useEffect(() => {
    const el = wrap.current;
    if (!el) return;
    const ro = new ResizeObserver(entries => setWidth(Math.max(320, Math.floor(entries[0].contentRect.width))));
    ro.observe(el);
    return () => ro.disconnect();
  }, []);

  const shares = bins.map(n => (total ? (n / total) * 100 : 0));
  const top = Math.max(10, ...shares);
  const step = niceStep(top);
  const yMax = Math.ceil(top / step) * step;
  const ticks = Array.from({ length: yMax / step + 1 }, (_, i) => i * step);

  // Geometry. The SVG includes its x-axis band, so nothing is cut off at the bottom.
  const H = 260, padL = 40, padR = 24, padT = 12, axisH = 34;
  const plotW = width - padL - padR, plotH = H - padT - axisH;
  const band = plotW / BINS;
  const gap = 2; // surface gap between adjacent bins
  const y = (v: number) => padT + plotH - (v / yMax) * plotH;

  const barPath = (x: number, top: number, w: number, h: number) => {
    const r = Math.min(4, h, w / 2);
    if (h <= 0) return '';
    return `M${x},${top + h} L${x},${top + r} Q${x},${top} ${x + r},${top} L${x + w - r},${top} Q${x + w},${top} ${x + w},${top + r} L${x + w},${top + h} Z`;
  };

  return (
    <div ref={wrap} style={{ position: 'relative', width: '100%' }}>
      <svg width={width} height={H} role="img" aria-label="Histogram of predicted recall across prompts" style={{ display: 'block' }}>
        {/* Gridlines and y ticks — hairline, recessive */}
        {ticks.map(t => (
          <g key={t}>
            <line x1={padL} x2={width - padR} y1={y(t)} y2={y(t)} stroke="var(--border-subtle)" strokeWidth={1} />
            <text x={padL - 8} y={y(t)} dy="0.32em" textAnchor="end" fontSize={11} fill="var(--text-muted)" style={{ fontVariantNumeric: 'tabular-nums' }}>{t}%</text>
          </g>
        ))}
        {/* Bins */}
        {shares.map((v, i) => {
          const x = padL + i * band + gap / 2;
          const w = band - gap;
          const top = y(v);
          const h = padT + plotH - top;
          return (
            <g key={i}>
              <path d={barPath(x, top, w, h)} fill="var(--chart-1)" opacity={hover === null || hover === i ? 1 : 0.7} />
              {/* The whole column is the hit target, bigger than the bar */}
              <rect x={padL + i * band} y={padT} width={band} height={plotH} fill="transparent"
                tabIndex={0} aria-label={`Recall ${i * 10}–${i === BINS - 1 ? 100 : i * 10 + 10}%: ${bins[i]} prompts, ${v.toFixed(1)}%`}
                onMouseEnter={() => setHover(i)} onMouseLeave={() => setHover(null)}
                onFocus={() => setHover(i)} onBlur={() => setHover(null)} style={{ outline: 'none', cursor: 'default' }} />
            </g>
          );
        })}
        {/* Baseline + x labels */}
        <line x1={padL} x2={width - padR} y1={padT + plotH} y2={padT + plotH} stroke="var(--border)" strokeWidth={1} />
        {Array.from({ length: BINS + 1 }, (_, i) => (
          <text key={i} x={padL + i * band} y={padT + plotH + 16} textAnchor="middle" fontSize={11} fill="var(--text-muted)">{i * 10}%</text>
        ))}
        <text x={padL + plotW / 2} y={H - 2} textAnchor="middle" fontSize={11} fill="var(--text-secondary)">predicted recall</text>
      </svg>
      {hover !== null && (
        <div role="status" style={{
          position: 'absolute', left: Math.min(Math.max(padL + hover * band + band / 2 - 80, 0), width - 160), top: Math.max(y(shares[hover]) - 64, 0),
          width: 160, padding: '8px 10px', borderRadius: 'var(--radius-sm)', background: 'var(--bg-primary)',
          border: '1px solid var(--border)', boxShadow: 'var(--shadow-md)', fontSize: 12, pointerEvents: 'none', color: 'var(--text-primary)',
        }}>
          <div style={{ color: 'var(--text-secondary)' }}>Recall {hover * 10}–{hover === BINS - 1 ? 100 : hover * 10 + 10}%</div>
          <div style={{ fontWeight: 700, marginTop: 2 }}>{shares[hover].toFixed(1)}% of prompts</div>
          <div style={{ color: 'var(--text-muted)' }}>{bins[hover]} of {total}</div>
        </div>
      )}
    </div>
  );
}

export function FlashcardsDashboard({ book }: { book: 'red' | 'green' }) {
  const [data, setData] = useState<FlashcardMemoryDto | null>(null);
  const [error, setError] = useState<string | null>(null);
  // Days back from today: 0 is today (the slider's right end), MAX_DAYS_BACK a year ago (its left end).
  const [daysBack, setDaysBack] = useState(0);
  const [showTable, setShowTable] = useState(false);

  useEffect(() => { setData(null); api.getFlashcardMemory(book).then(setData).catch(e => setError(String(e))); }, [book]);

  const view = useMemo(() => {
    if (!data) return null;
    const day = dayIndex(data.today) - daysBack;
    const bins = new Array(BINS).fill(0);
    let n = 0, sum = 0, atTarget = 0, below50 = 0;
    for (const p of data.prompts) {
      // Suspended prompts are out of rotation, so they are not part of what you are expected to remember.
      if (p.state === 'Suspended') continue;
      const exposure = dayIndex(p.exposure);
      if (exposure > day) continue; // not written yet on that day
      // State on that day: the last review on or before it, or the first exposure if none.
      let stability = p.initialStability, lastSeen = exposure;
      for (const r of p.reviews) {
        const rd = dayIndex(r.date);
        if (rd > day) break;
        stability = r.stabilityAfter; lastSeen = rd;
      }
      const rec = recall(stability, day - lastSeen);
      bins[Math.min(BINS - 1, Math.floor(rec * BINS))]++;
      n++; sum += rec;
      if (rec >= data.desiredRetention) atTarget++;
      if (rec < 0.5) below50++;
    }
    return { bins, n, mean: n ? sum / n : 0, atTarget: n ? atTarget / n : 0, below50: n ? below50 / n : 0, date: fromDayIndex(day) };
  }, [data, daysBack]);

  if (error) return <div style={{ padding: 24, color: '#e5484d', fontSize: 13 }}>{error}</div>;
  if (!data || !view) return <div style={{ padding: 24, color: 'var(--text-muted)' }}>Loading…</div>;

  return (
    <div style={{ flex: 1, minHeight: 0, overflowY: 'auto', padding: '16px 24px 32px' }}>
      <section style={{ border: '1px solid var(--border-subtle)', borderRadius: 'var(--radius-lg, 12px)', background: 'var(--bg-secondary)', padding: '18px 20px', maxWidth: 900 }}>
        <div style={{ display: 'flex', alignItems: 'baseline', gap: 10, flexWrap: 'wrap' }}>
          <h3 style={{ margin: 0, fontSize: 15, fontWeight: 700, color: 'var(--text-primary)' }}>Predicted recall across prompts</h3>
          <span style={{ fontSize: 12, color: 'var(--text-muted)' }}>
            {daysBack === 0 ? `today · ${fmtDate(view.date)}` : `${daysBack} day${daysBack === 1 ? '' : 's'} ago · ${fmtDate(view.date)}`}
          </span>
        </div>

        {/* Headline + supporting figures */}
        <div style={{ display: 'flex', gap: 28, alignItems: 'flex-end', flexWrap: 'wrap', margin: '14px 0 6px' }}>
          <div>
            <div style={{ fontSize: 48, fontWeight: 700, lineHeight: 1, color: 'var(--text-primary)' }}>{Math.round(view.mean * 100)}%</div>
            <div style={{ fontSize: 12, color: 'var(--text-secondary)', marginTop: 4 }}>average predicted recall</div>
          </div>
          <div>
            <div style={{ fontSize: 20, fontWeight: 700, color: 'var(--text-primary)' }}>{Math.round(view.atTarget * 100)}%</div>
            <div style={{ fontSize: 12, color: 'var(--text-secondary)' }}>at or above the {Math.round(data.desiredRetention * 100)}% target</div>
          </div>
          <div>
            <div style={{ fontSize: 20, fontWeight: 700, color: 'var(--text-primary)' }}>{Math.round(view.below50 * 100)}%</div>
            <div style={{ fontSize: 12, color: 'var(--text-secondary)' }}>below 50%</div>
          </div>
          <div>
            <div style={{ fontSize: 20, fontWeight: 700, color: 'var(--text-primary)' }}>{view.n}</div>
            <div style={{ fontSize: 12, color: 'var(--text-secondary)' }}>prompts in the {book} book{daysBack ? ' then' : ''}</div>
          </div>
        </div>

        {/* Days slider: a year ago on the left, today on the right (the default) */}
        <div style={{ display: 'flex', alignItems: 'center', gap: 12, margin: '14px 0 8px' }}>
          <span style={{ fontSize: 12, color: 'var(--text-muted)', whiteSpace: 'nowrap' }}>1 year ago</span>
          <input id="recall-days" type="range" min={-MAX_DAYS_BACK} max={0} step={1} value={-daysBack}
            aria-label="Day to show, from one year ago to today" aria-valuetext={daysBack === 0 ? 'today' : `${daysBack} days ago`}
            onChange={e => setDaysBack(-Number(e.target.value))} style={{ flex: 1, accentColor: 'var(--chart-1)' }} />
          <span style={{ fontSize: 12, color: 'var(--text-muted)', whiteSpace: 'nowrap' }}>today</span>
          <span style={{ fontSize: 13, fontWeight: 700, minWidth: 72, textAlign: 'right', color: 'var(--text-primary)', fontVariantNumeric: 'tabular-nums' }}>{daysBack === 0 ? 'today' : `−${daysBack} d`}</span>
          <button className="btn btn-sm" onClick={() => setDaysBack(0)} disabled={daysBack === 0}>Today</button>
        </div>

        {view.n === 0
          ? <div style={{ padding: '24px 0', fontSize: 13, color: 'var(--text-muted)' }}>No prompts existed in the {book} book on that day.</div>
          : <RecallHistogram bins={view.bins} total={view.n} />}

        <div style={{ display: 'flex', gap: 10, alignItems: 'center', marginTop: 6 }}>
          <button className="btn btn-sm" onClick={() => setShowTable(t => !t)}>{showTable ? 'Hide table' : 'Show table'}</button>
          <span style={{ fontSize: 11, color: 'var(--text-muted)' }}>Past days are rebuilt from each prompt's review history and the forgetting curve. Prompts from later notes, and suspended ones, are left out.</span>
        </div>
        {showTable && (
          <table style={{ marginTop: 10, borderCollapse: 'collapse', fontSize: 13, fontVariantNumeric: 'tabular-nums', color: 'var(--text-primary)' }}>
            <thead>
              <tr>{['Predicted recall', 'Prompts', 'Share'].map(h => (
                <th key={h} style={{ textAlign: h === 'Predicted recall' ? 'left' : 'right', padding: '4px 14px 4px 0', color: 'var(--text-secondary)', fontWeight: 600, borderBottom: '1px solid var(--border-subtle)' }}>{h}</th>
              ))}</tr>
            </thead>
            <tbody>
              {view.bins.map((n, i) => (
                <tr key={i}>
                  <td style={{ padding: '3px 14px 3px 0' }}>{i * 10}–{i === BINS - 1 ? 100 : i * 10 + 10}%</td>
                  <td style={{ padding: '3px 14px 3px 0', textAlign: 'right' }}>{n}</td>
                  <td style={{ padding: '3px 14px 3px 0', textAlign: 'right' }}>{view.n ? ((n / view.n) * 100).toFixed(1) : '0.0'}%</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </section>
    </div>
  );
}
