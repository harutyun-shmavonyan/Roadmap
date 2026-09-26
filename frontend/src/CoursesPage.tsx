import { useState, useEffect, useCallback, useMemo, useRef } from 'react';
import { marked } from 'marked';
import type {
  CourseSummaryDto, CourseDetailDto, CourseStageDto, LessonDetailDto, CourseEventDto,
  CourseResumeDto, CourseResourceDto, ExerciseDto, LessonStatus, CourseStatus,
} from './types';
import { api } from './api';

marked.setOptions({ gfm: true, breaks: true });

/**
 * The Courses tab. A course is written by the authoring agent over MCP — stages of lessons, each
 * lesson the sections its template prescribes plus graded exercises — and read here.
 *
 * Three depths, each with its own hash so the thing you were reading survives a reload and can be
 * linked: `#/courses`, `#/courses/{slug}`, `#/courses/{slug}/lessons/{code}`.
 *
 * Almost everything on screen is derived server-side (progress, scores, what is in focus) and
 * nothing is recomputed here: a percentage the tab worked out for itself is a percentage that can
 * disagree with the agent's.
 *
 * Colour does three jobs and no others — status, kind, and score. The reasoning, the tokens and
 * the contrast measurements live in the Courses block of styles.css.
 */

/* ─── status ───
   Five looks, not fifteen names: the vocabulary is longer than the number of distinct things a
   reader needs to tell apart at a glance. */

type Look = 'idle' | 'ready' | 'live' | 'done' | 'skip';

const LOOK: Record<string, Look> = {
  placeholder: 'idle', draft: 'idle', planned: 'idle',
  ready: 'ready',
  in_progress: 'live', submitted: 'live', active: 'live',
  completed: 'done',
  skipped: 'skip', archived: 'skip', paused: 'skip',
};

const look = (status: string): Look => LOOK[status] ?? 'idle';
const ink = (l: Look) => `var(--crs-${l}-ink)`;
const words = (s: string) => s.replace(/_/g, ' ');

function StatusPill({ status }: { status: string }) {
  const l = look(status);
  return (
    <span className="crs-pill" title={words(status)}
      style={{ color: ink(l), background: `var(--crs-${l}-bg)` }}>
      {words(status)}
    </span>
  );
}

/* A mark is good or bad, so it wears the status scale rather than an identity hue. */
const scoreLook = (score: number): Look => (score >= 85 ? 'done' : score >= 70 ? 'live' : 'skip');

function ScoreChip({ score }: { score: number }) {
  const l = scoreLook(score);
  return (
    <span className="crs-pill" title={`Lesson score ${score}%`}
      style={{ color: ink(l), background: `var(--crs-${l}-bg)` }}>
      {Math.round(score)}%
    </span>
  );
}

/* ─── kind ───
   A section kind or an exercise kind takes its hue from its place in the template's own list, so
   the hue belongs to the kind and not to what happens to be on screen. Eight hues; a template with
   more kinds than that reuses from the top rather than inventing one. */

function kindToken(kinds: string[], kind: string): string {
  const i = kinds.indexOf(kind);
  return `--k${(i < 0 ? kinds.length : i) % 8 + 1}`;
}
const kindHue = (kinds: string[], kind: string) => `var(${kindToken(kinds, kind)}-hue)`;

function KindChip({ kinds, kind, label }: { kinds: string[]; kind: string; label?: string }) {
  const t = kindToken(kinds, kind);
  return (
    <span className="crs-pill" style={{ color: `var(${t}-ink)`, background: `var(${t}-bg)` }}>
      {label ?? words(kind)}
    </span>
  );
}

/* Shape carries identity, colour reinforces it — the same rule the nav icons follow. A kind the
 * template invents gets the generic mark rather than no mark. */
const SECTION_ICON: Record<string, JSX.Element> = {
  problem: <><circle cx="12" cy="12" r="9" /><path d="M12 8v5" /><path d="M12 16.4v.1" /></>,
  theory: <><path d="M4 5.5h6a2 2 0 0 1 2 2v11a2.5 2.5 0 0 0-2.5-2H4z" /><path d="M20 5.5h-6a2 2 0 0 0-2 2v11a2.5 2.5 0 0 1 2.5-2H20z" /></>,
  questions: <><path d="M9.2 9.2a2.9 2.9 0 1 1 3.6 2.8c-.6.2-.8.7-.8 1.3v.6" /><path d="M12 17.4v.1" /><circle cx="12" cy="12" r="9" /></>,
  build: <><path d="M14.5 4.2a4.5 4.5 0 0 0-5.6 5.6L4 14.7V20h5.3l4.9-4.9a4.5 4.5 0 0 0 5.6-5.6l-2.9 2.9-2.3-2.3z" /></>,
  break_it: <><path d="M13.5 3.5 6 13h5l-1.5 7.5L18 11h-5z" /></>,
  capstone: <><path d="M12 3.5 14.6 9l6 .9-4.3 4.2 1 6-5.3-2.8L6.7 20l1-6L3.4 9.9 9.4 9z" /></>,
  flashcards: <><rect x="3.5" y="6.5" width="13" height="11" rx="2" /><path d="M7.5 6.5V5a1.5 1.5 0 0 1 1.5-1.5h9A1.5 1.5 0 0 1 19.5 5v9a1.5 1.5 0 0 1-1.5 1.5h-1.5" /></>,
  generic: <><rect x="4" y="4.5" width="16" height="15" rx="2.5" /><path d="M8 9.5h8M8 13h6" /></>,
};

function SectionIcon({ kind, color }: { kind: string; color: string }) {
  return (
    <svg className="crs-step-ico" viewBox="0 0 24 24" width="17" height="17" aria-hidden="true"
      fill="none" stroke={color} strokeWidth="1.9" strokeLinecap="round" strokeLinejoin="round">
      {SECTION_ICON[kind] ?? SECTION_ICON.generic}
    </svg>
  );
}

/* ─── motion ───
   One hook behind every number, bar and arc that moves. It tweens from the value it last showed to
   the new one, so a refetch reads as the course having moved rather than as the screen being
   replaced; under prefers-reduced-motion it returns the new value at once. */

function useTween(target: number, ms = 620): number {
  const reduced = useMemo(
    () => typeof window !== 'undefined' && window.matchMedia('(prefers-reduced-motion: reduce)').matches, []);
  const [shown, setShown] = useState(target);
  const from = useRef(target);
  const raf = useRef(0);

  useEffect(() => {
    if (reduced) { setShown(target); return; }
    const start = performance.now();
    const a = from.current;
    if (a === target) return;
    const step = (now: number) => {
      const t = Math.min(1, (now - start) / ms);
      // The same ease as the CSS transitions, so a tweened number and a tweened bar arrive together.
      const e = 1 - Math.pow(1 - t, 3);
      setShown(a + (target - a) * e);
      if (t < 1) raf.current = requestAnimationFrame(step);
      else from.current = target;
    };
    raf.current = requestAnimationFrame(step);
    return () => cancelAnimationFrame(raf.current);
  }, [target, ms, reduced]);

  useEffect(() => { if (reduced) from.current = target; }, [target, reduced]);
  return shown;
}

const pct = (x: number | null | undefined) => `${Math.round((x ?? 0) * 100)}%`;

function Pct({ value }: { value: number }) {
  return <>{Math.round(useTween(value) * 100)}%</>;
}

function Num({ value }: { value: number }) {
  const v = useTween(value);
  return <>{v.toFixed(v < 10 && Math.abs(v % 1) > 0.05 ? 1 : 0)}</>;
}

/* ─── primitives ─── */

function ProgressRing({ value, size = 76, stroke = 8, tone = 'live', children }:
  { value: number; size?: number; stroke?: number; tone?: Look; children?: React.ReactNode }) {
  const r = (size - stroke) / 2;
  const c = 2 * Math.PI * r;
  const v = Math.max(0, Math.min(1, value));
  return (
    <div style={{ position: 'relative', width: size, height: size, flex: `0 0 ${size}px` }}>
      <svg width={size} height={size} role="img" aria-label={`${Math.round(v * 100)} percent complete`}>
        <circle cx={size / 2} cy={size / 2} r={r} fill="none"
          stroke="var(--progress-track)" strokeWidth={stroke} />
        <circle className="crs-ring-arc" cx={size / 2} cy={size / 2} r={r} fill="none"
          stroke={ink(tone)} strokeWidth={stroke} strokeLinecap="round"
          strokeDasharray={c} strokeDashoffset={c * (1 - v)}
          transform={`rotate(-90 ${size / 2} ${size / 2})`} />
      </svg>
      <div style={{ position: 'absolute', inset: 0, display: 'grid', placeItems: 'center',
        fontSize: size > 80 ? 19 : size > 60 ? 15 : 12, fontWeight: 700, color: 'var(--text-primary)' }}>
        {children ?? <Pct value={v} />}
      </div>
    </div>
  );
}

function Bar({ value, tone = 'live', height = 5 }: { value: number; tone?: Look; height?: number }) {
  return (
    <div style={{ height, background: 'var(--progress-track)', borderRadius: 999, overflow: 'hidden' }}>
      <div className="crs-bar-fill"
        style={{ height: '100%', width: pct(value), background: ink(tone), borderRadius: 999 }} />
    </div>
  );
}

function Stat({ label, children, tone }: { label: string; children: React.ReactNode; tone?: Look }) {
  return (
    <div style={{ display: 'grid', gap: 2 }}>
      <div style={{ fontSize: 11, color: 'var(--text-secondary)', textTransform: 'uppercase',
        letterSpacing: '.06em', fontWeight: 800 }}>{label}</div>
      <div style={{ fontSize: 19, fontWeight: 700, lineHeight: 1.1,
        color: tone ? ink(tone) : 'var(--text-primary)' }}>{children}</div>
    </div>
  );
}

function Skel({ w, h, style }: { w?: number | string; h: number; style?: React.CSSProperties }) {
  return <div className="crs-skel" style={{ width: w ?? '100%', height: h, ...style }} />;
}

function Md({ src }: { src: string }) {
  return <div className="crs-md" dangerouslySetInnerHTML={{ __html: marked.parse(src) as string }} />;
}

/** Hand the document over as a real file, since that is what it is called. */
function downloadMd(name: string, body: string) {
  const url = URL.createObjectURL(new Blob([body], { type: 'text/markdown;charset=utf-8' }));
  const a = document.createElement('a');
  a.href = url; a.download = name;
  document.body.appendChild(a); a.click(); a.remove();
  setTimeout(() => URL.revokeObjectURL(url), 30_000);
}

function Resources({ items }: { items: CourseResourceDto[] }) {
  if (items.length === 0) return null;
  return (
    <div style={{ display: 'grid', gap: 8 }}>
      {items.map(r => (
        <div key={r.id} style={{ display: 'flex', gap: 8, alignItems: 'baseline', fontSize: 13.5 }}>
          <span className="crs-dot" style={{ background: 'var(--k2-hue)', alignSelf: 'center' }} />
          {r.url
            ? <a href={r.url} target="_blank" rel="noreferrer noopener"
                style={{ color: 'var(--accent)', fontWeight: 600 }}>{r.title} ↗</a>
            : <span style={{ fontWeight: 600, color: 'var(--text-primary)' }}>{r.title}</span>}
          <span style={{ color: 'var(--text-secondary)', fontSize: 11.5, textTransform: 'uppercase',
            letterSpacing: '.05em', fontWeight: 700 }}>{r.kind}</span>
          {r.noteMd && <span style={{ color: 'var(--text-secondary)', fontSize: 12.5 }}>{r.noteMd}</span>}
        </div>
      ))}
    </div>
  );
}

/* A collapsible block for the things worth having but not worth always seeing: the capstone, the
   timeline, the reference answer behind an exercise. */
function Fold({ title, count, children, open: initial = false }:
  { title: string; count?: number; children: React.ReactNode; open?: boolean }) {
  const [open, setOpen] = useState(initial);
  return (
    <div>
      <button className="btn btn-sm" aria-expanded={open} onClick={() => setOpen(o => !o)}>
        {open ? '▾' : '▸'} {title}{count !== undefined ? ` (${count})` : ''}
      </button>
      {open && <div style={{ marginTop: 12 }}>{children}</div>}
    </div>
  );
}

/* ─── the course timeline ─── */

const EVENT_WORD: Record<string, string> = {
  status_changed: 'status', submission_added: 'submitted', grade_added: 'graded',
  time_logged: 'time', structure_changed: 'written', handoff: 'handoff', note: 'note',
};
// The timeline's dots are the one place a hue names an event type rather than a section kind;
// they sit in a column of their own, beside the word they stand for.
const EVENT_HUE: Record<string, string> = {
  status_changed: 'var(--k2-hue)', submission_added: 'var(--k6-hue)', grade_added: 'var(--k3-hue)',
  time_logged: 'var(--k5-hue)', structure_changed: 'var(--k7-hue)', handoff: 'var(--k1-hue)',
  note: 'var(--k4-hue)',
};

function eventLine(e: CourseEventDto): string {
  const p = e.payload as Record<string, string | number | undefined>;
  switch (e.type) {
    case 'status_changed': return `${words(String(p.from ?? '?'))} → ${words(String(p.to ?? '?'))}`;
    case 'submission_added': return `attempt ${p.attemptNo} on ${p.exerciseTitle}`;
    case 'grade_added': return `${p.score}/${p.maxScore} on ${p.exerciseTitle}${p.regrade ? ' (regrade)' : ''}`;
    case 'time_logged': return `${p.minutes} min${p.note ? ` — ${p.note}` : ''}`;
    case 'handoff': return String(p.md ?? '');
    default: return String(p.summary ?? p.md ?? '');
  }
}

function when(iso: string): string {
  const t = Date.parse(iso);
  if (Number.isNaN(t)) return iso;
  const mins = Math.round((Date.now() - t) / 60000);
  if (mins < 1) return 'just now';
  if (mins < 60) return `${mins}m ago`;
  if (mins < 60 * 24) return `${Math.round(mins / 60)}h ago`;
  return new Date(t).toLocaleDateString();
}

function Timeline({ events }: { events: CourseEventDto[] }) {
  if (events.length === 0) return <p style={{ color: 'var(--text-secondary)', fontSize: 13 }}>Nothing yet.</p>;
  return (
    <div className="crs-tl">
      {events.map(e => (
        <div key={e.id} className="crs-tl-row">
          <span className="crs-tl-dot" style={{ ['--edge' as string]: EVENT_HUE[e.type] ?? 'var(--border)' }} />
          <span style={{ fontWeight: 800, color: 'var(--text-secondary)', fontSize: 11,
            textTransform: 'uppercase', letterSpacing: '.05em' }}>{EVENT_WORD[e.type] ?? e.type}</span>
          <span style={{ minWidth: 0, color: 'var(--text-primary)' }}>{eventLine(e)}</span>
          <span style={{ color: 'var(--text-secondary)', fontSize: 11.5, whiteSpace: 'nowrap' }}>
            {when(e.createdAt)} · {e.actor}
          </span>
        </div>
      ))}
    </div>
  );
}

/* ─── view 1: the course list ─── */

function CourseList({ onOpen }: { onOpen: (slug: string) => void }) {
  const [rows, setRows] = useState<CourseSummaryDto[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(() => {
    api.getCourses().then(setRows).catch(e => setError(String(e)));
  }, []);
  useEffect(load, [load]);

  if (error) return <p style={{ padding: 24, color: 'var(--danger)' }}>{error}</p>;

  if (!rows) return (
    <div style={{ padding: 24, display: 'grid', gap: 12, maxWidth: 900 }}>
      {[0, 1].map(i => (
        <div key={i} className="crs-card" style={{ display: 'flex', gap: 18, alignItems: 'center' }}>
          <Skel w={76} h={76} style={{ borderRadius: '50%' }} />
          <div style={{ flex: 1, display: 'grid', gap: 8 }}>
            <Skel w="45%" h={19} /><Skel w="70%" h={14} /><Skel h={5} />
          </div>
        </div>
      ))}
    </div>
  );

  if (rows.length === 0) return (
    <div style={{ padding: 32, maxWidth: 560, color: 'var(--text-secondary)', fontSize: 14, lineHeight: 1.7 }}>
      <div style={{ fontSize: 16, fontWeight: 600, color: 'var(--text-primary)', marginBottom: 8 }}>
        No courses yet
      </div>
      A course is written here by the authoring agent — it imports the plan, then fills in each
      lesson as you work through it. Ask it to load one and it will appear.
    </div>
  );

  return (
    <div style={{ padding: 24, display: 'grid', gap: 14, maxWidth: 900 }}>
      {rows.map(c => {
        const tone = look(c.status);
        return (
          <div key={c.id} className="crs-card crs-card-link crs-edge" role="link" tabIndex={0}
            onClick={() => onOpen(c.slug)}
            onKeyDown={e => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); onOpen(c.slug); } }}
            style={{ display: 'flex', gap: 18, alignItems: 'center',
              ['--edge' as string]: ink(tone), background: `var(--crs-hero), var(--bg-elevated)` }}>
            <ProgressRing value={c.progress} tone={tone} />
            <div style={{ flex: 1, minWidth: 0, display: 'grid', gap: 7 }}>
              <div style={{ display: 'flex', gap: 10, alignItems: 'center', flexWrap: 'wrap' }}>
                <span style={{ fontSize: 17.5, fontWeight: 750, color: 'var(--text-primary)' }}>{c.title}</span>
                <StatusPill status={c.status} />
              </div>
              {c.subtitle && <div style={{ fontSize: 13.5, color: 'var(--text-secondary)' }}>{c.subtitle}</div>}
              <div style={{ display: 'flex', gap: 20, flexWrap: 'wrap', fontSize: 12.5,
                color: 'var(--text-secondary)', marginTop: 2 }}>
                <span><b style={{ color: 'var(--text-primary)' }}><Pct value={c.definedFraction} /></b> written</span>
                {c.score != null && (
                  <span style={{ display: 'inline-flex', gap: 6, alignItems: 'center' }}>
                    scored <b style={{ color: ink(scoreLook(c.score)) }}>{c.score}%</b>
                  </span>
                )}
                {c.currentLesson && (
                  <span style={{ display: 'inline-flex', gap: 6, alignItems: 'center' }}>
                    <span className="crs-dot" style={{ background: ink(look(c.currentLesson.status)) }} />
                    on <b style={{ color: 'var(--text-primary)' }}>{c.currentLesson.code} {c.currentLesson.title}</b>
                  </span>
                )}
              </div>
            </div>
          </div>
        );
      })}
    </div>
  );
}

/* ─── view 2: one course ───
   The arc first: thirteen stages as one strip, each segment as wide as the weeks it is planned to
   take and as full as the work done in it. A rail of thirteen rows is a list you scroll; a strip is
   a shape you read, and it is the only place the whole course fits on one line. */

function CourseArc({ stages, activeId, onPick }:
  { stages: CourseStageDto[]; activeId: string; onPick: (id: string) => void }) {
  const total = stages.reduce((n, s) => n + (s.targetWeeks ?? 1), 0) || 1;
  const written = stages.reduce((n, s) => n + s.lessonsDefined, 0);
  const lessons = stages.reduce((n, s) => n + s.lessonsTotal, 0);
  return (
    <div style={{ display: 'grid', gap: 7 }}>
      <div className="crs-arc" role="group" aria-label="Stages of the course">
        {stages.map(s => {
          const tone = look(s.status);
          const done = s.status === 'skipped' ? 1 : s.progress;
          return (
            <button key={s.id} className={`crs-arc-seg ${s.id === activeId ? 'active' : ''}`}
              onClick={() => onPick(s.id)}
              title={`${s.code} · ${s.title} — ${words(s.status)}, ${Math.round(s.progress * 100)}% done, `
                + `${s.lessonsDefined}/${s.lessonsTotal} written`
                + (s.score != null ? `, scored ${s.score}%` : '')}
              aria-label={`${s.code} ${s.title}, ${words(s.status)}, ${Math.round(s.progress * 100)} percent done, ${s.lessonsDefined} of ${s.lessonsTotal} lessons written`}
              style={{ flex: `${(s.targetWeeks ?? 1) / total} 1 0`, ['--edge' as string]: ink(tone) }}>
              {/* Two depths in one segment: how much of the stage is written, and how much of it is
                  done. A course spends months with the first well ahead of the second, and a strip
                  that showed only progress would say "nothing here" about a stage fully drafted. */}
              <i className="crs-arc-written" style={{ width: pct(s.definedFraction) }} />
              <i style={{ width: pct(done) }} />
            </button>
          );
        })}
      </div>
      <div style={{ display: 'flex', justifyContent: 'space-between', gap: 12, fontSize: 11,
        color: 'var(--text-secondary)', fontWeight: 700, letterSpacing: '.04em' }}>
        <span style={{ flex: '0 0 auto', whiteSpace: 'nowrap' }}>{stages[0]?.code}</span>
        <span style={{ flex: '1 1 auto', textAlign: 'center', minWidth: 0, overflow: 'hidden',
          textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
          {stages.length} stages · {written}/{lessons} lessons written · {total} weeks planned
        </span>
        <span style={{ flex: '0 0 auto', whiteSpace: 'nowrap' }}>{stages[stages.length - 1]?.code}</span>
      </div>
    </div>
  );
}

function StageRail({ stages, activeId, onPick }:
  { stages: CourseStageDto[]; activeId: string; onPick: (id: string) => void }) {
  return (
    <div className="crs-rail" role="tablist" aria-label="Stages">
      {stages.map((s, i) => {
        const tone = look(s.status);
        return (
          <button key={s.id} role="tab" aria-selected={s.id === activeId}
            className={`crs-rail-item ${s.id === activeId ? 'active' : ''}`} onClick={() => onPick(s.id)}
            title={`${s.code} · ${s.title}`} style={{ ['--edge' as string]: ink(tone) }}>
            <span className="crs-rail-num">{i + 1}</span>
            <span style={{ display: 'grid', gap: 4, minWidth: 0 }}>
              <span style={{ display: 'flex', gap: 7, alignItems: 'baseline', minWidth: 0 }}>
                <span style={{ fontSize: 10.5, fontWeight: 800, color: 'var(--text-secondary)',
                  flex: '0 0 auto' }}>{s.code}</span>
                <span style={{ fontSize: 13, fontWeight: 600, color: 'var(--text-primary)',
                  overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{s.title}</span>
              </span>
              <Bar value={s.progress} tone={tone} height={3} />
              <span style={{ fontSize: 11, color: 'var(--text-secondary)' }}>
                {words(s.status)} · {s.lessonsDefined}/{s.lessonsTotal} written
                {s.targetWeeks != null ? ` · ${s.targetWeeks}w` : ''}
                {s.score != null && <>
                  {' · '}<b style={{ color: ink(scoreLook(s.score)) }}>{s.score}%</b>
                </>}
              </span>
            </span>
          </button>
        );
      })}
    </div>
  );
}

function CourseDetail({ slug, onOpenLesson, onBack }:
  { slug: string; onOpenLesson: (code: string) => void; onBack: () => void }) {
  const [course, setCourse] = useState<CourseDetailDto | null>(null);
  const [resume, setResume] = useState<CourseResumeDto | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [stageId, setStageId] = useState<string>('');
  const [busy, setBusy] = useState(false);

  const load = useCallback(() => {
    Promise.all([api.getCourse(slug), api.getCourseResume(slug)])
      .then(([c, r]) => { setCourse(c); setResume(r); })
      .catch(e => setError(String(e)));
  }, [slug]);
  useEffect(load, [load]);

  // Refetching on focus is what makes the tab feel live while the agent writes into it from
  // somewhere else; the tweens are what make the new numbers legible when it does.
  useEffect(() => {
    const onFocus = () => load();
    window.addEventListener('focus', onFocus);
    return () => window.removeEventListener('focus', onFocus);
  }, [load]);

  // Follow the course's own idea of where the work is, until the reader picks a stage themselves.
  const stages = course?.stages ?? [];
  const focusStage = useMemo(() => {
    const code = resume?.currentStage?.code;
    return stages.find(s => s.code === code)?.id
      ?? stages.find(s => s.status === 'in_progress')?.id
      ?? stages[0]?.id ?? '';
  }, [stages, resume?.currentStage?.code]);
  const active = stages.find(s => s.id === (stageId || focusStage));
  const sectionKinds = (course?.template.sections ?? []).map(s => s.kind);

  if (error) return <p style={{ padding: 24, color: 'var(--danger)' }}>{error}</p>;

  if (!course) return (
    <div style={{ padding: 24, display: 'grid', gap: 18, maxWidth: 1100 }}>
      <div className="crs-hero" style={{ display: 'flex', gap: 20, alignItems: 'center' }}>
        <Skel w={104} h={104} style={{ borderRadius: '50%' }} />
        <div style={{ flex: 1, display: 'grid', gap: 10 }}><Skel w="40%" h={26} /><Skel w="60%" h={15} /><Skel h={26} /></div>
      </div>
      <div className="crs-split" style={{ display: 'grid', gridTemplateColumns: '286px 1fr', gap: 20 }}>
        <div style={{ display: 'grid', gap: 2 }}>{[0, 1, 2, 3, 4].map(i => <Skel key={i} h={54} />)}</div>
        <div style={{ display: 'grid', gap: 10 }}>{[0, 1, 2].map(i => <Skel key={i} h={72} />)}</div>
      </div>
    </div>
  );

  const tone = look(course.status);
  const setStatus = async (status: CourseStatus) => {
    setBusy(true);
    try { setCourse(await api.setCourseStatus(slug, status)); } finally { setBusy(false); }
  };

  return (
    <div style={{ padding: 24, display: 'grid', gap: 18, maxWidth: 1100 }}>
      <button className="btn btn-sm" onClick={onBack} style={{ justifySelf: 'start' }}>← Courses</button>

      <div className="crs-hero" style={{ display: 'grid', gap: 18 }}>
        <div style={{ display: 'flex', gap: 22, alignItems: 'center', flexWrap: 'wrap' }}>
          <ProgressRing value={course.progress} size={104} stroke={10} tone={tone} />
          <div style={{ flex: 1, minWidth: 240, display: 'grid', gap: 7 }}>
            <div style={{ display: 'flex', gap: 10, alignItems: 'center', flexWrap: 'wrap' }}>
              <h2 style={{ margin: 0, fontSize: 23, letterSpacing: '-.015em', color: 'var(--text-primary)' }}>
                {course.title}
              </h2>
              <StatusPill status={course.status} />
            </div>
            {course.subtitle && (
              <div style={{ fontSize: 13.5, color: 'var(--text-secondary)', maxWidth: '62ch' }}>{course.subtitle}</div>
            )}
            <div style={{ display: 'flex', gap: 26, flexWrap: 'wrap', marginTop: 6 }}>
              <Stat label="Written"><Pct value={course.definedFraction} /></Stat>
              <Stat label="Score" tone={course.score != null ? scoreLook(course.score) : undefined}>
                {course.score != null ? `${course.score}%` : '—'}
              </Stat>
              {course.targetHoursPerWeek != null && <Stat label="Per week">{course.targetHoursPerWeek}</Stat>}
            </div>
          </div>
          <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap', alignSelf: 'flex-start' }}>
            {(['active', 'paused', 'completed'] as CourseStatus[])
              .filter(s => s !== course.status)
              .map(s => <button key={s} className="btn btn-sm" disabled={busy} onClick={() => setStatus(s)}>
                {s === 'active' ? 'Start' : s === 'paused' ? 'Pause' : 'Complete'}
              </button>)}
          </div>
        </div>
        <CourseArc stages={stages} activeId={active?.id ?? ''} onPick={setStageId} />
      </div>

      {/* Where to sit down, and what the last session said it was in the middle of. */}
      {(resume?.currentLesson || resume?.latestHandoff || resume?.nextUndefinedStage) && (
        <div className="crs-card crs-edge" style={{ display: 'grid', gap: 11,
          ['--edge' as string]: resume.currentLesson ? ink(look(resume.currentLesson.status)) : 'var(--k1-hue)' }}>
          {resume.currentLesson ? (
            <div style={{ display: 'flex', gap: 12, alignItems: 'center', flexWrap: 'wrap' }}>
              <span style={{ fontSize: 11, fontWeight: 800, color: 'var(--text-secondary)',
                textTransform: 'uppercase', letterSpacing: '.06em' }}>Continue</span>
              <button className="btn btn-sm btn-accent" onClick={() => onOpenLesson(resume.currentLesson!.code)}>
                {resume.currentLesson.code} · {resume.currentLesson.title} →
              </button>
              <StatusPill status={resume.currentLesson.status} />
              {resume.pendingExercises.length > 0 && (
                <span style={{ fontSize: 12.5, color: 'var(--text-secondary)' }}>
                  {resume.pendingExercises.length} exercise{resume.pendingExercises.length === 1 ? '' : 's'} still ungraded
                </span>
              )}
            </div>
          ) : resume.nextUndefinedStage && (
            <div style={{ fontSize: 13.5, color: 'var(--text-secondary)' }}>
              <span style={{ fontSize: 11, fontWeight: 800, textTransform: 'uppercase',
                letterSpacing: '.06em', marginRight: 10 }}>Next to write</span>
              <b style={{ color: 'var(--text-primary)' }}>
                {resume.nextUndefinedStage.code} {resume.nextUndefinedStage.title}
              </b>{' '}
              — {resume.nextUndefinedStage.placeholderLessons} lesson
              {resume.nextUndefinedStage.placeholderLessons === 1 ? '' : 's'} outlined, none written.
            </div>
          )}
          {resume.latestHandoff?.md && (
            <div style={{ fontSize: 13.5, color: 'var(--text-primary)', background: 'var(--bg-secondary)',
              borderRadius: 'var(--radius-md)', padding: '10px 13px', lineHeight: 1.6 }}>
              <span style={{ fontSize: 11, fontWeight: 800, color: 'var(--text-secondary)',
                textTransform: 'uppercase', letterSpacing: '.06em', marginRight: 8 }}>Handoff</span>
              {resume.latestHandoff.md}
              <span style={{ color: 'var(--text-secondary)', fontSize: 12 }}> — {when(resume.latestHandoff.createdAt)}</span>
            </div>
          )}
        </div>
      )}

      <div className="crs-split" style={{ display: 'grid', gridTemplateColumns: '286px 1fr', gap: 20, alignItems: 'start' }}>
        <StageRail stages={stages} activeId={active?.id ?? ''} onPick={setStageId} />

        <div style={{ display: 'grid', gap: 11, minWidth: 0 }}>
          {active && <>
            <div style={{ display: 'flex', gap: 10, alignItems: 'baseline', flexWrap: 'wrap' }}>
              <h3 style={{ margin: 0, fontSize: 17, color: 'var(--text-primary)' }}>{active.code} · {active.title}</h3>
              <StatusPill status={active.status} />
              {active.score != null && <ScoreChip score={active.score} />}
              {active.targetWeeks != null && (
                <span style={{ fontSize: 12.5, color: 'var(--text-secondary)' }}>
                  {active.targetWeeks} week{active.targetWeeks === 1 ? '' : 's'} planned
                </span>
              )}
            </div>
            {(active.lessons ?? []).length === 0
              ? <p style={{ color: 'var(--text-secondary)', fontSize: 13.5 }}>
                  This stage has no lessons outlined yet.
                </p>
              : (active.lessons ?? []).map(l => {
                const blank = l.status === 'placeholder';
                const lt = look(l.status);
                return (
                  <div key={l.id} className={`crs-card crs-edge ${blank ? '' : 'crs-card-link'}`}
                    role={blank ? undefined : 'link'} tabIndex={blank ? -1 : 0}
                    onClick={() => !blank && onOpenLesson(l.code)}
                    onKeyDown={e => { if (!blank && (e.key === 'Enter' || e.key === ' ')) { e.preventDefault(); onOpenLesson(l.code); } }}
                    style={{ padding: '13px 15px', display: 'grid', gap: 8,
                      ['--edge' as string]: blank ? 'var(--border-subtle)' : ink(lt) }}>
                    <div style={{ display: 'flex', gap: 10, alignItems: 'baseline', flexWrap: 'wrap' }}>
                      <span style={{ fontSize: 11, fontWeight: 800, color: 'var(--text-secondary)' }}>{l.code}</span>
                      <span style={{ fontSize: 15, fontWeight: 650, color: 'var(--text-primary)', minWidth: 0 }}>{l.title}</span>
                      <StatusPill status={l.status} />
                      {l.score != null && <ScoreChip score={l.score} />}
                      <span style={{ marginLeft: 'auto', fontSize: 11.5, color: 'var(--text-secondary)' }}>
                        {blank ? 'outlined only' : `${l.exerciseCount} exercise${l.exerciseCount === 1 ? '' : 's'}`}
                        {l.estimatedHours != null ? ` · ${l.estimatedHours} h` : ''}
                      </span>
                    </div>
                    <Bar value={l.progress} tone={lt} height={4} />
                  </div>
                );
              })}
          </>}
        </div>
      </div>

      {course.instructionsMd && (
        <Fold title="instructions.md">
          <div style={{ display: 'grid', gap: 10 }}>
            <div style={{ fontSize: 12.5, color: 'var(--text-secondary)' }}>
              How this course is taught, graded and written. The agent reads this before every
              session; change it by asking the agent, not here.
            </div>
            <Md src={course.instructionsMd} />
            <button className="btn btn-sm" style={{ justifySelf: 'start' }}
              onClick={() => downloadMd(`${course.slug}-instructions.md`, course.instructionsMd!)}>
              Download instructions.md ↓
            </button>
          </div>
        </Fold>
      )}
      {course.capstoneMd && <Fold title="Capstone"><Md src={course.capstoneMd} /></Fold>}
      {course.descriptionMd && <Fold title="About this course"><Md src={course.descriptionMd} /></Fold>}
      {course.resources.length > 0 && (
        <Fold title="Resources" count={course.resources.length}><Resources items={course.resources} /></Fold>
      )}
      <Fold title="Timeline"><Timeline events={resume?.recentEvents ?? []} /></Fold>
      {sectionKinds.length > 0 && (
        /* The legend: identity is never colour alone, and this is where the hues are named. */
        <div style={{ display: 'flex', gap: 14, flexWrap: 'wrap', fontSize: 11.5,
          color: 'var(--text-secondary)', paddingTop: 4 }}>
          {(course.template.sections ?? []).map(t => (
            <span key={t.kind} style={{ display: 'inline-flex', gap: 6, alignItems: 'center' }}>
              <span className="crs-dot" style={{ background: kindHue(sectionKinds, t.kind) }} />
              {t.title ?? words(t.kind)}
            </span>
          ))}
        </div>
      )}
    </div>
  );
}

/* ─── view 3: one lesson ─── */

function Exercise({ x, kinds }: { x: ExerciseDto; kinds: string[] }) {
  const subs = x.submissions ?? [];
  const latest = subs.length > 0 ? subs[subs.length - 1] : undefined;
  // The grade that counts is the newest one nothing supersedes; the rest is history, kept because
  // how the work went should not be rewritten by how it ended.
  const grades = subs.flatMap(s => s.grades ?? []);
  const superseded = new Set(grades.map(g => g.supersedes).filter(Boolean) as string[]);
  const live = grades.filter(g => !superseded.has(g.id))
    .sort((a, b) => Date.parse(b.gradedAt) - Date.parse(a.gradedAt))[0];
  const history = grades.filter(g => g.id !== live?.id);
  const band = live ? scoreLook((live.score / live.maxScore) * 100) : 'idle';

  return (
    <div className="crs-card crs-edge" style={{ display: 'grid', gap: 10,
      ['--edge' as string]: kindHue(kinds, x.kind) }}>
      <div style={{ display: 'flex', gap: 10, alignItems: 'baseline', flexWrap: 'wrap' }}>
        <span style={{ fontSize: 15, fontWeight: 650, color: 'var(--text-primary)' }}>{x.title}</span>
        <KindChip kinds={kinds} kind={x.kind} />
        {!x.required && <span style={{ fontSize: 12, color: 'var(--text-secondary)' }}>optional</span>}
        <span style={{ marginLeft: 'auto', fontSize: 12, color: 'var(--text-secondary)' }}>
          {x.attempts} attempt{x.attempts === 1 ? '' : 's'} · weight {x.weight} · out of {x.maxScore}
        </span>
      </div>

      <Md src={x.promptMd} />

      {live ? (
        <div style={{ display: 'grid', gap: 7, background: `var(--crs-${band}-bg)`,
          padding: '10px 12px', borderRadius: 'var(--radius-md)' }}>
          <div style={{ display: 'flex', gap: 10, alignItems: 'center', flexWrap: 'wrap' }}>
            <b style={{ color: ink(band), fontSize: 16 }}>{live.score} / {live.maxScore}</b>
            <div style={{ flex: '1 1 120px', maxWidth: 220 }}>
              <Bar value={live.score / live.maxScore} tone={band} height={5} />
            </div>
            <span style={{ fontSize: 12, color: 'var(--text-secondary)' }}>
              {live.gradedBy} · {when(live.gradedAt)}
            </span>
          </div>
          {live.feedbackMd && <Md src={live.feedbackMd} />}
        </div>
      ) : (
        <div style={{ fontSize: 12.5, color: 'var(--text-secondary)' }}>
          {latest ? 'Submitted, not graded yet.' : 'No attempt yet.'}
        </div>
      )}

      <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', alignItems: 'center' }}>
        {latest?.contentMd && <Fold title={`Attempt ${latest.attemptNo}`}><Md src={latest.contentMd} /></Fold>}
        {history.length > 0 && (
          <Fold title="Earlier marks" count={history.length}>
            <div style={{ display: 'grid', gap: 5, fontSize: 13 }}>
              {history.map(g => (
                <div key={g.id} style={{ color: 'var(--text-secondary)' }}>
                  <s>{g.score} / {g.maxScore}</s> · {g.gradedBy} · {when(g.gradedAt)}
                </div>
              ))}
            </div>
          </Fold>
        )}
        {x.referenceMd && <Fold title="Reference answer"><Md src={x.referenceMd} /></Fold>}
        {(latest?.links?.length ?? 0) > 0 && latest!.links.map((l, i) => (
          <a key={i} href={l.url} target="_blank" rel="noreferrer noopener"
            style={{ color: 'var(--accent)', fontWeight: 600, fontSize: 13 }}>{l.label ?? l.kind ?? l.url} ↗</a>
        ))}
      </div>
    </div>
  );
}

// Lessons move through these; anything may be set aside. The list mirrors what the API accepts, and
// the tab offers only the moves that make sense from where the lesson is.
const LESSON_MOVES: Record<LessonStatus, LessonStatus[]> = {
  placeholder: ['draft', 'skipped'],
  draft: ['ready', 'skipped'],
  ready: ['in_progress', 'skipped'],
  in_progress: ['submitted', 'completed', 'skipped'],
  submitted: ['in_progress', 'completed', 'skipped'],
  completed: ['in_progress'],
  skipped: ['draft', 'ready'],
};

function LessonView({ slug, code, onBack }: { slug: string; code: string; onBack: () => void }) {
  const [lesson, setLesson] = useState<LessonDetailDto | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [note, setNote] = useState('');
  const [said, setSaid] = useState<string | null>(null);
  const refs = useRef<Record<string, HTMLDivElement | null>>({});

  const load = useCallback(() => {
    api.getCourseLesson(slug, code).then(setLesson).catch(e => setError(String(e)));
  }, [slug, code]);
  useEffect(load, [load]);

  if (error) return <div style={{ padding: 24 }}>
    <button className="btn btn-sm" onClick={onBack}>← Course</button>
    <p style={{ color: 'var(--danger)' }}>{error}</p>
  </div>;

  if (!lesson) return (
    <div style={{ padding: 24, display: 'grid', gap: 16, maxWidth: 720, margin: '0 auto' }}>
      <Skel w={190} h={28} />
      <div className="crs-hero" style={{ display: 'grid', gap: 12 }}><Skel w="55%" h={26} /><Skel w="70%" h={19} /></div>
      <div style={{ display: 'flex', gap: 8 }}>{[0, 1, 2, 3, 4].map(i => <Skel key={i} w={122} h={33} style={{ borderRadius: 999 }} />)}</div>
      {[0, 1].map(i => <Skel key={i} h={150} />)}
    </div>
  );

  const sectionKinds = lesson.templateSections.map(s => s.kind);
  const exerciseKinds = lesson.templateExerciseKinds.map(k => k.kind);
  const written = new Map((lesson.sections ?? []).map(s => [s.kind, s]));
  const exercisesFor = (kind: string) => (lesson.exercises ?? []).filter(x => x.sectionKind === kind);
  // An exercise whose section kind is not in the template still has to appear somewhere.
  const orphans = (lesson.exercises ?? []).filter(x => !sectionKinds.includes(x.sectionKind));
  const tone = look(lesson.status);

  const move = async (to: LessonStatus) => {
    const needsForce = to === 'completed'
      && (lesson.exercises ?? []).some(x => x.required && !(x.submissions ?? []).some(s => (s.grades ?? []).length > 0));
    if (needsForce && !window.confirm(
      'Some required exercises have no grade yet. Complete anyway? The override is recorded in the timeline.'))
      return;
    setBusy(true);
    try { setLesson(await api.setLessonStatus(slug, code, to, needsForce)); }
    catch (e) { setSaid(String(e)); }
    finally { setBusy(false); }
  };

  const addNote = async () => {
    if (!note.trim()) return;
    setBusy(true);
    try {
      await api.logCourseEvent(slug, 'note', { md: note.trim() }, code);
      setNote(''); setSaid('Note saved.');
    } finally { setBusy(false); }
  };

  return (
    <div style={{ padding: '24px 24px 72px', display: 'grid', gap: 18, maxWidth: 720, margin: '0 auto' }}>
      <button className="btn btn-sm" onClick={onBack} style={{ justifySelf: 'start' }}>
        ← {lesson.stage.code} {lesson.stage.title}
      </button>

      <div className="crs-hero crs-edge" style={{ display: 'grid', gap: 12, ['--edge' as string]: ink(tone) }}>
        <div style={{ display: 'flex', gap: 10, alignItems: 'baseline', flexWrap: 'wrap' }}>
          <span style={{ fontSize: 12.5, fontWeight: 800, color: 'var(--text-secondary)' }}>{lesson.code}</span>
          <h2 style={{ margin: 0, fontSize: 22, letterSpacing: '-.015em', color: 'var(--text-primary)' }}>
            {lesson.title}
          </h2>
          <StatusPill status={lesson.status} />
        </div>
        <div style={{ display: 'flex', gap: 26, flexWrap: 'wrap', alignItems: 'center' }}>
          <Stat label="Progress"><Pct value={lesson.progress ?? 0} /></Stat>
          <Stat label="Score" tone={lesson.score != null ? scoreLook(lesson.score) : undefined}>
            {lesson.score != null ? `${Math.round(lesson.score)}%` : '—'}
          </Stat>
          <Stat label="Graded"><Pct value={lesson.gradedFraction ?? 0} /></Stat>
          {lesson.estimatedHours != null && <Stat label="Estimated">{lesson.estimatedHours} h</Stat>}
          <div style={{ flex: '1 1 160px', minWidth: 120 }}>
            <Bar value={lesson.progress ?? 0} tone={tone} height={6} />
          </div>
        </div>
        {lesson.summaryMd && <Md src={lesson.summaryMd} />}
      </div>

      {/* The template's rhythm, every step of it, written or blank — so a half-written lesson looks
          unfinished rather than short. The icon names the step; the hue is the same one its
          heading and its exercises wear further down. */}
      <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
        {lesson.templateSections.map(t => {
          const has = written.has(t.kind);
          const n = exercisesFor(t.kind).length;
          const hue = kindHue(sectionKinds, t.kind);
          return (
            <button key={t.kind} className={`crs-step ${has ? '' : 'blank'}`}
              style={{ ['--edge' as string]: hue }}
              onClick={() => refs.current[t.kind]?.scrollIntoView({ behavior: 'smooth', block: 'start' })}
              title={has ? `Go to ${t.title ?? t.kind}` : 'Not written yet'}>
              <SectionIcon kind={t.kind} color={has ? hue : 'var(--text-secondary)'} />
              {t.title ?? words(t.kind)}
              {n > 0 && <span className="crs-step-n">{n}</span>}
            </button>
          );
        })}
      </div>

      {lesson.templateSections.map(t => {
        const sec = written.get(t.kind);
        const xs = exercisesFor(t.kind);
        return (
          <div key={t.kind} ref={el => { refs.current[t.kind] = el; }} style={{ display: 'grid', gap: 12 }}>
            <h3 className="crs-sec-h crs-read" style={{ ['--edge' as string]: kindHue(sectionKinds, t.kind) }}>
              <SectionIcon kind={t.kind} color={kindHue(sectionKinds, t.kind)} />
              {t.title ?? words(t.kind)}
              {!t.required && <span style={{ textTransform: 'none', letterSpacing: 0, fontWeight: 500,
                color: 'var(--text-secondary)', fontSize: 12 }}>optional</span>}
            </h3>
            {sec ? <Md src={sec.contentMd} />
              : <p style={{ color: 'var(--text-secondary)', fontSize: 13.5, margin: 0 }}>Not written yet.</p>}
            {xs.map(x => <Exercise key={x.id} x={x} kinds={exerciseKinds} />)}
          </div>
        );
      })}

      {orphans.length > 0 && (
        <div style={{ display: 'grid', gap: 12 }}>
          <h3 className="crs-sec-h" style={{ ['--edge' as string]: 'var(--border)' }}>Other exercises</h3>
          {orphans.map(x => <Exercise key={x.id} x={x} kinds={exerciseKinds} />)}
        </div>
      )}

      {lesson.resources.length > 0 && (
        <Fold title="Resources" count={lesson.resources.length} open>
          <Resources items={lesson.resources} />
        </Fold>
      )}

      {/* The two things a person writes here. Everything else about a lesson is the agent's. */}
      <div className="crs-card" style={{ display: 'grid', gap: 12 }}>
        <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
          <span style={{ fontSize: 11, fontWeight: 800, color: 'var(--text-secondary)',
            textTransform: 'uppercase', letterSpacing: '.06em' }}>Move to</span>
          {LESSON_MOVES[lesson.status].map(to => (
            <button key={to} className="btn btn-sm" disabled={busy} onClick={() => move(to)}>{words(to)}</button>
          ))}
        </div>
        <div style={{ display: 'flex', gap: 8, alignItems: 'flex-start', flexWrap: 'wrap' }}>
          <textarea className="inline-input" rows={2} placeholder="A note on this lesson…"
            style={{ flex: 1, minWidth: 220, resize: 'vertical' }}
            value={note} onChange={e => setNote(e.target.value)} aria-label="Note" />
          <button className="btn btn-sm" disabled={busy || !note.trim()} onClick={addNote}>Add note</button>
        </div>
        {said && <div role="status" style={{ fontSize: 12.5, color: 'var(--text-secondary)' }}>{said}</div>}
      </div>
    </div>
  );
}

/* ─── the tab ─── */

export function CoursesPage({ sub, go }: { sub: string[]; go: (hash: string, replace?: boolean) => void }) {
  // #/courses → list, #/courses/{slug} → course, #/courses/{slug}/lessons/{code} → lesson.
  const slug = sub[0];
  const lessonCode = sub[1] === 'lessons' ? sub.slice(2).join('/') : undefined;

  if (!slug) return <CourseList onOpen={s => go(`#/courses/${encodeURIComponent(s)}`)} />;

  if (lessonCode) return (
    <div style={{ height: '100%', overflowY: 'auto', WebkitOverflowScrolling: 'touch' }}>
      <LessonView slug={slug} code={decodeURIComponent(lessonCode)}
        onBack={() => go(`#/courses/${encodeURIComponent(slug)}`)} />
    </div>
  );

  return (
    <div style={{ height: '100%', overflowY: 'auto', WebkitOverflowScrolling: 'touch' }}>
      <CourseDetail slug={slug}
        onOpenLesson={c => go(`#/courses/${encodeURIComponent(slug)}/lessons/${encodeURIComponent(c)}`)}
        onBack={() => go('#/courses')} />
    </div>
  );
}
