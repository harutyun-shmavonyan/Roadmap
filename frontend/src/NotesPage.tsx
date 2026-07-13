import { useState, useEffect } from 'react';
import { marked } from 'marked';
import type { NoteDto } from './types';
import { api } from './api';

type Book = 'red' | 'green';

marked.setOptions({ gfm: true, breaks: true });

// "2018-08-20" -> "20.08.2018"
function fmtDate(iso: string): string {
  const [y, m, d] = iso.split('-');
  return d && m && y ? `${d}.${m}.${y}` : iso;
}

export function NotesPage() {
  const [book, setBook] = useState<Book>('red');
  const [notes, setNotes] = useState<NoteDto[]>([]);
  const [selected, setSelected] = useState<number | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    setLoading(true);
    api.getNotes(book)
      .then(list => {
        setNotes(list);
        setSelected(list.length ? list[0].dayNumber : null);
      })
      .finally(() => setLoading(false));
  }, [book]);

  const current = notes.find(n => n.dayNumber === selected) ?? null;
  const accent = book === 'red' ? '#e5484d' : '#30a46c';

  return (
    <div style={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
      {/* Red / Green subtabs */}
      <div style={{ display: 'flex', gap: 8, padding: '12px 16px', borderBottom: '1px solid var(--border-subtle)', flexShrink: 0 }}>
        {(['red', 'green'] as Book[]).map(b => {
          const active = book === b;
          const c = b === 'red' ? '#e5484d' : '#30a46c';
          return (
            <button key={b} onClick={() => setBook(b)}
              style={{
                display: 'flex', alignItems: 'center', gap: 7, padding: '7px 16px',
                borderRadius: 'var(--radius-md)', cursor: 'pointer', fontSize: 14, fontWeight: 600,
                textTransform: 'capitalize',
                border: `1px solid ${active ? c : 'var(--border)'}`,
                background: active ? c : 'var(--bg-secondary)',
                color: active ? '#fff' : 'var(--text-secondary)',
              }}>
              <span style={{ width: 10, height: 10, borderRadius: '50%', background: active ? '#fff' : c, display: 'inline-block' }} />
              {b}
            </button>
          );
        })}
      </div>

      {loading ? (
        <div style={{ padding: 24, color: 'var(--text-muted)' }}>Loading...</div>
      ) : notes.length === 0 ? (
        <div style={{ padding: 24, color: 'var(--text-muted)' }}>No notes in the {book} book yet.</div>
      ) : (
        <div style={{ display: 'flex', flex: 1, minHeight: 0 }}>
          {/* Day list */}
          <div style={{ width: 220, flexShrink: 0, overflowY: 'auto', borderRight: '1px solid var(--border-subtle)', padding: 8 }}>
            {notes.map(n => {
              const active = n.dayNumber === selected;
              return (
                <button key={n.dayNumber} onClick={() => setSelected(n.dayNumber)}
                  style={{
                    display: 'block', width: '100%', textAlign: 'left', padding: '8px 10px', marginBottom: 4,
                    borderRadius: 'var(--radius-sm)', cursor: 'pointer', border: 'none',
                    borderLeft: `3px solid ${active ? accent : 'transparent'}`,
                    background: active ? 'var(--bg-secondary)' : 'transparent',
                    color: active ? 'var(--text-primary)' : 'var(--text-secondary)',
                  }}>
                  <div style={{ fontSize: 13, fontWeight: 600 }}>Day {n.dayNumber}</div>
                  <div style={{ fontSize: 12, color: 'var(--text-muted)' }}>{fmtDate(n.entryDate)}</div>
                </button>
              );
            })}
          </div>

          {/* Visualized markdown */}
          <div style={{ flex: 1, overflowY: 'auto', padding: '20px 28px' }}>
            {current && (
              <>
                <div style={{ display: 'flex', alignItems: 'baseline', gap: 12, marginBottom: 16 }}>
                  <h2 style={{ margin: 0, fontSize: 20, color: 'var(--text-primary)' }}>Day {current.dayNumber}</h2>
                  <span style={{ fontSize: 14, color: 'var(--text-muted)' }}>{fmtDate(current.entryDate)}</span>
                </div>
                <div className="md-body" dangerouslySetInnerHTML={{ __html: marked.parse(current.content) as string }} />
              </>
            )}
          </div>
        </div>
      )}
    </div>
  );
}
