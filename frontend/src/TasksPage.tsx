import { useState, useEffect, useCallback } from 'react';
import type { SingleTaskDto } from './types';
import { api } from './api';

interface Props { roadmapId: string; onBack: () => void; }

const PRIORITIES = ['High', 'Medium', 'Low'];
const DAYS = ['Mon','Tue','Wed','Thu','Fri','Sat','Sun'];
const DAY_VALUES = [1,2,3,4,5,6,0];
const todayStr = () => { const d = new Date(); return `${d.getFullYear()}-${(d.getMonth()+1).toString().padStart(2,'0')}-${d.getDate().toString().padStart(2,'0')}`; };

export function TasksPage({ roadmapId, onBack }: Props) {
  const [tasks, setTasks] = useState<SingleTaskDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [showCreate, setShowCreate] = useState(false);
  const [confirmDelete, setConfirmDelete] = useState<string | null>(null);

  const load = useCallback(async () => { setTasks(await api.getTasks(roadmapId)); }, [roadmapId]);
  useEffect(() => { setLoading(true); load().finally(() => setLoading(false)); }, [load]);

  const handleCreate = async (data: Record<string, unknown>) => {
    await api.createTask(roadmapId, data); setShowCreate(false); await load();
  };

  const handleDelete = async (tid: string) => {
    if (confirmDelete !== tid) { setConfirmDelete(tid); return; }
    await api.deleteTask(roadmapId, tid); setConfirmDelete(null); await load();
  };

  const handleComplete = async (tid: string, date: string) => {
    await api.completeTask(roadmapId, tid, date); await load();
  };

  const handleUncomplete = async (tid: string) => {
    await api.uncompleteTask(roadmapId, tid); await load();
  };

  if (loading) return <div className="app-shell"><div className="empty-state"><p>Loading...</p></div></div>;

  const uncompleted = tasks.filter(t => !t.isCompleted);
  const completed = tasks.filter(t => t.isCompleted);

  return (
    <div style={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '12px 20px', borderBottom: '1px solid var(--border-subtle)', flexShrink: 0 }}>
        <span style={{ fontSize: 16, fontWeight: 600 }}>Tasks</span>
        <button className="btn btn-accent btn-sm" onClick={() => setShowCreate(true)}>+ New Task</button>
      </div>

      <div className="perf-content">
        {uncompleted.length === 0 && completed.length === 0 && (
          <div className="empty-state" style={{ height: 'auto', padding: 60 }}>
            <p>No tasks yet.</p>
            <button className="btn btn-accent" onClick={() => setShowCreate(true)}>Create your first task</button>
          </div>
        )}

        {uncompleted.length > 0 && (
          <div className="perf-section">
            <h2>Active ({uncompleted.length})</h2>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
              {uncompleted.map(t => (
                <TaskCard key={t.id} task={t} onDelete={() => handleDelete(t.id)} confirmingDelete={confirmDelete === t.id}
                  onComplete={(date) => handleComplete(t.id, date)} />
              ))}
            </div>
          </div>
        )}

        {completed.length > 0 && (
          <div className="perf-section">
            <h2>Completed ({completed.length})</h2>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
              {completed.map(t => (
                <TaskCard key={t.id} task={t} onDelete={() => handleDelete(t.id)} confirmingDelete={confirmDelete === t.id}
                  onUncomplete={() => handleUncomplete(t.id)} />
              ))}
            </div>
          </div>
        )}
      </div>

      {showCreate && <CreateTaskModal onSubmit={handleCreate} onCancel={() => setShowCreate(false)} />}
    </div>
  );
}

function TaskCard({ task, onDelete, confirmingDelete, onUncomplete, onComplete }: {
  task: SingleTaskDto; onDelete: () => void; confirmingDelete: boolean;
  onUncomplete?: () => void; onComplete?: (date: string) => void;
}) {
  const today = todayStr();
  const [completing, setCompleting] = useState(false);
  const [completeDate, setCompleteDate] = useState(today);
  const isOverdue = task.dueDate && task.dueDate < today && !task.isCompleted;
  const priColor = task.priority === 'High' ? 'var(--danger)' : task.priority === 'Medium' ? '#e37400' : 'var(--text-muted)';

  return (
    <div style={{ background: 'var(--bg-primary)', border: '1px solid var(--border-subtle)',
      borderRadius: 'var(--radius-md)', boxShadow: 'var(--shadow-sm)', opacity: task.isCompleted ? 0.6 : 1 }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 12, padding: '14px 18px' }}>
        <span style={{ width: 8, height: 8, borderRadius: '50%', background: priColor, flexShrink: 0 }} />
        <div style={{ flex: 1, minWidth: 0 }}>
          <div style={{ fontSize: 15, fontWeight: 500, textDecoration: task.isCompleted ? 'line-through' : 'none' }}>{task.title}</div>
          <div style={{ display: 'flex', gap: 12, fontSize: 12, color: 'var(--text-muted)', marginTop: 3, flexWrap: 'wrap' }}>
            <span>{task.estimatedHours}h</span>
            <span>{task.points} pts</span>
            <span style={{ color: priColor, fontWeight: 500 }}>{task.priority}</span>
            {task.dueDate && <span style={{ color: isOverdue ? 'var(--danger)' : undefined }}>Due: {task.dueDate}</span>}
            {task.delayedUntil && <span>Delayed → {task.delayedUntil}</span>}
            {task.isCompleted && task.completedDate && <span style={{ color: 'var(--success)' }}>✓ {task.completedDate}</span>}
          </div>
        </div>
        <div style={{ display: 'flex', gap: 4 }}>
          {onComplete && !completing && (
            <button className="btn btn-sm btn-ghost" style={{ color: 'var(--success)' }}
              onClick={() => setCompleting(true)}>✓</button>
          )}
          {task.isCompleted && onUncomplete && <button className="btn btn-sm btn-ghost" onClick={onUncomplete}>↩</button>}
          <button className={`btn btn-sm ${confirmingDelete ? 'btn-danger' : 'btn-ghost'}`} onClick={onDelete}>
            {confirmingDelete ? 'Sure?' : '✕'}
          </button>
        </div>
      </div>
      {completing && (
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '0 18px 14px 18px', borderTop: '1px solid var(--border-subtle)', paddingTop: 10 }}>
          <span style={{ fontSize: 13, color: 'var(--text-muted)' }}>Completed on:</span>
          <input type="date" value={completeDate} onChange={e => setCompleteDate(e.target.value)}
            style={{ fontSize: 13, padding: '3px 6px' }} />
          <button className="btn btn-sm btn-accent" onClick={() => { onComplete?.(completeDate); setCompleting(false); }}>Confirm</button>
          <button className="btn btn-sm btn-ghost" onClick={() => setCompleting(false)}>Cancel</button>
        </div>
      )}
    </div>
  );
}

function CreateTaskModal({ onSubmit, onCancel }: { onSubmit: (data: Record<string, unknown>) => void; onCancel: () => void }) {
  const [title, setTitle] = useState('');
  const [priority, setPriority] = useState('Medium');
  const [hours, setHours] = useState('1');
  const [startDate, setStartDate] = useState(todayStr());
  const [dueDate, setDueDate] = useState('');
  const [weekdays, setWeekdays] = useState<number[]>([]);
  const toggleDay = (v: number) => setWeekdays(p => p.includes(v) ? p.filter(x => x !== v) : [...p, v].sort());

  const submit = () => {
    if (!title.trim()) return;
    onSubmit({
      title: title.trim(), priority, estimatedHours: parseFloat(hours) || 1,
      weekdays: weekdays.length > 0 ? JSON.stringify(weekdays) : null,
      startDate, dueDate: dueDate || null,
    });
  };

  return (
    <div className="modal-overlay" onClick={onCancel}><div className="modal" onClick={e => e.stopPropagation()}>
      <h2>New Task</h2>
      <label>Title</label>
      <input type="text" value={title} onChange={e => setTitle(e.target.value)} placeholder="What needs to be done?"
        onKeyDown={e => { if (e.key === 'Enter') submit(); }} autoFocus />
      <div className="form-row">
        <div><label>Priority</label><select value={priority} onChange={e => setPriority(e.target.value)}>
          {PRIORITIES.map(p => <option key={p} value={p}>{p}</option>)}</select></div>
        <div><label>Est. Hours</label><input type="number" value={hours} onChange={e => setHours(e.target.value)} step="0.5" min="0.5" /></div>
      </div>
      <div className="form-row">
        <div><label>Start Date</label><input type="date" value={startDate} onChange={e => setStartDate(e.target.value)} /></div>
        <div><label>Due Date (optional)</label><input type="date" value={dueDate} onChange={e => setDueDate(e.target.value)} /></div>
      </div>
      <label>Available Weekdays (empty = any day)</label>
      <div className="weekday-picker">{DAYS.map((d, i) => (
        <button key={i} type="button" className={`weekday-btn ${weekdays.includes(DAY_VALUES[i]) ? 'active' : ''}`}
          onClick={() => toggleDay(DAY_VALUES[i])}>{d}</button>
      ))}</div>
      <div className="modal-actions">
        <button className="btn" onClick={onCancel}>Cancel</button>
        <button className="btn btn-accent" onClick={submit} disabled={!title.trim()}>Create</button>
      </div>
    </div></div>
  );
}
