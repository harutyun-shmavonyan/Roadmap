import { useState, useEffect, useCallback, useRef } from 'react';
import type { RoadmapTree, NodeDto, ActionItemStatus, ScheduleTemplate, ScheduleBlockDef } from './types';
import { api } from './api';
import { fmtUnit } from './unitFormat';
import { AddNodeModal, EditNodeModal, PerDayEditor, TIMES, DURATIONS } from './AddNodeModal';
import { StatusHistoryModal } from './StatusHistoryModal';
import { ManageCategoriesModal } from './ManageCategoriesModal';
import { WorkLogHistoryModal } from './WorkLogHistoryModal';

const STATUSES: ActionItemStatus[] = ['NotStarted', 'Active', 'Paused', 'Stopped', 'Completed'];
const DAYS_SHORT = ['Sun','Mon','Tue','Wed','Thu','Fri','Sat'];

interface FlatCategory { id: string; title: string; depth: number; path: string; schedule: ScheduleTemplate | null; node: NodeDto; hasChildren: boolean; }

// True if this subtree contains any category (non-actionable) node, even nested under actionable nodes.
function containsCategory(nodes: NodeDto[]): boolean {
  return nodes.some(c => !c.isActionable || containsCategory(c.children));
}

function flattenCategories(nodes: NodeDto[], depth: number, pathParts: string[], collapsed: Set<string>): FlatCategory[] {
  const result: FlatCategory[] = [];
  for (const n of nodes) {
    if (!n.isActionable) {
      const path = [...pathParts, n.title].join(' / ');
      let sched: ScheduleTemplate | null = null;
      if (n.scheduleTemplate) try { sched = JSON.parse(n.scheduleTemplate); } catch {}
      const hasChildren = containsCategory(n.children);
      result.push({ id: n.id, title: n.title, depth, path, schedule: sched, node: n, hasChildren });
      if (!collapsed.has(n.id)) {
        result.push(...flattenCategories(n.children, depth + 1, [...pathParts, n.title], collapsed));
      }
    } else {
      // An actionable node is not a category itself, but categories can be nested under it
      // (nothing prevents that data shape). Recurse so those categories still surface in the tree.
      result.push(...flattenCategories(n.children, depth, pathParts, collapsed));
    }
  }
  return result;
}

function collectItems(nodes: NodeDto[], parentPath: string[]): { node: NodeDto; categories: string[] }[] {
  const result: { node: NodeDto; categories: string[] }[] = [];
  for (const n of nodes) {
    if (n.isActionable) {
      const cats = [...parentPath];
      n.categoryLinks.forEach(l => { if (!cats.includes(l.categoryTitle)) cats.push(l.categoryTitle); });
      result.push({ node: n, categories: cats });
    } else {
      result.push(...collectItems(n.children, [...parentPath, n.title]));
    }
  }
  return result;
}

function fmtSchedule(s: ScheduleTemplate): string {
  const days = s.days.map(d => DAYS_SHORT[d]).join('/');
  const h = Math.floor(s.startMinute / 60), m = s.startMinute % 60;
  const ap = h >= 12 ? 'PM' : 'AM', h12 = h === 0 ? 12 : h > 12 ? h - 12 : h;
  return `${days} ${h12}:${m.toString().padStart(2,'0')}${ap} ${s.durationMinutes}m`;
}

// Clamp menu position to viewport
function clampMenuPos(x: number, y: number): { x: number; y: number } {
  const menuW = 260;
  const maxX = window.innerWidth - menuW - 8;
  return { x: Math.min(x, maxX), y };
}

export function RoadmapPage({ roadmapId, onBack }: { roadmapId: string; onBack: () => void }) {
  const [tree, setTree] = useState<RoadmapTree | null>(null);
  const [loading, setLoading] = useState(true);
  const [selectedCat, setSelectedCat] = useState<string | null>(null);
  const [addModal, setAddModal] = useState<{ parentId: string | null; sortOrder: number } | null>(null);
  const [editNode, setEditNode] = useState<NodeDto | null>(null);
  const [editCatNode, setEditCatNode] = useState<NodeDto | null>(null);
  const [historyNodeId, setHistoryNodeId] = useState<string | null>(null);
  const [logHistoryNodeId, setLogHistoryNodeId] = useState<string | null>(null);
  const [catNode, setCatNode] = useState<NodeDto | null>(null);
  const [moveToNode, setMoveToNode] = useState<NodeDto | null>(null);
  const [menu, setMenu] = useState<{ x: number; y: number; node: NodeDto; isCat: boolean } | null>(null);
  const [confirmDelete, setConfirmDelete] = useState<string | null>(null);
  const [loggedMap, setLoggedMap] = useState<Record<string, number>>({});
  const menuRef = useRef<HTMLDivElement>(null);

  // Reposition menu if it overflows viewport
  useEffect(() => {
    if (!menu || !menuRef.current) return;
    const el = menuRef.current;
    const rect = el.getBoundingClientRect();
    const vw = window.innerWidth, vh = window.innerHeight;
    let nx = menu.x, ny = menu.y;
    if (rect.right > vw - 8) nx = Math.max(8, vw - rect.width - 8);
    if (rect.bottom > vh - 8) ny = Math.max(8, vh - rect.height - 8);
    if (nx !== menu.x || ny !== menu.y) setMenu(prev => prev ? { ...prev, x: nx, y: ny } : prev);
  }, [menu]);
  const [dragId, setDragId] = useState<string | null>(null);
  const [dropTarget, setDropTarget] = useState<{ id: string; pos: 'above' | 'below' } | null>(null);
  const [schedBlocks, setSchedBlocks] = useState<ScheduleBlockDef[]>([]);
  const [showBlockPanel, setShowBlockPanel] = useState(false);
  const [newBlockName, setNewBlockName] = useState('');
  const [collapsedCats, setCollapsedCats] = useState<Set<string>>(() => {
    try { const s = localStorage.getItem(`collapsed-cats-${roadmapId}`); return s ? new Set(JSON.parse(s)) : new Set(); } catch { return new Set(); }
  });
  const toggleCollapse = (id: string) => {
    setCollapsedCats(prev => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id); else next.add(id);
      localStorage.setItem(`collapsed-cats-${roadmapId}`, JSON.stringify([...next]));
      return next;
    });
  };
  const [editBlockId, setEditBlockId] = useState<string | null>(null);
  const [reorderBlockId, setReorderBlockId] = useState<string | null>(null);
  const [editCatId, setEditCatId] = useState<string | null>(null);
  const [editCatName, setEditCatName] = useState('');
  const [moveCatNode, setMoveCatNode] = useState<NodeDto | null>(null);
  const refresh = useCallback(async () => {
    const [treeData, actionables, blks] = await Promise.all([
      api.getTree(roadmapId),
      api.getActionables(roadmapId),
      api.getBlocks(roadmapId),
    ]);
    setTree(treeData); setSchedBlocks(blks);
    const map: Record<string, number> = {};
    actionables.forEach(a => { map[a.id] = a.totalLogged; });
    setLoggedMap(map);
  }, [roadmapId]);
  useEffect(() => { setLoading(true); refresh().finally(() => setLoading(false)); }, [refresh]);

  const categories = tree ? flattenCategories(tree.roots, 0, [], collapsedCats) : [];
  const allItems = tree ? collectItems(tree.roots, []) : [];
  const statusOrder: Record<string, number> = { Active: 0, NotStarted: 1, Paused: 2, Stopped: 3, Completed: 4 };
  const filtered = (selectedCat
    ? allItems.filter(i => { const cat = categories.find(c => c.id === selectedCat); return cat && i.categories.some(c => c === cat.title); })
    : allItems
  ).sort((a, b) => (statusOrder[a.node.status] ?? 9) - (statusOrder[b.node.status] ?? 9));

  const closeMenu = () => { setMenu(null); setConfirmDelete(null); };

  const handleCtx = (e: React.MouseEvent, node: NodeDto, isCat: boolean) => {
    e.preventDefault(); e.stopPropagation();
    const pos = clampMenuPos(e.clientX, e.clientY);
    setMenu({ ...pos, node, isCat }); setConfirmDelete(null);
  };

  const handleStatusChange = useCallback(async (nid: string, status: ActionItemStatus) => {
    await api.updateNodeStatus(roadmapId, nid, status); await refresh();
  }, [roadmapId, refresh]);

  const handleDelete = useCallback(async (nid: string) => {
    if (confirmDelete !== nid) { setConfirmDelete(nid); return; }
    await api.deleteNode(roadmapId, nid); setConfirmDelete(null); closeMenu(); await refresh();
  }, [roadmapId, confirmDelete, refresh]);

  const handleDuplicate = useCallback(async (node: NodeDto) => {
    await api.createNode(roadmapId, {
      parentId: node.parentId, title: `${node.title} (copy)`, isActionable: node.isActionable,
      sortOrder: node.sortOrder + 1, unit: node.unit, totalSize: node.totalSize,
      unitsPerHour: node.unitsPerHour, pointsPerUnit: node.pointsPerUnit, scheduleTemplate: node.scheduleTemplate,
    });
    await refresh();
  }, [roadmapId, refresh]);

  const handleReorder = useCallback(async (nid: string, direction: 'up' | 'down') => {
    try {
      await api.reorderNode(roadmapId, nid, direction);
      await refresh();
    } catch (e) { /* at edge, ignore */ }
  }, [roadmapId, refresh]);

  const handleMoveTo = useCallback(async (nodeId: string, newParentId: string | null) => {
    await api.moveNode(roadmapId, nodeId, newParentId, 0);
    setMoveToNode(null); await refresh();
  }, [roadmapId, refresh]);

  const handleDrop = useCallback(async (draggedId: string) => {
    if (!dropTarget || draggedId === dropTarget.id) return;
    const dropIdx = filtered.findIndex(f => f.node.id === dropTarget.id);
    if (dropIdx < 0) return;
    const targetNode = filtered[dropIdx].node;
    const newSort = dropTarget.pos === 'above' ? targetNode.sortOrder : targetNode.sortOrder + 1;
    await api.moveNode(roadmapId, draggedId, targetNode.parentId, newSort);
    await refresh();
  }, [roadmapId, filtered, refresh, dropTarget]);

  const handleCreate = useCallback(async (t: string, a: boolean, u?: string, ts?: number, uph?: number, ppu?: number, s?: string, checklist?: boolean) => {
    if (!addModal) return;
    await api.createNode(roadmapId, { parentId: addModal.parentId, title: t, isActionable: a, sortOrder: addModal.sortOrder,
      unit: u, totalSize: ts, unitsPerHour: uph, pointsPerUnit: ppu, scheduleTemplate: s, isChecklist: !!checklist });
    setAddModal(null); await refresh();
  }, [addModal, roadmapId, refresh]);

  const handleEditSave = useCallback(async (title: string, u: string|null, ts: number|null, uph: number|null, ppu: number|null, s: string|null, checklist: boolean) => {
    if (!editNode) return;
    await api.updateNode(roadmapId, editNode.id, { title, isActionable: editNode.isActionable,
      sortOrder: editNode.sortOrder, unit: u, totalSize: ts, unitsPerHour: uph, pointsPerUnit: ppu, scheduleTemplate: s, isChecklist: checklist });
    setEditNode(null); await refresh();
  }, [editNode, roadmapId, refresh]);

  const handleCatScheduleSave = useCallback(async (title: string, _u: string|null, _ts: number|null, _uph: number|null, _ppu: number|null, s: string|null, _c: boolean) => {
    if (!editCatNode) return;
    await api.updateNode(roadmapId, editCatNode.id, { title, isActionable: false,
      sortOrder: editCatNode.sortOrder, scheduleTemplate: s });
    setEditCatNode(null); await refresh();
  }, [editCatNode, roadmapId, refresh]);

  if (loading) return <div style={{display:'flex',height:'100%',alignItems:'center',justifyContent:'center',color:'var(--text-muted)',fontSize:16}}>Loading...</div>;

  return (
    <div style={{display:'flex',flexDirection:'column',height:'100%'}}>
      <div style={{display:'flex',alignItems:'center',justifyContent:'space-between',padding:'12px 20px',borderBottom:'1px solid var(--border-subtle)',flexShrink:0}}>
        <span style={{fontSize:14,color:'var(--text-muted)'}}>
          {selectedCat ? categories.find(c => c.id === selectedCat)?.path : 'All Items'} · {filtered.length} items
        </span>
        <button className="btn btn-sm" onClick={() => setAddModal({ parentId: selectedCat, sortOrder: 0 })}>+ Add</button>
      </div>

      <div className="roadmap-body">
        <aside className="cat-panel">
          <h2>Categories</h2>
          <div className={`cat-tree-item cat-all ${!selectedCat ? 'selected' : ''}`} onClick={() => setSelectedCat(null)}>
            All ({allItems.length})
          </div>
          {categories.map(cat => (
            <div key={cat.id}
              className={`cat-tree-item depth-${Math.min(cat.depth, 3)} ${selectedCat === cat.id ? 'selected' : ''}`}
              onClick={() => { if (editCatId !== cat.id) setSelectedCat(selectedCat === cat.id ? null : cat.id); }}
              onContextMenu={e => handleCtx(e, cat.node, true)}>
              {editCatId === cat.id ? (
                <input type="text" value={editCatName} onChange={e => setEditCatName(e.target.value)} autoFocus
                  onClick={e => e.stopPropagation()}
                  onKeyDown={async e => {
                    if (e.key === 'Enter' && editCatName.trim()) {
                      await api.updateNode(roadmapId, cat.id, { title: editCatName.trim(), isActionable: false, sortOrder: cat.node.sortOrder,
                        unit: null, totalSize: null, unitsPerHour: null, pointsPerUnit: null, scheduleTemplate: null });
                      setEditCatId(null); await refresh();
                    }
                    if (e.key === 'Escape') setEditCatId(null);
                  }}
                  onBlur={async () => {
                    if (editCatName.trim() && editCatName.trim() !== cat.title) {
                      await api.updateNode(roadmapId, cat.id, { title: editCatName.trim(), isActionable: false, sortOrder: cat.node.sortOrder,
                        unit: null, totalSize: null, unitsPerHour: null, pointsPerUnit: null, scheduleTemplate: null });
                      await refresh();
                    }
                    setEditCatId(null);
                  }}
                  style={{ width: '100%', fontSize: 14, padding: '4px 8px', background: 'var(--bg-primary)', border: '1px solid var(--accent)',
                    borderRadius: 'var(--radius-sm)', color: 'var(--text-primary)', outline: 'none', fontFamily: 'var(--font-body)' }} />
              ) : (
                <div style={{ display: 'flex', alignItems: 'center', gap: 4, width: '100%' }}>
                  {cat.hasChildren ? (
                    <span style={{ cursor: 'pointer', fontSize: 10, color: 'var(--text-muted)', width: 14, flexShrink: 0, textAlign: 'center' }}
                      onClick={e => { e.stopPropagation(); toggleCollapse(cat.id); }}>
                      {collapsedCats.has(cat.id) ? '▶' : '▼'}
                    </span>
                  ) : (
                    <span style={{ width: 14, flexShrink: 0 }} />
                  )}
                  <span style={{ flex: 1 }}>{cat.title}</span>
                </div>
              )}
            </div>
          ))}
          <div style={{ marginTop: 16, borderTop: '1px solid var(--border-subtle)', paddingTop: 12 }}>
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 8 }}>
              <h2 style={{ fontSize: 12, fontWeight: 600, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '0.06em' }}>Schedule Blocks</h2>
              <button className="btn btn-ghost btn-sm" style={{ fontSize: 11 }} onClick={() => setShowBlockPanel(!showBlockPanel)}>
                {showBlockPanel ? '▼' : '▶'}
              </button>
            </div>
            {showBlockPanel && (
              <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
                {schedBlocks.map(sb => (
                  <div key={sb.id} style={{ padding: '8px 10px', background: 'var(--bg-primary)', borderRadius: 'var(--radius-sm)', border: '1px solid var(--border-subtle)', fontSize: 13 }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: 6, marginBottom: 4 }}>
                      <span style={{ fontWeight: 600, flex: 1 }}>{sb.name}</span>
                      <button className="btn btn-ghost" style={{ padding: '0 4px', fontSize: 10 }}
                        onClick={() => setEditBlockId(sb.id)}>⚙</button>
                      <button className="btn btn-ghost" style={{ padding: '0 4px', fontSize: 10, color: 'var(--danger)' }}
                        onClick={async () => { await api.deleteBlock(roadmapId, sb.id); await refresh(); }}>✕</button>
                    </div>
                    {sb.scheduleTemplate && (() => { try { return <div style={{ fontSize: 10, color: 'var(--text-muted)' }}>{fmtSchedule(JSON.parse(sb.scheduleTemplate))}</div>; } catch { return null; } })()}
                    <div style={{ fontSize: 11, color: 'var(--text-muted)', marginTop: 2, cursor: sb.items.length > 0 ? 'pointer' : 'default' }}
                      onClick={() => { if (sb.items.length > 0) setReorderBlockId(sb.id); }}>
                      {sb.items.length} item{sb.items.length !== 1 ? 's' : ''}
                      {sb.items.length > 0 && <span style={{ color: 'var(--accent)', marginLeft: 4 }}>— click to reorder</span>}
                    </div>
                  </div>
                ))}
                <div style={{ display: 'flex', gap: 4, marginTop: 4 }}>
                  <input type="text" value={newBlockName} onChange={e => setNewBlockName(e.target.value)}
                    placeholder="New block..." onKeyDown={e => { if (e.key === 'Enter' && newBlockName.trim()) { api.createBlock(roadmapId, newBlockName.trim()).then(() => { setNewBlockName(''); refresh(); }); } }}
                    style={{ flex: 1, fontSize: 12, padding: '5px 8px', background: 'var(--bg-primary)', border: '1px solid var(--border)', borderRadius: 'var(--radius-sm)', color: 'var(--text-primary)', outline: 'none' }} />
                  <button className="btn btn-sm btn-accent" style={{ fontSize: 11, padding: '4px 8px' }}
                    disabled={!newBlockName.trim()} onClick={async () => { await api.createBlock(roadmapId, newBlockName.trim()); setNewBlockName(''); await refresh(); }}>+</button>
                </div>
              </div>
            )}
          </div>
        </aside>

        <div className="items-panel">
          {filtered.length === 0 && <div className="empty-state" style={{height:'auto',padding:60}}><p>No items here yet.</p></div>}
          {filtered.map(({ node, categories: cats }) => {
            const isCompleted = node.status === 'Completed';
            let schedInfo: string | null = null;
            if (node.scheduleTemplate) try { schedInfo = fmtSchedule(JSON.parse(node.scheduleTemplate)); } catch {}
            const block = schedBlocks.find(sb => sb.id === node.scheduleBlockId);
            const isDragging = dragId === node.id;
            const isDropAbove = dropTarget?.id === node.id && dropTarget.pos === 'above';
            const isDropBelow = dropTarget?.id === node.id && dropTarget.pos === 'below';
            return (
              <div key={node.id}
                className={`item-card ${isDragging ? 'dragging' : ''} ${isDropAbove ? 'drop-above' : ''} ${isDropBelow ? 'drop-below' : ''}`}
                draggable
                onDragStart={e => { setDragId(node.id); e.dataTransfer.effectAllowed = 'move'; }}
                onDragEnd={() => { setDragId(null); setDropTarget(null); }}
                onDragOver={e => {
                  e.preventDefault(); e.dataTransfer.dropEffect = 'move';
                  if (dragId === node.id) return;
                  const rect = e.currentTarget.getBoundingClientRect();
                  const mid = rect.top + rect.height / 2;
                  const pos = e.clientY < mid ? 'above' : 'below';
                  setDropTarget(prev => (prev?.id === node.id && prev?.pos === pos) ? prev : { id: node.id, pos });
                }}
                onDragLeave={e => {
                  if (!e.currentTarget.contains(e.relatedTarget as Node)) {
                    setDropTarget(prev => prev?.id === node.id ? null : prev);
                  }
                }}
                onDrop={e => { e.preventDefault(); if (dragId) handleDrop(dragId); setDragId(null); setDropTarget(null); }}
                onContextMenu={e => handleCtx(e, node, false)}
                onClick={() => setEditNode(node)}>
                <button
                  className={`task-check ${isCompleted ? 'checked' : ''}`}
                  title={isCompleted ? 'Mark as Active' : 'Mark as Completed'}
                  onClick={e => {
                    e.stopPropagation();
                    handleStatusChange(node.id, isCompleted ? 'Active' : 'Completed');
                  }}>
                  {isCompleted ? '✓' : ''}
                </button>
                <div className="item-info">
                  <div className="item-title" style={isCompleted ? {textDecoration:'line-through',opacity:0.6} : {}}>
                    {node.isChecklist && <span style={{ marginRight: 4 }} title="Checklist node">☑</span>}{node.title}
                  </div>
                  <div className="item-meta">
                    {node.unit && node.totalSize && <span>{fmtUnit(node.totalSize, node.unit)}</span>}
                    {node.unitsPerHour && <span>{node.unitsPerHour}/{node.unit?node.unit[0]:'u'}/hr</span>}
                    {node.pointsPerUnit && <span>{node.pointsPerUnit} pts</span>}
                    {schedInfo && <span>{schedInfo}</span>}
                  </div>
                  {cats.length > 0 && <div className="item-tags">{cats.map(c => <span key={c} className="item-tag">{c}</span>)}</div>}
                  {block && <div className="item-tags"><span className="item-tag" style={{ background: 'var(--accent-light)', color: 'var(--accent)', borderColor: 'var(--accent)' }}>⏱ {block.name}</span></div>}
                </div>
                <button className="btn btn-ghost btn-sm" title="View log history"
                  style={{ fontSize: 13, padding: '2px 6px', flexShrink: 0 }}
                  onClick={e => { e.stopPropagation(); setLogHistoryNodeId(node.id); }}>
                  📊
                </button>
                <span className={`status-badge status-${node.status.toLowerCase()}`}>
                  {node.status === 'NotStarted' ? 'New' : node.status}
                </span>
                {node.totalSize != null && node.totalSize > 0 && (
                  <div className="item-progress">
                    <div className="progress-bar">
                      <div className="progress-fill" style={{ width: `${Math.min(100, ((loggedMap[node.id] ?? 0) / node.totalSize) * 100)}%` }} />
                    </div>
                    <div className="progress-label">{Math.round(((loggedMap[node.id] ?? 0) / node.totalSize) * 100)}%</div>
                  </div>
                )}
              </div>
            );
          })}
        </div>
      </div>

      {/* Context menu — clamped to viewport */}
      {menu && <>
        <div className="ctx-overlay" onClick={closeMenu} onContextMenu={e => { e.preventDefault(); closeMenu(); }} />
        <div ref={menuRef} className="node-actions" style={{ left: menu.x, top: menu.y, maxHeight: 'calc(100vh - 16px)', overflowY: 'auto' }}>
          {menu.isCat ? <>
            <button onClick={() => { setEditCatId(menu.node.id); setEditCatName(menu.node.title); closeMenu(); }}>✏ Rename</button>
            <button onClick={() => { closeMenu(); setMoveCatNode(menu.node); }}>📁 Move to...</button>
            <button onClick={() => { closeMenu(); setAddModal({ parentId: menu.node.id, sortOrder: 0 }); }}>＋ Add item here</button>
            <div className="actions-divider" />
            <div className="actions-label">Reorder</div>
            <button onClick={async () => { try { await api.reorderNode(roadmapId, menu.node.id, 'up'); await refresh(); } catch {} closeMenu(); }}>↑ Move up</button>
            <button onClick={async () => { try { await api.reorderNode(roadmapId, menu.node.id, 'down'); await refresh(); } catch {} closeMenu(); }}>↓ Move down</button>
            <div className="actions-divider" />
            <button className={`danger ${confirmDelete === menu.node.id ? 'confirming' : ''}`}
              onClick={() => handleDelete(menu.node.id)}>
              {confirmDelete === menu.node.id ? '⚠ Confirm delete (includes children)' : '✕ Delete category'}
            </button>
          </> : <>
            <button onClick={() => { closeMenu(); setEditNode(menu.node); }}>⚙ Edit details</button>
            <button onClick={() => { closeMenu(); handleDuplicate(menu.node); }}>📋 Duplicate</button>
            <button onClick={() => { closeMenu(); setCatNode(menu.node); }}>🏷 Categories</button>
            <button onClick={() => { closeMenu(); setMoveToNode(menu.node); }}>📁 Move to...</button>
            <button onClick={() => { closeMenu(); setHistoryNodeId(menu.node.id); }}>📜 Status history</button>
            <button onClick={() => { closeMenu(); setLogHistoryNodeId(menu.node.id); }}>📊 Log history</button>
            {schedBlocks.length > 0 && <>
              <div className="actions-divider" />
              <div className="actions-label">Schedule Block</div>
              {menu.node.scheduleBlockId && (
                <button onClick={async () => {
                  await api.removeFromBlock(roadmapId, menu.node.scheduleBlockId!, menu.node.id);
                  closeMenu(); await refresh();
                }}>✕ Remove from block</button>
              )}
              {schedBlocks.filter(sb => sb.id !== menu.node.scheduleBlockId).map(sb => (
                <button key={sb.id} onClick={async () => {
                  if (menu.node.scheduleBlockId) await api.removeFromBlock(roadmapId, menu.node.scheduleBlockId, menu.node.id);
                  await api.assignToBlock(roadmapId, sb.id, menu.node.id);
                  closeMenu(); await refresh();
                }}>⏱ {sb.name}</button>
              ))}
            </>}
            <div className="actions-divider" />
            <div className="actions-label">Status</div>
            {STATUSES.map(s => (
              <button key={s} className={menu.node.status === s ? 'active-status' : ''}
                onClick={() => { closeMenu(); handleStatusChange(menu.node.id, s); }}>
                <span className={`status-dot status-${s.toLowerCase()}`} />
                {s === 'NotStarted' ? 'Not Started' : s}
              </button>
            ))}
            <div className="actions-divider" />
            <button className={`danger ${confirmDelete === menu.node.id ? 'confirming' : ''}`}
              onClick={() => handleDelete(menu.node.id)}>
              {confirmDelete === menu.node.id ? '⚠ Confirm delete' : '✕ Delete'}
            </button>
          </>}
        </div>
      </>}

      {addModal && <AddNodeModal onSubmit={handleCreate} onCancel={() => setAddModal(null)} />}
      {editNode && <EditNodeModal node={editNode} roadmapId={roadmapId} onSave={handleEditSave} onCancel={() => setEditNode(null)} />}
      {editCatNode && <EditNodeModal node={editCatNode} roadmapId={roadmapId} onSave={handleCatScheduleSave} onCancel={() => setEditCatNode(null)} />}
      {historyNodeId && <StatusHistoryModal roadmapId={roadmapId} nodeId={historyNodeId} onClose={() => setHistoryNodeId(null)} />}
      {logHistoryNodeId && <WorkLogHistoryModal roadmapId={roadmapId} nodeId={logHistoryNodeId} onClose={() => setLogHistoryNodeId(null)} />}
      {catNode && <ManageCategoriesModal roadmapId={roadmapId} node={catNode} onClose={() => setCatNode(null)} onChanged={() => { setCatNode(null); refresh(); }} />}
      {moveToNode && (
        <div className="modal-overlay" onClick={() => setMoveToNode(null)}>
          <div className="modal" onClick={e => e.stopPropagation()} style={{ maxWidth: 420 }}>
            <h2>Move "{moveToNode.title}" to...</h2>
            <div style={{ maxHeight: 350, overflowY: 'auto' }}>
              <div className="picker-item" style={{ padding: '10px 14px', marginBottom: 4 }}
                onClick={() => handleMoveTo(moveToNode.id, null)}>
                <div style={{ fontSize: 14, fontWeight: 500 }}>Root level</div>
                <span style={{ color: 'var(--accent)', fontSize: 14 }}>→</span>
              </div>
              {categories.filter(c => c.id !== moveToNode.parentId).map(cat => (
                <div key={cat.id} className="picker-item" style={{ padding: '10px 14px', marginBottom: 4, paddingLeft: 14 + cat.depth * 16 }}
                  onClick={() => handleMoveTo(moveToNode.id, cat.id)}>
                  <div>
                    <div style={{ fontSize: 14, fontWeight: 500 }}>{cat.title}</div>
                  </div>
                  <span style={{ color: 'var(--accent)', fontSize: 14 }}>→</span>
                </div>
              ))}
            </div>
            <div className="modal-actions"><button className="btn" onClick={() => setMoveToNode(null)}>Cancel</button></div>
          </div>
        </div>
      )}
      {moveCatNode && (
        <div className="modal-overlay" onClick={() => setMoveCatNode(null)}>
          <div className="modal" onClick={e => e.stopPropagation()} style={{ maxWidth: 420 }}>
            <h2>Move "{moveCatNode.title}" to...</h2>
            <p style={{ fontSize: 12, color: 'var(--text-muted)', marginBottom: 12 }}>Children will stay under this category.</p>
            <div style={{ maxHeight: 350, overflowY: 'auto' }}>
              <div className="picker-item" style={{ padding: '10px 14px', marginBottom: 4 }}
                onClick={async () => { await api.moveNode(roadmapId, moveCatNode.id, null, 0); setMoveCatNode(null); await refresh(); }}>
                <div style={{ fontSize: 14, fontWeight: 500 }}>Root level</div>
                <span style={{ color: 'var(--accent)', fontSize: 14 }}>→</span>
              </div>
              {categories.filter(c => c.id !== moveCatNode.id && c.id !== moveCatNode.parentId).map(cat => (
                <div key={cat.id} className="picker-item" style={{ padding: '10px 14px', marginBottom: 4, paddingLeft: 14 + cat.depth * 16 }}
                  onClick={async () => { await api.moveNode(roadmapId, moveCatNode.id, cat.id, 0); setMoveCatNode(null); await refresh(); }}>
                  <div style={{ fontSize: 14, fontWeight: 500 }}>{cat.title}</div>
                  <span style={{ color: 'var(--accent)', fontSize: 14 }}>→</span>
                </div>
              ))}
            </div>
            <div className="modal-actions"><button className="btn" onClick={() => setMoveCatNode(null)}>Cancel</button></div>
          </div>
        </div>
      )}
      {editBlockId && (() => {
        const sb = schedBlocks.find(b => b.id === editBlockId);
        if (!sb) return null;
        return <EditBlockModal block={sb} roadmapId={roadmapId} onClose={() => setEditBlockId(null)} onSaved={() => { setEditBlockId(null); refresh(); }} />;
      })()}
      {reorderBlockId && (() => {
        const sb = schedBlocks.find(b => b.id === reorderBlockId);
        if (!sb) return null;
        return <BlockReorderModal block={sb} roadmapId={roadmapId} onClose={() => setReorderBlockId(null)} onSaved={() => { setReorderBlockId(null); refresh(); }} />;
      })()}
    </div>
  );
}

const EDAYS = ['Mon','Tue','Wed','Thu','Fri','Sat','Sun'];
const EDAY_VALUES = [1,2,3,4,5,6,0];
const efmt = (m: number) => { const h=Math.floor(m/60),mm=m%60,ap=h>=12?'PM':'AM',h12=h===0?12:h>12?h-12:h; return `${h12}:${mm.toString().padStart(2,'0')} ${ap}`; };

interface PerDayEntry { startMinute: number; durationMinutes: number; }

function EditBlockModal({ block, roadmapId, onClose, onSaved }: { block: ScheduleBlockDef; roadmapId: string; onClose: () => void; onSaved: () => void }) {
  const parsed = block.scheduleTemplate ? (() => { try { return JSON.parse(block.scheduleTemplate) as ScheduleTemplate; } catch { return null; } })() : null;
  const [name, setName] = useState(block.name);
  const [days, setDays] = useState<number[]>(parsed?.days ?? []);
  const [startMin, setStartMin] = useState(parsed?.startMinute ?? 540);
  const [dur, setDur] = useState(parsed?.durationMinutes ?? 60);
  const [perDay, setPerDay] = useState<Record<string, PerDayEntry>>(() => {
    if (!parsed?.perDay) return {};
    const pd: Record<string, PerDayEntry> = {};
    for (const [k, v] of Object.entries(parsed.perDay)) pd[k] = { startMinute: v.startMinute, durationMinutes: v.durationMinutes };
    return pd;
  });
  const [showPerDay, setShowPerDay] = useState(() => !!parsed?.perDay && Object.keys(parsed.perDay).length > 0);
  const [busy, setBusy] = useState(false);

  const toggleDay = (v: number) => setDays(p => p.includes(v) ? p.filter(x => x !== v) : [...p, v]);

  const save = async () => {
    setBusy(true);
    let tmpl: string | null = null;
    if (days.length > 0) {
      const obj: Record<string, unknown> = { days, startMinute: startMin, durationMinutes: dur };
      if (showPerDay && Object.keys(perDay).length > 0) obj.perDay = perDay;
      tmpl = JSON.stringify(obj);
    }
    await api.updateBlock(roadmapId, block.id, name.trim() || block.name, tmpl ?? undefined);
    setBusy(false); onSaved();
  };

  return (
    <div className="modal-overlay" onClick={onClose}><div className="modal" onClick={e => e.stopPropagation()} style={{ maxWidth: 440 }}>
      <h2>Edit Block</h2>
      <label>Name</label>
      <input type="text" value={name} onChange={e => setName(e.target.value)} />
      <label>Days</label>
      <div className="weekday-picker">{EDAYS.map((d,i) => (
        <button key={i} type="button" className={`weekday-btn ${days.includes(EDAY_VALUES[i]) ? 'active' : ''}`} onClick={() => toggleDay(EDAY_VALUES[i])}>{d}</button>
      ))}</div>
      <div className="form-row">
        <div><label>Default Start</label><select value={startMin} onChange={e => setStartMin(+e.target.value)}>
          {TIMES.map(t => <option key={t} value={t}>{efmt(t)}</option>)}</select></div>
        <div><label>Default Duration (min)</label><input type="number" value={dur} min={5} step={5} onChange={e => setDur(+e.target.value)}
          style={{width:'100%'}} /></div>
      </div>
      {days.length > 0 && (
        <label className="checkbox-row" onClick={() => setShowPerDay(!showPerDay)}>
          <input type="checkbox" checked={showPerDay} readOnly />
          <span>Different time per day</span>
        </label>
      )}
      {showPerDay && days.length > 0 && <PerDayEditor days={days} perDay={perDay} onChange={setPerDay} defaultStart={startMin} defaultDur={dur} />}
      <div className="modal-actions">
        <button className="btn" onClick={onClose}>Cancel</button>
        <button className="btn btn-accent" onClick={save} disabled={busy}>Save</button>
      </div>
    </div></div>
  );
}
function BlockReorderModal({ block, roadmapId, onClose, onSaved }: { block: ScheduleBlockDef; roadmapId: string; onClose: () => void; onSaved: () => void }) {
  const [items, setItems] = useState(() => [...block.items].sort((a, b) => a.blockSortOrder - b.blockSortOrder));
  const [dragIdx, setDragIdx] = useState<number | null>(null);
  const [dropTarget, setDropTarget] = useState<{ idx: number; pos: 'above' | 'below' } | null>(null);
  const [busy, setBusy] = useState(false);
  const listRef = useRef<HTMLDivElement>(null);
  const scrollInterval = useRef<number | null>(null);

  // Auto-scroll when dragging near edges
  const startAutoScroll = (clientY: number) => {
    if (scrollInterval.current) cancelAnimationFrame(scrollInterval.current);
    const el = listRef.current;
    if (!el) return;
    const rect = el.getBoundingClientRect();
    const edgeZone = 40;
    const tick = () => {
      if (!el) return;
      if (clientY < rect.top + edgeZone) el.scrollTop -= 6;
      else if (clientY > rect.bottom - edgeZone) el.scrollTop += 6;
      scrollInterval.current = requestAnimationFrame(tick);
    };
    if (clientY < rect.top + edgeZone || clientY > rect.bottom - edgeZone) {
      scrollInterval.current = requestAnimationFrame(tick);
    }
  };
  const stopAutoScroll = () => { if (scrollInterval.current) { cancelAnimationFrame(scrollInterval.current); scrollInterval.current = null; } };

  const handleDrop = () => {
    if (dragIdx === null || !dropTarget) return;
    const newItems = [...items];
    const [dragged] = newItems.splice(dragIdx, 1);
    let insertIdx = dropTarget.idx;
    if (dropTarget.pos === 'below') insertIdx++;
    if (dragIdx < insertIdx) insertIdx--;
    newItems.splice(insertIdx, 0, dragged);
    setItems(newItems);
    setDragIdx(null); setDropTarget(null); stopAutoScroll();
  };

  const save = async () => {
    setBusy(true);
    await api.batchReorderBlockItems(roadmapId, block.id, items.map(i => i.nodeId));
    setBusy(false); onSaved();
  };

  const hasChanges = items.some((item, i) => item.nodeId !== block.items.sort((a, b) => a.blockSortOrder - b.blockSortOrder)[i]?.nodeId);

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal" onClick={e => e.stopPropagation()} style={{ maxWidth: 480, display: 'flex', flexDirection: 'column', maxHeight: '80vh' }}>
        <h2 style={{ flexShrink: 0 }}>Reorder — {block.name}</h2>
        <p style={{ fontSize: 12, color: 'var(--text-muted)', marginBottom: 12, flexShrink: 0 }}>
          Drag items to reorder the queue. First item plays first.
        </p>
        <div ref={listRef} style={{ flex: 1, overflowY: 'auto', minHeight: 100 }}>
          {items.map((item, idx) => {
            const isDragging = dragIdx === idx;
            const isDropAbove = dropTarget?.idx === idx && dropTarget.pos === 'above';
            const isDropBelow = dropTarget?.idx === idx && dropTarget.pos === 'below';
            return (
              <div key={item.nodeId}
                draggable
                onDragStart={e => { setDragIdx(idx); e.dataTransfer.effectAllowed = 'move'; }}
                onDragEnd={() => { setDragIdx(null); setDropTarget(null); stopAutoScroll(); }}
                onDragOver={e => {
                  e.preventDefault(); e.dataTransfer.dropEffect = 'move';
                  if (dragIdx === idx) return;
                  const rect = e.currentTarget.getBoundingClientRect();
                  const mid = rect.top + rect.height / 2;
                  const pos = e.clientY < mid ? 'above' : 'below';
                  setDropTarget(prev => (prev?.idx === idx && prev?.pos === pos) ? prev : { idx, pos });
                  startAutoScroll(e.clientY);
                }}
                onDragLeave={e => {
                  if (!e.currentTarget.contains(e.relatedTarget as Node))
                    setDropTarget(prev => prev?.idx === idx ? null : prev);
                }}
                onDrop={e => { e.preventDefault(); handleDrop(); }}
                style={{
                  display: 'flex', alignItems: 'center', gap: 10, padding: '10px 14px',
                  background: isDragging ? 'var(--accent-light)' : 'var(--bg-secondary)',
                  border: '1px solid var(--border-subtle)',
                  borderTop: isDropAbove ? '3px solid var(--accent)' : '1px solid var(--border-subtle)',
                  borderBottom: isDropBelow ? '3px solid var(--accent)' : '1px solid var(--border-subtle)',
                  borderRadius: 'var(--radius-sm)',
                  marginBottom: 4,
                  opacity: isDragging ? 0.4 : 1,
                  cursor: 'grab',
                  transition: 'border-color 0.1s',
                  position: 'relative',
                }}>
                {isDropAbove && <div style={{ position: 'absolute', top: -5, left: -2, width: 10, height: 10, borderRadius: '50%', background: 'var(--accent)' }} />}
                {isDropBelow && <div style={{ position: 'absolute', bottom: -5, left: -2, width: 10, height: 10, borderRadius: '50%', background: 'var(--accent)' }} />}
                <span style={{ color: 'var(--text-muted)', fontFamily: 'var(--font-mono)', fontSize: 12, width: 24, textAlign: 'right', flexShrink: 0 }}>{idx + 1}.</span>
                <span style={{ fontSize: 14, flex: 1 }}>{item.title}</span>
                <span style={{ fontSize: 11, color: 'var(--text-muted)' }}>{item.status}</span>
                <span style={{ fontSize: 16, color: 'var(--text-muted)', cursor: 'grab' }}>⠿</span>
              </div>
            );
          })}
        </div>
        <div className="modal-actions" style={{ flexShrink: 0, marginTop: 12 }}>
          <button className="btn" onClick={onClose}>Cancel</button>
          <button className="btn btn-accent" onClick={save} disabled={busy || !hasChanges}>
            {busy ? 'Saving...' : 'Save Order'}
          </button>
        </div>
      </div>
    </div>
  );
}
