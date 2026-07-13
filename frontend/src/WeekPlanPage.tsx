import { useState, useEffect, useCallback, useRef } from 'react';
import type { WeekPlan, WeekPlanGoal } from './types';
import { api } from './api';
import { fmtUnit } from './unitFormat';

interface Props { roadmapId: string; onBack: () => void; }

const todayStr = () => { const d=new Date(); return `${d.getFullYear()}-${(d.getMonth()+1).toString().padStart(2,'0')}-${d.getDate().toString().padStart(2,'0')}`; };
const shift = (s: string, n: number) => { const d=new Date(s+'T00:00:00'); d.setDate(d.getDate()+n); return `${d.getFullYear()}-${(d.getMonth()+1).toString().padStart(2,'0')}-${d.getDate().toString().padStart(2,'0')}`; };
const fmtWeek = (s: string) => {
  const mon = new Date(s+'T00:00:00');
  const sun = new Date(s+'T00:00:00'); sun.setDate(sun.getDate()+6);
  const f = (d: Date) => d.toLocaleDateString('en-US',{month:'short',day:'numeric'});
  return `${f(mon)} – ${f(sun)}, ${mon.getFullYear()}`;
};

export function WeekPlanPage({ roadmapId, onBack }: Props) {
  const [date, setDate] = useState(todayStr);
  const [plan, setPlan] = useState<WeekPlan | null>(null);
  const [noSprint, setNoSprint] = useState(false);
  const [loading, setLoading] = useState(true);
  const [newGoalTitle, setNewGoalTitle] = useState('');
  const [newGoalTarget, setNewGoalTarget] = useState('');
  const [newGoalAmount, setNewGoalAmount] = useState('');
  const [editingGoal, setEditingGoal] = useState<string | null>(null);
  const [loggingGoal, setLoggingGoal] = useState<string | null>(null);
  const quickResultRef = useRef<HTMLInputElement>(null);
  const [confirmDelete, setConfirmDelete] = useState<string | null>(null);
  const [wSortKey, setWSortKey] = useState<'title'|'sessions'|'planned'|'logged'|'pct'>('title');
  const [wSortDir, setWSortDir] = useState<'asc'|'desc'>('asc');

  const refresh = useCallback(async () => {
    const result = await api.getWeekPlan(roadmapId, date);
    if ('noSprint' in result) {
      setPlan(null);
      setNoSprint(true);
    } else {
      setPlan(result as WeekPlan);
      setNoSprint(false);
    }
  }, [roadmapId, date]);

  useEffect(() => { setLoading(true); refresh().finally(() => setLoading(false)); }, [refresh]);

  const handleAddGoal = useCallback(async () => {
    const t = newGoalTitle.trim(); if (!t) return;
    await api.addWeekGoal(roadmapId, date, t, newGoalTarget||undefined, newGoalAmount?+newGoalAmount:undefined);
    setNewGoalTitle(''); setNewGoalTarget(''); setNewGoalAmount('');
    await refresh();
  }, [newGoalTitle, newGoalTarget, newGoalAmount, roadmapId, date, refresh]);

  const handleUpdateGoal = useCallback(async (g: WeekPlanGoal, updates: Partial<WeekPlanGoal>) => {
    await api.updateWeekGoal(roadmapId, date, g.id, {
      title: updates.title ?? g.title,
      targetDescription: updates.targetDescription !== undefined ? updates.targetDescription : g.targetDescription,
      targetAmount: updates.targetAmount !== undefined ? updates.targetAmount : g.targetAmount,
      resultAmount: updates.resultAmount !== undefined ? updates.resultAmount : g.resultAmount,
      resultNote: updates.resultNote !== undefined ? updates.resultNote : g.resultNote,
      isCompleted: updates.isCompleted !== undefined ? updates.isCompleted : g.isCompleted,
    });
    setEditingGoal(null);
    await refresh();
  }, [roadmapId, date, refresh]);

  const handleDeleteGoal = useCallback(async (gid: string) => {
    if (confirmDelete !== gid) { setConfirmDelete(gid); return; }
    await api.deleteWeekGoal(roadmapId, date, gid);
    setConfirmDelete(null);
    await refresh();
  }, [confirmDelete, roadmapId, date, refresh]);

  const handleToggleClose = useCallback(async () => {
    await api.toggleWeekClose(roadmapId, date);
    await refresh();
  }, [roadmapId, date, refresh]);

  const goWeek = (n: number) => setDate(d => shift(d, n * 7));

  if (loading) return <div className="app-shell"><div className="empty-state"><p>Loading...</p></div></div>;

  if (noSprint || !plan) return (
    <div className="app-shell">
      <header className="app-header">
        <div><h1><span>◆</span> Week Plan</h1></div>
        <div className="header-actions">
          <button className="btn btn-sm" onClick={() => goWeek(-1)}>‹ Prev</button>
          <button className="btn btn-sm btn-accent" onClick={() => setDate(todayStr())}>This Week</button>
          <input type="date" className="date-picker" value={date} onChange={e => { if (e.target.value) setDate(e.target.value); }} />
          <button className="btn btn-sm" onClick={() => goWeek(1)}>Next ›</button>
          <button className="btn" onClick={onBack}>← Tree</button>
        </div>
      </header>
      <div className="empty-state" style={{ flex: 1 }}>
        <p style={{ fontSize: 18, maxWidth: 400, textAlign: 'center', lineHeight: 1.6 }}>No started sprint covers this week.</p>
        <p style={{ fontSize: 14, color: 'var(--text-muted)' }}>Create and start a sprint from the Schedule page first.</p>
      </div>
    </div>
  );

  const totalPlanned = plan.scheduledItems.reduce((s, i) => s + i.plannedUnits, 0);
  const totalLogged = plan.scheduledItems.reduce((s, i) => s + i.loggedUnits, 0);
  const goalsCompleted = plan.customGoals.filter(g => g.isCompleted).length;

  return (
    <div className="app-shell">
      <header className="app-header">
        <div><h1><span>◆</span> Week Plan</h1></div>
        <div className="header-actions">
          <button className="btn btn-sm" onClick={() => goWeek(-1)}>‹ Prev</button>
          <button className="btn btn-sm btn-accent" onClick={() => setDate(todayStr())}>This Week</button>
          <input type="date" className="date-picker" value={date} onChange={e => { if (e.target.value) setDate(e.target.value); }} />
          <span className="dayplan-date">{fmtWeek(plan.weekStart)}</span>
          <button className="btn btn-sm" onClick={() => goWeek(1)}>Next ›</button>
          {plan.activeSprint && <span className={`sprint-badge ${plan.activeSprint.isOpen?'':'closed'}`}>{plan.activeSprint.name}</span>}
          <button className={`btn btn-sm ${plan.isClosed?'btn-accent':'btn-danger'}`} onClick={handleToggleClose}>
            {plan.isClosed ? '🔓 Reopen' : '🔒 Close Week'}
          </button>
          <button className="btn" onClick={onBack}>← Tree</button>
        </div>
      </header>

      <div className="week-content">
        {plan.isClosed && <div className="week-closed-banner">This week is closed.</div>}

        {/* Summary cards */}
        <div className="perf-cards" style={{marginBottom:20}}>
          <div className="perf-card">
            <div className="perf-card-label">Scheduled Items</div>
            <div className="perf-card-value">{plan.scheduledItems.length}</div>
          </div>
          <div className="perf-card">
            <div className="perf-card-label">Planned Units</div>
            <div className="perf-card-value">{Math.round(totalPlanned*10)/10}</div>
          </div>
          <div className="perf-card accent">
            <div className="perf-card-label">Logged Units</div>
            <div className="perf-card-value">{Math.round(totalLogged*10)/10}</div>
          </div>
          <div className="perf-card">
            <div className="perf-card-label">Custom Goals</div>
            <div className="perf-card-value">{goalsCompleted}/{plan.customGoals.length}</div>
          </div>
        </div>

        {/* Scheduled items table — sortable */}
        <div className="perf-section">
          <h2>Scheduled Items (from templates)</h2>
          {plan.scheduledItems.length > 0 ? (
            <div className="perf-table-wrap">
              <table className="perf-table">
                <thead><tr>
                  <th className="sortable" onClick={() => { if (wSortKey==='title') setWSortDir(d=>d==='asc'?'desc':'asc'); else { setWSortKey('title'); setWSortDir('asc'); } }}>Item{wSortKey==='title'?(wSortDir==='asc'?' ↑':' ↓'):''}</th>
                  <th className="sortable" onClick={() => { if (wSortKey==='sessions') setWSortDir(d=>d==='asc'?'desc':'asc'); else { setWSortKey('sessions'); setWSortDir('asc'); } }}>Sessions{wSortKey==='sessions'?(wSortDir==='asc'?' ↑':' ↓'):''}</th>
                  <th className="sortable" onClick={() => { if (wSortKey==='planned') setWSortDir(d=>d==='asc'?'desc':'asc'); else { setWSortKey('planned'); setWSortDir('asc'); } }}>Planned{wSortKey==='planned'?(wSortDir==='asc'?' ↑':' ↓'):''}</th>
                  <th className="sortable" onClick={() => { if (wSortKey==='logged') setWSortDir(d=>d==='asc'?'desc':'asc'); else { setWSortKey('logged'); setWSortDir('asc'); } }}>Logged{wSortKey==='logged'?(wSortDir==='asc'?' ↑':' ↓'):''}</th>
                  <th className="sortable" onClick={() => { if (wSortKey==='pct') setWSortDir(d=>d==='asc'?'desc':'asc'); else { setWSortKey('pct'); setWSortDir('asc'); } }}>%{wSortKey==='pct'?(wSortDir==='asc'?' ↑':' ↓'):''}</th>
                </tr></thead>
                <tbody>
                  {[...plan.scheduledItems].sort((a, b) => {
                    let va: number|string = 0, vb: number|string = 0;
                    switch(wSortKey) {
                      case 'title': va=a.title.toLowerCase(); vb=b.title.toLowerCase(); break;
                      case 'sessions': va=a.sessionsThisWeek; vb=b.sessionsThisWeek; break;
                      case 'planned': va=a.plannedUnits; vb=b.plannedUnits; break;
                      case 'logged': va=a.loggedUnits; vb=b.loggedUnits; break;
                      case 'pct': va=a.isNodeCompleted?1:(a.plannedUnits>0?a.loggedUnits/a.plannedUnits:0); vb=b.isNodeCompleted?1:(b.plannedUnits>0?b.loggedUnits/b.plannedUnits:0); break;
                    }
                    if (va<vb) return wSortDir==='asc'?-1:1;
                    if (va>vb) return wSortDir==='asc'?1:-1;
                    return 0;
                  }).map(item => {
                    const pct = item.isNodeCompleted ? 100 : (item.plannedUnits > 0 ? Math.round((item.loggedUnits / item.plannedUnits) * 100) : 0);
                    return (
                      <tr key={item.nodeId}>
                        <td>{item.title}{item.isNodeCompleted ? <span title="Completed" style={{ marginLeft: 4 }}>✅</span> : item.willCompleteThisSprint && <span title={`Projected: ${item.projectedCompletionDate}`} style={{ marginLeft: 4 }}>🎯</span>}</td>
                        <td>{item.sessionsThisWeek}</td>
                        <td>{fmtUnit(item.plannedUnits, item.unit)}</td>
                        <td>{fmtUnit(item.loggedUnits, item.unit)}</td>
                        <td><span className={`perf-pct ${pct >= 100 ? 'done' : pct >= 50 ? 'mid' : 'low'}`}>{pct}%</span></td>
                      </tr>);
                  })}
                </tbody>
              </table>
            </div>
          ) : <p style={{color:'var(--text-muted)',fontSize:13}}>No active items with schedules this week.</p>}
        </div>

        {/* Projected completions this sprint */}
        {(() => {
          const completing = plan.scheduledItems.filter(i => i.willCompleteThisSprint);
          return completing.length > 0 ? (
            <div className="perf-section">
              <h2>Projected Completions ({completing.length})</h2>
              <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
                {completing.map(item => {
                  const totalPct = item.isNodeCompleted ? 100 : (item.totalSize && item.totalSize > 0
                    ? Math.round((item.totalLogged / item.totalSize) * 100) : 0);
                  return (
                    <div key={item.nodeId} style={{ display: 'flex', alignItems: 'center', gap: 10, padding: '10px 14px',
                      background: 'var(--bg-secondary)', borderRadius: 'var(--radius-sm)', fontSize: 14 }}>
                      <span style={{ fontSize: 16 }}>🎯</span>
                      <span style={{ flex: 1, fontWeight: 500 }}>{item.title}</span>
                      {item.projectedCompletionDate && (
                        <span style={{ fontSize: 12, color: 'var(--text-muted)', fontFamily: 'var(--font-mono)' }}>
                          ~{item.projectedCompletionDate}
                        </span>
                      )}
                      <span style={{ fontSize: 12, fontFamily: 'var(--font-mono)', color: 'var(--accent)' }}>
                        {totalPct}% done
                      </span>
                      {item.totalSize && <span style={{ fontSize: 11, color: 'var(--text-muted)' }}>
                        {fmtUnit(item.totalLogged, item.unit)} / {fmtUnit(item.totalSize, item.unit)}
                      </span>}
                    </div>
                  );
                })}
              </div>
            </div>
          ) : null;
        })()}

        {/* Completed tasks this week */}
        {plan.completedTasks && plan.completedTasks.length > 0 && (
          <div className="perf-section">
            <h2>Completed Tasks ({plan.completedTasks.length})</h2>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
              {plan.completedTasks.map(t => {
                const priColor = t.priority === 'High' ? 'var(--danger)' : t.priority === 'Medium' ? 'var(--warning)' : 'var(--text-muted)';
                return (
                  <div key={t.id} style={{ display: 'flex', alignItems: 'center', gap: 10, padding: '10px 14px',
                    background: 'var(--bg-secondary)', borderRadius: 'var(--radius-sm)', fontSize: 14 }}>
                    <span style={{ color: 'var(--success)', fontWeight: 600 }}>✓</span>
                    <span style={{ width: 6, height: 6, borderRadius: '50%', background: priColor, flexShrink: 0 }} />
                    <span style={{ flex: 1 }}>{t.title}</span>
                    <span style={{ fontSize: 12, color: 'var(--text-muted)', fontFamily: 'var(--font-mono)' }}>{t.estimatedHours}h · {t.points}pt</span>
                    <span style={{ fontSize: 12, color: 'var(--text-muted)' }}>{t.completedDate}</span>
                  </div>
                );
              })}
            </div>
          </div>
        )}

        {/* Custom logs this week */}
        {plan.customLogs && plan.customLogs.length > 0 && (
          <div className="perf-section">
            <h2>Custom Achievements ({plan.customLogs.length})</h2>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
              {plan.customLogs.map(cl => (
                <div key={cl.id} style={{ display: 'flex', alignItems: 'center', gap: 10, padding: '10px 14px',
                  background: 'var(--bg-secondary)', borderRadius: 'var(--radius-sm)', fontSize: 14 }}>
                  <span style={{ fontSize: 14 }}>⭐</span>
                  <span style={{ flex: 1 }}>{cl.title}</span>
                  <span style={{ fontSize: 12, color: 'var(--accent)', fontFamily: 'var(--font-mono)', fontWeight: 600 }}>+{cl.points}pt</span>
                  <span style={{ fontSize: 12, color: 'var(--text-muted)' }}>{cl.date}</span>
                </div>
              ))}
            </div>
          </div>
        )}

        {/* Custom goals */}
        <div className="perf-section">
          <h2>Custom Goals</h2>
          <div className="goals-list">
            {plan.customGoals.map(g => (
              <div key={g.id} className={`goal-card ${g.isCompleted ? 'completed' : ''}`}>
                {editingGoal === g.id ? (
                  <GoalEditor goal={g} onSave={(u) => handleUpdateGoal(g, u)} onCancel={() => setEditingGoal(null)} />
                ) : (
                  <>
                    <div className="goal-header">
                      <button className={`goal-check ${g.isCompleted ? 'checked' : ''}`}
                        onClick={() => {
                          if (plan.isClosed) return;
                          const newVal = !g.isCompleted;
                          setPlan(prev => prev ? { ...prev, customGoals: prev.customGoals.map(gg => gg.id === g.id ? { ...gg, isCompleted: newVal } : gg) } : prev);
                          handleUpdateGoal(g, { isCompleted: newVal });
                        }}
                        disabled={plan.isClosed}>
                        {g.isCompleted ? '✓' : ''}
                      </button>
                      <div className="goal-info">
                        <div className="goal-title">{g.title}</div>
                        {g.targetDescription && <div className="goal-target">{g.targetDescription}</div>}
                        {g.targetAmount != null && (
                          <div className="goal-target">
                            {g.resultAmount != null
                              ? <><span style={{ color: 'var(--accent)', fontWeight: 600 }}>{g.resultAmount}</span> / {g.targetAmount}{g.sprintGoalUnit ? ` ${g.sprintGoalUnit}` : ''}</>
                              : <>Target: {g.targetAmount}{g.sprintGoalUnit ? ` ${g.sprintGoalUnit}` : ''}</>}
                          </div>
                        )}
                        {g.targetAmount == null && g.resultAmount != null && (
                          <div className="goal-target">Result: <span style={{ color: 'var(--accent)', fontWeight: 600 }}>{g.resultAmount}</span>{g.resultNote ? ` — ${g.resultNote}` : ''}</div>
                        )}
                        {g.sprintGoalId && g.sprintGoalTarget != null && (
                          <div style={{ fontSize: 11, color: 'var(--accent)', marginTop: 2 }}>
                            Sprint: {g.sprintGoalLogged ?? 0} / {g.sprintGoalTarget}{g.sprintGoalUnit ? ` ${g.sprintGoalUnit}` : ''}
                            {' '}({g.sprintGoalTarget > 0 ? Math.round((g.sprintGoalLogged ?? 0) / g.sprintGoalTarget * 100) : 0}%)
                          </div>
                        )}
                      </div>
                      <div className="goal-actions">
                        {!plan.isClosed && <>
                          <button className="btn btn-ghost btn-sm" title="Log partial result"
                            onClick={() => { setLoggingGoal(g.id); setEditingGoal(null); setTimeout(() => quickResultRef.current?.focus(), 50); }}>+ log</button>
                          <button className="btn btn-ghost btn-sm" onClick={() => { setEditingGoal(g.id); setLoggingGoal(null); }}>✎</button>
                          <button className={`btn btn-ghost btn-sm ${confirmDelete===g.id?'btn-danger':''}`}
                            onClick={() => handleDeleteGoal(g.id)}>{confirmDelete===g.id?'Sure?':'✕'}</button>
                        </>}
                      </div>
                    </div>
                    {loggingGoal === g.id && (() => {
                      const doLog = async () => {
                        const raw = quickResultRef.current?.value ?? '';
                        const v = parseFloat(raw);
                        if (!raw || isNaN(v)) return;
                        try {
                          await api.updateWeekGoal(roadmapId, date, g.id, {
                            title: g.title,
                            targetDescription: g.targetDescription ?? null,
                            targetAmount: g.targetAmount ?? null,
                            resultAmount: (g.resultAmount ?? 0) + v,
                            resultNote: g.resultNote ?? null,
                            isCompleted: g.isCompleted,
                          });
                          setLoggingGoal(null);
                          await refresh();
                        } catch (err) {
                          console.error('Failed to log goal progress:', err);
                          alert('Save failed: ' + String(err));
                        }
                      };
                      return (
                        <div style={{ display: 'flex', gap: 8, alignItems: 'center', marginTop: 10, paddingTop: 10, borderTop: '1px solid var(--border-subtle)' }}>
                          {g.resultAmount != null && <span style={{ fontSize: 12, color: 'var(--text-muted)', whiteSpace: 'nowrap' }}>Now: {g.resultAmount} +</span>}
                          <input ref={quickResultRef} type="number" step="any" placeholder="amount to add"
                            defaultValue=""
                            onKeyDown={e => { if (e.key === 'Enter') doLog(); if (e.key === 'Escape') setLoggingGoal(null); }}
                            style={{ width: 120, background: 'var(--bg-primary)', border: '1px solid var(--accent)', borderRadius: 'var(--radius-sm)',
                              color: 'var(--text-primary)', fontFamily: 'var(--font-mono)', fontSize: 14, padding: '5px 10px', outline: 'none' }} />
                          {g.sprintGoalUnit && <span style={{ fontSize: 12, color: 'var(--text-muted)' }}>{g.sprintGoalUnit}</span>}
                          <button className="btn btn-sm btn-accent" onClick={doLog}>Save</button>
                          <button className="btn btn-sm" onClick={() => setLoggingGoal(null)}>Cancel</button>
                        </div>
                      );
                    })()}
                    {g.sprintGoalId && g.sprintGoalTarget != null && g.sprintGoalTarget > 0 && (
                      <div style={{ height: 4, background: 'var(--border-subtle)', borderRadius: 2, marginTop: 6, overflow: 'hidden' }}>
                        <div style={{ height: '100%', width: `${Math.min(100, Math.round((g.sprintGoalLogged ?? 0) / g.sprintGoalTarget * 100))}%`, background: 'var(--accent)', borderRadius: 2 }} />
                      </div>
                    )}
                  </>
                )}
              </div>
            ))}

            {/* Add new goal */}
            {!plan.isClosed && (
              <>
                <div className="goal-add-form">
                  <input type="text" className="goal-input" placeholder="New goal..." value={newGoalTitle}
                    onChange={e => setNewGoalTitle(e.target.value)}
                    onKeyDown={e => { if (e.key === 'Enter') handleAddGoal(); }} />
                  <input type="text" className="goal-input small" placeholder="Target (optional)" value={newGoalTarget}
                    onChange={e => setNewGoalTarget(e.target.value)} />
                  <input type="number" className="goal-input tiny" placeholder="Amt" value={newGoalAmount}
                    onChange={e => setNewGoalAmount(e.target.value)} />
                  <button className="btn btn-sm btn-accent" onClick={handleAddGoal} disabled={!newGoalTitle.trim()}>+ Add</button>
                </div>
                {plan.sprintGoals && plan.sprintGoals.length > 0 && (() => {
                  const linked = new Set(plan.customGoals.filter(g => g.sprintGoalId).map(g => g.sprintGoalId));
                  const unlinked = plan.sprintGoals.filter(sg => !linked.has(sg.id));
                  return unlinked.length > 0 ? (
                    <div style={{ marginTop: 8 }}>
                      <div style={{ fontSize: 12, color: 'var(--text-muted)', marginBottom: 6 }}>Link from sprint goal:</div>
                      <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6 }}>
                        {unlinked.map(sg => (
                          <button key={sg.id} className="btn btn-sm btn-ghost"
                            style={{ fontSize: 12 }}
                            onClick={async () => {
                              await api.addWeekGoal(roadmapId, date, sg.title, sg.unit ? `Sprint: ${fmtUnit(sg.targetAmount, sg.unit)}` : undefined, undefined, sg.id);
                              refresh();
                            }}>
                            🎯 {sg.title} ({fmtUnit(sg.loggedAmount, sg.unit)} / {fmtUnit(sg.targetAmount, sg.unit)})
                          </button>
                        ))}
                      </div>
                    </div>
                  ) : null;
                })()}
              </>
            )}
          </div>
        </div>
        <div style={{ height: 60 }} />
      </div>
    </div>
  );
}

// Inline goal editor
function GoalEditor({ goal, onSave, onCancel }: {
  goal: WeekPlanGoal;
  onSave: (u: Partial<WeekPlanGoal>) => void;
  onCancel: () => void;
}) {
  const [title, setTitle] = useState(goal.title);
  const [targetDesc, setTargetDesc] = useState(goal.targetDescription ?? '');
  const [targetAmt, setTargetAmt] = useState(goal.targetAmount?.toString() ?? '');
  const [resultAmt, setResultAmt] = useState(goal.resultAmount?.toString() ?? '');
  const [resultNote, setResultNote] = useState(goal.resultNote ?? '');

  const save = () => {
    onSave({
      title: title.trim() || goal.title,
      targetDescription: targetDesc || null,
      targetAmount: targetAmt ? +targetAmt : null,
      resultAmount: resultAmt ? +resultAmt : null,
      resultNote: resultNote || null,
    });
  };

  return (
    <div className="goal-editor">
      <div className="goal-editor-row">
        <label>Title</label>
        <input type="text" value={title} onChange={e => setTitle(e.target.value)} />
      </div>
      <div className="goal-editor-row">
        <label>Target</label>
        <input type="text" value={targetDesc} onChange={e => setTargetDesc(e.target.value)} placeholder="Description..." />
        <input type="number" value={targetAmt} onChange={e => setTargetAmt(e.target.value)} placeholder="Amt" style={{width:70}} />
      </div>
      <div className="goal-editor-row">
        <label>Result</label>
        <input type="number" value={resultAmt} onChange={e => setResultAmt(e.target.value)} placeholder="Amt" style={{width:70}} />
        <input type="text" value={resultNote} onChange={e => setResultNote(e.target.value)} placeholder="Note..." style={{flex:1}} />
      </div>
      <div className="goal-editor-actions">
        <button className="btn btn-sm btn-accent" onClick={save}>Save</button>
        <button className="btn btn-sm" onClick={onCancel}>Cancel</button>
      </div>
    </div>
  );
}
