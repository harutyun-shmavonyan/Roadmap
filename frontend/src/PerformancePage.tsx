import { useState, useEffect, useRef } from 'react';
import type { SprintDto, PerformanceSummary, PerformanceItem, CategoryTimeNode, SprintGoalDto } from './types';
import { api } from './api';
import { fmtUnit } from './unitFormat';
import { SprintModal } from './SprintModal';

interface Props { roadmapId: string; onBack: () => void; }

const COLORS = ['#4285f4', '#34a853', '#ea4335', '#fbbc04', '#46bdc6', '#e8710a', '#9334e6', '#f538a0'];

type SortKey = 'title' | 'sessions' | 'time' | 'planned' | 'done' | 'pct' | 'pts';
type SortDir = 'asc' | 'desc';

function sortItems(items: PerformanceItem[], key: SortKey, dir: SortDir): PerformanceItem[] {
  const sorted = [...items].sort((a, b) => {
    let va: number | string = 0, vb: number | string = 0;
    switch (key) {
      case 'title': va = a.title.toLowerCase(); vb = b.title.toLowerCase(); break;
      case 'sessions': va = a.scheduledSessions; vb = b.scheduledSessions; break;
      case 'time': va = a.totalMinutes; vb = b.totalMinutes; break;
      case 'planned': va = a.plannedUnits; vb = b.plannedUnits; break;
      case 'done': va = a.doneUnits; vb = b.doneUnits; break;
      case 'pct': va = a.isNodeCompleted ? 1 : (a.plannedUnits > 0 ? a.doneUnits / a.plannedUnits : 0); vb = b.isNodeCompleted ? 1 : (b.plannedUnits > 0 ? b.doneUnits / b.plannedUnits : 0); break;
      case 'pts': va = a.earnedPoints; vb = b.earnedPoints; break;
    }
    if (va < vb) return dir === 'asc' ? -1 : 1;
    if (va > vb) return dir === 'asc' ? 1 : -1;
    return 0;
  });
  return sorted;
}

export function PerformancePage({ roadmapId, onBack }: Props) {
  const [sprints, setSprints] = useState<SprintDto[]>([]);
  const [selectedSprint, setSelectedSprint] = useState('');
  const [perf, setPerf] = useState<PerformanceSummary | null>(null);
  const [loading, setLoading] = useState(true);
  const [showSprintModal, setShowSprintModal] = useState(false);
  const [expandedItem, setExpandedItem] = useState<string | null>(null);
  const [sortKey, setSortKey] = useState<SortKey>('title');
  const [sortDir, setSortDir] = useState<SortDir>('asc');
  const [selectedOverlay, setSelectedOverlay] = useState<Set<string>>(new Set());

  const loadSprints = async () => {
    const s = await api.getSprints(roadmapId);
    setSprints(s);
    if (s.length > 0 && !selectedSprint) setSelectedSprint(s[0].id);
    setLoading(false);
  };
  useEffect(() => { loadSprints(); }, [roadmapId]);
  const loadPerf = () => {
    if (selectedSprint) api.getPerformance(roadmapId, selectedSprint).then(p => {
      setPerf(p);
      setSelectedOverlay(new Set());
    }).catch(err => { console.error('Failed to load performance:', err); setPerf(null); });
  };
  useEffect(() => { loadPerf(); }, [roadmapId, selectedSprint]);

  const handleSort = (key: SortKey) => {
    if (sortKey === key) setSortDir(d => d === 'asc' ? 'desc' : 'asc');
    else { setSortKey(key); setSortDir('asc'); }
  };
  const arrow = (key: SortKey) => sortKey === key ? (sortDir === 'asc' ? ' ↑' : ' ↓') : '';

  const toggleOverlayItem = (nodeId: string) => {
    setSelectedOverlay(prev => {
      const next = new Set(prev);
      if (next.has(nodeId)) next.delete(nodeId); else next.add(nodeId);
      return next;
    });
  };

  if (loading) return <div className="app-shell"><div className="empty-state"><p>Loading...</p></div></div>;

  const sortedItems = perf ? sortItems(perf.items, sortKey, sortDir) : [];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '12px 20px', borderBottom: '1px solid var(--border-subtle)', flexShrink: 0, gap: 10, flexWrap: 'wrap' }}>
        <span style={{ fontSize: 16, fontWeight: 600 }}>Performance</span>
        <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
          {sprints.length > 0 && (
            <select className="sprint-select" value={selectedSprint} onChange={e => setSelectedSprint(e.target.value)}>
              {sprints.map(s => <option key={s.id} value={s.id}>{s.name} ({s.startDate} → {s.endDate})</option>)}
            </select>
          )}
          <button className="btn btn-sm" onClick={() => setShowSprintModal(true)}>Sprints</button>
        </div>
      </div>

      <div className="perf-content">
        {loading ? (
          <div className="empty-state" style={{ height: 'auto', padding: 60 }}>
            <p>Loading...</p>
          </div>
        ) : sprints.length === 0 ? (
          <div className="empty-state" style={{ height: 'auto', padding: 60 }}>
            <p>No sprints found.</p>
            <button className="btn btn-accent" onClick={() => setShowSprintModal(true)}>Create Sprint</button>
          </div>
        ) : !perf ? (
          <div className="empty-state" style={{ height: 'auto', padding: 60 }}>
            <p>Loading performance data...</p>
          </div>
        ) : (
          <>
            {/* Draft sprint indicator */}
            {(() => { const sel = sprints.find(s => s.id === selectedSprint); return sel && !sel.isStarted ? (
              <div style={{ background: 'var(--warning-light)', border: '1px solid var(--warning)', borderRadius: 'var(--radius-sm)', padding: '10px 16px', marginBottom: 16, fontSize: 13, color: 'var(--warning)' }}>
                ⚡ This is a <strong>draft sprint</strong> — showing projected plan based on current templates and queue. Start the sprint to begin logging work.
              </div>
            ) : null; })()}

            {/* Summary cards */}
            <div className="perf-cards">
              <div className="perf-card"><div className="perf-card-label">Planned Pts</div><div className="perf-card-value">{perf.totalPlannedPoints}</div></div>
              <div className="perf-card accent"><div className="perf-card-label">Earned Pts</div><div className="perf-card-value">{perf.totalEarnedPoints}</div></div>
              <div className="perf-card"><div className="perf-card-label">Completion</div>
                <div className="perf-card-value">{perf.totalPlannedPoints > 0 ? Math.round(perf.totalEarnedPoints / perf.totalPlannedPoints * 100) : 0}%</div></div>
              <div className="perf-card"><div className="perf-card-label">Items</div><div className="perf-card-value">{perf.items.length}</div></div>
            </div>

            {/* Items completing this sprint */}
            {perf.items.filter(i => i.willComplete).length > 0 && (
              <div className="perf-section">
                <h2>Completing This Sprint ({perf.items.filter(i => i.willComplete).length})</h2>
                <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
                  {perf.items.filter(i => i.willComplete).map(item => {
                    const pct = item.isNodeCompleted ? 100 : (item.plannedUnits > 0 ? Math.round(item.doneUnits / item.plannedUnits * 100) : 0);
                    return (
                      <div key={item.nodeId} style={{ display: 'flex', alignItems: 'center', gap: 10, padding: '10px 14px',
                        background: 'var(--bg-secondary)', borderRadius: 'var(--radius-sm)', fontSize: 14 }}>
                        <span style={{ fontSize: 16 }}>{item.isNodeCompleted ? '✅' : '🎯'}</span>
                        <span style={{ flex: 1, fontWeight: 500 }}>{item.title}</span>
                        {item.totalSize != null && (
                          <span style={{ fontSize: 12, color: 'var(--text-muted)', fontFamily: 'var(--font-mono)' }}>
                            {fmtUnit(item.doneUnits, item.unit)} / {fmtUnit(item.totalSize, item.unit)}
                          </span>
                        )}
                        {item.projectedCompletionDate && !item.isNodeCompleted && (
                          <span style={{ fontSize: 11, color: 'var(--accent)', fontFamily: 'var(--font-mono)' }}>
                            ~{item.projectedCompletionDate}
                          </span>
                        )}
                        {item.isNodeCompleted && <span style={{ fontSize: 11, color: 'var(--success)', fontWeight: 600 }}>Done!</span>}
                      </div>
                    );
                  })}
                </div>
              </div>
            )}

            {/* Sprint Goals */}
            <div className="perf-section">
              <h2>Sprint Goals</h2>
              <SprintGoalsPanel roadmapId={roadmapId} sprintId={selectedSprint!} goals={perf.sprintGoals || []} onChanged={loadPerf} />
            </div>

            {/* Category time breakdown */}
            {perf.categoryBreakdown && perf.categoryBreakdown.length > 0 && (
              <div className="perf-section">
                <h2>Time by Category</h2>
                <CategoryBreakdownTree nodes={perf.categoryBreakdown} totalMinutes={perf.categoryBreakdown.reduce((s, c) => s + c.totalMinutes, 0)} />
              </div>
            )}

            {/* Overall Progress Chart with ideal + 75% + 90% lines */}
            <div className="perf-section">
              <h2>Overall Progress</h2>
              <OverallChart items={perf.items} colors={COLORS} />
            </div>

            {/* Sortable summary table */}
            <div className="perf-section">
              <h2>Items</h2>
              <div className="perf-table-wrap">
                <table className="perf-table">
                  <thead><tr>
                    <th className="sortable" onClick={() => handleSort('title')}>Item{arrow('title')}</th>
                    <th className="sortable" onClick={() => handleSort('sessions')}>Sessions{arrow('sessions')}</th>
                    <th className="sortable" onClick={() => handleSort('time')}>Time{arrow('time')}</th>
                    <th className="sortable" onClick={() => handleSort('planned')}>Planned{arrow('planned')}</th>
                    <th className="sortable" onClick={() => handleSort('done')}>Done{arrow('done')}</th>
                    <th className="sortable" onClick={() => handleSort('pct')}>%{arrow('pct')}</th>
                    <th className="sortable" onClick={() => handleSort('pts')}>Pts{arrow('pts')}</th>
                  </tr></thead>
                  <tbody>
                    {sortedItems.map((item) => {
                      const origIdx = perf.items.findIndex(i => i.nodeId === item.nodeId);
                      const pct = item.isNodeCompleted ? 100 : (item.plannedUnits > 0 ? Math.round(item.doneUnits / item.plannedUnits * 100) : 0);
                      const isExpanded = expandedItem === item.nodeId;
                      return (
                        <tr key={item.nodeId} className={`perf-row ${isExpanded ? 'perf-row-active' : ''}`}
                          style={{ cursor: 'pointer' }}
                          onClick={() => setExpandedItem(isExpanded ? null : item.nodeId)}>
                          <td><span className="perf-dot" style={{ background: COLORS[origIdx % COLORS.length] }} />{item.title}
                            {item.isNodeCompleted
                              ? <span title="Completed" style={{ marginLeft: 4 }}>✅</span>
                              : item.willComplete && <span title={`Projected: ${item.projectedCompletionDate}`} style={{ marginLeft: 4 }}>🎯</span>}
                            <span style={{ fontSize: 11, color: 'var(--text-muted)', marginLeft: 6 }}>{isExpanded ? '▼' : '▶'}</span>
                          </td>
                          <td>{item.scheduledSessions}</td>
                          <td>{item.totalMinutes >= 60 ? `${Math.round(item.totalMinutes / 60 * 10) / 10}h` : `${Math.round(item.totalMinutes)}m`}</td>
                          <td>{fmtUnit(item.plannedUnits, item.unit)}</td>
                          <td>{fmtUnit(item.doneUnits, item.unit)}</td>
                          <td><span className={`perf-pct ${pct >= 100 ? 'done' : pct >= 50 ? 'mid' : 'low'}`}>{pct}%</span></td>
                          <td>{item.earnedPoints}</td>
                        </tr>);
                    })}
                  </tbody>
                </table>
              </div>
            </div>

            {/* Per-item chart when expanded */}
            {expandedItem && perf.items.filter(it => it.nodeId === expandedItem).map((item) => {
              const cIdx = perf.items.findIndex(i => i.nodeId === item.nodeId);
              return (
                <div key={item.nodeId} className="perf-section">
                  <h2 style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                    <span className="perf-dot" style={{ background: COLORS[cIdx % COLORS.length] }} />
                    {item.title}
                    {item.totalSize && <span style={{ fontSize: 13, color: 'var(--text-muted)', fontWeight: 400 }}>
                      ({item.doneUnits}/{item.totalSize} {item.unit})
                    </span>}
                  </h2>
                  <SingleItemChart item={item} color={COLORS[cIdx % COLORS.length]} />
                </div>
              );
            })}

            {/* Daily points */}
            <div className="perf-section">
              <h2>Daily Points</h2>
              <BarChart data={perf.dailyPoints.map(d => ({ label: d.date.slice(5), value: d.points }))} color={COLORS[0]} />
            </div>

            {/* All items overlay with select/deselect */}
            <div className="perf-section">
              <h2>All Items — Cumulative Progress</h2>
              <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6, marginBottom: 12 }}>
                <button className="btn btn-sm" style={{ fontSize: 11 }}
                  onClick={() => setSelectedOverlay(new Set(perf.items.map(i => i.nodeId)))}>
                  Select all
                </button>
                <button className="btn btn-sm" style={{ fontSize: 11 }}
                  onClick={() => setSelectedOverlay(new Set())}>
                  Clear all
                </button>
                {perf.items.map((item, idx) => {
                  const active = selectedOverlay.has(item.nodeId);
                  return (
                    <button key={item.nodeId}
                      className={`btn btn-sm ${active ? '' : 'btn-ghost'}`}
                      style={{
                        fontSize: 11, padding: '3px 10px',
                        borderColor: active ? COLORS[idx % COLORS.length] : 'var(--border)',
                        color: active ? COLORS[idx % COLORS.length] : 'var(--text-muted)',
                        background: active ? COLORS[idx % COLORS.length] + '18' : 'transparent',
                      }}
                      onClick={() => toggleOverlayItem(item.nodeId)}>
                      <span style={{ display: 'inline-block', width: 8, height: 8, borderRadius: '50%',
                        background: active ? COLORS[idx % COLORS.length] : 'var(--border)', marginRight: 4 }} />
                      {item.title}
                    </button>
                  );
                })}
              </div>
              {selectedOverlay.size > 0 ? (
                <AllItemsOverlayChart
                  items={perf.items.filter(i => selectedOverlay.has(i.nodeId))}
                  allItems={perf.items}
                  colors={COLORS} />
              ) : (
                <p style={{ color: 'var(--text-muted)', fontSize: 13, padding: 20, textAlign: 'center' }}>
                  Select items above to compare their progress.
                </p>
              )}
            </div>

            {/* Completed tasks in this sprint */}
            {perf.completedTasks && perf.completedTasks.length > 0 && (
              <div className="perf-section">
                <h2>Completed Tasks ({perf.completedTasks.length})</h2>
                <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
                  {perf.completedTasks.map(t => {
                    const priColor = t.priority === 'High' ? 'var(--danger)' : t.priority === 'Medium' ? 'var(--warning)' : 'var(--text-muted)';
                    return (
                      <div key={t.id} style={{ display: 'flex', alignItems: 'center', gap: 10, padding: '10px 14px',
                        background: 'var(--bg-secondary)', borderRadius: 'var(--radius-sm)', fontSize: 14 }}>
                        <span style={{ color: 'var(--success)', fontWeight: 600 }}>✓</span>
                        <span style={{ width: 6, height: 6, borderRadius: '50%', background: priColor, flexShrink: 0 }} />
                        <span style={{ flex: 1 }}>{t.title}</span>
                        <span style={{ fontSize: 12, color: 'var(--text-muted)', fontFamily: 'var(--font-mono)' }}>{t.estimatedHours}h · +{t.points}pt</span>
                        <span style={{ fontSize: 12, color: 'var(--text-muted)' }}>{t.completedDate}</span>
                      </div>
                    );
                  })}
                </div>
              </div>
            )}

            {/* Custom achievements in this sprint */}
            {perf.customLogs && perf.customLogs.length > 0 && (
              <div className="perf-section">
                <h2>Custom Achievements ({perf.customLogs.length})</h2>
                <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
                  {perf.customLogs.map(cl => (
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
          </>
        )}
      </div>
      {showSprintModal && <SprintModal roadmapId={roadmapId} onClose={() => { setShowSprintModal(false); loadSprints(); }} />}
    </div>
  );
}

// ===== Chart Components =====

const W = 700, H = 240, P = 48, PR = 20, PT = 24;

function useChartHover(svgRef: React.RefObject<SVGSVGElement | null>, n: number) {
  const [hoverIdx, setHoverIdx] = useState<number | null>(null);
  const handleMouse = (e: React.MouseEvent) => {
    if (!svgRef.current || n <= 1) return;
    const rect = svgRef.current.getBoundingClientRect();
    const x = (e.clientX - rect.left) / rect.width * W;
    const closest = Math.round(((x - P) / (W - P - PR)) * (n - 1));
    setHoverIdx(Math.max(0, Math.min(n - 1, closest)));
  };
  return { hoverIdx, handleMouse, clearHover: () => setHoverIdx(null) };
}

const xScale = (i: number, n: number) => P + (i / Math.max(n - 1, 1)) * (W - P - PR);
const yScale = (v: number) => H - P - (Math.min(v, 120) / 110) * (H - P - PT);

function XLabels({ dates }: { dates: { date: string }[] }) {
  const n = dates.length;
  const step = Math.max(1, Math.floor(n / 10));
  return <>{dates.map((d, i) => i % step === 0 && (
    <text key={i} x={xScale(i, n)} y={H - 10} fill="var(--text-muted)" fontSize={10} textAnchor="middle">{d.date.slice(5)}</text>
  ))}</>;
}

function YGrid() {
  return <>{[0, 25, 50, 75, 100].map(v => {
    const y = yScale(v);
    return <g key={v}>
      <line x1={P} y1={y} x2={W - PR} y2={y} stroke="var(--border-subtle)" strokeDasharray={v > 0 && v < 100 ? '3,3' : 'none'} />
      <text x={P - 8} y={y + 4} fill="var(--text-muted)" fontSize={10} textAnchor="end">{v}%</text>
    </g>;
  })}</>;
}

function HoverCrosshair({ hoverIdx, n, values, dates }: {
  hoverIdx: number; n: number;
  values: { label: string; value: number; color: string }[];
  dates: { date: string }[];
}) {
  const x = xScale(hoverIdx, n);
  return <>
    <line x1={x} y1={PT} x2={x} y2={H - P} stroke="var(--text-muted)" strokeWidth={1} strokeDasharray="4,3" opacity={0.7} />
    {values.map((v, i) => (
      <circle key={i} cx={x} cy={yScale(v.value)} r={5} fill={v.color} stroke="var(--bg-primary)" strokeWidth={2} />
    ))}
    <foreignObject x={Math.min(x + 12, W - 170)} y={PT} width={160} height={values.length * 20 + 28}>
      <div style={{
        background: 'var(--bg-elevated)', border: '1px solid var(--border)', borderRadius: 6,
        padding: '6px 10px', fontSize: 11, lineHeight: '18px', color: 'var(--text-primary)',
        boxShadow: '0 2px 8px rgba(60,64,67,0.2)',
      } as React.CSSProperties}>
        <div style={{ fontWeight: 600, marginBottom: 2, color: 'var(--text-muted)' }}>{dates[hoverIdx]?.date.slice(5)}</div>
        {values.map((v, i) => (
          <div key={i} style={{ display: 'flex', alignItems: 'center', gap: 4 }}>
            <span style={{ width: 8, height: 8, borderRadius: '50%', background: v.color, flexShrink: 0 }} />
            <span style={{ flex: 1, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{v.label}</span>
            <span style={{ fontWeight: 600, fontFamily: 'var(--font-mono)' }}>{Math.round(v.value * 10) / 10}%</span>
          </div>
        ))}
      </div>
    </foreignObject>
  </>;
}

/** Overall chart: accumulated avg % with ideal line + 75% + 90% reference lines */
function OverallChart({ items, colors }: { items: PerformanceItem[]; colors: string[] }) {
  const svgRef = useRef<SVGSVGElement>(null);
  if (items.length === 0 || items[0].dailyCumulative.length === 0) return <p style={{ color: 'var(--text-muted)' }}>No data.</p>;

  const dates = items[0].dailyCumulative;
  const n = dates.length;
  const { hoverIdx, handleMouse, clearHover } = useChartHover(svgRef, n);

  // Average cumulative % per day (actual)
  const avgData = dates.map((_, di) => {
    const vals = items.map(it => it.dailyCumulative[di]?.cumulativePercent ?? 0);
    return vals.reduce((a, b) => a + b, 0) / vals.length;
  });

  // Average ideal % per day (from backend — based on actual planned units per day)
  const idealData = dates.map((_, di) => {
    const vals = items.map(it => it.dailyCumulative[di]?.idealPercent ?? 0);
    return vals.reduce((a, b) => a + b, 0) / vals.length;
  });

  const linePath = avgData.map((v, i) => `${i === 0 ? 'M' : 'L'}${xScale(i, n)},${yScale(v)}`).join(' ');
  const idealPath = idealData.map((v, i) => `${i === 0 ? 'M' : 'L'}${xScale(i, n)},${yScale(v)}`).join(' ');
  const line75 = idealData.map((v, i) => `${i === 0 ? 'M' : 'L'}${xScale(i, n)},${yScale(v * 0.75)}`).join(' ');
  const line90 = idealData.map((v, i) => `${i === 0 ? 'M' : 'L'}${xScale(i, n)},${yScale(v * 0.90)}`).join(' ');

  return (
    <svg ref={svgRef} viewBox={`0 0 ${W} ${H}`} className="chart-svg"
      onMouseMove={handleMouse} onMouseLeave={clearHover} style={{ cursor: 'crosshair' }}>
      <YGrid />

      {/* Ideal line (100%) */}
      <path d={idealPath} fill="none" stroke="var(--text-muted)" strokeWidth={2.5} strokeDasharray="6,4" opacity={0.5} />
      {/* 90% line */}
      <path d={line90} fill="none" stroke="#8aca6a" strokeWidth={2} strokeDasharray="4,3" opacity={0.5} />
      {/* 75% line */}
      <path d={line75} fill="none" stroke="#d4aa5a" strokeWidth={2} strokeDasharray="4,3" opacity={0.5} />

      {/* Actual progress — line only */}
      <path d={linePath} fill="none" stroke={colors[0]} strokeWidth={2.5} strokeLinejoin="round" />

      {/* Legend for reference lines */}
      <g transform={`translate(${W - PR - 120}, ${PT + 2})`}>
        <line x1={0} y1={4} x2={16} y2={4} stroke="var(--text-muted)" strokeWidth={2.5} strokeDasharray="4,3" opacity={0.5} />
        <text x={20} y={7} fill="var(--text-muted)" fontSize={9}>Ideal (100%)</text>
        <line x1={0} y1={16} x2={16} y2={16} stroke="#8aca6a" strokeWidth={2} strokeDasharray="4,3" opacity={0.5} />
        <text x={20} y={19} fill="var(--text-muted)" fontSize={9}>90%</text>
        <line x1={0} y1={28} x2={16} y2={28} stroke="#d4aa5a" strokeWidth={2} strokeDasharray="4,3" opacity={0.5} />
        <text x={20} y={31} fill="var(--text-muted)" fontSize={9}>75%</text>
      </g>

      <XLabels dates={dates} />
      {hoverIdx !== null && (
        <HoverCrosshair hoverIdx={hoverIdx} n={n} dates={dates}
          values={[
            { label: 'Actual', value: avgData[hoverIdx], color: colors[0] },
            { label: 'Ideal', value: idealData[hoverIdx], color: '#888' },
            { label: '90%', value: idealData[hoverIdx] * 0.9, color: '#34a853' },
            { label: '75%', value: idealData[hoverIdx] * 0.75, color: '#f9ab00' },
          ]} />
      )}
    </svg>
  );
}

/** Single item chart — with ideal line */
function SingleItemChart({ item, color }: { item: PerformanceItem; color: string }) {
  const svgRef = useRef<SVGSVGElement>(null);
  if (item.dailyCumulative.length === 0) return null;
  const dates = item.dailyCumulative;
  const n = dates.length;
  const { hoverIdx, handleMouse, clearHover } = useChartHover(svgRef, n);

  const linePath = dates.map((d, i) => `${i === 0 ? 'M' : 'L'}${xScale(i, n)},${yScale(d.cumulativePercent)}`).join(' ');
  const idealPath = dates.map((d, i) => `${i === 0 ? 'M' : 'L'}${xScale(i, n)},${yScale(d.idealPercent)}`).join(' ');
  const line90 = dates.map((d, i) => `${i === 0 ? 'M' : 'L'}${xScale(i, n)},${yScale(d.idealPercent * 0.9)}`).join(' ');
  const line75 = dates.map((d, i) => `${i === 0 ? 'M' : 'L'}${xScale(i, n)},${yScale(d.idealPercent * 0.75)}`).join(' ');

  return (
    <svg ref={svgRef} viewBox={`0 0 ${W} ${H}`} className="chart-svg"
      onMouseMove={handleMouse} onMouseLeave={clearHover} style={{ cursor: 'crosshair' }}>
      <YGrid />
      <path d={idealPath} fill="none" stroke="var(--text-muted)" strokeWidth={2} strokeDasharray="5,3" opacity={0.5} />
      <path d={line90} fill="none" stroke="#8aca6a" strokeWidth={2} strokeDasharray="4,3" opacity={0.4} />
      <path d={line75} fill="none" stroke="#d4aa5a" strokeWidth={2} strokeDasharray="4,3" opacity={0.4} />
      <path d={linePath} fill="none" stroke={color} strokeWidth={2.5} strokeLinejoin="round" />
      <XLabels dates={dates} />
      {hoverIdx !== null && (
        <HoverCrosshair hoverIdx={hoverIdx} n={n} dates={dates}
          values={[
            { label: 'Actual', value: dates[hoverIdx].cumulativePercent, color },
            { label: 'Ideal', value: dates[hoverIdx].idealPercent, color: '#888' },
            { label: '90%', value: dates[hoverIdx].idealPercent * 0.9, color: '#34a853' },
            { label: '75%', value: dates[hoverIdx].idealPercent * 0.75, color: '#f9ab00' },
          ]} />
      )}
    </svg>
  );
}

/** All items overlay — only shows selected items */
function AllItemsOverlayChart({ items, allItems, colors }: { items: PerformanceItem[]; allItems: PerformanceItem[]; colors: string[] }) {
  const svgRef = useRef<SVGSVGElement>(null);
  if (items.length === 0) return null;
  const refItem = allItems[0];
  if (!refItem || refItem.dailyCumulative.length === 0) return null;

  const dates = refItem.dailyCumulative;
  const n = dates.length;
  const { hoverIdx, handleMouse, clearHover } = useChartHover(svgRef, n);

  return (
    <svg ref={svgRef} viewBox={`0 0 ${W} ${H}`} className="chart-svg"
      onMouseMove={handleMouse} onMouseLeave={clearHover} style={{ cursor: 'crosshair' }}>
      <YGrid />
      {items.map((item) => {
        const origIdx = allItems.findIndex(i => i.nodeId === item.nodeId);
        return (
          <path key={item.nodeId}
            d={item.dailyCumulative.map((d, i) => `${i === 0 ? 'M' : 'L'}${xScale(i, n)},${yScale(d.cumulativePercent)}`).join(' ')}
            fill="none" stroke={colors[origIdx % colors.length]} strokeWidth={2} strokeLinejoin="round" opacity={0.85} />
        );
      })}
      <XLabels dates={dates} />
      {hoverIdx !== null && (
        <HoverCrosshair hoverIdx={hoverIdx} n={n} dates={dates}
          values={items.map((item) => {
            const origIdx = allItems.findIndex(i => i.nodeId === item.nodeId);
            return {
              label: item.title,
              value: item.dailyCumulative[hoverIdx]?.cumulativePercent ?? 0,
              color: colors[origIdx % colors.length],
            };
          })} />
      )}
    </svg>
  );
}

function BarChart({ data, color }: { data: { label: string; value: number }[]; color: string }) {
  const svgRef = useRef<SVGSVGElement>(null);
  const [hoverIdx, setHoverIdx] = useState<number | null>(null);
  if (data.length === 0) return null;
  const BH = 160;
  const mx = Math.max(...data.map(d => d.value), 1);
  const bw = Math.max(4, (W - P - PR) / data.length - 2);
  const handleMouse = (e: React.MouseEvent) => {
    if (!svgRef.current) return;
    const rect = svgRef.current.getBoundingClientRect();
    const x = (e.clientX - rect.left) / rect.width * W;
    const idx = Math.floor(((x - P) / (W - P - PR)) * data.length);
    setHoverIdx(Math.max(0, Math.min(data.length - 1, idx)));
  };
  return (
    <svg ref={svgRef} viewBox={`0 0 ${W} ${BH}`} className="chart-svg"
      onMouseMove={handleMouse} onMouseLeave={() => setHoverIdx(null)} style={{ cursor: 'crosshair' }}>
      {data.map((d, i) => {
        const x = P + (i / data.length) * (W - P - PR);
        const h = (d.value / mx) * (BH - P * 1.5);
        const isHovered = hoverIdx === i;
        return <g key={i}>
          <rect x={x} y={BH - P - h} width={bw} height={h} fill={color} rx={2} opacity={isHovered ? 1 : 0.6} />
          {i % Math.max(1, Math.floor(data.length / 10)) === 0 && <text x={x + bw / 2} y={BH - 8} fill="var(--text-muted)" fontSize={9} textAnchor="middle">{d.label}</text>}
          {isHovered && <>
            <line x1={x + bw / 2} y1={20} x2={x + bw / 2} y2={BH - P} stroke="var(--text-muted)" strokeWidth={1} strokeDasharray="3,3" opacity={0.5} />
            <text x={x + bw / 2} y={BH - P - h - 6} fill="var(--text-primary)" fontSize={11} textAnchor="middle" fontWeight={600}>
              {Math.round(d.value * 10) / 10}
            </text>
          </>}
        </g>;
      })}
    </svg>
  );
}

function CategoryBreakdownTree({ nodes, totalMinutes }: { nodes: CategoryTimeNode[]; totalMinutes: number }) {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
      {nodes.map(cat => <CategoryRow key={cat.categoryName + cat.depth} cat={cat} totalMinutes={totalMinutes} />)}
    </div>
  );
}

function CategoryRow({ cat, totalMinutes }: { cat: CategoryTimeNode; totalMinutes: number }) {
  const [expanded, setExpanded] = useState(false);
  const hours = Math.round(cat.totalMinutes / 60 * 10) / 10;
  const pct = totalMinutes > 0 ? Math.round(cat.totalMinutes / totalMinutes * 100) : 0;
  const hasChildren = cat.children && cat.children.length > 0;
  const indent = cat.depth * 20;

  return (
    <>
      <div style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '7px 14px', paddingLeft: 14 + indent,
        background: cat.depth === 0 ? 'var(--bg-secondary)' : 'transparent',
        borderRadius: 'var(--radius-sm)', fontSize: cat.depth === 0 ? 14 : 13 }}>
        {hasChildren ? (
          <span style={{ cursor: 'pointer', fontSize: 10, color: 'var(--text-muted)', width: 14, textAlign: 'center', flexShrink: 0 }}
            onClick={() => setExpanded(!expanded)}>
            {expanded ? '▼' : '▶'}
          </span>
        ) : (
          <span style={{ width: 14, flexShrink: 0 }} />
        )}
        <span style={{ flex: 1, fontWeight: cat.depth === 0 ? 600 : 400 }}>{cat.categoryName}</span>
        <span style={{ fontSize: 12, fontFamily: 'var(--font-mono)', color: 'var(--text-secondary)', width: 48, textAlign: 'right' }}>{hours}h</span>
        <span style={{ fontSize: 12, fontFamily: 'var(--font-mono)', color: 'var(--accent)', width: 48, textAlign: 'right' }}>{cat.totalPoints}pt</span>
        <div style={{ width: 70, height: 5, background: 'var(--border-subtle)', borderRadius: 3, overflow: 'hidden', flexShrink: 0 }}>
          <div style={{ height: '100%', width: `${pct}%`, background: cat.depth === 0 ? 'var(--accent)' : 'var(--text-muted)', borderRadius: 3 }} />
        </div>
        <span style={{ fontSize: 11, color: 'var(--text-muted)', fontFamily: 'var(--font-mono)', width: 30, textAlign: 'right' }}>{pct}%</span>
      </div>
      {expanded && hasChildren && cat.children.map(child =>
        <CategoryRow key={child.categoryName + child.depth} cat={child} totalMinutes={totalMinutes} />
      )}
    </>
  );
}

function SprintGoalsPanel({ roadmapId, sprintId, goals, onChanged }: { roadmapId: string; sprintId: string; goals: SprintGoalDto[]; onChanged: () => void }) {
  const [showAdd, setShowAdd] = useState(false);
  const [editId, setEditId] = useState<string | null>(null);
  const [logId, setLogId] = useState<string | null>(null);
  const [logAmount, setLogAmount] = useState('');
  const [logBusy, setLogBusy] = useState(false);
  const logRef = useRef<HTMLInputElement>(null);

  useEffect(() => { if (logId && logRef.current) setTimeout(() => logRef.current?.focus(), 50); }, [logId]);

  const today = new Date().toISOString().split('T')[0];

  const handleLog = async (goalId: string) => {
    const v = parseFloat(logAmount);
    if (isNaN(v) || v <= 0) return;
    setLogBusy(true);
    try { await api.logSprintGoal(roadmapId, sprintId, goalId, today, v); setLogId(null); setLogAmount(''); onChanged(); }
    finally { setLogBusy(false); }
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
      {goals.map(g => {
        const pct = g.targetAmount > 0 ? Math.min(100, Math.round(g.loggedAmount / g.targetAmount * 100)) : 0;
        const done = pct >= 100;
        return (
          <div key={g.id} style={{ padding: '12px 14px', background: 'var(--bg-secondary)', borderRadius: 'var(--radius-sm)' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 6 }}>
              <span style={{ fontSize: 15 }}>{done ? '✅' : '🎯'}</span>
              <span style={{ flex: 1, fontWeight: 600, fontSize: 14 }}>{g.title}</span>
              <span style={{ fontSize: 12, fontFamily: 'var(--font-mono)', color: done ? 'var(--success)' : 'var(--text-secondary)' }}>
                {fmtUnit(g.loggedAmount, g.unit)} / {fmtUnit(g.targetAmount, g.unit)}
              </span>
              <span style={{ fontSize: 12, fontFamily: 'var(--font-mono)', color: done ? 'var(--success)' : 'var(--accent)', fontWeight: 600 }}>{pct}%</span>
            </div>
            {g.description && <div style={{ fontSize: 12, color: 'var(--text-muted)', marginBottom: 6 }}>{g.description}</div>}
            <div style={{ height: 6, background: 'var(--border-subtle)', borderRadius: 3, overflow: 'hidden', marginBottom: 8 }}>
              <div style={{ height: '100%', width: `${pct}%`, background: done ? 'var(--success)' : 'var(--accent)', borderRadius: 3, transition: 'width 0.3s' }} />
            </div>
            <div style={{ display: 'flex', gap: 6 }}>
              {logId === g.id ? (
                <>
                  <input ref={logRef} type="number" value={logAmount} step="any" placeholder="Amount..."
                    onChange={e => setLogAmount(e.target.value)}
                    onKeyDown={e => { if (e.key === 'Enter') handleLog(g.id); if (e.key === 'Escape') { setLogId(null); setLogAmount(''); } }}
                    style={{ width: 80, fontSize: 13, padding: '4px 8px', background: 'var(--bg-primary)', border: '1px solid var(--border)',
                      borderRadius: 'var(--radius-sm)', color: 'var(--text-primary)', fontFamily: 'var(--font-mono)', outline: 'none' }} />
                  <button className="btn btn-sm btn-accent" onClick={() => handleLog(g.id)} disabled={logBusy}>Log</button>
                  <button className="btn btn-sm" onClick={() => { setLogId(null); setLogAmount(''); }}>Cancel</button>
                </>
              ) : (
                <>
                  <button className="btn btn-sm btn-ghost" onClick={() => setLogId(g.id)}>+ Log</button>
                  <button className="btn btn-sm btn-ghost" onClick={() => setEditId(g.id)}>Edit</button>
                  <button className="btn btn-sm btn-ghost" style={{ color: 'var(--danger)' }}
                    onClick={async () => { await api.deleteSprintGoal(roadmapId, sprintId, g.id); onChanged(); }}>Delete</button>
                </>
              )}
            </div>
          </div>
        );
      })}

      {showAdd ? (
        <SprintGoalForm roadmapId={roadmapId} sprintId={sprintId} onDone={() => { setShowAdd(false); onChanged(); }} onCancel={() => setShowAdd(false)} />
      ) : (
        <button className="btn btn-sm btn-ghost" style={{ alignSelf: 'flex-start' }} onClick={() => setShowAdd(true)}>+ Add Sprint Goal</button>
      )}

      {editId && (() => {
        const g = goals.find(x => x.id === editId);
        if (!g) return null;
        return <SprintGoalForm roadmapId={roadmapId} sprintId={sprintId} existing={g} onDone={() => { setEditId(null); onChanged(); }} onCancel={() => setEditId(null)} />;
      })()}
    </div>
  );
}

function SprintGoalForm({ roadmapId, sprintId, existing, onDone, onCancel }: {
  roadmapId: string; sprintId: string; existing?: SprintGoalDto; onDone: () => void; onCancel: () => void;
}) {
  const [title, setTitle] = useState(existing?.title ?? '');
  const [unit, setUnit] = useState(existing?.unit ?? '');
  const [target, setTarget] = useState(existing?.targetAmount?.toString() ?? '');
  const [desc, setDesc] = useState(existing?.description ?? '');
  const [busy, setBusy] = useState(false);
  const ref = useRef<HTMLInputElement>(null);
  useEffect(() => { ref.current?.focus(); }, []);

  const save = async () => {
    const t = title.trim(); const ta = parseFloat(target);
    if (!t || isNaN(ta) || ta <= 0) return;
    setBusy(true);
    try {
      if (existing) await api.updateSprintGoal(roadmapId, sprintId, existing.id, t, ta, unit || undefined, desc || undefined);
      else await api.createSprintGoal(roadmapId, sprintId, t, ta, unit || undefined, desc || undefined);
      onDone();
    } finally { setBusy(false); }
  };

  return (
    <div style={{ padding: '12px 14px', background: 'var(--bg-secondary)', borderRadius: 'var(--radius-sm)', display: 'flex', flexDirection: 'column', gap: 8 }}>
      <input ref={ref} type="text" value={title} onChange={e => setTitle(e.target.value)} placeholder="Goal title..."
        onKeyDown={e => { if (e.key === 'Enter') save(); if (e.key === 'Escape') onCancel(); }}
        style={{ fontSize: 14, padding: '8px 10px', background: 'var(--bg-primary)', border: '1px solid var(--border)',
          borderRadius: 'var(--radius-sm)', color: 'var(--text-primary)', outline: 'none' }} />
      <div style={{ display: 'flex', gap: 8 }}>
        <input type="number" value={target} onChange={e => setTarget(e.target.value)} placeholder="Target amount" min={1} step="any"
          style={{ flex: 1, fontSize: 13, padding: '6px 8px', background: 'var(--bg-primary)', border: '1px solid var(--border)',
            borderRadius: 'var(--radius-sm)', color: 'var(--text-primary)', fontFamily: 'var(--font-mono)', outline: 'none' }} />
        <input type="text" value={unit} onChange={e => setUnit(e.target.value)} placeholder="Unit (e.g. km, books)"
          style={{ width: 100, fontSize: 13, padding: '6px 8px', background: 'var(--bg-primary)', border: '1px solid var(--border)',
            borderRadius: 'var(--radius-sm)', color: 'var(--text-primary)', outline: 'none' }} />
      </div>
      <input type="text" value={desc} onChange={e => setDesc(e.target.value)} placeholder="Description (optional)"
        style={{ fontSize: 13, padding: '6px 8px', background: 'var(--bg-primary)', border: '1px solid var(--border)',
          borderRadius: 'var(--radius-sm)', color: 'var(--text-primary)', outline: 'none' }} />
      <div style={{ display: 'flex', gap: 8 }}>
        <button className="btn btn-sm" onClick={onCancel}>Cancel</button>
        <button className="btn btn-sm btn-accent" onClick={save} disabled={busy || !title.trim()}>{existing ? 'Update' : 'Create'}</button>
      </div>
    </div>
  );
}
