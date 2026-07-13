import { useState, useEffect } from 'react';
import type { WorkLogHistory } from './types';
import { api } from './api';
import { fmtUnit } from './unitFormat';

interface Props { roadmapId: string; nodeId: string; onClose: () => void; }

export function WorkLogHistoryModal({ roadmapId, nodeId, onClose }: Props) {
  const [data, setData] = useState<WorkLogHistory | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    setLoading(true);
    api.getNodeLogs(roadmapId, nodeId).then(setData).finally(() => setLoading(false));
  }, [roadmapId, nodeId]);

  const total = data ? data.entries.reduce((s, e) => s + e.amount, 0) : 0;

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal" onClick={e => e.stopPropagation()} style={{ maxWidth: 520 }}>
        <h2>Log History{data ? ` — ${data.nodeTitle}` : ''}</h2>

        {loading ? <p style={{ color: 'var(--text-muted)' }}>Loading...</p> : !data || data.entries.length === 0 ? (
          <p style={{ color: 'var(--text-muted)', padding: 20 }}>No work logged yet.</p>
        ) : (
          <>
            <div style={{ display: 'flex', gap: 16, marginBottom: 16, fontSize: 13, color: 'var(--text-muted)' }}>
              <span>Total: <strong style={{ color: 'var(--text-primary)' }}>{fmtUnit(Math.round(total * 100) / 100, data.unit)}</strong></span>
              <span>Entries: <strong style={{ color: 'var(--text-primary)' }}>{data.entries.length}</strong></span>
            </div>
            <div style={{ maxHeight: 400, overflowY: 'auto' }}>
              <table className="perf-table" style={{ fontSize: 13 }}>
                <thead>
                  <tr><th>Date</th><th>Amount</th><th>Sprint</th><th>Note</th></tr>
                </thead>
                <tbody>
                  {data.entries.map(e => (
                    <tr key={e.id}>
                      <td style={{ fontFamily: 'var(--font-mono)', whiteSpace: 'nowrap' }}>{e.date}</td>
                      <td style={{ fontFamily: 'var(--font-mono)' }}>{fmtUnit(e.amount, data.unit)}</td>
                      <td style={{ fontSize: 12, color: 'var(--text-muted)' }}>{e.sprintName ?? '—'}</td>
                      <td style={{ fontSize: 12, color: 'var(--text-muted)' }}>{e.note ?? ''}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </>
        )}

        <div className="modal-actions" style={{ marginTop: 16 }}>
          <button className="btn" onClick={onClose}>Close</button>
        </div>
      </div>
    </div>
  );
}
