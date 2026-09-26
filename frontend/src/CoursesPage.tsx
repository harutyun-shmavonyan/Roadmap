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
 */

/* ─── status vocabulary ─── */

// Five looks, not fifteen names. The tokens live in styles.css next to the rest of the palette.
type Look = 'idle' | 'ready' | 'live' | 'done' | 'skip';

const LOOK: Record<string, Look> = {
  placeholder: 'idle', draft: 'idle', planned: 'idle',
  ready: 'ready',
  in_progress: 'live', submitted: 'live', active: 'live',
  completed: 'done',
  skipped: 'skip', archived: 'skip', paused: 'skip',
};

const words = (s: string) => s.replace(/_/g, ' ');

function StatusPill({ status, title }: { status: string; title?: string }) {
  const look = LOOK[status] ?? 'idle';
  return (
    <span className="crs-pill" title={title ?? words(status)}
      style={{ color: `var(--crs-${look}-ink)`, background: `var(--crs-${look}-bg)` }}>
      {words(status)}
    </span>
  );
}

/* ─── motion ───
   One hook behind every number and bar that moves. It tweens from the value it last showed to the
   new one, so a refetch reads as the course having moved rather than as the screen being replaced;
   under prefers-reduced-motion it simply returns the new value. */

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

function Num({ value, unit }: { value: number; unit?: string }) {
  const v = useTween(value);
  return <>{v.toFixed(v < 10 && v % 1 !== 0 ? 1 : 0)}{unit ? ` ${unit}` : ''}</>;
}

/* ─── primitives ─── */

function ProgressRing({ value, size = 76, stroke = 8, look = 'live', children }:
  { value: number; size?: number; stroke?: number; look?: Look; children?: React.ReactNode }) {
  const r = (size - stroke) / 2;
  const c = 2 * Math.PI * r;
  const clamped = Math.max(0, Math.min(1, value));
  return (
    <div style={{ position: 'relative', width: size, height: size, flex: `0 0 ${size}px` }}>
      <svg width={size} height={size} role="img"
        aria-label={`${Math.round(clamped * 100)} percent complete`}>
        <circle cx={size / 2} cy={size / 2} r={r} fill="none"
          stroke="var(--progress-track)" strokeWidth={stroke} />
        <circle className="crs-ring-arc" cx={size / 2} cy={size / 2} r={r} fill="none"
          stroke={`var(--crs-${look}-ink)`} strokeWidth={stroke} strokeLinecap="round"
          strokeDasharray={c} strokeDashoffset={c * (1 - clamped)}
          transform={`rotate(-90 ${size / 2} ${size / 2})`} />
      </svg>
      <div style={{ position: 'absolute', inset: 0, display: 'grid', placeItems: 'center',
        fontSize: size > 60 ? 15 : 12, fontWeight: 700, color: 'var(--text-primary)' }}>
        {children ?? <Pct value={clamped} />}
      </div>
    </div>
  );
}

function Bar({ value, look = 'live', height = 5 }: { value: number; look?: Look; height?: number }) {
  return (
    <div style={{ height, background: 'var(--progress-track)', borderRadius: 999, overflow: 'hidden' }}>
      <div className="crs-bar-fill" style={{ height: '100%', width: pct(value),
        background: `var(--crs-${look}-ink)`, borderRadius: 999 }} />
    </div>
  );
}

function Stat({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div>
      <div style={{ fontSize: 11.5, color: 'var(--text-muted)', textTransform: 'uppercase',
        letterSpacing: '.05em', fontWeight: 700 }}>{label}</div>
      <div style={{ fontSize: 15, fontWeight: 600, color: 'var(--text-primary)', marginTop: 2 }}>{children}</div>
    </div>
  );
}

function Skel({ w, h, style }: { w?: number | string; h: number; style?: React.CSSProperties }) {
  return <div className="crs-skel" style={{ width: w ?? '100%', height: h, ...style }} />;
}

function Md({ src }: { src: string }) {
  return <div className="crs-md" dangerouslySetInnerHTML={{ __html: marked.parse(src) as string }} />;
}

function Resources({ items }: { items: CourseResourceDto[] }) {
  if (items.length === 0) return null;
  return (
    <div style={{ display: 'grid', gap: 6 }}>
      {items.map(r => (
        <div key={r.id} style={{ fontSize: 13.5 }}>
          {r.url
            ? <a href={r.url} target="_blank" rel="noreferrer noopener"
                style={{ color: 'var(--accent)', fontWeight: 600 }}>{r.title} ↗</a>
            : <span style={{ fontWeight: 600, color: 'var(--text-primary)' }}>{r.title}</span>}
          <span style={{ color: 'var(--text-muted)', marginLeft: 8, fontSize: 12 }}>{r.kind}</span>
          {r.noteMd && <div style={{ color: 'var(--text-muted)', fontSize: 12.5, marginTop: 2 }}>{r.noteMd}</div>}
        </div>
      ))}
    </div>
  );
}

/* A collapsible block, used for the things worth having but not worth always seeing: the capstone,
   the timeline, the reference answer behind an exercise. */
function Fold({ title, count, children, open: initial = false }:
  { title: string; count?: number; children: React.ReactNode; open?: boolean }) {
  const [open, setOpen] = useState(initial);
  return (
    <div>
      <button className="btn btn-sm" aria-expanded={open} onClick={() => setOpen(o => !o)}>
        {open ? '▾' : '▸'} {title}{count !== undefined ? ` (${count})` : ''}
      </button>
      {open && <div style={{ marginTop: 10 }}>{children}</div>}
    </div>
  );
}

/* ─── the course timeline ─── */

const EVENT_WORD: Record<string, string> = {
  status_changed: 'status', submission_added: 'submitted', grade_added: 'graded',
  time_logged: 'time', structure_changed: 'written', handoff: 'handoff', note: 'note',
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
  if (events.length === 0) return <p style={{ color: 'var(--text-muted)', fontSize: 13 }}>Nothing yet.</p>;
  return (
    <div style={{ display: 'grid', gap: 8 }}>
      {events.map(e => (
        <div key={e.id} style={{ display: 'flex', gap: 10, fontSize: 13, alignItems: 'baseline' }}>
          <span style={{ flex: '0 0 78px', color: 'var(--text-muted)', fontSize: 12 }}>{when(e.createdAt)}</span>
          <span style={{ flex: '0 0 74px', fontWeight: 700, color: 'var(--text-secondary)', fontSize: 11.5,
            textTransform: 'uppercase', letterSpacing: '.04em' }}>{EVENT_WORD[e.type] ?? e.type}</span>
          <span style={{ minWidth: 0, color: 'var(--text-primary)' }}>{eventLine(e)}</span>
          <span style={{ marginLeft: 'auto', color: 'var(--text-muted)', fontSize: 11.5 }}>{e.actor}</span>
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
    <div style={{ padding: 32, maxWidth: 560, color: 'var(--text-muted)', fontSize: 14, lineHeight: 1.7 }}>
      <div style={{ fontSize: 16, fontWeight: 600, color: 'var(--text-primary)', marginBottom: 8 }}>
        No courses yet
      </div>
      A course is written here by the authoring agent — it imports the plan, then fills in each
      lesson as you work through it. Ask it to load one and it will appear.
    </div>
  );

  return (
    <div style={{ padding: 24, display: 'grid', gap: 12, maxWidth: 900 }}>
      {rows.map(c => (
        <div key={c.id} className="crs-card crs-card-link" role="link" tabIndex={0}
          onClick={() => onOpen(c.slug)}
          onKeyDown={e => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); onOpen(c.slug); } }}
          style={{ display: 'flex', gap: 18, alignItems: 'center' }}>
          <ProgressRing value={c.progress} look={LOOK[c.status] ?? 'live'} />
          <div style={{ flex: 1, minWidth: 0, display: 'grid', gap: 6 }}>
            <div style={{ display: 'flex', gap: 10, alignItems: 'center', flexWrap: 'wrap' }}>
              <span style={{ fontSize: 17, fontWeight: 700, color: 'var(--text-primary)' }}>{c.title}</span>
              <StatusPill status={c.status} />
            </div>
            {c.subtitle && <div style={{ fontSize: 13.5, color: 'var(--text-muted)' }}>{c.subtitle}</div>}
            <div style={{ display: 'flex', gap: 18, flexWrap: 'wrap', fontSize: 12.5, color: 'var(--text-muted)' }}>
              <span><b style={{ color: 'var(--text-secondary)' }}><Pct value={c.definedFraction} /></b> written</span>
              <span><b style={{ color: 'var(--text-secondary)' }}><Num value={c.hoursLogged} /></b> h logged</span>
              <span><b style={{ color: 'var(--text-secondary)' }}><Num value={c.estimatedHoursRemaining} /></b> h left</span>
              {c.currentLesson && <span>on <b style={{ color: 'var(--text-secondary)' }}>{c.currentLesson.code} {c.currentLesson.title}</b></span>}
            </div>
          </div>
        </div>
      ))}
    </div>
  );
}

/* ─── view 2: one course ─── */

function StageRail({ stages, activeId, onPick }:
  { stages: CourseStageDto[]; activeId: string; onPick: (id: string) => void }) {
  return (
    <div className="crs-rail" role="tablist" aria-label="Stages">
      {stages.map(s => (
        <button key={s.id} role="tab" aria-selected={s.id === activeId}
          className={`crs-rail-item ${s.id === activeId ? 'active' : ''}`} onClick={() => onPick(s.id)}>
          <div style={{ display: 'flex', gap: 8, alignItems: 'baseline' }}>
            <span style={{ fontSize: 11.5, fontWeight: 800, color: 'var(--text-muted)' }}>{s.code}</span>
            <span style={{ fontSize: 13.5, fontWeight: 600, color: 'var(--text-primary)', minWidth: 0 }}>{s.title}</span>
          </div>
          <Bar value={s.progress} look={LOOK[s.status] ?? 'idle'} height={4} />
          <div style={{ fontSize: 11.5, color: 'var(--text-muted)', display: 'flex', gap: 8, flexWrap: 'wrap' }}>
            <span>{words(s.status)}</span>
            <span>·</span>
            <span>{s.lessonsDefined}/{s.lessonsTotal} written</span>
            {s.targetWeeks != null && <><span>·</span><span>{s.targetWeeks}w</span></>}
          </div>
        </button>
      ))}
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
  // somewhere else; the tweens above are what make the new numbers legible when it does.
  useEffect(() => {
    const onFocus = () => load();
    window.addEventListener('focus', onFocus);
    return () => window.removeEventListener('focus', onFocus);
  }, [load]);

  // Follow the course's own idea of where the work is, until the reader picks a stage themselves.
  const stages = course?.stages ?? [];
  const focusStage = useMemo(() => {
    const code = resume?.currentStage?.code;
    return stages.find(s => s.code === code)?.id ?? stages[0]?.id ?? '';
  }, [stages, resume?.currentStage?.code]);
  const active = stages.find(s => s.id === (stageId || focusStage));

  if (error) return <p style={{ padding: 24, color: 'var(--danger)' }}>{error}</p>;

  if (!course) return (
    <div style={{ padding: 24, display: 'grid', gap: 18, maxWidth: 1100 }}>
      <div style={{ display: 'flex', gap: 18, alignItems: 'center' }}>
        <Skel w={92} h={92} style={{ borderRadius: '50%' }} />
        <div style={{ flex: 1, display: 'grid', gap: 8 }}><Skel w="40%" h={24} /><Skel w="60%" h={15} /></div>
      </div>
      <div className="crs-split" style={{ display: 'grid', gridTemplateColumns: '300px 1fr', gap: 18 }}>
        <div style={{ display: 'grid', gap: 8 }}>{[0, 1, 2].map(i => <Skel key={i} h={62} />)}</div>
        <div style={{ display: 'grid', gap: 8 }}>{[0, 1, 2, 3].map(i => <Skel key={i} h={58} />)}</div>
      </div>
    </div>
  );

  const setStatus = async (status: CourseStatus) => {
    setBusy(true);
    try { setCourse(await api.setCourseStatus(slug, status)); } finally { setBusy(false); }
  };

  return (
    <div style={{ padding: 24, display: 'grid', gap: 20, maxWidth: 1100 }}>
      <button className="btn btn-sm" onClick={onBack} style={{ justifySelf: 'start' }}>← Courses</button>

      <div style={{ display: 'flex', gap: 20, alignItems: 'center', flexWrap: 'wrap' }}>
        <ProgressRing value={course.progress} size={92} stroke={9} look={LOOK[course.status] ?? 'live'} />
        <div style={{ flex: 1, minWidth: 240, display: 'grid', gap: 6 }}>
          <div style={{ display: 'flex', gap: 10, alignItems: 'center', flexWrap: 'wrap' }}>
            <h2 style={{ margin: 0, fontSize: 22, color: 'var(--text-primary)' }}>{course.title}</h2>
            <StatusPill status={course.status} />
          </div>
          {course.subtitle && <div style={{ fontSize: 14, color: 'var(--text-muted)' }}>{course.subtitle}</div>}
          <div style={{ display: 'flex', gap: 22, flexWrap: 'wrap', marginTop: 4 }}>
            <Stat label="Written"><Pct value={course.definedFraction} /></Stat>
            <Stat label="Hours logged"><Num value={course.hoursLogged} /></Stat>
            <Stat label="Hours left"><Num value={course.estimatedHoursRemaining} /></Stat>
            {course.targetHoursPerWeek != null && <Stat label="Target / week">{course.targetHoursPerWeek} h</Stat>}
          </div>
        </div>
        <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
          {(['active', 'paused', 'completed'] as CourseStatus[])
            .filter(s => s !== course.status)
            .map(s => <button key={s} className="btn btn-sm" disabled={busy} onClick={() => setStatus(s)}>
              {s === 'active' ? 'Start' : s === 'paused' ? 'Pause' : 'Complete'}
            </button>)}
        </div>
      </div>

      {/* Where to sit down, and what the last session said it was in the middle of. */}
      {(resume?.currentLesson || resume?.latestHandoff || resume?.nextUndefinedStage) && (
        <div className="crs-card" style={{ display: 'grid', gap: 10 }}>
          {resume.currentLesson ? (
            <div style={{ display: 'flex', gap: 12, alignItems: 'center', flexWrap: 'wrap' }}>
              <span style={{ fontSize: 12, fontWeight: 700, color: 'var(--text-muted)',
                textTransform: 'uppercase', letterSpacing: '.05em' }}>Continue</span>
              <button className="btn btn-sm btn-accent" onClick={() => onOpenLesson(resume.currentLesson!.code)}>
                {resume.currentLesson.code} · {resume.currentLesson.title} →
              </button>
              <StatusPill status={resume.currentLesson.status} />
              {resume.pendingExercises.length > 0 && (
                <span style={{ fontSize: 12.5, color: 'var(--text-muted)' }}>
                  {resume.pendingExercises.length} exercise{resume.pendingExercises.length === 1 ? '' : 's'} still ungraded
                </span>
              )}
            </div>
          ) : resume.nextUndefinedStage && (
            <div style={{ fontSize: 13.5, color: 'var(--text-muted)' }}>
              Nothing is ready to work on. Next to be written:{' '}
              <b style={{ color: 'var(--text-primary)' }}>
                {resume.nextUndefinedStage.code} {resume.nextUndefinedStage.title}
              </b>{' '}
              ({resume.nextUndefinedStage.placeholderLessons} lesson
              {resume.nextUndefinedStage.placeholderLessons === 1 ? '' : 's'} outlined).
            </div>
          )}
          {resume.latestHandoff?.md && (
            <div style={{ fontSize: 13.5, color: 'var(--text-secondary)', borderLeft: '3px solid var(--border)',
              paddingLeft: 10, lineHeight: 1.6 }}>
              {resume.latestHandoff.md}
              <span style={{ color: 'var(--text-muted)', fontSize: 12 }}> — {when(resume.latestHandoff.createdAt)}</span>
            </div>
          )}
        </div>
      )}

      <div className="crs-split" style={{ display: 'grid', gridTemplateColumns: '300px 1fr', gap: 18, alignItems: 'start' }}>
        <StageRail stages={stages} activeId={active?.id ?? ''} onPick={setStageId} />

        <div style={{ display: 'grid', gap: 10, minWidth: 0 }}>
          {active && <>
            <div style={{ display: 'flex', gap: 10, alignItems: 'baseline', flexWrap: 'wrap' }}>
              <h3 style={{ margin: 0, fontSize: 16, color: 'var(--text-primary)' }}>{active.code} · {active.title}</h3>
              <StatusPill status={active.status} />
            </div>
            {(active.lessons ?? []).length === 0
              ? <p style={{ color: 'var(--text-muted)', fontSize: 13.5 }}>
                  This stage has no lessons outlined yet.
                </p>
              : (active.lessons ?? []).map(l => {
                const blank = l.status === 'placeholder';
                return (
                  <div key={l.id} className={`crs-card ${blank ? '' : 'crs-card-link'}`}
                    role={blank ? undefined : 'link'} tabIndex={blank ? -1 : 0}
                    onClick={() => !blank && onOpenLesson(l.code)}
                    onKeyDown={e => { if (!blank && (e.key === 'Enter' || e.key === ' ')) { e.preventDefault(); onOpenLesson(l.code); } }}
                    style={{ padding: '12px 14px', display: 'grid', gap: 7, opacity: blank ? .62 : 1 }}>
                    <div style={{ display: 'flex', gap: 10, alignItems: 'baseline', flexWrap: 'wrap' }}>
                      <span style={{ fontSize: 11.5, fontWeight: 800, color: 'var(--text-muted)' }}>{l.code}</span>
                      <span style={{ fontSize: 14.5, fontWeight: 600, color: 'var(--text-primary)', minWidth: 0 }}>{l.title}</span>
                      <StatusPill status={l.status} />
                      {l.score != null && <span style={{ fontSize: 12.5, color: 'var(--crs-done-ink)', fontWeight: 700 }}>{l.score}%</span>}
                    </div>
                    <Bar value={l.progress} look={LOOK[l.status] ?? 'idle'} height={4} />
                    <div style={{ fontSize: 11.5, color: 'var(--text-muted)', display: 'flex', gap: 8, flexWrap: 'wrap' }}>
                      {blank
                        ? <span>outlined only — not written yet</span>
                        : <span>{l.exerciseCount} exercise{l.exerciseCount === 1 ? '' : 's'}</span>}
                      {l.estimatedHours != null && <><span>·</span><span>{l.estimatedHours} h</span></>}
                    </div>
                  </div>
                );
              })}
          </>}
        </div>
      </div>

      {course.capstoneMd && <Fold title="Capstone"><Md src={course.capstoneMd} /></Fold>}
      {course.descriptionMd && <Fold title="About this course"><Md src={course.descriptionMd} /></Fold>}
      {course.resources.length > 0 && (
        <Fold title="Resources" count={course.resources.length}><Resources items={course.resources} /></Fold>
      )}
      <Fold title="Timeline"><Timeline events={resume?.recentEvents ?? []} /></Fold>
    </div>
  );
}

/* ─── view 3: one lesson ─── */

function Exercise({ x }: { x: ExerciseDto }) {
  const subs = x.submissions ?? [];
  const latest = subs.length > 0 ? subs[subs.length - 1] : undefined;
  // The grade that counts is the newest one that nothing supersedes; the rest is history, kept
  // because how the work went should not be rewritten by how it ended.
  const grades = subs.flatMap(s => s.grades ?? []);
  const superseded = new Set(grades.map(g => g.supersedes).filter(Boolean) as string[]);
  const live = grades.filter(g => !superseded.has(g.id))
    .sort((a, b) => Date.parse(b.gradedAt) - Date.parse(a.gradedAt))[0];
  const history = grades.filter(g => g.id !== live?.id);

  return (
    <div className="crs-card" style={{ display: 'grid', gap: 9 }}>
      <div style={{ display: 'flex', gap: 10, alignItems: 'baseline', flexWrap: 'wrap' }}>
        <span style={{ fontSize: 14.5, fontWeight: 650, color: 'var(--text-primary)' }}>{x.title}</span>
        <span className="crs-pill" style={{ color: 'var(--text-secondary)', background: 'var(--bg-hover)' }}>{words(x.kind)}</span>
        {!x.required && <span style={{ fontSize: 12, color: 'var(--text-muted)' }}>optional</span>}
        <span style={{ marginLeft: 'auto', fontSize: 12.5, color: 'var(--text-muted)' }}>
          {x.attempts} attempt{x.attempts === 1 ? '' : 's'} · weight {x.weight} · out of {x.maxScore}
        </span>
      </div>

      <Md src={x.promptMd} />

      {live ? (
        <div style={{ display: 'flex', gap: 10, alignItems: 'baseline', flexWrap: 'wrap',
          background: 'var(--crs-done-bg)', padding: '8px 10px', borderRadius: 'var(--radius-md)' }}>
          <b style={{ color: 'var(--crs-done-ink)', fontSize: 15 }}>{live.score} / {live.maxScore}</b>
          <span style={{ fontSize: 12, color: 'var(--text-muted)' }}>{live.gradedBy} · {when(live.gradedAt)}</span>
          {live.feedbackMd && <div style={{ flexBasis: '100%', fontSize: 13.5, color: 'var(--text-secondary)' }}>
            <Md src={live.feedbackMd} /></div>}
        </div>
      ) : (
        <div style={{ fontSize: 12.5, color: 'var(--text-muted)' }}>
          {latest ? 'Submitted, not graded yet.' : 'No attempt yet.'}
        </div>
      )}

      {latest?.contentMd && <Fold title={`Attempt ${latest.attemptNo}`}><Md src={latest.contentMd} /></Fold>}
      {(latest?.links?.length ?? 0) > 0 && (
        <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap', fontSize: 13 }}>
          {latest!.links.map((l, i) => (
            <a key={i} href={l.url} target="_blank" rel="noreferrer noopener"
              style={{ color: 'var(--accent)', fontWeight: 600 }}>{l.label ?? l.kind ?? l.url} ↗</a>
          ))}
        </div>
      )}
      {history.length > 0 && (
        <Fold title="Earlier marks" count={history.length}>
          <div style={{ display: 'grid', gap: 5, fontSize: 13 }}>
            {history.map(g => (
              <div key={g.id} style={{ color: 'var(--text-muted)' }}>
                <s>{g.score} / {g.maxScore}</s> · {g.gradedBy} · {when(g.gradedAt)}
              </div>
            ))}
          </div>
        </Fold>
      )}
      {x.referenceMd && <Fold title="Reference answer"><Md src={x.referenceMd} /></Fold>}
    </div>
  );
}

// Lessons move through these; anything may be set aside. The list is what the API accepts, and the
// tab offers only the moves that make sense from where the lesson is.
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
  const [minutes, setMinutes] = useState('');
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
    <div style={{ padding: 24, display: 'grid', gap: 16, maxWidth: 880 }}>
      <Skel w={140} h={28} /><Skel w="55%" h={26} />
      <div style={{ display: 'flex', gap: 8 }}>{[0, 1, 2, 3, 4].map(i => <Skel key={i} w={110} h={32} style={{ borderRadius: 999 }} />)}</div>
      {[0, 1].map(i => <Skel key={i} h={150} />)}
    </div>
  );

  const written = new Map((lesson.sections ?? []).map(s => [s.kind, s]));
  const exercisesFor = (kind: string) => (lesson.exercises ?? []).filter(x => x.sectionKind === kind);
  // An exercise whose section kind is not in the template still has to appear somewhere.
  const orphans = (lesson.exercises ?? []).filter(
    x => !lesson.templateSections.some(t => t.kind === x.sectionKind));

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

  const logTime = async () => {
    const m = Number(minutes);
    if (!Number.isFinite(m) || m <= 0) return;
    setBusy(true);
    try {
      await api.logCourseEvent(slug, 'time_logged', { minutes: m }, code);
      setMinutes(''); setSaid(`Logged ${m} min.`);
    } finally { setBusy(false); }
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
    <div style={{ padding: 24, display: 'grid', gap: 18, maxWidth: 880 }}>
      <button className="btn btn-sm" onClick={onBack} style={{ justifySelf: 'start' }}>
        ← {lesson.stage.code} {lesson.stage.title}
      </button>

      <div style={{ display: 'grid', gap: 8 }}>
        <div style={{ display: 'flex', gap: 10, alignItems: 'baseline', flexWrap: 'wrap' }}>
          <span style={{ fontSize: 12.5, fontWeight: 800, color: 'var(--text-muted)' }}>{lesson.code}</span>
          <h2 style={{ margin: 0, fontSize: 21, color: 'var(--text-primary)' }}>{lesson.title}</h2>
          <StatusPill status={lesson.status} />
        </div>
        <div style={{ display: 'flex', gap: 22, flexWrap: 'wrap' }}>
          <Stat label="Progress"><Pct value={lesson.progress ?? 0} /></Stat>
          <Stat label="Score">{lesson.score != null ? <Pct value={lesson.score / 100} /> : '—'}</Stat>
          <Stat label="Graded"><Pct value={lesson.gradedFraction ?? 0} /></Stat>
          {lesson.estimatedHours != null && <Stat label="Estimated">{lesson.estimatedHours} h</Stat>}
        </div>
        {lesson.summaryMd && <Md src={lesson.summaryMd} />}
      </div>

      {/* The template's rhythm, every step of it, written or blank — so a half-written lesson looks
          unfinished rather than short. */}
      <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
        {lesson.templateSections.map(t => {
          const has = written.has(t.kind);
          return (
            <button key={t.kind} className={`crs-step ${has ? '' : 'blank'}`}
              onClick={() => refs.current[t.kind]?.scrollIntoView({ behavior: 'smooth', block: 'start' })}
              title={has ? `Go to ${t.title ?? t.kind}` : 'Not written yet'}>
              <span aria-hidden="true">{has ? '●' : '○'}</span>
              {t.title ?? words(t.kind)}
              {exercisesFor(t.kind).length > 0 && (
                <span style={{ color: 'var(--text-muted)', fontWeight: 600 }}>{exercisesFor(t.kind).length}</span>
              )}
            </button>
          );
        })}
      </div>

      {lesson.templateSections.map(t => {
        const sec = written.get(t.kind);
        const xs = exercisesFor(t.kind);
        return (
          <div key={t.kind} ref={el => { refs.current[t.kind] = el; }} style={{ display: 'grid', gap: 12 }}>
            <h3 style={{ margin: '6px 0 0', fontSize: 15, color: 'var(--text-secondary)',
              textTransform: 'uppercase', letterSpacing: '.05em' }}>
              {t.title ?? words(t.kind)}
              {!t.required && <span style={{ textTransform: 'none', letterSpacing: 0, fontWeight: 400,
                color: 'var(--text-muted)', fontSize: 12.5 }}> · optional</span>}
            </h3>
            {sec ? <Md src={sec.contentMd} />
              : <p style={{ color: 'var(--text-muted)', fontSize: 13.5, margin: 0 }}>
                  Not written yet.
                </p>}
            {xs.map(x => <Exercise key={x.id} x={x} />)}
          </div>
        );
      })}

      {orphans.length > 0 && (
        <div style={{ display: 'grid', gap: 12 }}>
          <h3 style={{ margin: '6px 0 0', fontSize: 15, color: 'var(--text-secondary)',
            textTransform: 'uppercase', letterSpacing: '.05em' }}>Other exercises</h3>
          {orphans.map(x => <Exercise key={x.id} x={x} />)}
        </div>
      )}

      {lesson.resources.length > 0 && (
        <Fold title="Resources" count={lesson.resources.length} open>
          <Resources items={lesson.resources} />
        </Fold>
      )}

      {/* The three things a person writes here. Everything else about a lesson is the agent's. */}
      <div className="crs-card" style={{ display: 'grid', gap: 12 }}>
        <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
          <span style={{ fontSize: 11.5, fontWeight: 700, color: 'var(--text-muted)',
            textTransform: 'uppercase', letterSpacing: '.05em' }}>Move to</span>
          {LESSON_MOVES[lesson.status].map(to => (
            <button key={to} className="btn btn-sm" disabled={busy} onClick={() => move(to)}>{words(to)}</button>
          ))}
        </div>
        <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
          <input className="inline-input" style={{ width: 92, minWidth: 0, flex: '0 0 auto' }}
            inputMode="numeric" placeholder="minutes"
            value={minutes} onChange={e => setMinutes(e.target.value)} aria-label="Minutes spent" />
          <button className="btn btn-sm" disabled={busy || !minutes} onClick={logTime}>Log time</button>
        </div>
        <div style={{ display: 'flex', gap: 8, alignItems: 'flex-start', flexWrap: 'wrap' }}>
          <textarea className="inline-input" rows={2} placeholder="A note on this lesson…"
            style={{ flex: 1, minWidth: 220, resize: 'vertical' }}
            value={note} onChange={e => setNote(e.target.value)} aria-label="Note" />
          <button className="btn btn-sm" disabled={busy || !note.trim()} onClick={addNote}>Add note</button>
        </div>
        {said && <div role="status" style={{ fontSize: 12.5, color: 'var(--text-muted)' }}>{said}</div>}
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
