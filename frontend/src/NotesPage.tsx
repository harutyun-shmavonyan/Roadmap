import { useState, useEffect, useRef } from 'react';
import { marked } from 'marked';
import type { NoteDto } from './types';
import { api } from './api';
import { usePhone, usePhoneDetail } from './usePhone';

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
  const phone = usePhone();
  const shell = useRef<HTMLDivElement>(null);
  const detail = usePhoneDetail(shell);
  const showList = !phone || !detail.open, showNote = !phone || detail.open;

  useEffect(() => {
    detail.back();
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
    <div ref={shell} className="m-shell" style={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
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
        <div className="m-flow" style={{ display: 'flex', flex: 1, minHeight: 0 }}>
          {/* Day list — on a phone a screen of its own, the note opening on the next */}
          {showList && <div className="m-flow" style={phone ? { flex: 1, padding: 8 }
            : { width: 220, flexShrink: 0, overflowY: 'auto', borderRight: '1px solid var(--border-subtle)', padding: 8 }}>
            {notes.map(n => {
              const active = n.dayNumber === selected;
              return (
                <button key={n.dayNumber} onClick={() => { setSelected(n.dayNumber); if (phone) detail.show(); }}
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
          </div>}

          {/* Visualized markdown */}
          {showNote && <div className="m-flow" style={{ flex: 1, minWidth: 0, overflowY: 'auto', padding: phone ? '12px 16px 24px' : '20px 28px' }}>
            {phone && <button className="btn btn-ghost btn-sm" style={{ marginBottom: 10, paddingLeft: 0 }} onClick={detail.back}>← All days</button>}
            {current && (
              <>
                <div style={{ display: 'flex', alignItems: 'baseline', gap: 12, marginBottom: 16 }}>
                  <h2 style={{ margin: 0, fontSize: 20, color: 'var(--text-primary)' }}>Day {current.dayNumber}</h2>
                  <span style={{ fontSize: 14, color: 'var(--text-muted)' }}>{fmtDate(current.entryDate)}</span>
                </div>
                <div className="md-body" dangerouslySetInnerHTML={{ __html: marked.parse(current.content) as string }} />
              </>
            )}
          </div>}
        </div>
      )}
    </div>
  );
}
