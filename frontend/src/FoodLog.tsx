import { useState, useEffect, useCallback, useRef } from 'react';
import type { FoodLogDayDto, FoodLogEntryDto, MacroTotals, MealDto, MealSlot } from './types';
import { api } from './api';
import { SLOTS, SLOT_EMOJI, KCAL_EMOJI, PROTEIN_EMOJI, CARBS_EMOJI, FAT_EMOJI } from './nutritionShared';

// ── dates and numbers ──

const iso = (d: Date) =>
  `${d.getFullYear()}-${(d.getMonth() + 1).toString().padStart(2, '0')}-${d.getDate().toString().padStart(2, '0')}`;
const todayIso = () => iso(new Date());
const daysBefore = (dateIso: string, n: number) => {
  const [y, m, d] = dateIso.split('-').map(Number);
  return iso(new Date(y, m - 1, d - n));
};

/** "Today", "Yesterday", or "Thu 2 Oct" — the year only when it is not this one. */
function dayLabel(dateIso: string): string {
  const today = todayIso();
  if (dateIso === today) return 'Today';
  if (dateIso === daysBefore(today, 1)) return 'Yesterday';
  const [y, m, d] = dateIso.split('-').map(Number);
  const date = new Date(y, m - 1, d);
  return date.toLocaleDateString(undefined, {
    weekday: 'short', day: 'numeric', month: 'short',
    ...(y !== new Date().getFullYear() ? { year: 'numeric' } : {}),
  });
}

/** The slot a meal eaten right now most likely belongs to — the same rule as the server's. */
function slotForNow(): MealSlot {
  const h = new Date().getHours();
  return h < 11 ? 'Breakfast' : h < 15 ? 'Lunch' : h < 18 ? 'Snack' : h < 23 ? 'Dinner' : 'Snack';
}

const kcal = (n: number) => Math.round(n).toLocaleString();
const grams = (n: number) => (Math.round(n * 10) / 10).toString();

// "" -> null so an empty macro stays unknown rather than becoming 0. Decimals are fine here.
const toDec = (text: string): number | null => {
  const t = text.trim();
  if (!t) return null;
  const n = Number(t);
  return Number.isFinite(n) ? Math.max(0, n) : null;
};

// The range presets — one control scopes the charts and the day cards alike.
const RANGES = [7, 14, 30, 90];

// ── the history view ──

export function FoodLogView({ refreshKey }: { refreshKey: number }) {
  const [days, setDays] = useState<FoodLogDayDto[]>([]);
  const [span, setSpan] = useState(30);
  const [asTable, setAsTable] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  // An entry opens it for editing; a date opens an empty form for that day.
  const [editing, setEditing] = useState<FoodLogEntryDto | { date: string } | null>(null);

  const to = todayIso();
  const from = daysBefore(to, span - 1);

  const load = useCallback(async () => {
    setError(null);
    try { setDays(await api.getFoodLog(from, to)); }
    catch { setError('Could not load the food log.'); }
    finally { setLoading(false); }
  }, [from, to]);

  useEffect(() => { load(); }, [load, refreshKey]);

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') setEditing(null); };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, []);

  // Averages over the days that have anything logged — an empty day is a gap in the log, not a fast.
  const avgOf = (pick: (t: MacroTotals) => number | null) => {
    const xs = days.map(d => pick(d.totals)).filter((n): n is number => n != null);
    return xs.length === 0 ? null : xs.reduce((a, b) => a + b, 0) / xs.length;
  };
  const avgKcal = avgOf(t => t.calories), avgProtein = avgOf(t => t.proteinG);

  return (
    <div style={{ display: 'flex', flexDirection: 'column', height: '100%', minHeight: 0 }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 12, padding: '12px 20px', flexWrap: 'wrap',
        borderBottom: '1px solid var(--border-subtle)', flexShrink: 0 }}>
        <div className="nav-tabs" role="group" aria-label="Range">
          {RANGES.map(r => (
            <button key={r} className={`nav-tab ${span === r ? 'active' : ''}`} aria-pressed={span === r}
              onClick={() => setSpan(r)}>{r} days</button>
          ))}
        </div>
        <span style={{ fontSize: 13, color: 'var(--text-muted)' }}>
          {days.length} day{days.length === 1 ? '' : 's'} logged
          {avgKcal != null && <> · {KCAL_EMOJI} avg {kcal(avgKcal)} kcal/day</>}
          {avgProtein != null && <> · {PROTEIN_EMOJI} {grams(Math.round(avgProtein))} g protein</>}
        </span>
        <button className="btn btn-accent btn-sm" style={{ marginLeft: 'auto' }}
          onClick={() => setEditing({ date: todayIso() })}>+ Log food</button>
      </div>

      <div style={{ flex: 1, overflowY: 'auto', padding: 20 }}>
        {loading ? (
          <div style={{ color: 'var(--text-muted)' }}>Loading...</div>
        ) : error ? (
          <div style={{ color: 'var(--danger)' }}>{error}</div>
        ) : (
          <div style={{ display: 'flex', flexDirection: 'column', gap: 14, maxWidth: 980, margin: '0 auto' }}>
            {days.length === 0 && (
              <div style={{ textAlign: 'center', color: 'var(--text-muted)', padding: '60px 20px' }}>
                <div style={{ fontSize: 32, marginBottom: 12 }}>🍽️</div>
                <div style={{ fontSize: 15, marginBottom: 16, color: 'var(--text-secondary)' }}>
                  Nothing logged in the last {span} days.
                </div>
                <button className="btn btn-accent btn-sm" onClick={() => setEditing({ date: todayIso() })}>+ Log what you ate today</button>
              </div>
            )}
            {days.length > 0 && (
              <DayByDay days={days} from={from} to={to} asTable={asTable} onToggle={() => setAsTable(t => !t)} />
            )}
            {days.map(d => (
              <DayCard key={d.date} day={d} onOpen={e => setEditing(e)} onAdd={() => setEditing({ date: d.date })} />
            ))}
          </div>
        )}
      </div>

      {editing && (
        <FoodLogForm
          entry={'id' in editing ? editing : null}
          date={editing.date}
          onCancel={() => setEditing(null)}
          onSaved={async () => { setEditing(null); await load(); }} />
      )}
    </div>
  );
}

// ── day by day: one chart per measure ──

type Measure = { key: keyof MacroTotals; title: string; emoji: string; unit: string; color: string };
const MEASURES: Measure[] = [
  { key: 'calories', title: 'Calories', emoji: KCAL_EMOJI, unit: 'kcal', color: 'var(--macro-kcal)' },
  { key: 'proteinG', title: 'Protein', emoji: PROTEIN_EMOJI, unit: 'g', color: 'var(--macro-protein)' },
  { key: 'carbsG', title: 'Carbs', emoji: CARBS_EMOJI, unit: 'g', color: 'var(--macro-carbs)' },
  { key: 'fatG', title: 'Fat', emoji: FAT_EMOJI, unit: 'g', color: 'var(--macro-fat)' },
];

const shortDate = (dateIso: string) => {
  const [y, m, d] = dateIso.split('-').map(Number);
  return new Date(y, m - 1, d).toLocaleDateString(undefined, { day: 'numeric', month: 'short' });
};

/** Every date from..to, oldest first — days with nothing logged stay in as gaps. */
function datesBetween(from: string, to: string): string[] {
  const out: string[] = [];
  for (let d = from; d <= to; d = daysBefore(d, -1)) out.push(d);
  return out;
}

/**
 * Four small multiples, not one chart: calories and grams are different scales, and one axis per
 * measure is the only honest way to put them side by side. Each is one series, so its title names
 * it and there is no legend box. The range above scopes these and the day cards alike.
 */
function DayByDay({ days, from, to, asTable, onToggle }: {
  days: FoodLogDayDto[]; from: string; to: string; asTable: boolean; onToggle: () => void;
}) {
  const byDate = new Map(days.map(d => [d.date, d]));
  const dates = datesBetween(from, to);
  const anyIncomplete = days.some(d => d.incompleteEntries > 0);
  return (
    <section aria-labelledby="daybyday-h">
      <div style={{ display: 'flex', alignItems: 'baseline', gap: 12, marginBottom: anyIncomplete && !asTable ? 4 : 10 }}>
        <h3 id="daybyday-h" style={{ margin: 0, fontSize: 15, fontWeight: 600 }}>Day by day</h3>
        <button className="btn btn-ghost btn-sm" style={{ marginLeft: 'auto' }} onClick={onToggle} aria-pressed={asTable}>
          {asTable ? 'Show charts' : 'Show as table'}
        </button>
      </div>
      {anyIncomplete && !asTable && (
        <div style={{ fontSize: 12, color: 'var(--text-secondary)', display: 'flex', alignItems: 'center', gap: 6, marginBottom: 10 }}>
          {/* The key mirrors the mark: a full bar beside a faded one. */}
          <span aria-hidden style={{ display: 'inline-flex', gap: 2, alignItems: 'flex-end' }}>
            <span style={{ width: 6, height: 12, borderRadius: '2px 2px 0 0', background: 'var(--text-secondary)' }} />
            <span style={{ width: 6, height: 12, borderRadius: '2px 2px 0 0', background: 'var(--text-secondary)', opacity: 0.4 }} />
          </span>
          A faded bar is a day with unknown macros, so it is a lower bound.
        </div>
      )}
      {asTable ? (
        <DayTable days={days} />
      ) : (
        <div style={{ display: 'grid', gap: 12, gridTemplateColumns: 'repeat(auto-fit, minmax(min(100%, 380px), 1fr))' }}>
          {MEASURES.map(m => (
            <DailyChart key={m.key} measure={m} dates={dates}
              points={dates.map(d => {
                const day = byDate.get(d);
                return { date: d, value: day ? day.totals[m.key] : null, incomplete: !!day && day.incompleteEntries > 0, logged: !!day };
              })} />
          ))}
        </div>
      )}
    </section>
  );
}

function useWidth<T extends HTMLElement>(): [React.RefObject<T>, number] {
  const ref = useRef<T>(null);
  const [w, setW] = useState(0);
  useEffect(() => {
    if (!ref.current) return;
    const ro = new ResizeObserver(([e]) => setW(e.contentRect.width));
    ro.observe(ref.current);
    return () => ro.disconnect();
  }, []);
  return [ref, w];
}

/** A clean step for ~3 gridlines: 1, 2 or 5 times a power of ten. */
function niceStep(max: number): number {
  const raw = max / 3;
  const pow = Math.pow(10, Math.floor(Math.log10(raw)));
  const m = raw / pow;
  return (m <= 1 ? 1 : m <= 2 ? 2 : m <= 5 ? 5 : 10) * pow;
}

type Point = { date: string; value: number | null; incomplete: boolean; logged: boolean };

function DailyChart({ measure, dates, points }: { measure: Measure; dates: string[]; points: Point[] }) {
  const [boxRef, width] = useWidth<HTMLDivElement>();
  const [hover, setHover] = useState<number | null>(null);
  const H = 130, top = 6, bottom = 20, left = 40;
  const innerW = Math.max(0, width - left - 4);
  const values = points.map(p => p.value).filter((v): v is number => v != null);
  const avg = values.length ? values.reduce((a, b) => a + b, 0) / values.length : null;
  const step = niceStep(Math.max(...values, 1));
  const max = Math.ceil(Math.max(...values, 1) / step) * step;
  const ticks = Array.from({ length: Math.round(max / step) + 1 }, (_, i) => i * step);
  const plotH = H - top - bottom;
  const y = (v: number) => top + plotH - (v / max) * plotH;
  const slot = dates.length ? innerW / dates.length : 0;
  // Thin marks: at most 24px, and a 2px surface gap between neighbours.
  const barW = Math.max(1, Math.min(24, slot - 2));
  const x = (i: number) => left + i * slot + (slot - barW) / 2;
  const labelEvery = Math.max(1, Math.ceil(dates.length / Math.max(2, Math.floor(innerW / 56))));
  const fmt = (v: number) => (measure.unit === 'kcal' ? kcal(v) : grams(v));

  // A bar with a 4px rounded data-end and a square foot on the baseline.
  const bar = (i: number, v: number) => {
    const x0 = x(i), y0 = y(v), h = top + plotH - y0;
    if (h <= 0) return '';
    const r = Math.min(4, barW / 2, h);
    return `M${x0},${top + plotH} V${y0 + r} Q${x0},${y0} ${x0 + r},${y0} H${x0 + barW - r} Q${x0 + barW},${y0} ${x0 + barW},${y0 + r} V${top + plotH} Z`;
  };
  const hp = hover != null ? points[hover] : null;

  return (
    <div className="foodlog-chart">
      <div style={{ display: 'flex', alignItems: 'baseline', gap: 8, marginBottom: 6 }}>
        <span style={{ fontSize: 14, fontWeight: 600 }}>{measure.emoji} {measure.title}</span>
        <span style={{ fontSize: 12, color: 'var(--text-secondary)' }}>{measure.unit}/day</span>
        {avg != null && (
          <span style={{ marginLeft: 'auto', fontSize: 12, color: 'var(--text-secondary)' }}>
            avg <strong style={{ color: 'var(--text-primary)', fontWeight: 600 }}>{kcal(avg)}</strong> {measure.unit}
          </span>
        )}
      </div>
      <div ref={boxRef} style={{ position: 'relative' }} onPointerLeave={() => setHover(null)}>
        {width > 0 && (
          <svg width={width} height={H} role="img"
            aria-label={`${measure.title} per day, ${dates.length} days${avg != null ? `, average ${kcal(avg)} ${measure.unit}` : ''}. The table view lists every value.`}>
            {ticks.map(t => (
              <g key={t}>
                <line x1={left} x2={width - 4} y1={y(t)} y2={y(t)} stroke="var(--border-subtle)" strokeWidth={1} />
                <text className="tick" x={left - 6} y={y(t) + 4} textAnchor="end">{fmt(t)}</text>
              </g>
            ))}
            {points.map((p, i) => p.value != null && p.value > 0 && (
              <path key={p.date} d={bar(i, p.value)} fill={measure.color}
                opacity={(p.incomplete ? 0.4 : 1) * (hover === i ? 0.75 : 1)} />
            ))}
            {dates.map((d, i) => (i % labelEvery === 0 || i === dates.length - 1) && (dates.length - 1 - i >= labelEvery || i === dates.length - 1) && (
              <text key={d} className="tick" x={left + i * slot + slot / 2} y={H - 4} textAnchor="middle">{shortDate(d)}</text>
            ))}
            {/* The hit target is the whole day's column, not the painted bar. */}
            {dates.map((d, i) => (
              <rect key={d} x={left + i * slot} y={top} width={slot} height={plotH} fill="transparent"
                onPointerEnter={() => setHover(i)} />
            ))}
          </svg>
        )}
        {hp && (
          <div className="foodlog-tip" style={{ left: Math.min(Math.max(left + hover! * slot + slot / 2, 70), width - 70), top: hp.value ? y(hp.value) - 6 : top + plotH - 6 }}>
            <div style={{ color: 'var(--text-secondary)' }}>{dayLabel(hp.date)}</div>
            {hp.value != null ? (
              <div><strong style={{ fontSize: 14 }}>{fmt(hp.value)}</strong> {measure.unit}{hp.incomplete && <span style={{ color: 'var(--text-secondary)' }}> · lower bound</span>}</div>
            ) : (
              <div style={{ color: 'var(--text-secondary)' }}>{hp.logged ? 'unknown' : 'nothing logged'}</div>
            )}
          </div>
        )}
      </div>
    </div>
  );
}

/** The same figures as the charts, one row per logged day — the view that needs no hovering. */
function DayTable({ days }: { days: FoodLogDayDto[] }) {
  const cell = (v: number | null, unit: string) => (v == null ? '—' : `${unit === 'kcal' ? kcal(v) : grams(v)}`);
  return (
    <div style={{ overflowX: 'auto', background: 'var(--bg-primary)', border: '1px solid var(--border-subtle)', borderRadius: 'var(--radius-md)' }}>
      <table className="foodlog-table">
        <thead>
          <tr><th scope="col">Day</th>{MEASURES.map(m => <th key={m.key} scope="col">{m.title} ({m.unit})</th>)}<th scope="col">Note</th></tr>
        </thead>
        <tbody>
          {days.map(d => (
            <tr key={d.date}>
              <th scope="row" style={{ fontWeight: 500, textTransform: 'none', letterSpacing: 0, fontSize: 13, color: 'var(--text-primary)' }}>
                {dayLabel(d.date)}
              </th>
              {MEASURES.map(m => <td key={m.key}>{cell(d.totals[m.key], m.unit)}</td>)}
              <td style={{ color: 'var(--text-secondary)' }}>{d.incompleteEntries > 0 ? 'lower bound' : ''}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function DayCard({ day, onOpen, onAdd }: {
  day: FoodLogDayDto; onOpen: (e: FoodLogEntryDto) => void; onAdd: () => void;
}) {
  return (
    <section style={{ background: 'var(--bg-primary)', border: '1px solid var(--border-subtle)',
      borderRadius: 'var(--radius-md)', overflow: 'hidden' }}>
      <header style={{ display: 'flex', alignItems: 'baseline', gap: 12, flexWrap: 'wrap', padding: '14px 18px 10px' }}>
        <h3 style={{ margin: 0, fontSize: 16, fontWeight: 600 }}>{dayLabel(day.date)}</h3>
        <span style={{ fontSize: 12, color: 'var(--text-muted)' }}>{day.date}</span>
        <button className="btn btn-ghost btn-sm" style={{ marginLeft: 'auto', padding: '2px 8px' }}
          onClick={onAdd} title="Log something else for this day">+ Add</button>
      </header>

      <div style={{ padding: '0 18px 12px' }}>
        <TotalsLine totals={day.totals} />
        {day.incompleteEntries === 0 && <MacroSplit totals={day.totals} />}
        {day.incompleteEntries > 0 && (
          <div style={{ fontSize: 12, color: 'var(--k5-ink)', marginTop: 6 }}
            title="Totals only add up the macros that are known">
            {day.incompleteEntries} of {day.entryCount} entr{day.entryCount === 1 ? 'y has' : 'ies have'} unknown
            macros, so the totals are a lower bound.
          </div>
        )}
      </div>

      <ul style={{ listStyle: 'none', margin: 0, padding: 0, borderTop: '1px solid var(--border-subtle)' }}>
        {day.entries.map(e => (
          <li key={e.id}>
            <button type="button" onClick={() => onOpen(e)} className="foodlog-row" title="Edit this entry">
              <span style={{ fontSize: 16 }} title={e.slot}>{SLOT_EMOJI[e.slot]}</span>
              <span style={{ flex: 1, minWidth: 0 }}>
                <span style={{ fontWeight: 500 }}>{e.name}</span>
                {e.servings !== 1 && <span style={{ color: 'var(--text-muted)' }}> × {grams(e.servings)}</span>}
                {e.note && <span style={{ display: 'block', fontSize: 12, color: 'var(--text-muted)' }}>{e.note}</span>}
              </span>
              <EntryMacros totals={e.totals} />
            </button>
          </li>
        ))}
      </ul>
    </section>
  );
}

/** The day's headline: kcal large, then the three macros in grams. Unknown figures are left out. */
function TotalsLine({ totals }: { totals: MacroTotals }) {
  const parts = [
    { emoji: PROTEIN_EMOJI, title: 'Protein', g: totals.proteinG },
    { emoji: CARBS_EMOJI, title: 'Carbs', g: totals.carbsG },
    { emoji: FAT_EMOJI, title: 'Fat', g: totals.fatG },
  ];
  return (
    <div style={{ display: 'flex', alignItems: 'baseline', gap: 16, flexWrap: 'wrap' }}>
      <span title="Calories" style={{ fontSize: 24, fontWeight: 700, lineHeight: 1 }}>
        <span style={{ fontSize: 15, marginRight: 4 }}>{KCAL_EMOJI}</span>
        {totals.calories != null ? kcal(totals.calories) : '—'}
        <span style={{ fontSize: 12, fontWeight: 600, color: 'var(--text-muted)', marginLeft: 4 }}>kcal</span>
      </span>
      {parts.map(p => (
        <span key={p.title} title={p.title} style={{ fontFamily: 'var(--font-mono)', fontSize: 14, color: 'var(--text-secondary)' }}>
          <span style={{ fontFamily: 'initial', marginRight: 3 }}>{p.emoji}</span>
          {p.g != null ? grams(p.g) : '—'}<span style={{ color: 'var(--text-muted)' }}>g</span>
          <span style={{ fontFamily: 'var(--font-body)', fontSize: 11, color: 'var(--text-muted)', marginLeft: 4 }}>{p.title.toLowerCase()}</span>
        </span>
      ))}
    </div>
  );
}

/**
 * Where the day's calories came from: protein and carbs at 4 kcal/g, fat at 9. Drawn only for a
 * day whose every entry has all its macros — a split of partial figures would misstate the balance.
 */
function MacroSplit({ totals }: { totals: MacroTotals }) {
  const { proteinG: p, carbsG: c, fatG: f } = totals;
  if (p == null || c == null || f == null) return null;
  const parts = [
    { key: 'protein', label: 'Protein', kcal: p * 4, color: 'var(--macro-protein)' },
    { key: 'carbs', label: 'Carbs', kcal: c * 4, color: 'var(--macro-carbs)' },
    { key: 'fat', label: 'Fat', kcal: f * 9, color: 'var(--macro-fat)' },
  ];
  const sum = parts.reduce((a, x) => a + x.kcal, 0);
  if (sum <= 0) return null;
  const pct = (x: number) => Math.round((x / sum) * 100);
  return (
    <div style={{ marginTop: 10 }}>
      <div role="img" aria-label={parts.map(x => `${x.label} ${pct(x.kcal)}%`).join(', ')}
        style={{ display: 'flex', height: 8, borderRadius: 4, overflow: 'hidden', gap: 2 }}>
        {parts.map(x => x.kcal > 0 && (
          <div key={x.key} style={{ flexGrow: x.kcal, background: x.color }} title={`${x.label} ${pct(x.kcal)}% of calories`} />
        ))}
      </div>
      <div style={{ display: 'flex', gap: 14, marginTop: 5, fontSize: 11, color: 'var(--text-muted)' }}>
        {parts.map(x => (
          <span key={x.key} style={{ display: 'inline-flex', alignItems: 'center', gap: 5 }}>
            <span style={{ width: 8, height: 8, borderRadius: 2, background: x.color, display: 'inline-block' }} />
            {x.label} {pct(x.kcal)}%
          </span>
        ))}
      </div>
    </div>
  );
}

function EntryMacros({ totals }: { totals: MacroTotals }) {
  return (
    <span style={{ display: 'flex', gap: 10, alignItems: 'baseline', flexShrink: 0, fontFamily: 'var(--font-mono)', fontSize: 12, color: 'var(--text-secondary)' }}>
      <span style={{ fontWeight: 600, color: 'var(--text-primary)' }}>{totals.calories != null ? `${kcal(totals.calories)} kcal` : '? kcal'}</span>
      <span title="Protein">{totals.proteinG != null ? `${grams(totals.proteinG)}P` : '?P'}</span>
      <span title="Carbs">{totals.carbsG != null ? `${grams(totals.carbsG)}C` : '?C'}</span>
      <span title="Fat">{totals.fatG != null ? `${grams(totals.fatG)}F` : '?F'}</span>
    </span>
  );
}

// ── logging one thing ──

/**
 * Log something eaten, or edit an entry. Picking a meal from the book fills the name and the
 * per-serving macros (still editable — a restaurant portion is not the recipe); the entry keeps
 * those numbers as they are now, so editing the meal later never rewrites the day.
 */
export function FoodLogForm({ entry, date, meal, onCancel, onSaved }: {
  entry: FoodLogEntryDto | null; date: string; meal?: MealDto | null;
  onCancel: () => void; onSaved: () => void | Promise<void>;
}) {
  const start = entry ?? null;
  const [day, setDay] = useState(start?.date ?? date);
  const [slot, setSlot] = useState<MealSlot>(start?.slot ?? meal?.slot ?? slotForNow());
  const [mealId, setMealId] = useState<string | null>(start?.mealId ?? meal?.id ?? null);
  const [name, setName] = useState(start?.name ?? meal?.name ?? '');
  const [servings, setServings] = useState((start?.servings ?? 1).toString());
  const [calories, setCalories] = useState((start?.calories ?? meal?.calories)?.toString() ?? '');
  const [protein, setProtein] = useState((start?.proteinG ?? meal?.proteinG)?.toString() ?? '');
  const [carbs, setCarbs] = useState((start?.carbsG ?? meal?.carbsG)?.toString() ?? '');
  const [fat, setFat] = useState((start?.fatG ?? meal?.fatG)?.toString() ?? '');
  const [note, setNote] = useState(start?.note ?? '');
  const [meals, setMeals] = useState<MealDto[]>([]);
  const [busy, setBusy] = useState(false);
  const [confirm, setConfirm] = useState(false);
  const [err, setErr] = useState<string | null>(null);

  useEffect(() => { api.getMeals().then(setMeals).catch(() => setMeals([])); }, []);

  const pickMeal = (id: string) => {
    if (!id) { setMealId(null); return; }
    const m = meals.find(x => x.id === id);
    if (!m) return;
    setMealId(m.id);
    setName(m.name);
    setCalories(m.calories?.toString() ?? '');
    setProtein(m.proteinG?.toString() ?? '');
    setCarbs(m.carbsG?.toString() ?? '');
    setFat(m.fatG?.toString() ?? '');
  };

  const n = toDec(servings) ?? 0;
  const times = (v: string) => { const x = toDec(v); return x == null ? null : x * n; };
  const preview = [times(calories), times(protein), times(carbs), times(fat)];
  const valid = name.trim().length > 0 && n > 0 && n <= 50 && /^\d{4}-\d{2}-\d{2}$/.test(day);

  const submit = async () => {
    if (!valid || busy) return;
    setBusy(true); setErr(null);
    const body = {
      date: day, slot, mealId, name: name.trim(), servings: n,
      calories: toDec(calories), proteinG: toDec(protein), carbsG: toDec(carbs), fatG: toDec(fat),
      note: note.trim() || null,
    };
    try {
      if (start) await api.updateFoodLogEntry(start.id, body);
      else await api.createFoodLogEntry(body);
      await onSaved();
    } catch {
      setErr('Could not save. Try again.');
      setBusy(false);
    }
  };

  const remove = async () => {
    if (!start) return;
    setBusy(true);
    try { await api.deleteFoodLogEntry(start.id); await onSaved(); }
    catch { setErr('Could not delete. Try again.'); setBusy(false); }
  };

  return (
    <div className="modal-overlay" onClick={onCancel}>
      <div className="modal" onClick={e => e.stopPropagation()} style={{ maxWidth: 520 }}>
        <h2>{start ? 'Edit entry' : 'Log food'}</h2>

        <label>From the meal book (optional)</label>
        <select value={mealId ?? ''} onChange={e => pickMeal(e.target.value)} aria-label="From the meal book">
          <option value="">— something else —</option>
          {SLOTS.map(s => {
            const inSlot = meals.filter(m => m.slot === s);
            return inSlot.length === 0 ? null : (
              <optgroup key={s} label={`${SLOT_EMOJI[s]} ${s}`}>
                {inSlot.map(m => <option key={m.id} value={m.id}>{m.name}</option>)}
              </optgroup>
            );
          })}
          {/* An entry from a meal that has since been deleted still shows what it was. */}
          {mealId && !meals.some(m => m.id === mealId) && <option value={mealId}>{name}</option>}
        </select>

        <label>What you ate</label>
        <input type="text" value={name} autoFocus={!meal && !start} onChange={e => setName(e.target.value)}
          placeholder="2 eggs on rye toast" />

        <div className="form-row">
          <div>
            <label>Day</label>
            <input type="date" value={day} max={todayIso()} onChange={e => setDay(e.target.value)} />
          </div>
          <div>
            <label>Slot</label>
            <select value={slot} onChange={e => setSlot(e.target.value as MealSlot)}>
              {SLOTS.map(s => <option key={s} value={s}>{SLOT_EMOJI[s]} {s}</option>)}
            </select>
          </div>
          <div>
            <label>Servings</label>
            <input type="number" min={0.1} step={0.5} value={servings} onChange={e => setServings(e.target.value)} />
          </div>
        </div>

        <div style={{ fontSize: 12, color: 'var(--text-muted)', margin: '2px 0 8px' }}>Macros per serving — leave blank if unknown</div>
        <div className="form-row">
          <div><label>{KCAL_EMOJI} Kcal</label><input type="number" min={0} value={calories} onChange={e => setCalories(e.target.value)} placeholder="420" /></div>
          <div><label>{PROTEIN_EMOJI} Protein</label><input type="number" min={0} value={protein} onChange={e => setProtein(e.target.value)} placeholder="30" /></div>
          <div><label>{CARBS_EMOJI} Carbs</label><input type="number" min={0} value={carbs} onChange={e => setCarbs(e.target.value)} placeholder="35" /></div>
          <div><label>{FAT_EMOJI} Fat</label><input type="number" min={0} value={fat} onChange={e => setFat(e.target.value)} placeholder="20" /></div>
        </div>

        {n > 0 && n !== 1 && preview.some(v => v != null) && (
          <div style={{ fontSize: 13, color: 'var(--text-secondary)', marginBottom: 10 }}>
            × {grams(n)} servings ={' '}
            {preview[0] != null ? `${kcal(preview[0])} kcal` : '? kcal'} ·{' '}
            {preview[1] != null ? `${grams(preview[1])} g protein` : '? protein'} ·{' '}
            {preview[2] != null ? `${grams(preview[2])} g carbs` : '? carbs'} ·{' '}
            {preview[3] != null ? `${grams(preview[3])} g fat` : '? fat'}
          </div>
        )}

        <label>Note</label>
        <input type="text" value={note} onChange={e => setNote(e.target.value)} placeholder="Restaurant portion, estimated" />

        {err && <div style={{ color: 'var(--danger)', fontSize: 13, marginBottom: 8 }}>{err}</div>}

        <div className="modal-actions" style={{ justifyContent: start ? 'space-between' : 'flex-end' }}>
          {start && (
            <button type="button" className={`btn btn-sm ${confirm ? 'btn-danger' : 'btn-ghost'}`} disabled={busy}
              onClick={() => (confirm ? remove() : setConfirm(true))}>
              {confirm ? 'Delete for good?' : 'Delete'}
            </button>
          )}
          <div style={{ display: 'flex', gap: 8 }}>
            <button type="button" className="btn btn-sm" onClick={onCancel}>Cancel</button>
            <button type="button" className="btn btn-sm btn-accent" onClick={submit} disabled={!valid || busy}>
              {busy ? 'Saving...' : start ? 'Save' : 'Log it'}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
