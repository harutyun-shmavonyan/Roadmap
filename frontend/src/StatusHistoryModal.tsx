import { useState, useEffect } from 'react';
import type { StatusChangeDto } from './types';
import { api } from './api';

interface Props { roadmapId: string; nodeId: string; onClose: () => void; }

export function StatusHistoryModal({ roadmapId, nodeId, onClose }: Props) {
  const [history, setHistory] = useState<StatusChangeDto[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    api.getNodeHistory(roadmapId, nodeId).then(h => { setHistory(h); setLoading(false); });
  }, [roadmapId, nodeId]);

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal" onClick={e => e.stopPropagation()} style={{ maxWidth: 520 }}>
        <h2>Status History</h2>
        {loading ? <p style={{ color: 'var(--text-muted)' }}>Loading...</p> : (
          history.length === 0 ? <p style={{ color: 'var(--text-muted)' }}>No status changes recorded yet.</p> : (
            <div className="history-list">
              {history.map(h => (
                <div key={h.id} className="history-entry">
                  <div className="history-statuses">
                    <span className={`status-badge status-${h.oldStatus.toLowerCase()}`}>
                      {h.oldStatus === 'NotStarted' ? 'New' : h.oldStatus}
                    </span>
                    <span className="history-arrow">→</span>
                    <span className={`status-badge status-${h.newStatus.toLowerCase()}`}>
                      {h.newStatus === 'NotStarted' ? 'New' : h.newStatus}
                    </span>
                  </div>
                  <div className="history-meta">
                    <span className={`history-trigger ${h.trigger}`}>{h.trigger === 'auto_completed' ? '✓ Auto-completed' : '✎ Manual'}</span>
                    <span className="history-date">{h.changedAt}</span>
                  </div>
                </div>
              ))}
            </div>
          )
        )}
        <div className="modal-actions" style={{ marginTop: 16 }}>
          <button className="btn" onClick={onClose}>Close</button>
        </div>
      </div>
    </div>
  );
}
