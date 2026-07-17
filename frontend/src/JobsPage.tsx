import { useState, useEffect, useCallback } from 'react';
import type { JobRunDto, JobRunSummaryDto, JobPostingDto } from './types';
import { api } from './api';

// "2026-07-14" -> "14.07.2026"
function fmtDate(iso: string): string {
  const [y, m, d] = iso.split('-');
  return d && m && y ? `${d}.${m}.${y}` : iso;
}

function scoreColor(score: number): string {
  if (score >= 80) return '#30a46c';
  if (score >= 60) return '#f5a524';
  return '#8b8b8b';
}

export function JobsPage() {
  const [runs, setRuns] = useState<JobRunSummaryDto[]>([]);
  const [run, setRun] = useState<JobRunDto | null>(null);
  const [idx, setIdx] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Open the most recent run by default; the day list only drives the picker.
  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const [list, latest] = await Promise.all([
          api.getJobRuns(),
          api.getLatestJobRun().catch(() => null), // 404 when nothing imported yet
        ]);
        if (cancelled) return;
        setRuns(list);
        setRun(latest);
      } catch {
        if (!cancelled) setError('Could not load job runs.');
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => { cancelled = true; };
  }, []);

  const selectDay = useCallback(async (date: string) => {
    if (date === run?.runDate) return;
    setLoading(true);
    try {
      const r = await api.getJobRun(date);
      setRun(r);
      setIdx(0);
    } catch {
      setError(`Could not load ${date}.`);
    } finally {
      setLoading(false);
    }
  }, [run?.runDate]);

  const postings = run?.postings ?? [];
  const total = postings.length;
  const current: JobPostingDto | undefined = postings[idx];

  const prev = useCallback(() => setIdx(i => Math.max(0, i - 1)), []);
  const next = useCallback(() => setIdx(i => Math.min(total - 1, i + 1)), [total]);

  // Arrow keys page through the day's postings, same as the on-screen buttons.
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'ArrowLeft') prev();
      if (e.key === 'ArrowRight') next();
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [prev, next]);

  if (loading && !run) return <div style={{ padding: 24, color: 'var(--text-muted)' }}>Loading...</div>;
  if (error) return <div style={{ padding: 24, color: 'var(--danger)' }}>{error}</div>;

  if (!run) return (
    <div className="empty-state" style={{ padding: 40, textAlign: 'center', color: 'var(--text-muted)' }}>
      <div style={{ fontSize: 32, marginBottom: 12 }}>🧭</div>
      <div style={{ fontSize: 15, marginBottom: 6, color: 'var(--text-secondary)' }}>No job runs yet.</div>
      <div style={{ fontSize: 13 }}>Run the Finder scout and import it with the <code>import_job_run</code> MCP tool.</div>
    </div>
  );

  return (
    <div style={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
      {/* Day picker + run meta */}
      <div style={{ display: 'flex', alignItems: 'center', gap: 12, padding: '12px 16px',
        borderBottom: '1px solid var(--border-subtle)', flexShrink: 0, flexWrap: 'wrap' }}>
        <select className="sprint-select" value={run.runDate}
          onChange={e => selectDay(e.target.value)}
          style={{ fontSize: 14, fontWeight: 600 }}>
          {runs.map(r => (
            <option key={r.id} value={r.runDate}>
              {fmtDate(r.runDate)} — {r.postingCount} posting{r.postingCount === 1 ? '' : 's'}
            </option>
          ))}
        </select>
        <span style={{ fontSize: 13, color: 'var(--text-muted)' }}>
          {total} kept of {run.rawCount} scraped · last {run.maxAgeDays}d
        </span>
        <span style={{ fontSize: 12, color: 'var(--text-muted)', opacity: 0.8 }}>
          {run.queries.join(' · ')}
        </span>
      </div>

      {total === 0 ? (
        <div style={{ padding: 24, color: 'var(--text-muted)' }}>No postings survived the filters on this day.</div>
      ) : (
        <div style={{ flex: 1, display: 'flex', alignItems: 'stretch', minHeight: 0 }}>
          <ArrowButton dir="left" onClick={prev} disabled={idx === 0} />

          <div style={{ flex: 1, overflowY: 'auto', padding: '20px 8px', minWidth: 0 }}>
            {current && <PostingCard key={current.id} posting={current} />}
          </div>

          <ArrowButton dir="right" onClick={next} disabled={idx >= total - 1} />
        </div>
      )}

      {/* Position counter */}
      {total > 0 && (
        <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', gap: 10,
          padding: '10px 16px', borderTop: '1px solid var(--border-subtle)', flexShrink: 0 }}>
          <span style={{ fontSize: 13, color: 'var(--text-muted)', fontVariantNumeric: 'tabular-nums' }}>
            {idx + 1} / {total}
          </span>
          <span style={{ fontSize: 11, color: 'var(--text-muted)', opacity: 0.6 }}>← → to navigate</span>
        </div>
      )}
    </div>
  );
}

function ArrowButton({ dir, onClick, disabled }: { dir: 'left' | 'right'; onClick: () => void; disabled: boolean }) {
  return (
    <button onClick={onClick} disabled={disabled}
      aria-label={dir === 'left' ? 'Previous posting' : 'Next posting'}
      style={{
        flexShrink: 0, width: 56, border: 'none', background: 'transparent',
        cursor: disabled ? 'default' : 'pointer',
        color: disabled ? 'var(--border)' : 'var(--text-secondary)',
        fontSize: 28, display: 'flex', alignItems: 'center', justifyContent: 'center',
        opacity: disabled ? 0.35 : 1, transition: 'color .15s',
      }}>
      {dir === 'left' ? '‹' : '›'}
    </button>
  );
}

function PostingCard({ posting: p }: { posting: JobPostingDto }) {
  const isEu = p.bucket === 'eu-allowed';
  const [showGaps, setShowGaps] = useState(false);
  // Bind to a const so the null-narrowing survives into the nested .map callback below.
  const fit = p.cvFitScore;
  return (
    <div className="item-card" style={{ maxWidth: 780, margin: '0 auto', padding: 24,
      background: 'var(--bg-secondary)', border: '1px solid var(--border-subtle)',
      borderRadius: 'var(--radius-lg)' }}>

      {/* Title + scores */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: 16, marginBottom: 4 }}>
        <h2 style={{ margin: 0, fontSize: 21, lineHeight: 1.3, color: 'var(--text-primary)' }}>{p.title}</h2>
        <div style={{ flexShrink: 0, display: 'flex', gap: 8 }}>
          {p.score != null && (
            <div style={{ minWidth: 52, textAlign: 'center', padding: '6px 10px',
              borderRadius: 'var(--radius-md)', background: scoreColor(p.score), color: '#fff' }}>
              <div style={{ fontSize: 18, fontWeight: 700, lineHeight: 1 }}>{Math.round(p.score)}</div>
              <div style={{ fontSize: 9, opacity: 0.85, letterSpacing: 0.5 }}>SCORE</div>
            </div>
          )}
          {fit != null && (
            <button type="button" onClick={() => setShowGaps(v => !v)}
              title="How well your tailored CV fits this job — click for what's missing"
              style={{ minWidth: 52, textAlign: 'center', padding: '6px 10px', border: 'none', cursor: 'pointer',
                borderRadius: 'var(--radius-md)', background: scoreColor(fit), color: '#fff',
                outline: showGaps ? '2px solid var(--text-primary)' : 'none', outlineOffset: 1 }}>
              <div style={{ fontSize: 18, fontWeight: 700, lineHeight: 1 }}>{fit}</div>
              <div style={{ fontSize: 9, opacity: 0.9, letterSpacing: 0.5 }}>CV FIT {showGaps ? '▲' : '▾'}</div>
            </button>
          )}
        </div>
      </div>

      {/* CV-fit gap breakdown — what's missing to reach a 100% fit, highest-impact first */}
      {fit != null && showGaps && (
        <div style={{ padding: 12, marginTop: 10, marginBottom: 4, borderRadius: 'var(--radius-md)',
          background: 'var(--bg-primary)', borderLeft: `3px solid ${scoreColor(fit)}` }}>
          <div style={{ fontSize: 11, textTransform: 'uppercase', letterSpacing: 0.6,
            color: 'var(--text-muted)', marginBottom: 8 }}>
            Missing for a 100% fit · {fit}/100
          </div>
          {p.cvFitGaps.length === 0 ? (
            <div style={{ fontSize: 14, color: 'var(--text-secondary)' }}>No gaps recorded — this CV is a strong match.</div>
          ) : (
            <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
              {[...p.cvFitGaps].sort((a, b) => b.points - a.points).map((g, i) => (
                <div key={i} style={{ display: 'flex', gap: 10, alignItems: 'baseline' }}>
                  <span style={{ flexShrink: 0, minWidth: 34, textAlign: 'right', fontWeight: 700,
                    fontVariantNumeric: 'tabular-nums', color: scoreColor(fit) }}>+{g.points}</span>
                  <span style={{ fontSize: 14, lineHeight: 1.5, color: 'var(--text-secondary)' }}>
                    <span style={{ color: 'var(--text-primary)' }}>{g.label}</span>
                    {g.note && <span style={{ color: 'var(--text-muted)' }}> — {g.note}</span>}
                  </span>
                </div>
              ))}
            </div>
          )}
        </div>
      )}

      <div style={{ fontSize: 15, color: 'var(--accent)', fontWeight: 600, marginBottom: 12 }}>{p.company}</div>

      {/* Badges */}
      <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6, marginBottom: 14 }}>
        <Badge text={isEu ? 'EU — check EOR' : 'Armenia-compatible'}
          color={isEu ? '#f5a524' : '#30a46c'} />
        {p.seniorityClass && <Badge text={p.seniorityClass} />}
        <Badge text={p.source} />
        {p.aiKeywordHits > 0 && <Badge text={`AI signals: ${p.aiKeywordHits}`} />}
        {p.geoHints.map(h => <Badge key={h} text={h} />)}
      </div>

      {/* Meta line */}
      <div style={{ fontSize: 13, color: 'var(--text-muted)', marginBottom: 14, lineHeight: 1.7 }}>
        {p.postedAt && <>Posted {fmtDate(p.postedAt)}<span style={{ margin: '0 8px' }}>·</span></>}
        {p.location && <>{p.location}<span style={{ margin: '0 8px' }}>·</span></>}
        Found by {p.queries.length} quer{p.queries.length === 1 ? 'y' : 'ies'}
        {p.queries.length > 0 && <span style={{ opacity: 0.75 }}> ({p.queries.join(', ')})</span>}
      </div>

      {/* Actions */}
      <div style={{ display: 'flex', flexWrap: 'wrap', gap: 8, marginBottom: 16 }}>
        <a href={p.url} target="_blank" rel="noopener noreferrer"
          className="btn btn-accent btn-sm" style={{ display: 'inline-block', textDecoration: 'none' }}>
          Open posting ↗
        </a>
        {p.hasCv && (
          <button type="button" className="btn btn-sm"
            onClick={() => {
              const slug = p.company.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '');
              api.downloadPostingCv(p.id, `cv-${slug || 'posting'}.pdf`).catch(err => alert(String(err)));
            }}>
            Download tailored CV ⬇
          </button>
        )}
      </div>

      {/* Why it scored this way */}
      {p.reasoning && (
        <div style={{ padding: 12, marginBottom: 16, borderRadius: 'var(--radius-md)',
          background: 'var(--bg-primary)', borderLeft: '3px solid var(--accent)' }}>
          <div style={{ fontSize: 11, textTransform: 'uppercase', letterSpacing: 0.6,
            color: 'var(--text-muted)', marginBottom: 5 }}>Assessment</div>
          <div style={{ fontSize: 14, lineHeight: 1.6, color: 'var(--text-secondary)' }}>{p.reasoning}</div>
        </div>
      )}

      {/* What the tailored CV changed vs. the master CV — as bullet points */}
      {p.cvChangeList && (() => {
        const changes = p.cvChangeList.split(/[;\n]+/).map(s => s.trim()).filter(Boolean);
        return (
          <div style={{ padding: 12, marginBottom: 16, borderRadius: 'var(--radius-md)',
            background: 'var(--bg-primary)', borderLeft: '3px solid #30a46c' }}>
            <div style={{ fontSize: 11, textTransform: 'uppercase', letterSpacing: 0.6,
              color: 'var(--text-muted)', marginBottom: 6 }}>CV changes vs. original</div>
            <ul style={{ margin: 0, paddingLeft: 18, fontSize: 14, lineHeight: 1.6, color: 'var(--text-secondary)' }}>
              {changes.map((c, i) => <li key={i} style={{ marginBottom: 4 }}>{c}</li>)}
            </ul>
          </div>
        );
      })()}

      {/* Description — the pipeline stores plain text, so preserve its line breaks. */}
      <div style={{ fontSize: 14, lineHeight: 1.65, color: 'var(--text-secondary)', whiteSpace: 'pre-wrap' }}>
        {p.description}
      </div>
    </div>
  );
}

function Badge({ text, color }: { text: string; color?: string }) {
  return (
    <span style={{
      fontSize: 11, fontWeight: 600, padding: '3px 8px', borderRadius: 'var(--radius-sm)',
      background: color ?? 'var(--bg-primary)',
      color: color ? '#fff' : 'var(--text-muted)',
      border: color ? 'none' : '1px solid var(--border-subtle)',
    }}>{text}</span>
  );
}
