import { useState, useEffect, useCallback, useMemo, useRef } from 'react';
import type { NewsletterSummaryDto, NewsletterCursorDto } from './types';
import { api } from './api';

// "2026-09-15" -> "15.09"
function fmtShort(iso: string): string {
  const [, m, d] = iso.split('-');
  return d && m ? `${d}.${m}` : iso;
}
// "2026-09-15" -> "15.09.2026"
function fmtDate(iso: string): string {
  const [y, m, d] = iso.split('-');
  return d && m && y ? `${d}.${m}.${y}` : iso;
}
// Day name for the list row, so a fortnight of dates reads as days rather than numbers.
function dayName(iso: string): string {
  const t = Date.parse(`${iso}T12:00:00Z`);
  return Number.isNaN(t) ? '' : new Date(t).toLocaleDateString(undefined, { weekday: 'short', timeZone: 'UTC' });
}
// "…until 2026-09-15T18:20:00Z" -> "18:20"
function fmtTime(iso: string): string {
  const t = Date.parse(iso);
  return Number.isNaN(t) ? '' : new Date(t).toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' });
}

// Injected into the edition before it is framed. Editions are written by the newsletter agent as
// standalone pages, so they may set a viewport-height layout; neutralising that is what lets the
// frame auto-size to the content instead of ratcheting taller forever. The reporter is how a
// sandboxed, opaque-origin document tells this page how tall it is.
const FRAME_SHIM =
  `<style>html,body{height:auto !important;min-height:0 !important}</style>` +
  `<script>(function(){function p(){try{parent.postMessage({__nlHeight:document.documentElement.scrollHeight},'*')}catch(e){}}` +
  `addEventListener('load',p);addEventListener('resize',p);document.addEventListener('load',p,true);` +
  `if(window.ResizeObserver){try{new ResizeObserver(p).observe(document.documentElement)}catch(e){}}` +
  `p();setTimeout(p,60);setTimeout(p,300);setTimeout(p,1200);})();<\/script>`;

function withShim(html: string): string {
  return /<\/body>/i.test(html) ? html.replace(/<\/body>/i, FRAME_SHIM + '</body>') : html + FRAME_SHIM;
}

// Open the edition full-screen in a new browser tab via a blob URL. It was fetched with the
// bearer token and stays private — no route of the app is publicly reachable.
function openInNewTab(html: string) {
  const url = URL.createObjectURL(new Blob([html], { type: 'text/html' }));
  window.open(url, '_blank', 'noopener');
  setTimeout(() => URL.revokeObjectURL(url), 60_000);
}

// The edition in a sandboxed frame that grows to its content, so the surrounding pane scrolls.
function EditionFrame({ html }: { html: string }) {
  const [height, setHeight] = useState(600);
  const srcDoc = useMemo(() => withShim(html), [html]);
  useEffect(() => {
    const onMsg = (e: MessageEvent) => {
      const h = e.data && typeof e.data === 'object' ? (e.data as { __nlHeight?: unknown }).__nlHeight : undefined;
      if (typeof h === 'number' && isFinite(h) && h > 0) {
        // Ignore jitter within the slack we add, so our height and the reported one can't chase
        // each other upward.
        const next = Math.ceil(h) + 4;
        setHeight(prev => (Math.abs(next - prev) > 4 ? next : prev));
      }
    };
    window.addEventListener('message', onMsg);
    return () => window.removeEventListener('message', onMsg);
  }, []);
  return (
    <iframe
      title="Newsletter edition"
      srcDoc={srcDoc}
      sandbox="allow-scripts allow-popups allow-popups-to-escape-sandbox"
      style={{ width: '100%', height, border: 'none', display: 'block', background: 'transparent' }}
    />
  );
}

// Collapse the two panes into one column on phones.
function useNarrow(): boolean {
  const q = '(max-width: 720px)';
  const [narrow, setNarrow] = useState(() => typeof window !== 'undefined' && window.matchMedia(q).matches);
  useEffect(() => {
    const mq = window.matchMedia(q);
    const on = () => setNarrow(mq.matches);
    mq.addEventListener('change', on);
    return () => mq.removeEventListener('change', on);
  }, []);
  return narrow;
}

export function NewsletterPage() {
  const narrow = useNarrow();
  const [list, setList] = useState<NewsletterSummaryDto[]>([]);
  const [cursor, setCursor] = useState<NewsletterCursorDto | null>(null);
  const [selId, setSelId] = useState<string | null>(null);
  const [html, setHtml] = useState<{ id: string; body: string } | null>(null);
  const [loading, setLoading] = useState(true);
  const [htmlLoading, setHtmlLoading] = useState(false);
  const [busy, setBusy] = useState(false);
  const [mobileOpen, setMobileOpen] = useState(false);
  const paneRef = useRef<HTMLDivElement>(null);

  const load = useCallback(async (selectId?: string) => {
    const [rows, cur] = await Promise.all([api.getNewsletters(), api.getNewsletterCursor()]);
    setList(rows);
    setCursor(cur);
    setSelId(prev => selectId ?? (prev && rows.some(r => r.id === prev) ? prev : rows[0]?.id ?? null));
  }, []);

  useEffect(() => { setLoading(true); load().finally(() => setLoading(false)); }, [load]);

  // Fetch the selected edition's document. Kept with the id it belongs to so a stale body is
  // never shown under a new heading.
  useEffect(() => {
    if (!selId) { setHtml(null); return; }
    let alive = true;
    setHtmlLoading(true);
    api.getNewsletterHtml(selId)
      .then(body => { if (alive) setHtml({ id: selId, body }); })
      .catch(() => { if (alive) setHtml(null); })
      .finally(() => { if (alive) setHtmlLoading(false); });
    return () => { alive = false; };
  }, [selId]);

  // A new edition starts at the top rather than wherever the last one was left.
  useEffect(() => { if (paneRef.current) paneRef.current.scrollTop = 0; }, [selId]);

  const sel = list.find(n => n.id === selId) ?? null;
  const shownHtml = sel && html?.id === sel.id ? html.body : null;
  const unread = list.filter(n => !n.isRead).length;

  const toggleRead = async (n: NewsletterSummaryDto) => {
    setBusy(true);
    try {
      await (n.isRead ? api.markNewsletterUnread(n.id) : api.markNewsletterRead(n.id));
      await load(n.id);
    } finally { setBusy(false); }
  };

  const openEdition = (id: string) => { setSelId(id); setMobileOpen(true); };

  // ---- List ----
  const listPanel = (mobile: boolean) => (
    <div style={{
      overflowY: 'auto', WebkitOverflowScrolling: 'touch', padding: 8,
      ...(mobile ? { flex: 1, minHeight: 0, width: '100%' } : { width: 260, flexShrink: 0, borderRight: '1px solid var(--border-subtle)' }),
    }}>
      {list.length === 0 ? (
        <div style={{ padding: 16, color: 'var(--text-muted)', fontSize: 13, lineHeight: 1.6 }}>
          No editions yet. The newsletter agent publishes them here — it asks the app where you
          stopped reading and covers the news from there.
        </div>
      ) : list.map(n => {
        const active = n.id === selId && !mobile;
        return (
          <div key={n.id} onClick={() => openEdition(n.id)}
            style={{
              display: 'flex', alignItems: 'flex-start', gap: 10, padding: mobile ? '13px 12px' : '9px 10px',
              marginBottom: 4, borderRadius: 'var(--radius-sm)', cursor: 'pointer',
              borderLeft: `3px solid ${active ? 'var(--accent)' : 'transparent'}`,
              background: active ? 'var(--bg-secondary)' : 'transparent',
            }}>
            <button title={n.isRead ? 'Mark as unread' : 'Mark as read'} disabled={busy}
              onClick={e => { e.stopPropagation(); toggleRead(n); }}
              style={{
                flexShrink: 0, marginTop: 1, width: mobile ? 24 : 18, height: mobile ? 24 : 18, borderRadius: 5,
                cursor: 'pointer', border: `2px solid ${n.isRead ? 'var(--accent)' : 'var(--border)'}`,
                background: n.isRead ? 'var(--accent)' : 'transparent', color: '#fff',
                fontSize: mobile ? 15 : 12, lineHeight: mobile ? '20px' : '14px', padding: 0,
              }}>{n.isRead ? '✓' : ''}</button>
            <div style={{ minWidth: 0, flex: 1 }}>
              <div style={{ display: 'flex', alignItems: 'baseline', gap: 6 }}>
                <span style={{ fontSize: mobile ? 15 : 13.5, fontWeight: n.isRead ? 500 : 700, color: active ? 'var(--text-primary)' : 'var(--text-secondary)' }}>
                  {fmtDate(n.issueDate)}
                </span>
                <span style={{ fontSize: 11, color: 'var(--text-muted)' }}>{dayName(n.issueDate)}</span>
              </div>
              <div style={{ fontSize: 12, color: 'var(--text-muted)', marginTop: 3, display: 'flex', gap: 8, flexWrap: 'wrap' }}>
                {n.itemCount > 0 && <span>📰 {n.itemCount}</span>}
                <span>{n.coveredFrom === n.issueDate ? 'today' : `from ${fmtShort(n.coveredFrom)}`}</span>
                {!n.isRead && <span style={{ color: 'var(--accent)', fontWeight: 600 }}>new</span>}
              </div>
            </div>
            {mobile && <span style={{ color: 'var(--text-muted)', fontSize: 18, alignSelf: 'center' }}>›</span>}
          </div>
        );
      })}
      {list.length > 0 && (
        <div style={{ padding: '10px 10px 4px', fontSize: 11, color: 'var(--text-muted)', lineHeight: 1.5 }}>
          Keeping the last {cursor?.retentionDays ?? 14} days. Older editions are dropped when a new one arrives.
        </div>
      )}
    </div>
  );

  // ---- Reader ----
  const readerPanel = (mobile: boolean) => (
    <div ref={paneRef} style={{ flex: 1, minWidth: 0, minHeight: 0, overflowY: 'auto', WebkitOverflowScrolling: 'touch' }}>
      {/* One sticky stack: the phone's back bar and the edition's own bar, so neither scrolls away. */}
      <div style={{ position: 'sticky', top: 0, zIndex: 5, background: 'var(--bg-primary)' }}>
        {mobile && (
          <div style={{ display: 'flex', padding: '8px 10px', borderBottom: '1px solid var(--border-subtle)' }}>
            <button className="btn btn-ghost btn-sm" onClick={() => setMobileOpen(false)}>← All editions</button>
          </div>
        )}
        {sel && (
          <div style={{ display: 'flex', alignItems: 'center', flexWrap: 'wrap', gap: 8, padding: '12px 16px', borderBottom: '1px solid var(--border-subtle)' }}>
            <div style={{ minWidth: 0, flex: 1 }}>
              <div style={{ fontSize: 16, fontWeight: 700, color: 'var(--text-primary)', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                {sel.title}
              </div>
              <div style={{ fontSize: 12.5, color: 'var(--text-muted)', marginTop: 2, display: 'flex', gap: 8, flexWrap: 'wrap' }}>
                <span>Covers {fmtDate(sel.coveredFrom)} → {fmtDate(sel.issueDate)} {fmtTime(sel.coveredUntil)}</span>
                {sel.itemCount > 0 && <><span>·</span><span>{sel.itemCount} stories</span></>}
                {sel.isRead && sel.readOn && <><span>·</span><span style={{ color: 'var(--accent)', fontWeight: 600 }}>✓ Read {fmtDate(sel.readOn)}</span></>}
              </div>
            </div>
            <button className="btn btn-sm" disabled={!shownHtml} onClick={() => shownHtml && openInNewTab(shownHtml)}
              title="Open this edition full-screen in a new browser tab">Open in new tab ↗</button>
            <button className={`btn btn-sm ${sel.isRead ? '' : 'btn-accent'}`} disabled={busy} onClick={() => toggleRead(sel)}
              title={sel.isRead ? 'Send the agent back to the edition before this one' : 'Ticking this is where the next edition starts from'}>
              {sel.isRead ? 'Mark as unread' : '✓ Mark as read'}
            </button>
          </div>
        )}
      </div>

      {!sel ? (
        <div style={{ padding: 24, color: 'var(--text-muted)', fontSize: 14 }}>
          {mobile ? 'Loading…' : 'Select an edition to read.'}
        </div>
      ) : htmlLoading && !shownHtml ? (
        <div style={{ padding: 24, color: 'var(--text-muted)', fontSize: 14 }}>Rendering…</div>
      ) : shownHtml ? (
        <EditionFrame html={shownHtml} />
      ) : (
        <div style={{ padding: 24, color: 'var(--text-muted)', fontSize: 14 }}>Could not load this edition.</div>
      )}
    </div>
  );

  const readerOpen = narrow && mobileOpen && !!selId;

  // What the next run will cover, in the words the tab thinks in.
  const cursorLine = cursor && (
    cursor.issueCount === 0
      ? `Nothing published yet — a first run would cover the last ${cursor.windowDays} days.`
      : cursor.lastReadDate
        ? `Next edition picks up from ${fmtDate(cursor.lastReadDate)}${cursor.cappedToMaxWindow ? ` (capped to ${cursor.maxWindowDays} days)` : ''}.`
        : `Nothing read yet — next edition picks up where ${cursor.latestIssueDate ? fmtDate(cursor.latestIssueDate) : 'the last one'} stopped.`
  );

  return (
    <div style={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
      {!readerOpen && (
        <div style={{ display: 'flex', alignItems: 'center', gap: 12, padding: '12px 16px', borderBottom: '1px solid var(--border-subtle)', flexShrink: 0, flexWrap: 'wrap' }}>
          <span style={{ fontSize: 15, fontWeight: 600, color: 'var(--text-primary)' }}>📰 Professional Newsletter</span>
          <span style={{ fontSize: 13, color: 'var(--text-muted)' }}>
            {unread > 0 ? `${unread} unread` : list.length > 0 ? 'all read' : ''}
          </span>
          <div style={{ flex: 1 }} />
          {cursorLine && <span style={{ fontSize: 12, color: 'var(--text-muted)' }}>{cursorLine}</span>}
        </div>
      )}

      {loading ? (
        <div style={{ padding: 24, color: 'var(--text-muted)' }}>Loading...</div>
      ) : narrow ? (
        readerOpen ? readerPanel(true) : listPanel(true)
      ) : (
        <div style={{ display: 'flex', flex: 1, minHeight: 0 }}>
          {listPanel(false)}
          {readerPanel(false)}
        </div>
      )}
    </div>
  );
}
