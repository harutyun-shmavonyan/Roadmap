import { useState, useEffect, useCallback } from 'react';
import type { NodeDto, RoadmapTree, CategoryLinkDto } from './types';
import { api } from './api';

interface Props {
  roadmapId: string;
  node: NodeDto;
  onClose: () => void;
  onChanged: () => void;
}

export function ManageCategoriesModal({ roadmapId, node, onClose, onChanged }: Props) {
  const [tree, setTree] = useState<RoadmapTree | null>(null);
  const [loading, setLoading] = useState(true);
  const [confirmRemove, setConfirmRemove] = useState<string | null>(null);

  useEffect(() => {
    api.getTree(roadmapId).then(t => { setTree(t); setLoading(false); });
  }, [roadmapId]);

  // Collect all category (non-actionable) nodes from tree
  const categories: { id: string; title: string; path: string }[] = [];
  if (tree) {
    const collect = (nodes: NodeDto[], pathParts: string[]) => {
      for (const n of nodes) {
        if (!n.isActionable) {
          const path = [...pathParts, n.title].join(' / ');
          categories.push({ id: n.id, title: n.title, path });
          collect(n.children, [...pathParts, n.title]);
        }
      }
    };
    collect(tree.roots, []);
  }

  // Exclude the primary parent and already-linked categories
  const linkedIds = new Set(node.categoryLinks.map(l => l.categoryId));
  if (node.parentId) linkedIds.add(node.parentId);
  const available = categories.filter(c => !linkedIds.has(c.id));

  const handleAdd = useCallback(async (categoryId: string) => {
    await api.addCategoryLink(roadmapId, node.id, categoryId);
    onChanged();
  }, [roadmapId, node.id, onChanged]);

  const handleRemove = useCallback(async (linkId: string) => {
    if (confirmRemove !== linkId) { setConfirmRemove(linkId); return; }
    await api.removeCategoryLink(roadmapId, node.id, linkId);
    setConfirmRemove(null);
    onChanged();
  }, [roadmapId, node.id, confirmRemove, onChanged]);

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal" onClick={e => e.stopPropagation()} style={{ maxWidth: 520 }}>
        <h2>Categories — {node.title}</h2>

        <p style={{ fontSize: 12, color: 'var(--text-muted)', marginBottom: 12 }}>
          Primary parent: <strong style={{ color: 'var(--text-secondary)' }}>{node.parentId ? 'set via tree position' : 'root level'}</strong>
        </p>

        {/* Current links */}
        {node.categoryLinks.length > 0 && (
          <div style={{ marginBottom: 16 }}>
            <div style={{ fontSize: 11, fontWeight: 600, color: 'var(--text-muted)', textTransform: 'uppercase', marginBottom: 6 }}>
              Additional Categories
            </div>
            {node.categoryLinks.map(link => (
              <div key={link.linkId} style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '6px 0', borderBottom: '1px solid var(--border-subtle)' }}>
                <span style={{ flex: 1, fontSize: 13 }}>{link.categoryTitle}</span>
                <button
                  className={`btn btn-sm ${confirmRemove === link.linkId ? 'btn-danger' : ''}`}
                  onClick={() => handleRemove(link.linkId)}>
                  {confirmRemove === link.linkId ? 'Sure?' : '✕ Remove'}
                </button>
              </div>
            ))}
          </div>
        )}

        {/* Add new link */}
        {loading ? (
          <p style={{ color: 'var(--text-muted)', fontSize: 12 }}>Loading categories...</p>
        ) : available.length > 0 ? (
          <div>
            <div style={{ fontSize: 11, fontWeight: 600, color: 'var(--text-muted)', textTransform: 'uppercase', marginBottom: 6 }}>
              Add to Category
            </div>
            <div style={{ maxHeight: 200, overflowY: 'auto' }}>
              {available.map(cat => (
                <div key={cat.id} className="picker-item" style={{ padding: '8px 12px', marginBottom: 4 }}
                  onClick={() => handleAdd(cat.id)}>
                  <div>
                    <div style={{ fontSize: 13, fontWeight: 500 }}>{cat.title}</div>
                    <div style={{ fontSize: 10, color: 'var(--text-muted)' }}>{cat.path}</div>
                  </div>
                  <span style={{ color: 'var(--accent)', fontSize: 14 }}>+</span>
                </div>
              ))}
            </div>
          </div>
        ) : (
          <p style={{ color: 'var(--text-muted)', fontSize: 12 }}>All categories are already linked.</p>
        )}

        <div className="modal-actions" style={{ marginTop: 16 }}>
          <button className="btn" onClick={onClose}>Close</button>
        </div>
      </div>
    </div>
  );
}
