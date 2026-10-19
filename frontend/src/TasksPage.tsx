import { useState, useEffect, useCallback, useMemo, type CSSProperties } from 'react';
import type { SingleTaskDto, TaskProjectDto, SaveTaskRequest } from './types';
import { api } from './api';

/*
 * The Tasks tab: one-off tasks, grouped by project, each able to wait on others.
 *
 *  - A project is a label the user files a task under; the page shows one section per project (and
 *    "No project" for the rest), with a chip row to narrow to one. Colours are palette keys the
 *    server stores; styles.css maps them to theme-aware tokens, so a chip, a dot and a progress bar
 *    all take their colour from one inline `--pj`.
 *  - A dependency is "waits on": a task with an unfinished prerequisite is *blocked* — marked, sorted
 *    after the ready ones, and left out of the day's schedule by the server. Completing it anyway is
 *    allowed, after a word of warning. The picker in the form greys out anything that would make a
 *    loop, and the server refuses the same with the loop spelled out, which the form shows verbatim.
 */

interface Props { roadmapId: string; onBack: () => void; }

const PRIORITIES = ['High', 'Medium', 'Low'];
const PRIORITY_RANK: Record<string, number> = { High: 0, Medium: 1, Low: 2 };
const DAYS = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];
const DAY_VALUES = [1, 2, 3, 4, 5, 6, 0];
/** The eight hues of the app's validated palette; the server accepts exactly these keys. */
const PROJECT_COLORS = ['blue', 'orange', 'aqua', 'yellow', 'magenta', 'green', 'violet', 'red'];
const NO_PROJECT = '__none__';
type Filter = 'all' | string;
type ModalState = { mode: 'create'; projectId: string | null } | { mode: 'edit'; task: SingleTaskDto };

const todayStr = () => { const d = new Date(); return `${d.getFullYear()}-${(d.getMonth() + 1).toString().padStart(2, '0')}-${d.getDate().toString().padStart(2, '0')}`; };
const priColor = (p: string) => p === 'High' ? 'var(--danger)' : p === 'Medium' ? '#e37400' : 'var(--text-muted)';
/** Inline `--pj` so one class can colour a chip, dot or bar from the project's palette key. */
const pj = (color?: string | null): CSSProperties => color ? ({ '--pj': `var(--pj-${color})` } as CSSProperties) : {};
/** The least-used hue, ties in palette order — the same rule the server applies when none is given. */
const nextColor = (projects: TaskProjectDto[]) => {
  const counts = new Map<string, number>();
  for (const p of projects) counts.set(p.color, (counts.get(p.color) ?? 0) + 1);
  return PROJECT_COLORS.reduce((best, c) => (counts.get(c) ?? 0) < (counts.get(best) ?? 0) ? c : best, PROJECT_COLORS[0]);
};
/** `API 400: "that would make a cycle: …"` → the sentence. */
const errText = (e: unknown) => {
  const m = e instanceof Error ? e.message : String(e);
  const body = m.replace(/^API \d+:\s*/, '');
  try { const j: unknown = JSON.parse(body); return typeof j === 'string' ? j : body; } catch { return body || m; }
};
/** Every task that waits on `id`, directly or through others — none of them may become its prerequisite. */
function downstream(id: string, byId: Map<string, SingleTaskDto>): Set<string> {
  const seen = new Set<string>(); const stack = [id];
  while (stack.length) {
    const cur = stack.pop()!;
    for (const d of byId.get(cur)?.dependents ?? []) if (!seen.has(d.id)) { seen.add(d.id); stack.push(d.id); }
  }
  return seen;
}
/** Ready before blocked, then High → Low, then nearest due date; otherwise the server's order (oldest first). */
function sortOpen(a: SingleTaskDto, b: SingleTaskDto) {
  if (a.isBlocked !== b.isBlocked) return a.isBlocked ? 1 : -1;
  const pr = PRIORITY_RANK[a.priority] - PRIORITY_RANK[b.priority]; if (pr) return pr;
  if (a.dueDate !== b.dueDate) { if (!a.dueDate) return 1; if (!b.dueDate) return -1; return a.dueDate < b.dueDate ? -1 : 1; }
  return 0;
}

export function TasksPage({ roadmapId }: Props) {
  const [tasks, setTasks] = useState<SingleTaskDto[]>([]);
  const [projects, setProjects] = useState<TaskProjectDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [filter, setFilter] = useState<Filter>('all');
  const [doneOpen, setDoneOpen] = useState<Set<string>>(new Set());
  const [modal, setModal] = useState<ModalState | null>(null);
  const [manage, setManage] = useState(false);
  const [confirmDelete, setConfirmDelete] = useState<string | null>(null);
  const [flash, setFlash] = useState<string | null>(null);

  const load = useCallback(async () => {
    const [t, p] = await Promise.all([api.getTasks(roadmapId), api.getTaskProjects(roadmapId)]);
    setTasks(t); setProjects(p);
  }, [roadmapId]);
  useEffect(() => { setLoading(true); setError(null); load().catch(e => setError(errText(e))).finally(() => setLoading(false)); }, [load]);

  // Esc closes whichever dialog is open.
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') { setModal(null); setManage(false); } };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, []);

  // A filter pointing at a deleted project falls back to everything.
  useEffect(() => {
    if (filter !== 'all' && filter !== NO_PROJECT && !projects.some(p => p.id === filter)) setFilter('all');
  }, [projects, filter]);

  const run = async (fn: () => Promise<unknown>) => {
    try { setError(null); await fn(); await load(); } catch (e) { setError(errText(e)); }
  };

  const byId = useMemo(() => new Map(tasks.map(t => [t.id, t])), [tasks]);
  const unassigned = useMemo(() => tasks.filter(t => !t.project), [tasks]);
  const groups = useMemo(() => {
    const list: { key: string; project: TaskProjectDto | null; tasks: SingleTaskDto[] }[] = [];
    for (const p of projects)
      if (filter === 'all' || filter === p.id) list.push({ key: p.id, project: p, tasks: tasks.filter(t => t.project?.id === p.id) });
    if (filter === NO_PROJECT || (filter === 'all' && (unassigned.length > 0 || projects.length === 0)))
      list.push({ key: NO_PROJECT, project: null, tasks: unassigned });
    return list;
  }, [tasks, projects, filter, unassigned]);

  /** Bring another task into view — switching filter and unfolding "done" if that is where it lives. */
  const jump = (id: string) => {
    const t = byId.get(id); if (!t) return;
    const key = t.project?.id ?? NO_PROJECT;
    if (filter !== 'all' && filter !== key) setFilter('all');
    if (t.isCompleted) setDoneOpen(s => new Set(s).add(key));
    setFlash(id);
    setTimeout(() => document.getElementById(`tk-${id}`)?.scrollIntoView({ block: 'center', behavior: 'smooth' }), 30);
    setTimeout(() => setFlash(f => (f === id ? null : f)), 1600);
  };

  if (loading) return <div className="tk-shell"><div className="empty-state"><p>Loading...</p></div></div>;

  const open = tasks.filter(t => !t.isCompleted);
  const blocked = open.filter(t => t.isBlocked).length;
  const done = tasks.length - open.length;
  const openUnassigned = unassigned.filter(t => !t.isCompleted).length;
  const createIn = filter !== 'all' && filter !== NO_PROJECT ? filter : null;

  return (
    <div className="tk-shell">
      <div className="tk-header">
        <div>
          <span className="tk-title">Tasks</span>
          {tasks.length > 0 && (
            <span className="tk-sub">{open.length} open{blocked ? ` · ${blocked} blocked` : ''} · {done} done</span>
          )}
        </div>
        <div className="tk-actions">
          <button className="btn btn-sm" onClick={() => setManage(true)}>Projects</button>
          <button className="btn btn-accent btn-sm" onClick={() => setModal({ mode: 'create', projectId: createIn })}>+ New task</button>
        </div>
      </div>

      {projects.length > 0 && (
        <div className="tk-filters" role="tablist" aria-label="Filter by project">
          <button role="tab" aria-selected={filter === 'all'} className={`tk-chip ${filter === 'all' ? 'on' : ''}`} onClick={() => setFilter('all')}>
            All <span className="tk-count">{open.length}</span>
          </button>
          {projects.map(p => (
            <button key={p.id} role="tab" aria-selected={filter === p.id} className={`tk-chip ${filter === p.id ? 'on' : ''}`} style={pj(p.color)} onClick={() => setFilter(p.id)}>
              <span className="tk-dot" />{p.name} <span className="tk-count">{p.openCount}</span>
            </button>
          ))}
          {unassigned.length > 0 && (
            <button role="tab" aria-selected={filter === NO_PROJECT} className={`tk-chip ${filter === NO_PROJECT ? 'on' : ''}`} onClick={() => setFilter(NO_PROJECT)}>
              No project <span className="tk-count">{openUnassigned}</span>
            </button>
          )}
        </div>
      )}

      <div className="tk-body">
        {error && <div className="tk-error" role="alert">{error}</div>}

        {tasks.length === 0 && projects.length === 0 ? (
          <div className="empty-state" style={{ height: 'auto', padding: 60 }}>
            <p>No tasks yet.</p>
            <button className="btn btn-accent" onClick={() => setModal({ mode: 'create', projectId: null })}>Create your first task</button>
          </div>
        ) : groups.map(g => (
          <TaskGroup key={g.key} project={g.project} tasks={g.tasks}
            showDone={doneOpen.has(g.key)}
            onToggleDone={() => setDoneOpen(s => { const n = new Set(s); if (n.has(g.key)) n.delete(g.key); else n.add(g.key); return n; })}
            onAdd={() => setModal({ mode: 'create', projectId: g.project?.id ?? null })}
            renderTask={t => (
              <TaskCard key={t.id} task={t} flashing={flash === t.id} confirmingDelete={confirmDelete === t.id}
                onEdit={() => setModal({ mode: 'edit', task: t })}
                onJump={jump}
                onDelete={() => { if (confirmDelete !== t.id) { setConfirmDelete(t.id); return; } setConfirmDelete(null); run(() => api.deleteTask(roadmapId, t.id)); }}
                onComplete={date => run(() => api.completeTask(roadmapId, t.id, date))}
                onUncomplete={() => run(() => api.uncompleteTask(roadmapId, t.id))} />
            )} />
        ))}
      </div>

      {modal && (
        <TaskModal roadmapId={roadmapId} state={modal} tasks={tasks} projects={projects}
          onClose={changed => { setModal(null); if (changed) run(async () => {}); }} />
      )}
      {manage && (
        <ProjectsModal roadmapId={roadmapId} projects={projects} onChanged={() => run(async () => {})} onClose={() => setManage(false)} />
      )}
    </div>
  );
}

function TaskGroup({ project, tasks, showDone, onToggleDone, onAdd, renderTask }: {
  project: TaskProjectDto | null; tasks: SingleTaskDto[]; showDone: boolean;
  onToggleDone: () => void; onAdd: () => void; renderTask: (t: SingleTaskDto) => JSX.Element;
}) {
  const open = tasks.filter(t => !t.isCompleted).sort(sortOpen);
  const done = tasks.filter(t => t.isCompleted);
  const blocked = open.filter(t => t.isBlocked).length;
  const pct = tasks.length ? Math.round(done.length / tasks.length * 100) : 0;
  return (
    <section className="tk-group" style={pj(project?.color)} aria-label={project?.name ?? 'No project'}>
      <div className="tk-group-head">
        <span className="tk-group-name"><span className="tk-dot" />{project?.name ?? 'No project'}</span>
        <span className="tk-group-meta">{open.length} open{blocked ? ` · ${blocked} blocked` : ''} · {done.length} done</span>
        {tasks.length > 0 && (
          <div className="tk-progress" role="progressbar" aria-valuenow={pct} aria-valuemin={0} aria-valuemax={100} title={`${pct}% done`}>
            <i style={{ width: `${pct}%` }} />
          </div>
        )}
        <div className="tk-group-actions">
          {done.length > 0 && <button className="btn btn-ghost btn-sm" onClick={onToggleDone}>{showDone ? 'Hide done' : `Show ${done.length} done`}</button>}
          <button className="btn btn-ghost btn-sm" onClick={onAdd} title={project ? `Add a task to ${project.name}` : 'Add a task'}>+ Add</button>
        </div>
      </div>
      {open.length === 0 && !showDone && (
        <div className="tk-empty">{tasks.length === 0 ? 'Nothing here yet.' : 'All done here.'}</div>
      )}
      <div className="tk-list">
        {open.map(renderTask)}
        {showDone && done.map(renderTask)}
      </div>
    </section>
  );
}

function TaskCard({ task, flashing, confirmingDelete, onEdit, onDelete, onComplete, onUncomplete, onJump }: {
  task: SingleTaskDto; flashing: boolean; confirmingDelete: boolean;
  onEdit: () => void; onDelete: () => void; onComplete: (date: string) => void; onUncomplete: () => void; onJump: (id: string) => void;
}) {
  const today = todayStr();
  const [completing, setCompleting] = useState(false);
  const [completeDate, setCompleteDate] = useState(today);
  const isOverdue = !!task.dueDate && task.dueDate < today && !task.isCompleted;
  const waiting = task.dependsOn.filter(d => !d.isCompleted);
  const cls = ['tk-card', task.isCompleted ? 'done' : '', task.isBlocked ? 'blocked' : '', flashing ? 'flash' : ''].filter(Boolean).join(' ');

  return (
    <div id={`tk-${task.id}`} className={cls}>
      <div className="tk-row">
        <span className="tk-pri" style={{ background: priColor(task.priority) }} title={`${task.priority} priority`} />
        <div className="tk-main">
          <div className="tk-name">
            <span>{task.title}</span>
            {task.isBlocked && (
              <span className="tk-pill blocked" title={`Waiting on ${waiting.map(w => w.title).join(', ')}`}>Blocked</span>
            )}
          </div>
          <div className="tk-meta">
            <span>{task.estimatedHours}h</span>
            <span>{task.points} pts</span>
            <span style={{ color: priColor(task.priority), fontWeight: 500 }}>{task.priority}</span>
            {task.dueDate && <span style={{ color: isOverdue ? 'var(--danger)' : undefined, fontWeight: isOverdue ? 600 : undefined }}>{isOverdue ? 'Overdue' : 'Due'} {task.dueDate}</span>}
            {task.delayedUntil && !task.isCompleted && <span>Delayed → {task.delayedUntil}</span>}
            {task.isCompleted && task.completedDate && <span style={{ color: 'var(--success)' }}>✓ {task.completedDate}</span>}
          </div>
        </div>
        <div className="tk-card-actions">
          {!task.isCompleted && !completing && (
            <button className="btn btn-sm btn-ghost" style={{ color: 'var(--success)' }} title="Complete" aria-label="Complete" onClick={() => setCompleting(true)}>✓</button>
          )}
          {task.isCompleted && <button className="btn btn-sm btn-ghost" title="Mark as not done" aria-label="Mark as not done" onClick={onUncomplete}>↩</button>}
          <button className="btn btn-sm btn-ghost" title="Edit" aria-label="Edit" onClick={onEdit}>✎</button>
          <button className={`btn btn-sm ${confirmingDelete ? 'btn-danger' : 'btn-ghost'}`} title="Delete" aria-label="Delete" onClick={onDelete}>
            {confirmingDelete ? 'Sure?' : '✕'}
          </button>
        </div>
      </div>

      {(task.dependsOn.length > 0 || task.dependents.length > 0) && (
        <div className="tk-deps">
          {task.dependsOn.length > 0 && <>
            <span className="tk-deps-label">Waits on</span>
            {task.dependsOn.map(d => (
              <button key={d.id} type="button" className={`tk-ref ${d.isCompleted ? 'done' : 'open'}`} onClick={() => onJump(d.id)}
                title={d.isCompleted ? 'Done — go to it' : 'Not done yet — go to it'}>{d.isCompleted ? '✓ ' : ''}{d.title}</button>
            ))}
          </>}
          {task.dependents.length > 0 && <>
            <span className="tk-deps-label" style={{ marginLeft: task.dependsOn.length ? 10 : 0 }}>Unblocks</span>
            {task.dependents.map(d => (
              <button key={d.id} type="button" className={`tk-ref ${d.isCompleted ? 'done' : ''}`} onClick={() => onJump(d.id)} title="Go to it">{d.title}</button>
            ))}
          </>}
        </div>
      )}

      {completing && (
        <div className="tk-complete-row">
          {task.isBlocked && <span className="tk-warn">Still waiting on {waiting.map(w => w.title).join(', ')}. Complete it anyway?</span>}
          <span>Completed on</span>
          <input type="date" value={completeDate} onChange={e => setCompleteDate(e.target.value)} aria-label="Completion date" />
          <button className="btn btn-sm btn-accent" onClick={() => { onComplete(completeDate); setCompleting(false); }}>Confirm</button>
          <button className="btn btn-sm btn-ghost" onClick={() => setCompleting(false)}>Cancel</button>
        </div>
      )}
    </div>
  );
}

function Swatches({ value, onChange }: { value: string; onChange: (c: string) => void }) {
  return (
    <div className="tk-swatches" role="radiogroup" aria-label="Colour">
      {PROJECT_COLORS.map(c => (
        <button key={c} type="button" role="radio" aria-checked={value === c} aria-label={c} title={c}
          className={`tk-swatch ${value === c ? 'on' : ''}`} style={pj(c)} onClick={() => onChange(c)} />
      ))}
    </div>
  );
}

function TaskModal({ roadmapId, state, tasks, projects, onClose }: {
  roadmapId: string; state: ModalState; tasks: SingleTaskDto[]; projects: TaskProjectDto[]; onClose: (changed: boolean) => void;
}) {
  const editing = state.mode === 'edit' ? state.task : null;
  const [title, setTitle] = useState(editing?.title ?? '');
  const [priority, setPriority] = useState(editing?.priority ?? 'Medium');
  const [hours, setHours] = useState(String(editing?.estimatedHours ?? 1));
  const [startDate, setStartDate] = useState(editing?.startDate ?? todayStr());
  const [dueDate, setDueDate] = useState(editing?.dueDate ?? '');
  const [weekdays, setWeekdays] = useState<number[]>(() => {
    try { return editing?.weekdays ? (JSON.parse(editing.weekdays) as number[]) : []; } catch { return []; }
  });
  const [projectId, setProjectId] = useState(editing?.project?.id ?? (state.mode === 'create' ? state.projectId ?? '' : ''));
  const [newProject, setNewProject] = useState<{ name: string; color: string } | null>(null);
  const [deps, setDeps] = useState<string[]>(editing?.dependsOn.map(d => d.id) ?? []);
  const [search, setSearch] = useState('');
  const [saving, setSaving] = useState(false);
  const [err, setErr] = useState<string | null>(null);
  // A project created from the form outlives a cancelled task, so the list behind must refresh.
  const [touched, setTouched] = useState(false);

  const byId = useMemo(() => new Map(tasks.map(t => [t.id, t])), [tasks]);
  // Anything that already waits on this task (directly or through others) cannot be its prerequisite.
  const forbidden = useMemo(() => (editing ? downstream(editing.id, byId) : new Set<string>()), [editing, byId]);
  const options = useMemo(() => {
    const q = search.trim().toLowerCase();
    return tasks
      .filter(t => t.id !== editing?.id && (!t.isCompleted || deps.includes(t.id))
        && (!q || t.title.toLowerCase().includes(q) || (t.project?.name.toLowerCase().includes(q) ?? false)))
      .sort((a, b) => {
        const sa = deps.includes(a.id) ? 0 : 1, sb = deps.includes(b.id) ? 0 : 1;
        if (sa !== sb) return sa - sb;
        return PRIORITY_RANK[a.priority] - PRIORITY_RANK[b.priority];
      });
  }, [tasks, editing, deps, search]);

  const toggleDay = (v: number) => setWeekdays(p => (p.includes(v) ? p.filter(x => x !== v) : [...p, v].sort((a, b) => a - b)));
  const toggleDep = (id: string) => setDeps(p => (p.includes(id) ? p.filter(x => x !== id) : [...p, id]));

  const submit = async () => {
    if (!title.trim() || saving) return;
    setSaving(true); setErr(null);
    try {
      let pid: string | null = projectId || null;
      if (newProject) {
        const name = newProject.name.trim();
        if (!name) { setErr('Give the new project a name, or pick an existing one.'); setSaving(false); return; }
        const created = await api.createTaskProject(roadmapId, { name, color: newProject.color });
        // Keep it if the task save below fails, so a retry does not try to create it twice.
        pid = created.id; setProjectId(created.id); setNewProject(null); setTouched(true);
      }
      const body: SaveTaskRequest = {
        title: title.trim(), priority, estimatedHours: parseFloat(hours) || 1,
        weekdays: weekdays.length > 0 ? JSON.stringify(weekdays) : null,
        startDate, dueDate: dueDate || null, projectId: pid, dependsOnIds: deps,
      };
      if (editing) await api.updateTask(roadmapId, editing.id, body);
      else await api.createTask(roadmapId, body);
      onClose(true);
    } catch (e) { setErr(errText(e)); } finally { setSaving(false); }
  };

  const canSubmit = !!title.trim() && !saving && (!newProject || !!newProject.name.trim());

  return (
    <div className="modal-overlay" onClick={() => onClose(touched)}>
      <div className="modal tk-modal" role="dialog" aria-modal="true" aria-labelledby="tk-modal-title" onClick={e => e.stopPropagation()}>
        <h2 id="tk-modal-title">{editing ? 'Edit task' : 'New task'}</h2>

        <label htmlFor="tk-title">Title</label>
        <input id="tk-title" type="text" value={title} onChange={e => setTitle(e.target.value)} placeholder="What needs to be done?"
          onKeyDown={e => { if (e.key === 'Enter') submit(); }} autoFocus />

        <label htmlFor="tk-project">Project</label>
        <select id="tk-project" value={newProject ? '__new__' : projectId}
          onChange={e => {
            if (e.target.value === '__new__') setNewProject({ name: '', color: nextColor(projects) });
            else { setNewProject(null); setProjectId(e.target.value); }
          }}>
          <option value="">No project</option>
          {projects.map(p => <option key={p.id} value={p.id}>{p.name}</option>)}
          <option value="__new__">+ New project…</option>
        </select>
        {newProject && (
          <div className="tk-newproj">
            <input type="text" value={newProject.name} placeholder="Project name" aria-label="New project name" autoFocus
              onChange={e => setNewProject({ ...newProject, name: e.target.value })}
              onKeyDown={e => { if (e.key === 'Enter') submit(); }} />
            <Swatches value={newProject.color} onChange={c => setNewProject({ ...newProject, color: c })} />
          </div>
        )}

        <div className="form-row">
          <div>
            <label htmlFor="tk-priority">Priority</label>
            <select id="tk-priority" value={priority} onChange={e => setPriority(e.target.value)}>
              {PRIORITIES.map(p => <option key={p} value={p}>{p}</option>)}
            </select>
          </div>
          <div>
            <label htmlFor="tk-hours">Est. hours</label>
            <input id="tk-hours" type="number" value={hours} onChange={e => setHours(e.target.value)} step="0.5" min="0.5" />
          </div>
        </div>
        <div className="form-row">
          <div><label htmlFor="tk-start">Start date</label><input id="tk-start" type="date" value={startDate} onChange={e => setStartDate(e.target.value)} /></div>
          <div><label htmlFor="tk-due">Due date (optional)</label><input id="tk-due" type="date" value={dueDate} onChange={e => setDueDate(e.target.value)} /></div>
        </div>

        <label>Available weekdays (empty = any day)</label>
        <div className="weekday-picker">
          {DAYS.map((d, i) => (
            <button key={i} type="button" className={`weekday-btn ${weekdays.includes(DAY_VALUES[i]) ? 'active' : ''}`}
              aria-pressed={weekdays.includes(DAY_VALUES[i])} onClick={() => toggleDay(DAY_VALUES[i])}>{d}</button>
          ))}
        </div>

        <label htmlFor="tk-dep-search">Waits on</label>
        <div className="tk-hint">Tasks that must be done before this one. It stays out of the daily schedule until they are.</div>
        <div className="tk-picker">
          {deps.length > 0 && (
            <div className="tk-selected">
              {deps.map(id => {
                const t = byId.get(id);
                return (
                  <span key={id} className={`tk-ref ${t?.isCompleted ? 'done' : 'open'}`}>
                    {t?.title ?? '…'}
                    <button type="button" aria-label={`Remove ${t?.title ?? 'dependency'}`} onClick={() => toggleDep(id)}>×</button>
                  </span>
                );
              })}
            </div>
          )}
          <input id="tk-dep-search" type="text" value={search} onChange={e => setSearch(e.target.value)}
            placeholder={tasks.filter(t => t.id !== editing?.id).length > 0 ? 'Search tasks…' : 'No other tasks yet'} />
          <div className="tk-picker-list" role="listbox" aria-label="Tasks this one waits on">
            {options.length === 0 && <div className="tk-empty" style={{ padding: '10px 12px' }}>No matching tasks.</div>}
            {options.map(t => {
              const dis = forbidden.has(t.id); const on = deps.includes(t.id);
              return (
                <label key={t.id} className={`tk-opt ${dis ? 'disabled' : ''}`}
                  title={dis ? 'This task already waits on the one you are editing — choosing it would make a loop' : undefined}>
                  <input type="checkbox" checked={on} disabled={dis} onChange={() => toggleDep(t.id)} />
                  <span className="tk-pri" style={{ background: priColor(t.priority) }} />
                  <span className="tk-opt-title">{t.title}</span>
                  {dis ? <span className="tk-opt-why">would loop</span>
                    : t.isCompleted ? <span className="tk-opt-why">done</span>
                    : t.project && <span className="tk-opt-proj"><span className="tk-dot" style={pj(t.project.color)} />{t.project.name}</span>}
                </label>
              );
            })}
          </div>
        </div>

        {err && <div className="tk-error" role="alert">{err}</div>}
        <div className="modal-actions">
          <button className="btn" onClick={() => onClose(touched)}>Cancel</button>
          <button className="btn btn-accent" onClick={submit} disabled={!canSubmit}>{saving ? 'Saving…' : editing ? 'Save' : 'Create'}</button>
        </div>
      </div>
    </div>
  );
}

function ProjectsModal({ roadmapId, projects, onChanged, onClose }: {
  roadmapId: string; projects: TaskProjectDto[]; onChanged: () => void; onClose: () => void;
}) {
  const [name, setName] = useState('');
  const [color, setColor] = useState(nextColor(projects));
  const [err, setErr] = useState<string | null>(null);
  const [confirm, setConfirm] = useState<string | null>(null);
  const [paletteFor, setPaletteFor] = useState<string | null>(null);

  // The pick for the next project follows the list as it grows.
  useEffect(() => { setColor(nextColor(projects)); }, [projects]);

  const run = async (fn: () => Promise<unknown>) => { try { setErr(null); await fn(); onChanged(); } catch (e) { setErr(errText(e)); } };
  const add = () => {
    const n = name.trim(); if (!n) return;
    run(async () => { await api.createTaskProject(roadmapId, { name: n, color }); setName(''); });
  };

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal tk-modal" role="dialog" aria-modal="true" aria-labelledby="tk-projects-title" onClick={e => e.stopPropagation()}>
        <h2 id="tk-projects-title">Projects</h2>
        <div className="tk-hint" style={{ marginTop: -12 }}>A project groups tasks. Deleting one keeps its tasks, unassigned.</div>

        {projects.length === 0 && <div className="tk-empty">No projects yet.</div>}
        {projects.map(p => (
          <ProjectRow key={p.id} project={p} paletteOpen={paletteFor === p.id} confirming={confirm === p.id}
            onTogglePalette={() => setPaletteFor(paletteFor === p.id ? null : p.id)}
            onRename={n => run(() => api.updateTaskProject(roadmapId, p.id, { name: n, color: p.color }))}
            onRecolor={c => { setPaletteFor(null); run(() => api.updateTaskProject(roadmapId, p.id, { name: p.name, color: c })); }}
            onDelete={() => { if (confirm !== p.id) { setConfirm(p.id); return; } setConfirm(null); run(() => api.deleteTaskProject(roadmapId, p.id)); }} />
        ))}

        <div className="tk-proj-add">
          <label htmlFor="tk-proj-name">New project</label>
          <div className="tk-proj-row" style={{ borderBottom: 'none' }}>
            <span className="tk-swatch static" style={pj(color)} aria-hidden="true" />
            <input id="tk-proj-name" type="text" value={name} placeholder="Project name" onChange={e => setName(e.target.value)}
              onKeyDown={e => { if (e.key === 'Enter') add(); }} />
            <button className="btn btn-sm btn-accent" onClick={add} disabled={!name.trim()}>Add</button>
          </div>
          <Swatches value={color} onChange={setColor} />
        </div>

        {err && <div className="tk-error" role="alert">{err}</div>}
        <div className="modal-actions">
          <button className="btn" onClick={onClose}>Done</button>
        </div>
      </div>
    </div>
  );
}

function ProjectRow({ project, paletteOpen, confirming, onTogglePalette, onRename, onRecolor, onDelete }: {
  project: TaskProjectDto; paletteOpen: boolean; confirming: boolean;
  onTogglePalette: () => void; onRename: (name: string) => void; onRecolor: (color: string) => void; onDelete: () => void;
}) {
  const [name, setName] = useState(project.name);
  useEffect(() => { setName(project.name); }, [project.name]);
  const commit = () => { const n = name.trim(); if (!n) { setName(project.name); return; } if (n !== project.name) onRename(n); };
  const total = project.openCount + project.doneCount;
  return (
    <div className="tk-proj">
      <div className="tk-proj-row" style={pj(project.color)}>
        <button type="button" className="tk-swatch" style={pj(project.color)} title="Change colour" aria-label={`Colour: ${project.color}. Change`}
          aria-expanded={paletteOpen} onClick={onTogglePalette} />
        <input type="text" value={name} aria-label={`Rename ${project.name}`} onChange={e => setName(e.target.value)}
          onBlur={commit} onKeyDown={e => { if (e.key === 'Enter') (e.target as HTMLInputElement).blur(); if (e.key === 'Escape') setName(project.name); }} />
        <span className="tk-proj-counts" title={`${project.openCount} open, ${project.doneCount} done${project.blockedCount ? `, ${project.blockedCount} blocked` : ''}`}>
          {project.openCount} open · {project.doneCount} done
        </span>
        <button className={`btn btn-sm ${confirming ? 'btn-danger' : 'btn-ghost'}`} onClick={onDelete}
          title={total ? `Delete — its ${total} task${total === 1 ? '' : 's'} stay, unassigned` : 'Delete'} aria-label="Delete project">
          {confirming ? (total ? `Sure? (${total} kept)` : 'Sure?') : '✕'}
        </button>
      </div>
      {paletteOpen && <Swatches value={project.color} onChange={onRecolor} />}
    </div>
  );
}
