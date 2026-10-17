import { useState, useEffect, useCallback, useMemo, useRef, type RefObject } from 'react';
import type { NewsletterSummaryDto } from './types';
import { api } from './api';

// "2026-09-15" -> "15.09.2026"
function fmtDate(iso: string): string {
  const [y, m, d] = iso.split('-');
  return d && m && y ? `${d}.${m}.${y}` : iso;
}
// "2026-09-15T18:20:00Z" -> "18:20"
function fmtTime(iso: string): string {
  const t = Date.parse(iso);
  return Number.isNaN(t) ? '' : new Date(t).toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' });
}
// How stale the edition on screen is, in plain words.
function published(iso: string): string {
  const t = Date.parse(`${iso}T12:00:00Z`);
  if (Number.isNaN(t)) return iso;
  const days = Math.round((Date.now() - t) / 86_400_000);
  return days <= 0 ? 'today' : days === 1 ? 'yesterday' : `${days} days ago`;
}

// Injected into the edition before it is framed. Editions are written by the newsletter agent as
// standalone pages, so they may set a viewport-height layout; neutralising that is what lets the
// frame auto-size to the content instead of ratcheting taller forever. The reporter is how a
// sandboxed, opaque-origin document tells this page how tall it is — measured from the root's
// box rather than scrollHeight, which never drops below the frame's own height and so could only
// ever grow (an edition that hides sections, like a category filter, must be able to shrink it).
//
// Because the frame is as tall as its content it never scrolls, so two things are relayed:
// - in-page links (`href="#…"`) ask this page to scroll the pane to their target;
// - this page tells the edition which slice of it is on screen (`--nl-top`, `--nl-vh` on its
//   root, `data-nl-framed` set), and the edition's Details modal — `position: fixed`, which in a
//   full-height frame would centre on the middle of the whole document — is pinned to that slice.
const FRAME_SHIM =
  `<style>html,body{height:auto !important;min-height:0 !important}` +
  `[data-nl-framed] .modal{position:absolute !important;top:var(--nl-top,0px) !important;bottom:auto !important;height:var(--nl-vh,100vh) !important}` +
  `[data-nl-framed] .mbox{max-height:calc(var(--nl-vh,100vh) * 0.88) !important}</style>` +
  `<script>(function(){var de=document.documentElement;de.setAttribute('data-nl-framed','');` +
  `function p(){try{parent.postMessage({__nlHeight:Math.ceil(de.getBoundingClientRect().height)},'*')}catch(e){}}` +
  `addEventListener('load',p);addEventListener('resize',p);document.addEventListener('load',p,true);` +
  `if(window.ResizeObserver){try{new ResizeObserver(p).observe(de)}catch(e){}}` +
  `p();setTimeout(p,60);setTimeout(p,300);setTimeout(p,1200);` +
  `document.addEventListener('click',function(e){var a=e.target&&e.target.closest&&e.target.closest('a[href^="#"]');if(!a)return;` +
  `var el=document.getElementById(decodeURIComponent(a.getAttribute('href').slice(1)));if(!el)return;e.preventDefault();` +
  `try{parent.postMessage({__nlScrollTo:el.getBoundingClientRect().top+(window.scrollY||0)},'*')}catch(x){}});` +
  `addEventListener('message',function(e){var v=e.data&&e.data.__nlViewport;if(!v||e.source!==parent)return;` +
  `de.style.setProperty('--nl-top',v.top+'px');de.style.setProperty('--nl-vh',v.height+'px')});})();<\/script>`;

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

type FrameMessage = { __nlHeight?: unknown; __nlScrollTo?: unknown };

// The edition in a sandboxed frame that grows to its content, so the page (not the frame) scrolls.
// `pane` is that scrolling page and `bar` its sticky header, which a scrolled-to target and the
// visible slice reported to the edition both have to clear.
function EditionFrame({ html, pane, bar }: {
  html: string; pane: RefObject<HTMLDivElement>; bar: RefObject<HTMLDivElement>;
}) {
  const frame = useRef<HTMLIFrameElement>(null);
  const [height, setHeight] = useState(600);
  const srcDoc = useMemo(() => withShim(html), [html]);
  useEffect(() => {
    const onMsg = (e: MessageEvent) => {
      if (!frame.current || e.source !== frame.current.contentWindow) return;
      const d: FrameMessage = e.data && typeof e.data === 'object' ? e.data : {};
      const h = d.__nlHeight;
      if (typeof h === 'number' && isFinite(h) && h > 0) {
        // Ignore jitter within the slack we add, so our height and the reported one can't chase
        // each other upward.
        const next = Math.ceil(h) + 4;
        setHeight(prev => (Math.abs(next - prev) > 4 ? next : prev));
      }
      const y = d.__nlScrollTo;
      const p = pane.current;
      if (typeof y === 'number' && isFinite(y) && p) {
        const top = frame.current.getBoundingClientRect().top - p.getBoundingClientRect().top
          + p.scrollTop + y - (bar.current?.offsetHeight ?? 0) - 8;
        p.scrollTo({ top: Math.max(0, top), behavior: 'smooth' });
      }
    };
    window.addEventListener('message', onMsg);
    return () => window.removeEventListener('message', onMsg);
  }, [pane, bar]);

  // Tell the edition which slice of it is on screen, on every scroll, resize and reload.
  useEffect(() => {
    const p = pane.current, f = frame.current;
    if (!p || !f) return;
    let raf = 0;
    const post = () => {
      raf = 0;
      if (!f.contentWindow) return;
      const fr = f.getBoundingClientRect(), pr = p.getBoundingClientRect();
      const top = Math.max(0, pr.top + (bar.current?.offsetHeight ?? 0) - fr.top);
      const bottom = Math.min(fr.height, pr.bottom - fr.top);
      f.contentWindow.postMessage({ __nlViewport: { top: Math.round(top), height: Math.max(0, Math.round(bottom - top)) } }, '*');
    };
    const queue = () => { if (!raf) raf = requestAnimationFrame(post); };
    p.addEventListener('scroll', queue, { passive: true });
    window.addEventListener('resize', queue);
    f.addEventListener('load', queue);
    queue();
    return () => {
      p.removeEventListener('scroll', queue);
      window.removeEventListener('resize', queue);
      f.removeEventListener('load', queue);
      if (raf) cancelAnimationFrame(raf);
    };
  }, [pane, bar, srcDoc, height]);

  return (
    <iframe
      ref={frame}
      title="Newsletter edition"
      srcDoc={srcDoc}
      sandbox="allow-scripts allow-popups allow-popups-to-escape-sandbox"
      style={{ width: '100%', height, border: 'none', display: 'block', background: 'transparent' }}
    />
  );
}

/**
 * The newsletter is today's news, so the tab shows exactly one edition: the most recent one, with
 * the window it covers stated above it. Earlier editions are still kept server-side for a
 * fortnight — the agent's cursor is anchored on the newest edition ticked read — they are simply
 * not something to browse.
 */
export function NewsletterPage() {
  const [issue, setIssue] = useState<NewsletterSummaryDto | null>(null);
  const [html, setHtml] = useState<{ id: string; body: string } | null>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const pane = useRef<HTMLDivElement>(null);
  const bar = useRef<HTMLDivElement>(null);

  const load = useCallback(async () => {
    const rows = await api.getNewsletters();
    // The list comes back newest first; everything behind the head is history the tab doesn't show.
    setIssue(rows[0] ?? null);
  }, []);

  useEffect(() => { setLoading(true); load().finally(() => setLoading(false)); }, [load]);

  useEffect(() => {
    if (!issue) { setHtml(null); return; }
    let alive = true;
    const id = issue.id;
    api.getNewsletterHtml(id)
      .then(body => { if (alive) setHtml({ id, body }); })
      .catch(() => { if (alive) setHtml(null); });
    return () => { alive = false; };
  }, [issue?.id]);

  const shownHtml = issue && html?.id === issue.id ? html.body : null;

  const toggleRead = async () => {
    if (!issue) return;
    setBusy(true);
    try {
      await (issue.isRead ? api.markNewsletterUnread(issue.id) : api.markNewsletterRead(issue.id));
      await load();
    } finally { setBusy(false); }
  };

  if (loading) return <div style={{ padding: 24, color: 'var(--text-muted)' }}>Loading...</div>;

  if (!issue) {
    return (
      <div style={{ padding: 32, maxWidth: 560, color: 'var(--text-muted)', fontSize: 14, lineHeight: 1.7 }}>
        <div style={{ fontSize: 16, fontWeight: 600, color: 'var(--text-primary)', marginBottom: 8 }}>📰 No edition yet</div>
        The newsletter agent publishes one here. It asks the app where you stopped reading and covers
        the news from there.
      </div>
    );
  }

  return (
    <div ref={pane} style={{ height: '100%', overflowY: 'auto', WebkitOverflowScrolling: 'touch' }}>
      {/* What this edition is and, above all, which stretch of news it considered. */}
      <div ref={bar} style={{ position: 'sticky', top: 0, zIndex: 5, background: 'var(--bg-primary)',
        borderBottom: '1px solid var(--border-subtle)', padding: '12px 16px',
        display: 'flex', alignItems: 'center', gap: 10, flexWrap: 'wrap' }}>
        <div style={{ minWidth: 0, flex: 1 }}>
          <div style={{ fontSize: 16, fontWeight: 700, color: 'var(--text-primary)', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
            {issue.title}
          </div>
          <div style={{ fontSize: 12.5, color: 'var(--text-muted)', marginTop: 3, display: 'flex', gap: 8, flexWrap: 'wrap', alignItems: 'center' }}>
            <span>
              Covering news from <b style={{ color: 'var(--text-secondary)' }}>{fmtDate(issue.coveredFrom)}</b>
              {' → '}
              <b style={{ color: 'var(--text-secondary)' }}>{fmtDate(issue.issueDate)} {fmtTime(issue.coveredUntil)}</b>
            </span>
            <span>·</span>
            <span>published {published(issue.issueDate)}</span>
            {issue.itemCount > 0 && <><span>·</span><span>{issue.itemCount} stories</span></>}
            {issue.isRead && issue.readOn && <><span>·</span><span style={{ color: 'var(--accent)', fontWeight: 600 }}>✓ Read {fmtDate(issue.readOn)}</span></>}
          </div>
        </div>
        <button className="btn btn-sm" disabled={!shownHtml} onClick={() => shownHtml && openInNewTab(shownHtml)}
          title="Open this edition full-screen in a new browser tab">Open in new tab ↗</button>
        <button className={`btn btn-sm ${issue.isRead ? '' : 'btn-accent'}`} disabled={busy} onClick={toggleRead}
          title={issue.isRead ? 'Send the next edition back over this ground' : 'Ticking this is where the next edition starts from'}>
          {issue.isRead ? 'Mark as unread' : '✓ Mark as read'}
        </button>
      </div>

      {shownHtml ? (
        <EditionFrame html={shownHtml} pane={pane} bar={bar} />
      ) : (
        <div style={{ padding: 24, color: 'var(--text-muted)', fontSize: 14 }}>Rendering…</div>
      )}
    </div>
  );
}
