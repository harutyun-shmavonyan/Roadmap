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

// Which detail modal is open for the current posting.
type ModalKind = 'assessment' | 'fit' | 'changes';

export function JobsPage() {
  const [runs, setRuns] = useState<JobRunSummaryDto[]>([]);
  const [run, setRun] = useState<JobRunDto | null>(null);
  const [idx, setIdx] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [modal, setModal] = useState<ModalKind | null>(null);

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

  // Close any open detail modal when the posting or day changes.
  useEffect(() => { setModal(null); }, [idx, run?.runDate]);

  // Arrow keys page through the day's postings; Esc closes an open modal (and
  // while a modal is open the arrows don't navigate the card behind it).
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') { setModal(null); return; }
      if (modal) return;
      if (e.key === 'ArrowLeft') prev();
      if (e.key === 'ArrowRight') next();
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [prev, next, modal]);

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
            {current && <PostingCard key={current.id} posting={current} onOpen={setModal} />}
          </div>

          <ArrowButton dir="right" onClick={next} disabled={idx >= total - 1} />
        </div>
      )}

      {current && modal && <PostingModal kind={modal} posting={current} onClose={() => setModal(null)} />}

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

function PostingCard({ posting: p, onOpen }: { posting: JobPostingDto; onOpen: (k: ModalKind) => void }) {
  const isEu = p.bucket === 'eu-allowed';
  const fit = p.cvFitScore;
  const eligColor = isEu ? 'var(--warning)' : 'var(--success)';
  const eligBg = isEu ? 'var(--warning-light)' : 'var(--success-light)';

  const metas: string[] = [];
  if (p.postedAt) metas.push(`Posted ${fmtDate(p.postedAt)}`);
  if (p.location) metas.push(p.location);
  const queryLabel = `Found by ${p.queries.length} quer${p.queries.length === 1 ? 'y' : 'ies'}`;

  const downloadCv = () => {
    const slug = p.company.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '');
    api.downloadPostingCv(p.id, `cv-${slug || 'posting'}.pdf`).catch(err => alert(String(err)));
  };

  return (
    <div className="jobs-card" style={{ maxWidth: 760, margin: '0 auto',
      background: 'var(--bg-primary)', border: '1px solid var(--border-subtle)',
      borderRadius: 'var(--radius-lg)', overflow: 'hidden', boxShadow: 'var(--shadow-sm)' }}>

      {/* Eligibility accent strip */}
      <div style={{ height: 4, background: eligColor }} />

      <div style={{ padding: '22px 26px 26px' }}>
        {/* Header: eligibility pill + title + company | score tiles */}
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: 20 }}>
          <div style={{ minWidth: 0 }}>
            <span style={{ display: 'inline-flex', alignItems: 'center', gap: 6, fontSize: 11, fontWeight: 700,
              padding: '3px 10px 3px 8px', borderRadius: 999, color: eligColor, background: eligBg }}>
              <span style={{ width: 6, height: 6, borderRadius: 999, background: eligColor }} />
              {isEu ? 'EU · check EOR' : 'Armenia-eligible'}
            </span>
            <h2 style={{ margin: '11px 0 3px', fontSize: 22, lineHeight: 1.25, fontWeight: 700,
              color: 'var(--text-primary)' }}>{p.title}</h2>
            <div style={{ fontSize: 15, color: 'var(--accent)', fontWeight: 600 }}>{p.company}</div>
          </div>
          <div style={{ display: 'flex', gap: 10, flexShrink: 0 }}>
            {p.score != null && (
              <ScoreMeter label="SCORE" value={Math.round(p.score)} color={scoreColor(p.score)}
                onClick={p.reasoning ? () => onOpen('assessment') : undefined}
                title={p.reasoning ? 'Why this scored this way — click for the assessment' : undefined} />
            )}
            {fit != null && (
              <ScoreMeter label="CV FIT" value={fit} color={scoreColor(fit)}
                onClick={() => onOpen('fit')}
                title="How well your tailored CV fits this job — click for what's missing" />
            )}
          </div>
        </div>

        {/* Signal chips */}
        <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6, margin: '16px 0 14px' }}>
          {p.seniorityClass && <Chip text={p.seniorityClass} />}
          <Chip text={p.source} />
          {p.aiKeywordHits > 0 && <Chip text={`AI signals ${p.aiKeywordHits}`} />}
          {p.geoHints.map(h => <Chip key={h} text={h} />)}
        </div>

        {/* Meta line — queries live in the tooltip to keep the card calm */}
        <div style={{ fontSize: 13, color: 'var(--text-muted)' }}>
          {metas.map((m, i) => (
            <span key={i}>{i > 0 && <span style={{ margin: '0 8px', opacity: 0.45 }}>·</span>}{m}</span>
          ))}
          {metas.length > 0 && <span style={{ margin: '0 8px', opacity: 0.45 }}>·</span>}
          <span title={p.queries.join(', ')} style={{ borderBottom: p.queries.length ? '1px dotted var(--border)' : 'none', cursor: p.queries.length ? 'help' : 'default' }}>
            {queryLabel}
          </span>
        </div>

        {/* Actions */}
        <div style={{ display: 'flex', flexWrap: 'wrap', gap: 8, marginTop: 18 }}>
          <a href={p.url} target="_blank" rel="noopener noreferrer"
            className="btn btn-accent btn-sm" style={{ textDecoration: 'none' }}>Open posting ↗</a>
          {p.hasCv && (
            <button type="button" className="btn btn-sm" onClick={downloadCv}>⬇ Tailored CV</button>
          )}
          {p.reasoning && (
            <button type="button" className="btn btn-sm" onClick={() => onOpen('assessment')}>Assessment</button>
          )}
          {p.cvChangeList && (
            <button type="button" className="btn btn-sm" onClick={() => onOpen('changes')}>CV changes</button>
          )}
        </div>

        {/* Description */}
        <div style={{ marginTop: 22, paddingTop: 18, borderTop: '1px solid var(--border-subtle)' }}>
          <div style={{ fontSize: 11, fontWeight: 700, letterSpacing: '0.06em', textTransform: 'uppercase',
            color: 'var(--text-muted)', marginBottom: 9 }}>Description</div>
          <div style={{ fontSize: 14, lineHeight: 1.7, color: 'var(--text-secondary)', whiteSpace: 'pre-wrap' }}>
            {p.description}
          </div>
        </div>
      </div>
    </div>
  );
}

// A compact stat tile: big number + label + a thin progress bar tinted by the score.
// Renders as a button (with a ⓘ affordance) when clickable, else a plain div.
function ScoreMeter({ label, value, color, onClick, title }:
  { label: string; value: number; color: string; onClick?: () => void; title?: string }) {
  const pct = Math.max(4, Math.min(100, value));
  const style = { minWidth: 84, padding: '9px 13px 10px', borderRadius: 'var(--radius-md)',
    background: 'var(--bg-secondary)', border: '1px solid var(--border-subtle)', textAlign: 'left' as const };
  const inner = (
    <>
      <div style={{ display: 'flex', alignItems: 'baseline', justifyContent: 'space-between', gap: 8 }}>
        <span style={{ fontSize: 22, fontWeight: 700, lineHeight: 1, color }}>{value}</span>
        <span style={{ fontSize: 9, fontWeight: 700, letterSpacing: '0.06em', color: 'var(--text-muted)' }}>
          {label}{onClick ? ' ⓘ' : ''}
        </span>
      </div>
      <div style={{ height: 4, borderRadius: 3, background: 'var(--progress-track)', marginTop: 8, overflow: 'hidden' }}>
        <div style={{ width: `${pct}%`, height: '100%', background: color, borderRadius: 3 }} />
      </div>
    </>
  );
  return onClick
    ? <button type="button" className="jobs-tile" onClick={onClick} title={title} style={{ ...style, cursor: 'pointer' }}>{inner}</button>
    : <div style={style}>{inner}</div>;
}

// Detail modal for a posting: the assessment, the CV-fit gap breakdown, or the CV changes.
function PostingModal({ kind, posting: p, onClose }:
  { kind: ModalKind; posting: JobPostingDto; onClose: () => void }) {
  const fit = p.cvFitScore;
  const changes = (p.cvChangeList || '').split(/[;\n]+/).map(s => s.trim()).filter(Boolean);
  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal" onClick={e => e.stopPropagation()} style={{ maxWidth: 560 }}>
        {kind === 'assessment' && (
          <>
            <h2>Assessment{p.score != null ? ` · Score ${Math.round(p.score)}/100` : ''}</h2>
            <div style={{ fontSize: 14, lineHeight: 1.7, color: 'var(--text-secondary)' }}>
              {p.reasoning || 'No assessment recorded.'}
            </div>
          </>
        )}
        {kind === 'changes' && (
          <>
            <h2>CV changes vs. original</h2>
            {changes.length === 0
              ? <div style={{ color: 'var(--text-muted)' }}>No changes recorded.</div>
              : <ul style={{ margin: 0, paddingLeft: 20, fontSize: 14, lineHeight: 1.7, color: 'var(--text-secondary)' }}>
                  {changes.map((c, i) => <li key={i} style={{ marginBottom: 6 }}>{c}</li>)}
                </ul>}
          </>
        )}
        {kind === 'fit' && fit != null && (
          <>
            <h2>Missing for a 100% fit · {fit}/100</h2>
            {p.cvFitGaps.length === 0
              ? <div style={{ color: 'var(--text-muted)' }}>No gaps recorded — this CV is a strong match.</div>
              : <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
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
                </div>}
          </>
        )}
        <div className="modal-actions">
          <button type="button" className="btn btn-sm" onClick={onClose}>Close</button>
        </div>
      </div>
    </div>
  );
}

function Chip({ text }: { text: string }) {
  return (
    <span style={{
      fontSize: 11, fontWeight: 600, padding: '3px 10px', borderRadius: 999,
      background: 'var(--bg-secondary)', color: 'var(--text-muted)',
      border: '1px solid var(--border-subtle)', textTransform: 'capitalize',
    }}>{text}</span>
  );
}
