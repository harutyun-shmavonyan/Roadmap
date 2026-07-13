import { useState, useEffect, useCallback } from 'react';
import type { HabitDto, SprintDto, SprintHabitDto } from './types';
import { api } from './api';

interface Props { roadmapId: string; onBack: () => void; }

export function HabitsPage({ roadmapId, onBack }: Props) {
  const [habits, setHabits] = useState<HabitDto[]>([]);
  const [sprints, setSprints] = useState<SprintDto[]>([]);
  const [selectedSprint, setSelectedSprint] = useState('');
  const [sprintHabits, setSprintHabits] = useState<SprintHabitDto[]>([]);
  const [newName, setNewName] = useState('');
  const [loading, setLoading] = useState(true);
  const [confirmDelete, setConfirmDelete] = useState<string | null>(null);

  const loadAll = useCallback(async () => {
    const [h, s] = await Promise.all([api.getHabits(roadmapId), api.getSprints(roadmapId)]);
    setHabits(h); setSprints(s);
    if (s.length > 0) setSelectedSprint(prev => prev || s[0].id);
  }, [roadmapId]);

  useEffect(() => { setLoading(true); loadAll().finally(() => setLoading(false)); }, [loadAll]);
  useEffect(() => {
    if (selectedSprint) api.getSprintHabits(roadmapId, selectedSprint).then(setSprintHabits);
  }, [roadmapId, selectedSprint]);

  const handleCreate = async () => {
    const n = newName.trim(); if (!n) return;
    await api.createHabit(roadmapId, n); setNewName(''); await loadAll();
  };

  const handleDeleteHabit = async (hid: string) => {
    if (confirmDelete !== hid) { setConfirmDelete(hid); return; }
    await api.deleteHabit(roadmapId, hid); setConfirmDelete(null); await loadAll();
    if (selectedSprint) setSprintHabits(await api.getSprintHabits(roadmapId, selectedSprint));
  };

  const handleAddToSprint = async (habitId: string) => {
    if (!selectedSprint) return;
    try { await api.addSprintHabit(roadmapId, selectedSprint, habitId); }
    catch { /* already added */ }
    setSprintHabits(await api.getSprintHabits(roadmapId, selectedSprint));
  };

  const handleRemoveFromSprint = async (shid: string) => {
    if (!selectedSprint) return;
    await api.removeSprintHabit(roadmapId, selectedSprint, shid);
    setSprintHabits(await api.getSprintHabits(roadmapId, selectedSprint));
  };

  if (loading) return <div className="app-shell"><div className="empty-state"><p>Loading...</p></div></div>;

  return (
    <div style={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '12px 20px', borderBottom: '1px solid var(--border-subtle)', flexShrink: 0, gap: 10, flexWrap: 'wrap' }}>
        <span style={{ fontSize: 16, fontWeight: 600 }}>Habits</span>
        <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
          {sprints.length > 0 && (
            <select className="sprint-select" value={selectedSprint} onChange={e => setSelectedSprint(e.target.value)}>
              {sprints.map(s => <option key={s.id} value={s.id}>{s.name}</option>)}
            </select>
          )}
        </div>
      </div>

      <div className="perf-content">
        {/* Global habit library */}
        <div className="perf-section">
          <h2>Habit Library</h2>
          <div style={{ display: 'flex', gap: 8, marginBottom: 16 }}>
            <input type="text" value={newName} onChange={e => setNewName(e.target.value)}
              onKeyDown={e => { if (e.key === 'Enter') handleCreate(); }}
              placeholder="New habit name..." style={{ flex: 1 }} />
            <button className="btn btn-accent btn-sm" onClick={handleCreate} disabled={!newName.trim()}>+ Add</button>
          </div>
          {habits.length === 0 ? (
            <p style={{ color: 'var(--text-muted)', fontSize: 13 }}>No habits yet. Create one above.</p>
          ) : (
            <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
              {habits.map(h => {
                const sh = sprintHabits.find(s => s.habitId === h.id);
                const isTracked = !!sh && !sh.isPaused;
                const isPaused = !!sh && sh.isPaused;
                return (
                  <div key={h.id} style={{ display: 'flex', alignItems: 'center', gap: 10, padding: '8px 12px', background: 'var(--bg-secondary)', borderRadius: 'var(--radius-sm)' }}>
                    <span style={{ flex: 1, fontSize: 14 }}>{h.name}</span>
                    {selectedSprint && !isTracked && !isPaused && (
                      <button className="btn btn-sm btn-accent" onClick={() => handleAddToSprint(h.id)}>+ Track</button>
                    )}
                    {selectedSprint && isTracked && (
                      <span style={{ fontSize: 12, color: 'var(--success)', fontWeight: 500 }}>✓ Tracked</span>
                    )}
                    {selectedSprint && isPaused && (
                      <button className="btn btn-sm" style={{ fontSize: 12, color: 'var(--warning)' }}
                        onClick={() => handleAddToSprint(h.id)}>▶ Resume</button>
                    )}
                    {!selectedSprint && (
                      <span style={{ fontSize: 11, color: 'var(--text-muted)' }}>Create a sprint first</span>
                    )}
                    <button className={`btn btn-sm ${confirmDelete === h.id ? 'btn-danger' : 'btn-ghost'}`}
                      onClick={() => handleDeleteHabit(h.id)}>
                      {confirmDelete === h.id ? 'Sure?' : '✕'}
                    </button>
                  </div>
                );
              })}
            </div>
          )}
        </div>

        {/* Sprint habits with streaks */}
        {selectedSprint && (
          <div className="perf-section">
            <h2>Sprint Tracking</h2>
            {sprintHabits.length === 0 ? (
              <p style={{ color: 'var(--text-muted)', fontSize: 13 }}>No habits tracked in this sprint. Add habits from the library above.</p>
            ) : (
              <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
                {sprintHabits.map(sh => (
                  <div key={sh.sprintHabitId} style={{
                    background: 'var(--bg-secondary)', borderRadius: 'var(--radius-sm)', padding: 16,
                    opacity: sh.isPaused ? 0.6 : 1,
                  }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: sh.isPaused ? 0 : 10 }}>
                      <span style={{ fontSize: 15, fontWeight: 600, flex: 1 }}>
                        {sh.name}
                        {sh.isPaused && <span style={{ fontSize: 12, color: 'var(--warning)', fontWeight: 500, marginLeft: 8 }}>⏸ Paused</span>}
                      </span>
                      {sh.isFormed && <span style={{ fontSize: 12, background: 'var(--success-light)', color: 'var(--success)', border: '1px solid var(--success)', padding: '2px 8px', borderRadius: 10 }}>✓ Formed</span>}
                      {!sh.isPaused && (
                        <span style={{ fontSize: 13, color: 'var(--text-muted)', fontFamily: 'var(--font-mono)' }}>
                          Streak: {sh.currentStreak} / Best: {sh.bestStreak}
                        </span>
                      )}
                      {sh.isPaused ? (
                        <button className="btn btn-sm btn-accent" onClick={async () => {
                          await api.resumeSprintHabit(roadmapId, selectedSprint, sh.sprintHabitId);
                          setSprintHabits(await api.getSprintHabits(roadmapId, selectedSprint));
                        }}>▶ Resume</button>
                      ) : (
                        <button className="btn btn-sm btn-ghost" title="Pause habit" onClick={async () => {
                          await api.pauseSprintHabit(roadmapId, selectedSprint, sh.sprintHabitId);
                          setSprintHabits(await api.getSprintHabits(roadmapId, selectedSprint));
                        }}>⏸</button>
                      )}
                    </div>
                    {/* Check grid — only show for active habits */}
                    {!sh.isPaused && (
                      <div style={{ display: 'flex', gap: 2, flexWrap: 'wrap' }}>
                        {sh.checks.map(c => (
                          <div key={c.date} title={c.date}
                            style={{
                              width: 18, height: 18, borderRadius: 3,
                              background: c.isChecked ? 'var(--success)' : 'var(--bg-primary)',
                              border: '1px solid var(--border-subtle)',
                              opacity: c.isChecked ? 1 : 0.4,
                            }} />
                        ))}
                      </div>
                    )}
                  </div>
                ))}
              </div>
            )}
          </div>
        )}
      </div>
    </div>
  );
}
