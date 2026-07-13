import { useState, useEffect, useCallback } from 'react';
import type { SprintDto } from './types';
import { api } from './api';

interface Props { roadmapId: string; onClose: () => void; }

export function SprintModal({ roadmapId, onClose }: Props) {
  const [sprints, setSprints] = useState<SprintDto[]>([]);
  const [name, setName] = useState('');
  const [startDate, setStartDate] = useState('');
  const [endDate, setEndDate] = useState('');
  const [loading, setLoading] = useState(true);
  const [confirmDelete, setConfirmDelete] = useState<string | null>(null);
  const [confirmClose, setConfirmClose] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');

  const load = useCallback(async () => { setSprints(await api.getSprints(roadmapId)); }, [roadmapId]);
  useEffect(() => { setLoading(true); load().finally(() => setLoading(false)); }, [load]);

  const handleCreate = async () => {
    if (!name.trim() || !startDate || !endDate) return;
    setError(''); setBusy(true);
    try {
      await api.createSprint(roadmapId, name.trim(), startDate, endDate);
      setName(''); setStartDate(''); setEndDate(''); await load();
    } catch (e) { setError((e as Error).message); }
    finally { setBusy(false); }
  };

  const handleDelete = async (sid: string) => {
    if (confirmDelete !== sid) { setConfirmDelete(sid); setConfirmClose(null); return; }
    setBusy(true);
    try { await api.deleteSprint(roadmapId, sid); setConfirmDelete(null); await load(); }
    catch (e) { setError((e as Error).message); }
    finally { setBusy(false); }
  };

  const handleStart = async (sid: string) => {
    setError(''); setBusy(true);
    try { await api.startSprint(roadmapId, sid); await load(); }
    catch (e) { setError((e as Error).message); }
    finally { setBusy(false); }
  };

  const handleCloseSprint = async (sid: string) => {
    if (confirmClose !== sid) { setConfirmClose(sid); setConfirmDelete(null); return; }
    setError(''); setBusy(true);
    try { await api.closeSprint(roadmapId, sid); setConfirmClose(null); await load(); }
    catch (e) { setError((e as Error).message); }
    finally { setBusy(false); }
  };

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal" onClick={e => e.stopPropagation()} style={{ maxWidth: 560 }}>
        <h2>Sprints</h2>

        {error && <div style={{ background: 'var(--danger-dim)', border: '1px solid var(--danger)', borderRadius: 'var(--radius-sm)', padding: '10px 14px', marginBottom: 16, fontSize: 13, color: 'var(--danger)' }}>{error}</div>}

        {loading ? <p style={{ color: 'var(--text-muted)' }}>Loading...</p> : <>
          {sprints.length > 0 && (
            <div style={{ marginBottom: 20 }}>
              {sprints.map(s => (
                <div key={s.id} style={{ display: 'flex', alignItems: 'center', gap: 10, padding: '12px 0', borderBottom: '1px solid var(--border-subtle)', flexWrap: 'wrap' }}>
                  <span className={`sprint-badge ${s.isOpen && s.isStarted ? '' : 'closed'}`}>
                    {!s.isOpen ? 'Closed' : s.isStarted ? 'Active' : 'Draft'}
                  </span>
                  <span style={{ flex: 1, fontSize: 15, fontWeight: 500, minWidth: 80 }}>{s.name}</span>
                  <span style={{ fontSize: 13, color: 'var(--text-muted)' }}>{s.startDate} → {s.endDate}</span>
                  <div style={{ display: 'flex', gap: 6 }}>
                    {!s.isStarted && s.isOpen && (
                      <button className="btn btn-sm btn-accent" onClick={() => handleStart(s.id)} disabled={busy}>▶ Start</button>
                    )}
                    {s.isOpen && (
                      <button
                        className={`btn btn-sm ${confirmClose === s.id ? 'btn-warning' : ''}`}
                        onClick={() => handleCloseSprint(s.id)}
                        disabled={busy}
                        title="Close this sprint"
                      >
                        {confirmClose === s.id ? 'Confirm close?' : '⏹ Close'}
                      </button>
                    )}
                    {!s.isOpen && (
                      <span style={{ fontSize: 12, color: 'var(--text-muted)', fontStyle: 'italic', padding: '4px 8px' }}>Ended</span>
                    )}
                    <button className={`btn btn-sm ${confirmDelete === s.id ? 'btn-danger' : ''}`}
                      onClick={() => handleDelete(s.id)} disabled={busy}>{confirmDelete === s.id ? 'Sure?' : '✕'}</button>
                  </div>
                </div>
              ))}
            </div>
          )}

          <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
            <label>New Sprint</label>
            <input type="text" value={name} onChange={e => setName(e.target.value)} placeholder="Sprint name..." />
            <div className="form-row">
              <div><label>Start Date</label><input type="date" value={startDate} onChange={e => setStartDate(e.target.value)} /></div>
              <div><label>End Date</label><input type="date" value={endDate} onChange={e => setEndDate(e.target.value)} /></div>
            </div>
            <p style={{ fontSize: 12, color: 'var(--text-muted)', margin: 0 }}>
              Sprint dates must not overlap with existing sprints. Close or delete old sprints first.
            </p>
            <div className="modal-actions">
              <button className="btn" onClick={onClose}>Done</button>
              <button className="btn btn-accent" onClick={handleCreate} disabled={!name.trim() || !startDate || !endDate || busy}>
                {busy ? '...' : 'Create'}
              </button>
            </div>
          </div>
        </>}
      </div>
    </div>
  );
}
