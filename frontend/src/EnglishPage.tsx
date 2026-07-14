import { useState, useEffect, useMemo } from 'react';
import type { VocabEntryDto, VocabStatsDto } from './types';
import { api } from './api';

// Read-only view over the vocabulary store. Entries are created and graded through the
// MCP tools during a chat review; the tab exists to see what has been learned and how
// well it is holding — plus a delete, so a junk entry doesn't need a chat round-trip.

type Filter = 'all' | 'due' | 'New' | 'Learning' | 'Young' | 'Mature';

const STRENGTH_COLOR: Record<string, string> = {
  New: '#8b8b8b',
  Learning: '#e5484d',
  Young: '#f5a623',
  Mature: '#30a46c',
};

const FREQ_COLOR: Record<string, string> = {
  VeryCommon: '#30a46c',
  Common: '#5b8def',
  Uncommon: '#f5a623',
  Rare: '#8b8b8b',
};

// "PhrasalVerb" -> "Phrasal verb"
function humanize(s: string): string {
  const spaced = s.replace(/([a-z])([A-Z])/g, '$1 $2');
  return spaced.charAt(0).toUpperCase() + spaced.slice(1).toLowerCase();
}

function fmtDate(iso: string): string {
  const [y, m, d] = iso.slice(0, 10).split('-');
  return d && m && y ? `${d}.${m}.${y}` : iso;
}

/** "in 3 days" / "today" / "2 days ago" — the interval matters more than the date. */
function dueLabel(dueOn: string): string {
  const today = new Date();
  today.setHours(0, 0, 0, 0);
  const due = new Date(dueOn + 'T00:00:00');
  const days = Math.round((due.getTime() - today.getTime()) / 86_400_000);
  if (days === 0) return 'due today';
  if (days < 0) return `overdue by ${-days}d`;
  if (days === 1) return 'due tomorrow';
  return `due in ${days}d`;
}

function Pill({ text, color }: { text: string; color: string }) {
  return (
    <span style={{
      fontSize: 11, fontWeight: 600, padding: '2px 8px', borderRadius: 999,
      background: `${color}1a`, color, border: `1px solid ${color}55`, whiteSpace: 'nowrap',
    }}>{text}</span>
  );
}

function Stat({ label, value, color }: { label: string; value: number | string; color?: string }) {
  return (
    <div style={{
      padding: '10px 14px', borderRadius: 'var(--radius-md)', background: 'var(--bg-secondary)',
      border: '1px solid var(--border-subtle)', minWidth: 92,
    }}>
      <div style={{ fontSize: 20, fontWeight: 700, color: color ?? 'var(--text-primary)' }}>{value}</div>
      <div style={{ fontSize: 11, color: 'var(--text-muted)', marginTop: 2 }}>{label}</div>
    </div>
  );
}

/** The grade history as a row of coloured squares — the shape of the learning curve at a glance. */
function GradeTrail({ grades }: { grades: number[] }) {
  if (!grades.length) return <span style={{ fontSize: 12, color: 'var(--text-muted)' }}>never reviewed</span>;
  return (
    <div style={{ display: 'flex', gap: 3 }}>
      {grades.map((g, i) => (
        <span key={i} title={`grade ${g}`}
          style={{
            width: 14, height: 14, borderRadius: 3, fontSize: 9, fontWeight: 700,
            display: 'flex', alignItems: 'center', justifyContent: 'center',
            background: g >= 3 ? '#30a46c' : '#e5484d', color: '#fff',
          }}>{g}</span>
      ))}
    </div>
  );
}

export function EnglishPage() {
  const [entries, setEntries] = useState<VocabEntryDto[]>([]);
  const [stats, setStats] = useState<VocabStatsDto | null>(null);
  const [filter, setFilter] = useState<Filter>('all');
  const [search, setSearch] = useState('');
  const [expanded, setExpanded] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  const load = () => {
    setLoading(true);
    Promise.all([api.getVocab(), api.getVocabStats()])
      .then(([list, s]) => { setEntries(list); setStats(s); })
      .finally(() => setLoading(false));
  };

  useEffect(load, []);

  const remove = async (e: VocabEntryDto) => {
    if (!confirm(`Delete "${e.term}" and its whole review history?`)) return;
    await api.deleteVocabEntry(e.id);
    load();
  };

  const shown = useMemo(() => {
    const q = search.trim().toLowerCase();
    return entries.filter(e => {
      if (filter === 'due' && !e.isDue) return false;
      if (filter !== 'all' && filter !== 'due' && e.strength !== filter) return false;
      if (!q) return true;
      return e.term.toLowerCase().includes(q) || e.definition.toLowerCase().includes(q);
    });
  }, [entries, filter, search]);

  const filters: Filter[] = ['all', 'due', 'New', 'Learning', 'Young', 'Mature'];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', height: '100%', overflow: 'hidden' }}>
      {/* Stats */}
      <div style={{ display: 'flex', gap: 10, padding: '14px 16px', flexWrap: 'wrap', flexShrink: 0 }}>
        <Stat label="words" value={stats?.total ?? 0} />
        <Stat label="due today" value={stats?.dueToday ?? 0} color={stats && stats.dueToday > 0 ? '#e5484d' : undefined} />
        <Stat label="learning" value={stats?.learning ?? 0} color={STRENGTH_COLOR.Learning} />
        <Stat label="young" value={stats?.young ?? 0} color={STRENGTH_COLOR.Young} />
        <Stat label="mature" value={stats?.mature ?? 0} color={STRENGTH_COLOR.Mature} />
        <Stat label="reviews (7d)" value={stats?.reviewsLast7Days ?? 0} />
        <Stat label="lapses" value={stats?.lapses ?? 0} />
      </div>

      {/* Filters */}
      <div style={{
        display: 'flex', gap: 8, padding: '0 16px 12px', alignItems: 'center',
        borderBottom: '1px solid var(--border-subtle)', flexShrink: 0, flexWrap: 'wrap',
      }}>
        {filters.map(f => {
          const active = filter === f;
          return (
            <button key={f} onClick={() => setFilter(f)}
              style={{
                padding: '6px 12px', borderRadius: 'var(--radius-md)', cursor: 'pointer',
                fontSize: 13, fontWeight: 600, textTransform: 'capitalize',
                border: `1px solid ${active ? 'var(--accent, #5b8def)' : 'var(--border)'}`,
                background: active ? 'var(--accent, #5b8def)' : 'var(--bg-secondary)',
                color: active ? '#fff' : 'var(--text-secondary)',
              }}>{f}</button>
          );
        })}
        <input
          value={search}
          onChange={e => setSearch(e.target.value)}
          placeholder="Search term or meaning…"
          style={{
            marginLeft: 'auto', padding: '7px 12px', minWidth: 220, fontSize: 13,
            borderRadius: 'var(--radius-md)', border: '1px solid var(--border)',
            background: 'var(--bg-secondary)', color: 'var(--text-primary)',
          }}
        />
      </div>

      {/* List */}
      {loading ? (
        <div style={{ padding: 24, color: 'var(--text-muted)' }}>Loading…</div>
      ) : entries.length === 0 ? (
        <div style={{ padding: 24, color: 'var(--text-muted)' }}>
          Nothing saved yet. Ask me about a word or idiom in chat and say “save it” — it will show up here and enter the review queue.
        </div>
      ) : shown.length === 0 ? (
        <div style={{ padding: 24, color: 'var(--text-muted)' }}>No entries match this filter.</div>
      ) : (
        <div style={{ flex: 1, overflowY: 'auto', padding: '12px 16px' }}>
          {shown.map(e => {
            const open = expanded === e.id;
            return (
              <div key={e.id}
                style={{
                  border: '1px solid var(--border-subtle)', borderRadius: 'var(--radius-md)',
                  marginBottom: 8, background: 'var(--bg-secondary)', overflow: 'hidden',
                }}>
                {/* Header row */}
                <div
                  onClick={() => setExpanded(open ? null : e.id)}
                  style={{ display: 'flex', alignItems: 'center', gap: 10, padding: '12px 14px', cursor: 'pointer' }}>
                  <div style={{ flex: 1, minWidth: 0 }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
                      <span style={{ fontSize: 15, fontWeight: 700, color: 'var(--text-primary)' }}>{e.term}</span>
                      <Pill text={humanize(e.kind)} color="#8b8b8b" />
                      <Pill text={humanize(e.frequency)} color={FREQ_COLOR[e.frequency] ?? '#8b8b8b'} />
                      <Pill text={e.strength} color={STRENGTH_COLOR[e.strength] ?? '#8b8b8b'} />
                      {e.isDue && <Pill text="due" color="#e5484d" />}
                    </div>
                    <div style={{
                      fontSize: 13, color: 'var(--text-secondary)', marginTop: 4,
                      overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap',
                    }}>{e.definition}</div>
                  </div>
                  <div style={{ textAlign: 'right', flexShrink: 0 }}>
                    <div style={{ fontSize: 12, color: e.isDue ? '#e5484d' : 'var(--text-muted)' }}>{dueLabel(e.dueOn)}</div>
                    <div style={{ fontSize: 11, color: 'var(--text-muted)', marginTop: 2 }}>
                      {e.totalReviews} review{e.totalReviews === 1 ? '' : 's'} · ease {e.easeFactor}
                    </div>
                  </div>
                </div>

                {/* Detail */}
                {open && (
                  <div style={{ padding: '0 14px 14px', borderTop: '1px solid var(--border-subtle)' }}>
                    <div style={{ display: 'flex', gap: 24, flexWrap: 'wrap', marginTop: 12 }}>
                      <div style={{ flex: 1, minWidth: 260 }}>
                        {(e.glossHy || e.glossRu) && (
                          <div style={{ fontSize: 13, color: 'var(--text-secondary)', marginBottom: 10 }}>
                            {e.glossHy && <div>🇦🇲 {e.glossHy}</div>}
                            {e.glossRu && <div>🇷🇺 {e.glossRu}</div>}
                          </div>
                        )}

                        {e.examples.length > 0 && (
                          <>
                            <div style={{ fontSize: 11, fontWeight: 700, color: 'var(--text-muted)', textTransform: 'uppercase', marginBottom: 4 }}>Examples</div>
                            <ul style={{ margin: '0 0 10px', paddingLeft: 18, fontSize: 13, color: 'var(--text-secondary)' }}>
                              {e.examples.map((x, i) => <li key={i} style={{ marginBottom: 3 }}>{x}</li>)}
                            </ul>
                          </>
                        )}

                        {e.collocations.length > 0 && (
                          <div style={{ marginBottom: 10 }}>
                            <div style={{ fontSize: 11, fontWeight: 700, color: 'var(--text-muted)', textTransform: 'uppercase', marginBottom: 4 }}>Collocations</div>
                            <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
                              {e.collocations.map((c, i) => <Pill key={i} text={c} color="#5b8def" />)}
                            </div>
                          </div>
                        )}

                        {e.synonyms.length > 0 && (
                          <div style={{ marginBottom: 10 }}>
                            <div style={{ fontSize: 11, fontWeight: 700, color: 'var(--text-muted)', textTransform: 'uppercase', marginBottom: 4 }}>Synonyms</div>
                            <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
                              {e.synonyms.map((s, i) => <Pill key={i} text={s} color="#8b8b8b" />)}
                            </div>
                          </div>
                        )}

                        {e.memoryHook && (
                          <div style={{
                            fontSize: 13, color: 'var(--text-secondary)', padding: 10, marginBottom: 10,
                            borderRadius: 'var(--radius-sm)', background: 'var(--bg-primary)',
                            borderLeft: '3px solid #f5a623',
                          }}>
                            <span style={{ fontWeight: 700 }}>Hook: </span>{e.memoryHook}
                          </div>
                        )}

                        {e.notes && <div style={{ fontSize: 13, color: 'var(--text-muted)' }}>{e.notes}</div>}
                      </div>

                      {/* Right column: schedule + history */}
                      <div style={{ width: 260, flexShrink: 0 }}>
                        <div style={{ fontSize: 11, fontWeight: 700, color: 'var(--text-muted)', textTransform: 'uppercase', marginBottom: 6 }}>Schedule</div>
                        <div style={{ fontSize: 12, color: 'var(--text-secondary)', lineHeight: 1.7, marginBottom: 12 }}>
                          <div>Register: {humanize(e.register)}</div>
                          <div>Interval: {e.intervalDays}d · ease {e.easeFactor}</div>
                          <div>Next: {fmtDate(e.dueOn)} ({dueLabel(e.dueOn)})</div>
                          <div>Lapses: {e.lapses}</div>
                          <div>Added: {fmtDate(e.createdAt)}</div>
                        </div>

                        <div style={{ fontSize: 11, fontWeight: 700, color: 'var(--text-muted)', textTransform: 'uppercase', marginBottom: 6 }}>Grades</div>
                        {/* Oldest → newest, so the trail reads left to right. */}
                        <GradeTrail grades={[...e.reviews].reverse().map(r => r.grade)} />

                        {e.reviews.length > 0 && (
                          <div style={{ marginTop: 10, fontSize: 12, color: 'var(--text-muted)', maxHeight: 140, overflowY: 'auto' }}>
                            {e.reviews.map((r, i) => (
                              <div key={i} style={{ marginBottom: 6, paddingBottom: 6, borderBottom: '1px solid var(--border-subtle)' }}>
                                <div>
                                  <strong style={{ color: r.grade >= 3 ? '#30a46c' : '#e5484d' }}>{r.grade}</strong>
                                  {' · '}{fmtDate(r.reviewedAt)}
                                  {' · '}{r.intervalBefore}d → {r.intervalAfter}d
                                </div>
                                {r.note && <div style={{ fontStyle: 'italic', marginTop: 2 }}>{r.note}</div>}
                              </div>
                            ))}
                          </div>
                        )}

                        <button onClick={() => remove(e)}
                          style={{
                            marginTop: 12, padding: '6px 12px', fontSize: 12, fontWeight: 600, cursor: 'pointer',
                            borderRadius: 'var(--radius-sm)', border: '1px solid #e5484d55',
                            background: 'transparent', color: '#e5484d',
                          }}>Delete</button>
                      </div>
                    </div>
                  </div>
                )}
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}
